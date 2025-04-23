// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package adr

import (
	"context"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/services/adr/internal/adrbaseservice"
	"github.com/Azure/iot-operations-sdks/go/services/adr/internal/aeptypeservice"
)

const (
	aepNameTokenKey = "aepName"
	defaultTimeout  = 10 * time.Second
)

type (
	Asset                          = adrbaseservice.Asset
	AssetEndpointProfile           = adrbaseservice.AssetEndpointProfile
	NotificationResponse           = adrbaseservice.NotificationResponse
	AssetStatus                    = adrbaseservice.AssetStatus
	AssetEndpointProfileStatus     = adrbaseservice.AssetEndpointProfileStatus
	DetectedAsset                  = adrbaseservice.DetectedAsset
	DiscoveredAssetEndpointProfile = aeptypeservice.DiscoveredAssetEndpointProfile
)

// Error represents an error returned by the ADR service.
type Error struct {
	Message       string
	PropertyName  string
	PropertyValue string
	Condition     any
}

func (e *Error) Error() string {
	return e.Message
}

// Client manages interactions with the Azure Device Registry.
type Client struct {
	logger         *slog.Logger
	protocol       *protocol.Application
	mqtt           protocol.MqttClient
	adrBase        *adrbaseservice.AdrBaseServiceClient
	aepTypes       *aeptypeservice.AepTypeServiceClient
	observedAeps   map[string]struct{}
	observedAssets map[string]struct{}
	mu             sync.RWMutex
	listeners      protocol.Listeners
	onAssetUpdate  func(aepName string, asset *Asset) error
	onAepUpdate    func(aepName string, profile *AssetEndpointProfile) error
}

// ClientOption represents a single option for the client.
type ClientOption interface{ client(*ClientOptions) }

// ClientOptions are the resolved options for the client.
type ClientOptions struct {
	Logger        *slog.Logger
	OnAssetUpdate func(string, *Asset) error
	OnAepUpdate   func(string, *AssetEndpointProfile) error
}

type (
	withLogger struct{ *slog.Logger }

	withAssetUpdateHandler struct {
		fn func(string, *Asset) error
	}

	withAepUpdateHandler struct {
		fn func(string, *AssetEndpointProfile) error
	}
)

// New creates a new ADR client.
func New(
	app *protocol.Application,
	client protocol.MqttClient,
	opt ...ClientOption,
) (c *Client, err error) {
	var opts ClientOptions
	opts.Apply(opt)

	c = &Client{
		logger:         slog.Default(),
		protocol:       app,
		mqtt:           client,
		observedAeps:   map[string]struct{}{},
		observedAssets: map[string]struct{}{},
		onAssetUpdate:  opts.OnAssetUpdate,
		onAepUpdate:    opts.OnAepUpdate,
	}

	if opts.Logger != nil {
		c.logger = opts.Logger
	}

	listeners := protocol.Listeners{}
	defer func() {
		if err != nil {
			listeners.Close()
		}
	}()

	var telemetryHandlers adrbaseservice.AdrBaseServiceTelemetryHandlers

	adrBase, err := adrbaseservice.NewAdrBaseServiceClient(
		app,
		client,
		telemetryHandlers,
	)
	if err != nil {
		return nil, err
	}
	listeners = append(listeners, adrBase)

	aepTypes, err := aeptypeservice.NewAepTypeServiceClient(app, client)
	if err != nil {
		return nil, err
	}
	listeners = append(listeners, aepTypes)

	c.adrBase = adrBase
	c.aepTypes = aepTypes
	c.listeners = listeners

	assetUpdateReceiver, err := protocol.NewTelemetryReceiver(
		app,
		client,
		protocol.JSON[Asset]{},
		"adr/v1/assets/{aepName}/update",
		c.handleAssetUpdateTelemetry,
	)
	if err != nil {
		return nil, err
	}
	listeners = append(c.listeners, assetUpdateReceiver)

	aepUpdateReceiver, err := protocol.NewTelemetryReceiver(
		app,
		client,
		protocol.JSON[AssetEndpointProfile]{},
		"adr/v1/assetendpointprofiles/{aepName}/update",
		c.handleAepUpdateTelemetry,
	)
	if err != nil {
		return nil, err
	}
	listeners = append(c.listeners, aepUpdateReceiver)

	return c, nil
}

