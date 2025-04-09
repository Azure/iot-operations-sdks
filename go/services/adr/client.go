// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package adr

import (
	"context"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/adr/adrbaseservice"
	"github.com/Azure/iot-operations-sdks/go/services/adr/aeptypeservice"
)

const defaultTimeout = 10 * time.Second

// Client represents a client for interacting with the Asset and Device Registry service.
type Client struct {
	protocol.Listeners
	assetServiceClient          *adrbaseservice.AssetServiceClient
	assetEndpointProfileClient  *aeptypeservice.AssetEndpointProfileServiceClient
	mu                          sync.Mutex
	observingAssetUpdates       bool
	observingAepUpdates         bool
	assetUpdateCh               chan AssetUpdate
	aepUpdateCh                 chan AepUpdate
	log                         *slog.Logger
}

// AssetUpdate represents an asset update event
type AssetUpdate struct {
	AepName string
	Asset   *adrbaseservice.Asset
}

// AepUpdate represents an asset endpoint profile update event
type AepUpdate struct {
	AepName string
	Profile *adrbaseservice.AssetEndpointProfile
}

// ClientOption represents options for configuring the client
type ClientOption func(*Client)

// WithLogger sets the logger for the client
func WithLogger(logger *slog.Logger) ClientOption {
	return func(c *Client) {
		c.log = logger
	}
}

// New creates a new ADR service client
func New(app *protocol.Application, mqttClient protocol.MqttClient, opts ...ClientOption) (*Client, error) {
	assetClient, err := adrbaseservice.NewAssetServiceClient(app, mqttClient)
	if err != nil {
		return nil, fmt.Errorf("failed to create asset service client: %w", err)
	}

	aepClient, err := aeptypeservice.NewAssetEndpointProfileServiceClient(app, mqttClient)
	if err != nil {
		return nil, fmt.Errorf("failed to create asset endpoint profile service client: %w", err)
	}

	client := &Client{
		assetServiceClient:         assetClient,
		assetEndpointProfileClient: aepClient,
		assetUpdateCh:              make(chan AssetUpdate),
		aepUpdateCh:                make(chan AepUpdate),
		log:                        slog.Default(),
	}

	for _, opt := range opts {
		opt(client)
	}

	client.Listeners = append(client.Listeners, assetClient, aepClient)

	return client, nil
}

// Start initializes the client
func (c *Client) Start(ctx context.Context) error {
	// Start the underlying clients
	if err := c.assetServiceClient.Start(ctx); err != nil {
		return err
	}
	return c.assetEndpointProfileClient.Start(ctx)
}

// Close releases all resources
func (c *Client) Close() {
	c.Listeners.Close()
	close(c.assetUpdateCh)
	close(c.aepUpdateCh)
}

// AssetUpdates returns a channel that receives asset update notifications
func (c *Client) AssetUpdates() <-chan AssetUpdate {
	return c.assetUpdateCh
}

// AepUpdates returns a channel that receives asset endpoint profile update notifications
func (c *Client) AepUpdates() <-chan AepUpdate {
	return c.aepUpdateCh
}

// ObserveAssetEndpointProfileUpdates subscribes to asset endpoint profile updates
func (c *Client) ObserveAssetEndpointProfileUpdates(ctx context.Context, aepName string) (*adrbaseservice.NotificationResponse, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	
	c.observingAepUpdates = true
	
	notifyRequest := &adrbaseservice.NotifyOnAssetEndpointProfileUpdateRequestPayload{
		NotificationRequest: adrbaseservice.NotificationMessageTypeOn,
	}

	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.NotifyOnAssetEndpointProfileUpdate(
		ctx, 
		*notifyRequest,
		protocol.WithTopicTokens(additionalTokens),
	)
	
	if err != nil {
		return nil, err
	}
	
	return &resp.NotificationResponse, nil
}

// UnobserveAssetEndpointProfileUpdates unsubscribes from asset endpoint profile updates
func (c *Client) UnobserveAssetEndpointProfileUpdates(ctx context.Context, aepName string) (*adrbaseservice.NotificationResponse, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	
	notifyRequest := &adrbaseservice.NotifyOnAssetEndpointProfileUpdateRequestPayload{
		NotificationRequest: adrbaseservice.NotificationMessageTypeOff,
	}

	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.NotifyOnAssetEndpointProfileUpdate(
		ctx, 
		*notifyRequest,
		protocol.WithTopicTokens(additionalTokens),
	)
	
	if err != nil {
		return nil, err
	}
	
	c.observingAepUpdates = false
	return &resp.NotificationResponse, nil
}

