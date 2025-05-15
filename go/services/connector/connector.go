// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package connector

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"sync"

	"github.com/fsnotify/fsnotify"
)

const ADRResourcesNameMountPath = "ADR_RESOURCES_NAME_MOUNT_PATH"

// DeviceEndpointRef represents a device and its associated endpoint.
type DeviceEndpointRef struct {
	// The name of the device
	DeviceName string
	// The name of the inbound endpoint
	InboundEndpointName string
}

type Error struct {
	Message string
	Cause   error
}

func (e *Error) Error() string {
	if e.Cause != nil {
		return fmt.Sprintf("%s: %v", e.Message, e.Cause)
	}
	return e.Message
}

func (d DeviceEndpointRef) String() string {
	return fmt.Sprintf("%s_%s", d.DeviceName, d.InboundEndpointName)
}

// ParseDeviceEndpointRef parses a string into a DeviceEndpointRef.
func ParseDeviceEndpointRef(value string) (DeviceEndpointRef, error) {
	if strings.HasPrefix(value, "..") {
		return DeviceEndpointRef{}, &Error{
			Message: "DeviceEndpointRef cannot start with '..'",
		}
	}

	parts := strings.Split(value, "_")
	if len(parts) != 2 {
		return DeviceEndpointRef{}, &Error{
			Message: "Failed to parse DeviceEndpointRef from string",
		}
	}

	return DeviceEndpointRef{
		DeviceName:          parts[0],
		InboundEndpointName: parts[1],
	}, nil
}

// AssetRef represents an asset associated with a specific device and endpoint.
type AssetRef struct {
	// The name of the asset
	Name string

	// The associated device endpoint
	DeviceEndpointRef
}

// AssetDeletionChan is used to notify when an asset has been deleted.
type AssetDeletionChan chan struct{}

// ConnectionStatus represents the connection status of a mount point.
type ConnectionStatus int

const (
	ConnectionStatusDisconnected ConnectionStatus = iota
	ConnectionStatusConnected
)

// ConnectionMonitor monitors the connection status of a mount point.
type ConnectionMonitor struct {
	mu           sync.RWMutex
	status       ConnectionStatus
	statusChange *sync.Cond
}

// NewConnectionMonitor creates a new ConnectionMonitor.
func NewConnectionMonitor() *ConnectionMonitor {
	m := &ConnectionMonitor{
		status: ConnectionStatusDisconnected,
	}
	m.statusChange = sync.NewCond(&m.mu)
	return m
}

// IsConnected returns true if the connection is currently connected.
func (m *ConnectionMonitor) IsConnected() bool {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.status == ConnectionStatusConnected
}

// SetConnected updates the connection status to connected.
func (m *ConnectionMonitor) SetConnected() {
	m.mu.Lock()
	defer m.mu.Unlock()

	if m.status != ConnectionStatusConnected {
		m.status = ConnectionStatusConnected
		m.statusChange.Broadcast()
	}
}

// SetDisconnected updates the connection status to disconnected.
func (m *ConnectionMonitor) SetDisconnected() {
	m.mu.Lock()
	defer m.mu.Unlock()

	if m.status != ConnectionStatusDisconnected {
		m.status = ConnectionStatusDisconnected
		m.statusChange.Broadcast()
	}
}