// Start starts the client and its listeners.
func (c *Client) Start(ctx context.Context) error {
	return c.listeners.Start(ctx)
}

// Close all underlying resources.
func (c *Client) Close(ctx context.Context) error {
	c.listeners.Close()
	return nil
}

// ObserveAssetEndpointProfileUpdates starts observation of asset endpoint profile updates.
func (c *Client) ObserveAssetEndpointProfileUpdates(
	ctx context.Context,
	aepName string,
) (*NotificationResponse, error) {
	c.logger.Debug(
		"Observing asset endpoint profile updates",
		"aepName",
		aepName,
	)

	req := adrbaseservice.NotifyOnAssetEndpointProfileUpdateRequestPayload{
		NotificationRequest: adrbaseservice.On,
	}

	resp, err := c.adrBase.NotifyOnAssetEndpointProfileUpdate(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	c.mu.Lock()
	c.observedAeps[aepName] = struct{}{}
	c.mu.Unlock()

	return &resp.Payload.NotificationResponse, nil
}

// UnobserveAssetEndpointProfileUpdates stops observation of asset endpoint profile updates.
func (c *Client) UnobserveAssetEndpointProfileUpdates(
	ctx context.Context,
	aepName string,
) (*NotificationResponse, error) {
	c.logger.Debug(
		"Unobserving asset endpoint profile updates",
		"aepName",
		aepName,
	)

	req := adrbaseservice.NotifyOnAssetEndpointProfileUpdateRequestPayload{
		NotificationRequest: adrbaseservice.Off,
	}

	resp, err := c.adrBase.NotifyOnAssetEndpointProfileUpdate(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	c.mu.Lock()
	delete(c.observedAeps, aepName)
	c.mu.Unlock()

	return &resp.Payload.NotificationResponse, nil
}

// ObserveAssetUpdates starts observation of asset updates.
func (c *Client) ObserveAssetUpdates(
	ctx context.Context,
	aepName, assetName string,
) (*NotificationResponse, error) {
	c.logger.Debug(
		"Observing asset updates",
		"aepName",
		aepName,
		"assetName",
		assetName,
	)

	req := adrbaseservice.NotifyOnAssetUpdateRequestPayload{
		NotificationRequest: adrbaseservice.NotifyOnAssetUpdateRequestSchema{
			AssetName:               assetName,
			NotificationMessageType: adrbaseservice.On,
		},
	}

	// Use the appropriate command invoker
	resp, err := c.adrBase.NotifyOnAssetUpdate(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	key := aepName + "~" + assetName
	c.mu.Lock()
	c.observedAssets[key] = struct{}{}
	c.mu.Unlock()

	return &resp.Payload.NotificationResponse, nil
}

// UnobserveAssetUpdates stops observation of asset updates.
func (c *Client) UnobserveAssetUpdates(
	ctx context.Context,
	aepName, assetName string,
) (*NotificationResponse, error) {
	c.logger.Debug(
		"Unobserving asset updates",
		"aepName",
		aepName,
		"assetName",
		assetName,
	)

	req := adrbaseservice.NotifyOnAssetUpdateRequestPayload{
		NotificationRequest: adrbaseservice.NotifyOnAssetUpdateRequestSchema{
			AssetName:               assetName,
			NotificationMessageType: adrbaseservice.Off,
		},
	}

	resp, err := c.adrBase.NotifyOnAssetUpdate(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	key := aepName + "~" + assetName
	c.mu.Lock()
	delete(c.observedAssets, key)
	c.mu.Unlock()

	return &resp.Payload.NotificationResponse, nil
}

// GetAssetEndpointProfile retrieves an asset endpoint profile by name.
func (c *Client) GetAssetEndpointProfile(
	ctx context.Context,
	aepName string,
) (*AssetEndpointProfile, error) {
	c.logger.Debug("Getting asset endpoint profile", "aepName", aepName)

	resp, err := c.adrBase.GetAssetEndpointProfile(
		ctx,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	return &resp.Payload.AssetEndpointProfile, nil
}

// UpdateAssetEndpointProfileStatus updates the status of an asset endpoint profile.
func (c *Client) UpdateAssetEndpointProfileStatus(
	ctx context.Context,
	aepName string,
	status *AssetEndpointProfileStatus,
) (*AssetEndpointProfile, error) {
	c.logger.Debug("Updating asset endpoint profile status", "aepName", aepName)

	req := adrbaseservice.UpdateAssetEndpointProfileStatusRequestPayload{
		AssetEndpointProfileStatusUpdate: *status,
	}

	resp, err := c.adrBase.UpdateAssetEndpointProfileStatus(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	return &resp.Payload.UpdatedAssetEndpointProfile, nil
}

// GetAsset retrieves an asset by name.
func (c *Client) GetAsset(
	ctx context.Context,
	aepName, assetName string,
) (*Asset, error) {
	c.logger.Debug("Getting asset", "aepName", aepName, "assetName", assetName)

	req := adrbaseservice.GetAssetRequestPayload{
		AssetName: assetName,
	}

	resp, err := c.adrBase.GetAsset(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	return &resp.Payload.Asset, nil
}

// UpdateAssetStatus updates the status of an asset.
func (c *Client) UpdateAssetStatus(
	ctx context.Context,
	aepName string,
	asset *Asset,
) (*Asset, error) {
	c.logger.Debug(
		"Updating asset status",
		"aepName",
		aepName,
		"assetName",
		asset.Name,
		asset.Status,
	)

	req := adrbaseservice.UpdateAssetStatusRequestPayload{
		AssetStatusUpdate: adrbaseservice.UpdateAssetStatusRequestSchema{
			AssetName:   asset.Name,
			AssetStatus: *asset.Status,
		},
	}

	resp, err := c.adrBase.UpdateAssetStatus(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	return &resp.Payload.UpdatedAsset, nil
}

// CreateDetectedAsset creates a detected asset.
func (c *Client) CreateDetectedAsset(
	ctx context.Context,
	aepName string,
	asset *DetectedAsset,
) (*adrbaseservice.CreateDetectedAssetResponsePayload, error) {
	req := adrbaseservice.CreateDetectedAssetRequestPayload{
		DetectedAsset: *asset,
	}

	resp, err := c.adrBase.CreateDetectedAsset(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	// Return the actual payload type instead of *NotificationResponse
	return &resp.Payload, nil
}

// CreateDiscoveredAssetEndpointProfile creates a discovered asset endpoint profile.
func (c *Client) CreateDiscoveredAssetEndpointProfile(
	ctx context.Context,
	aepName string,
	profile *DiscoveredAssetEndpointProfile,
) (*aeptypeservice.CreateDiscoveredAssetEndpointProfileResponsePayload, error) {
	c.logger.Debug(
		"Creating discovered asset endpoint profile",
		"aepName",
		aepName,
	)

	req := aeptypeservice.CreateDiscoveredAssetEndpointProfileRequestPayload{
		DiscoveredAssetEndpointProfile: *profile,
	}

	resp, err := c.aepTypes.CreateDiscoveredAssetEndpointProfile(
		ctx,
		req,
		protocol.WithTopicTokens{aepNameTokenKey: aepName},
	)
	if err != nil {
		return nil, translateError(err)
	}

	return &resp.Payload, nil
}

// handleAssetUpdateTelemetry processes asset update telemetry messages.
func (c *Client) handleAssetUpdateTelemetry(
	ctx context.Context,
	msg *protocol.TelemetryMessage[Asset],
) error {
	aepName := msg.TopicTokens[aepNameTokenKey]

	// Skip if we are not observing the asset.
	assetKey := aepName + "~" + msg.Payload.Name
	c.mu.RLock()
	_, observed := c.observedAssets[assetKey]
	c.mu.RUnlock()
	if !observed {
		msg.Ack()
		return nil
	}

	if c.onAssetUpdate == nil {
		msg.Ack()
		return nil
	}

	err := c.onAssetUpdate(aepName, &msg.Payload)
	msg.Ack()
	return err
}

func (c *Client) handleAepUpdateTelemetry(
	ctx context.Context,
	msg *protocol.TelemetryMessage[AssetEndpointProfile],
) error {
	aepName := msg.TopicTokens[aepNameTokenKey]

	// Skip if we are not observing the asset endpoint profile.
	c.mu.RLock()
	_, observed := c.observedAeps[aepName]
	c.mu.RUnlock()
	if !observed {
		msg.Ack()
		return nil
	}

	if c.onAepUpdate == nil {
		msg.Ack()
		return nil
	}

	err := c.onAepUpdate(aepName, &msg.Payload)
	msg.Ack()
	return err
}

// translateError converts protocol errors to client errors.
func translateError(err error) error {
	if err == nil {
		return nil
	}

	switch e := err.(type) {
	case *errors.Remote:
		switch k := e.Kind.(type) {
		case errors.ConfigurationInvalid:
			return &Error{
				Message:       err.Error(),
				PropertyName:  k.PropertyName,
				PropertyValue: fmt.Sprint(k.PropertyValue),
			}
		case errors.UnknownError:
			if k.PropertyName != "" {
				return &Error{
					Message:       err.Error(),
					PropertyName:  k.PropertyName,
					PropertyValue: fmt.Sprint(k.PropertyValue),
				}
			}
		case errors.StateInvalid:
			return &Error{
				Message:      err.Error(),
				PropertyName: k.PropertyName,
			}
		}

	case *errors.Client:
		switch k := e.Kind.(type) {
		case errors.ConfigurationInvalid:
			return &Error{
				Message:       err.Error(),
				PropertyName:  k.PropertyName,
				PropertyValue: fmt.Sprint(k.PropertyValue),
			}
		case errors.UnknownError:
			if k.PropertyName != "" {
				return &Error{
					Message:       err.Error(),
					PropertyName:  k.PropertyName,
					PropertyValue: fmt.Sprint(k.PropertyValue),
				}
			}
		}
	}

	return err
}

// Apply resolves the provided list of options.
func (o *ClientOptions) Apply(
	opts []ClientOption,
	rest ...ClientOption,
) {
	for opt := range options.Apply[ClientOption](opts, rest...) {
		opt.client(o)
	}
}

func (o withLogger) client(
	opt *ClientOptions,
) {
	opt.Logger = o.Logger
}

func (o withAssetUpdateHandler) client(
	opt *ClientOptions,
) {
	opt.OnAssetUpdate = o.fn
}

func (o withAepUpdateHandler) client(
	opt *ClientOptions,
) {
	opt.OnAepUpdate = o.fn
}

// WithLogger enables logging with the provided slog logger.
func WithLogger(logger *slog.Logger) ClientOption {
	return withLogger{logger}
}

// WithAssetUpdateHandler sets a handler for asset update events.
func WithAssetUpdateHandler(
	handler func(aepName string, asset *Asset) error,
) ClientOption {
	return withAssetUpdateHandler{handler}
}

// WithAepUpdateHandler sets a handler for asset endpoint profile update events.
func WithAepUpdateHandler(
	handler func(aepName string, profile *AssetEndpointProfile) error,
) ClientOption {
	return withAepUpdateHandler{handler}
}