// GetAssetEndpointProfile gets an asset endpoint profile by name
func (c *Client) GetAssetEndpointProfile(ctx context.Context, aepName string) (*adrbaseservice.AssetEndpointProfile, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.GetAssetEndpointProfile(
		ctx,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.AssetEndpointProfile, nil
}

// UpdateAssetEndpointProfileStatus updates the status of an asset endpoint profile
func (c *Client) UpdateAssetEndpointProfileStatus(
	ctx context.Context, 
	aepName string, 
	requestPayload adrbaseservice.UpdateAssetEndpointProfileStatusRequestPayload,
) (*adrbaseservice.AssetEndpointProfile, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.UpdateAssetEndpointProfileStatus(
		ctx,
		requestPayload,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.UpdatedAssetEndpointProfile, nil
}

// ObserveAssetUpdates subscribes to asset updates
func (c *Client) ObserveAssetUpdates(ctx context.Context, aepName string, assetName string) (*adrbaseservice.NotificationResponse, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	
	c.observingAssetUpdates = true
	
	notifyRequest := &adrbaseservice.NotifyOnAssetUpdateRequestPayload{
		NotificationRequest: &adrbaseservice.NotifyOnAssetUpdateRequestSchema{
			AssetName:              assetName,
			NotificationMessageType: adrbaseservice.NotificationMessageTypeOn,
		},
	}

	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.NotifyOnAssetUpdate(
		ctx, 
		*notifyRequest,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return &resp.NotificationResponse, nil
}

// UnobserveAssetUpdates unsubscribes from asset updates
func (c *Client) UnobserveAssetUpdates(ctx context.Context, aepName string, assetName string) (*adrbaseservice.NotificationResponse, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	
	notifyRequest := &adrbaseservice.NotifyOnAssetUpdateRequestPayload{
		NotificationRequest: &adrbaseservice.NotifyOnAssetUpdateRequestSchema{
			AssetName:              assetName,
			NotificationMessageType: adrbaseservice.NotificationMessageTypeOff,
		},
	}

	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.NotifyOnAssetUpdate(
		ctx, 
		*notifyRequest,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	c.observingAssetUpdates = false
	return &resp.NotificationResponse, nil
}

// GetAsset gets an asset by name
func (c *Client) GetAsset(ctx context.Context, aepName string, requestPayload adrbaseservice.GetAssetRequestPayload) (*adrbaseservice.Asset, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.GetAsset(
		ctx,
		requestPayload,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.Asset, nil
}

// UpdateAssetStatus updates the status of an asset
func (c *Client) UpdateAssetStatus(
	ctx context.Context, 
	aepName string, 
	requestPayload adrbaseservice.UpdateAssetStatusRequestPayload,
) (*adrbaseservice.Asset, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.UpdateAssetStatus(
		ctx,
		requestPayload,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.UpdatedAsset, nil
}

// CreateDetectedAsset creates a detected asset
func (c *Client) CreateDetectedAsset(
	ctx context.Context, 
	aepName string, 
	requestPayload adrbaseservice.CreateDetectedAssetRequestPayload,
) (*adrbaseservice.CreateDetectedAssetResponseSchema, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetServiceClient.CreateDetectedAsset(
		ctx,
		requestPayload,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.CreateDetectedAssetResponse, nil
}

// CreateDiscoveredAssetEndpointProfile creates a discovered asset endpoint profile
func (c *Client) CreateDiscoveredAssetEndpointProfile(
	ctx context.Context, 
	aepName string, 
	requestPayload aeptypeservice.CreateDiscoveredAssetEndpointProfileRequestPayload,
) (*aeptypeservice.CreateDiscoveredAssetEndpointProfileResponseSchema, error) {
	additionalTokens := map[string]string{"aepName": aepName}
	
	resp, err := c.assetEndpointProfileClient.CreateDiscoveredAssetEndpointProfile(
		ctx,
		requestPayload,
		protocol.WithTopicTokens(additionalTokens),
		protocol.WithTimeout(defaultTimeout),
	)
	
	if err != nil {
		return nil, err
	}
	
	return resp.CreateDiscoveredAssetEndpointProfileResponse, nil
}