// WaitForConnected waits until the connection is connected.
func (m *ConnectionMonitor) WaitForConnected(ctx context.Context) error {
	done := make(chan struct{})

	go func() {
		m.mu.Lock()
		defer m.mu.Unlock()
		defer close(done)

		for m.status != ConnectionStatusConnected {
			m.statusChange.Wait()
		}
	}()

	select {
	case <-done:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

// WaitForDisconnected waits until the connection is disconnected.
func (m *ConnectionMonitor) WaitForDisconnected(ctx context.Context) error {
	done := make(chan struct{})

	go func() {
		m.mu.Lock()
		defer m.mu.Unlock()
		defer close(done)

		for m.status != ConnectionStatusDisconnected {
			m.statusChange.Wait()
		}
	}()

	select {
	case <-done:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

// DeviceEndpointCreateObservation represents an observation for device endpoint creation events.
type DeviceEndpointCreateObservation struct {
	mountPath    string
	deviceChan   chan DeviceNotification
	fileMountMap *FileMountMap
	cancel       context.CancelFunc
	connMonitor  *ConnectionMonitor
	logger       *slog.Logger

	watcher     *fsnotify.Watcher
	lastDevices map[string]DeviceEndpointRef
}

// DeviceNotification contains information about a device endpoint notification.
type DeviceNotification struct {
	DeviceEndpoint DeviceEndpointRef
	AssetObserver  *AssetCreateObservation
}

// AssetNotification contains information about an asset notification.
type AssetNotification struct {
	Asset         AssetRef
	DeletionToken AssetDeletionChan
}

// AssetCreateObservation represents an observation for asset creation events.
type AssetCreateObservation struct {
	assetChan chan AssetNotification
}

// FileMountMap tracks device endpoints and their associated assets.
type FileMountMap struct {
	mu             sync.RWMutex
	fileMountPath  string
	deviceMap      map[string]DeviceData
	createDeviceTx chan DeviceNotification
	logger         *slog.Logger
}

// DeviceData contains data related to a device endpoint.
type DeviceData struct {
	assetCreationTx chan AssetNotification
	trackedAssets   map[string]AssetDeletionChan
}

// NewFileMountMap creates a new FileMountMap.
func NewFileMountMap(fileMountPath string, logger *slog.Logger) *FileMountMap {
	return &FileMountMap{
		fileMountPath:  fileMountPath,
		deviceMap:      make(map[string]DeviceData),
		createDeviceTx: make(chan DeviceNotification, 100),
		logger:         logger,
	}
}

// UpdateDeviceEndpoints updates the device endpoints in the file mount map.
func (f *FileMountMap) UpdateDeviceEndpoints(
	devices map[string]DeviceEndpointRef,
) {
	f.mu.Lock()
	defer f.mu.Unlock()

	for deviceKey, data := range f.deviceMap {
		if _, ok := devices[deviceKey]; !ok {
			close(data.assetCreationTx)

			for _, deletionChan := range data.trackedAssets {
				close(deletionChan)
			}
			delete(f.deviceMap, deviceKey)
		}
	}

	for deviceKey, deviceRef := range devices {
		if _, ok := f.deviceMap[deviceKey]; !ok {
			assetCreationTx := make(chan AssetNotification, 100)
			assetCreationRx := make(chan AssetNotification, 100)

			go func() {
				defer close(assetCreationRx)
				for msg := range assetCreationTx {
					assetCreationRx <- msg
				}
			}()

			f.deviceMap[deviceKey] = DeviceData{
				assetCreationTx: assetCreationTx,
				trackedAssets:   make(map[string]AssetDeletionChan),
			}

			assetObserver := &AssetCreateObservation{
				assetChan: assetCreationRx,
			}

			f.logger.Info("New device detected", "device", deviceRef.String())

			f.createDeviceTx <- DeviceNotification{
				DeviceEndpoint: deviceRef,
				AssetObserver:  assetObserver,
			}
		}

		f.updateAssets(deviceKey, deviceRef)
	}
}

// updateAssets updates the assets associated with a device endpoint.
func (f *FileMountMap) updateAssets(
	deviceKey string,
	deviceRef DeviceEndpointRef,
) {
	assets, err := getAssetNames(f.fileMountPath, deviceRef)
	if err != nil {
		f.logger.Warn(
			"Failed to get asset names",
			"error",
			err,
			"device",
			deviceRef.String(),
		)
		return
	}

	deviceData, ok := f.deviceMap[deviceKey]
	if !ok {
		return
	}

	// Track assets that no longer exist
	assetsToRemove := make([]string, 0)
	for assetName := range deviceData.trackedAssets {
		if _, ok := assets[assetName]; !ok {
			assetsToRemove = append(assetsToRemove, assetName)
		}
	}

	// Remove assets that no longer exist
	for _, assetName := range assetsToRemove {
		if deletionChan, ok := deviceData.trackedAssets[assetName]; ok {
			close(deletionChan)
			delete(deviceData.trackedAssets, assetName)
		}
	}

	// Add new assets
	for assetName, assetRef := range assets {
		if _, ok := deviceData.trackedAssets[assetName]; !ok {
			deletionChan := make(AssetDeletionChan)
			deviceData.trackedAssets[assetName] = deletionChan

			f.logger.Info("New asset detected",
				"device", deviceRef.String(),
				"asset", assetName)

			deviceData.assetCreationTx <- AssetNotification{
				Asset:         assetRef,
				DeletionToken: deletionChan,
			}
		}
	}
}

// NewDeviceEndpointCreateObservation creates a new DeviceEndpointCreateObservation.
func NewDeviceEndpointCreateObservation(
	ctx context.Context,
	logger *slog.Logger,
) (*DeviceEndpointCreateObservation, error) {
	mountPath, err := GetMountPath()
	if err != nil {
		return nil, err
	}

	fileMountMap := NewFileMountMap(mountPath, logger)
	connMonitor := NewConnectionMonitor()
	observeCtx, cancel := context.WithCancel(ctx)

	watcher, err := fsnotify.NewWatcher()
	if err != nil {
		cancel()
		return nil, fmt.Errorf("failed to create watcher: %w", err)
	}

	// Always watch the parent directory so that we can detect the mount point being created/removed.
	parentDir := filepath.Dir(mountPath)
	if err := watcher.Add(parentDir); err != nil {
		watcher.Close()
		cancel()
		return nil, fmt.Errorf("failed to watch parent directory: %w", err)
	}

	// If the mount currently exists, start watching it.
	if _, err := os.Stat(mountPath); err == nil {
		if err := watcher.Add(mountPath); err != nil {
			watcher.Close()
			cancel()
			return nil, fmt.Errorf("failed to watch mount path: %w", err)
		}
	}

	observation := &DeviceEndpointCreateObservation{
		mountPath:    mountPath,
		deviceChan:   fileMountMap.createDeviceTx,
		fileMountMap: fileMountMap,
		cancel:       cancel,
		connMonitor:  connMonitor,
		logger:       logger,
		watcher:      watcher,
	}

	// Initialize with existing devices
	_, err = os.Stat(mountPath)
	switch {
	case err == nil:
		// Mount path exists, check for devices
		devices, err := getDeviceEndpointNames(mountPath)
		if err != nil {
			cancel()
			watcher.Close()
			return nil, err
		}

		fileMountMap.UpdateDeviceEndpoints(devices)
		connMonitor.SetConnected()
	case os.IsNotExist(err):
		// Mount path doesn't exist yet
		connMonitor.SetDisconnected()
	default:
		cancel()
		watcher.Close()
		return nil, &Error{Message: "Failed to access mount path", Cause: err}
	}

	// Start watcher loop
	go observation.watchLoop(observeCtx)

	return observation, nil
}

// watchLoop processes fsnotify events and updates the file mount map.
func (o *DeviceEndpointCreateObservation) watchLoop(ctx context.Context) {
	defer o.watcher.Close()

	for {
		select {
		case <-ctx.Done():
			return
		case event, ok := <-o.watcher.Events:
			if !ok {
				return
			}

			created := event.Has(fsnotify.Create)
			removed := event.Has(fsnotify.Remove) || event.Has(fsnotify.Rename)
			written := created || removed || event.Has(fsnotify.Write)

			// Handle creation or removal of the mount path itself.
			if event.Name == o.mountPath && created {
				// Mount appeared – start watching it.
				_ = o.watcher.Add(o.mountPath)
				o.connMonitor.SetConnected()
			}
			if event.Name == o.mountPath && removed {
				// Mount disappeared.
				_ = o.watcher.Remove(o.mountPath)
				o.connMonitor.SetDisconnected()
				o.lastDevices = nil
				continue
			}

			// Only process events within the mount directory when connected.
			if !o.connMonitor.IsConnected() {
				continue
			}

			// For any relevant operation inside mount path, reconcile.
			if strings.HasPrefix(event.Name, o.mountPath) && written {
				o.reconcile()
			}

		case err, ok := <-o.watcher.Errors:
			if ok {
				o.logger.Warn("watcher error", "error", err)
			}
		}
	}
}

// reconcile reads current state and updates the FileMountMap.
func (o *DeviceEndpointCreateObservation) reconcile() {
	devices, err := getDeviceEndpointNames(o.mountPath)
	if err != nil {
		o.logger.Warn(
			"Failed to get device endpoint names",
			"error",
			err,
		)
		return
	}

	if !reflect.DeepEqual(devices, o.lastDevices) {
		o.fileMountMap.UpdateDeviceEndpoints(devices)
		o.lastDevices = devices
	}
}

// RecvNotification receives the next device endpoint notification.
func (o *DeviceEndpointCreateObservation) RecvNotification(
	ctx context.Context,
) (*DeviceNotification, error) {
	select {
	case notification, ok := <-o.deviceChan:
		if !ok {
			return nil, errors.New("device channel closed")
		}
		return &notification, nil
	case <-ctx.Done():
		return nil, ctx.Err()
	}
}

// Close stops the observation and releases resources.
func (o *DeviceEndpointCreateObservation) Close() {
	o.cancel()
	_ = o.watcher.Close()
}

// GetConnectionMonitor returns the connection monitor for this observation.
func (o *DeviceEndpointCreateObservation) GetConnectionMonitor() *ConnectionMonitor {
	return o.connMonitor
}

// RecvNotification receives the next asset notification.
func (o *AssetCreateObservation) RecvNotification(
	ctx context.Context,
) (*AssetNotification, error) {
	select {
	case notification, ok := <-o.assetChan:
		if !ok {
			return nil, errors.New("asset channel closed")
		}
		return &notification, nil
	case <-ctx.Done():
		return nil, ctx.Err()
	}
}

// WaitForDeletion waits for the asset to be deleted.
func WaitForDeletion(
	ctx context.Context,
	deletionToken AssetDeletionChan,
) error {
	select {
	case <-deletionToken:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

// GetMountPath gets the mount path of the Azure Device Registry resources.
func GetMountPath() (string, error) {
	mountPath, ok := os.LookupEnv(ADRResourcesNameMountPath)
	if !ok {
		return "", &Error{
			Message: "ADR_RESOURCES_NAME_MOUNT_PATH environment variable not set",
		}
	}
	return mountPath, nil
}

// getDeviceEndpointNames gets names of all available device endpoints from the file mount.
func getDeviceEndpointNames(
	mountPath string,
) (map[string]DeviceEndpointRef, error) {
	entries, err := os.ReadDir(mountPath)
	if err != nil {
		return nil, &Error{Message: "Failed to read directory", Cause: err}
	}

	deviceEndpoints := make(map[string]DeviceEndpointRef)

	for _, entry := range entries {
		if entry.IsDir() || strings.HasPrefix(entry.Name(), "..") {
			continue
		}

		deviceEndpoint, err := ParseDeviceEndpointRef(entry.Name())
		if err != nil {
			continue
		}

		deviceEndpoints[deviceEndpoint.String()] = deviceEndpoint
	}

	return deviceEndpoints, nil
}

// getAssetNames gets the names of all assets associated with a DeviceEndpointRef from the file mount.
func getAssetNames(
	mountPath string,
	deviceEndpoint DeviceEndpointRef,
) (map[string]AssetRef, error) {
	filePath := filepath.Join(mountPath, deviceEndpoint.String())

	content, err := os.ReadFile(filePath)
	if err != nil {
		return nil, &Error{Message: "Failed to read file", Cause: err}
	}

	if len(content) == 0 {
		return make(map[string]AssetRef), nil
	}

	assetNames := strings.Split(string(content), ";")
	assets := make(map[string]AssetRef)

	for _, assetName := range assetNames {
		if assetName == "" {
			continue
		}

		assets[assetName] = AssetRef{
			Name:              assetName,
			DeviceEndpointRef: deviceEndpoint,
		}
	}

	return assets, nil
}
