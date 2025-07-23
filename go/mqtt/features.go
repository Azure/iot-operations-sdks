// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

// AIOPersistence is the user-property used to indicate to the AIO broker that
// it should persist messages to disk.
const AIOPersistence = "aio-persistence"

// WithConnectPersist is a convenience option to set the AIO persistence flag on
// the CONNECT request.
func WithConnectPersist() SessionClientOption {
	return WithConnectUserProperties{AIOPersistence: "true"}
}

// WithPersist is a convenience option to set the AIO persistence flag on a
// PUBLISH request.
func WithPersist() PublishOption {
	return WithUserProperties{AIOPersistence: "true"}
}

// Utility to check for AIO Broker features in user properties.
func (o *SessionClientOptions) checkFeatures(p map[string]string) error {
	if o.DisableAIOBrokerFeatures && p != nil {
		if _, ok := p[AIOPersistence]; ok {
			return &InvalidAIOBrokerFeature{
				feature: AIOPersistence,
			}
		}
	}
	return nil
}

// Utility to initialize features inherent to the session client.
func (o *SessionClientOptions) initFeatures() error {
	if err := o.checkFeatures(o.ConnectUserProperties); err != nil {
		return err
	}
	if !o.DisableAIOBrokerFeatures {
		if o.ConnectUserProperties == nil {
			o.ConnectUserProperties = make(map[string]string, 1)
		}
		o.ConnectUserProperties["metriccategory"] = "aiosdk-go"
	}
	return nil
}
