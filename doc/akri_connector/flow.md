# Akri Connector Application Flow

## Overview

There are two main flows within an Akri Connector, [Asset discovery](#asset-discovery) and [Protocol translation](#protocol-translation). Below outlines the steps involved to implement each flow.

[TODO] review and flesh out content

## Setup

* Get the broker configuration:
    * ConnectionSettings.FromFileMount
* Connect to broker: 
    * SessionClient.Connect
* Get AEP: 
    * AdrClient.GetAssetendpointProfile
* Connect to AEP:
* Observe the AEP for changes: 
    * AdrClient.ObserverAssetEndpointProfile

## Asset discovery

Asset discovery is an optional step if the Asset Endpoint supports discoverable elements. Discovery usually occurs when the application starts, and it can also continue throughout the lifetime of the application if assets can be dynamically added to the endpoint service.

[TODO] Define how to discover

## Protocol translation

* Get the list of Asset definitions for the endpoint:
    * AdrClient.GetAssetIds
* Get each Asset:
    * AdrClient.GetAsset
* Observe each Asset for changes:
    * AdrClient.ObserveAsset

* Receive loop (endpoint -> mqtt)
    * Wait for Asset data (either polling or notified)
    * Get Asset payload from end endpoint
    * Construct MQTT message
    * Add payload based on Asset definition
    * SendTelemetry with model ID

* Send loop (mqtt -> endpoint)
    * 
