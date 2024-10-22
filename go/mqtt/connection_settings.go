// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"crypto/tls"
	"fmt"
	"net/url"
	"os"
	"strconv"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/sosodev/duration"
)

// Connection string example:
// HostName=localhost;TcpPort=1883;UseTls=True;ClientId=Test.
func (cs *connectionSettings) fromConnectionString(
	connStr string,
) error {
	settingsMap := parseToSettingsMap(connStr, ";")
	return cs.applySettingsMap(settingsMap)
}

// Environment variable example:
// MQTT_HOST_NAME=localhost
// MQTT_TCP_PORT = 8883
// MQTT_USE_TLS = true.
func (cs *connectionSettings) fromEnv() error {
	envVars := os.Environ()

	settingsMap := parseToSettingsMap(envVars, "=")
	return cs.applySettingsMap(settingsMap)
}

func parseToSettingsMap(
	input any,
	delimiter string,
) map[string]string {
	settingsMap := make(map[string]string)

	switch v := input.(type) {
	case string:
		// Parse connection string.
		v = strings.TrimSuffix(v, delimiter)
		params := strings.Split(v, delimiter)
		for _, param := range params {
			kv := strings.SplitN(param, "=", 2)
			if len(kv) == 2 {
				k := strings.ToLower(strings.TrimSpace(kv[0]))
				v := strings.TrimSpace(kv[1])
				settingsMap[k] = v
			}
		}
	case []string:
		// Parse environment variables.
		for _, envVar := range v {
			kv := strings.SplitN(envVar, delimiter, 2)
			if len(kv) == 2 && strings.HasPrefix(kv[0], "MQTT_") {
				k := strings.ToLower(
					strings.ReplaceAll(
						strings.TrimPrefix(kv[0], "MQTT_"),
						"_",
						"",
					),
				)
				v := strings.TrimSpace(kv[1])
				settingsMap[k] = v
			}
		}
	}
	return settingsMap
}

func (cs *connectionSettings) applySettingsMap(
	settingsMap map[string]string,
) error {
	if cs == nil {
		cs = &connectionSettings{}
	}
	if cs.authOptions == nil {
		cs.authOptions = &AuthOptions{}
	}

	if settingsMap["hostname"] == "" {
		return &errors.Error{
			Kind:         errors.ConfigurationInvalid,
			Message:      "HostName must not be empty",
			PropertyName: "HostName",
		}
	}

	if settingsMap["tcpport"] == "" {
		return &errors.Error{
			Kind:         errors.ConfigurationInvalid,
			Message:      "TcpPort must not be empty",
			PropertyName: "TcpPort",
		}
	}

	if settingsMap["usetls"] == "true" {
		cs.useTLS = true
		cs.serverURL = "tls://"
	} else {
		cs.serverURL = "tcp://"
	}
	cs.serverURL += settingsMap["hostname"]
	cs.serverURL += ":" + settingsMap["tcpport"]

	if password, exists := settingsMap["password"]; exists {
		cs.password = []byte(password)
	}

	assignIfExists(settingsMap, "clientid", &cs.clientID)
	assignIfExists(settingsMap, "username", &cs.username)
	assignIfExists(settingsMap, "passwordfile", &cs.passwordFile)
	assignIfExists(settingsMap, "certfile", &cs.certFile)
	assignIfExists(settingsMap, "keyfile", &cs.keyFile)
	assignIfExists(settingsMap, "keyfilepassword", &cs.keyFilePassword)
	assignIfExists(settingsMap, "cafile", &cs.caFile)

	if settingsMap["authmethod"] != "" || settingsMap["satAuthFile"] != "" {
		assignIfExists(
			settingsMap,
			"authmethod",
			&cs.authOptions.AuthMethod,
		)
		assignIfExists(
			settingsMap,
			"satauthfile",
			&cs.authOptions.SatAuthFile,
		)
	}

	cs.caRequireRevocationCheck = settingsMap["carequirerevocationcheck"] ==
		"true"

	if value, exists := settingsMap["keepalive"]; exists {
		keepAlive, err := duration.Parse(value)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "invalid KeepAlive in connection string",
				PropertyName:  "KeepAlive",
				PropertyValue: keepAlive,
			}
		}
		cs.keepAlive = keepAlive.ToTimeDuration()
	}

	if value, exists := settingsMap["sessionexpiry"]; exists {
		sessionExpiry, err := duration.Parse(value)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "invalid SessionExpiry in connection string",
				PropertyName:  "SessionExpiry",
				PropertyValue: sessionExpiry,
			}
		}
		cs.sessionExpiry = sessionExpiry.ToTimeDuration()
	}

	if value, exists := settingsMap["authinterval"]; exists {
		authinterval, err := duration.Parse(value)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "invalid AuthInterval in connection string",
				PropertyName:  "AuthInterval",
				PropertyValue: authinterval,
			}
		}
		cs.authOptions.AuthInterval = authinterval.ToTimeDuration()
	}

	if value, exists := settingsMap["receivemaximum"]; exists {
		receiveMaximum, err := strconv.ParseUint(value, 10, 16)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "invalid ReceiveMaximum in connection string",
				PropertyName:  "ReceiveMaximum",
				PropertyValue: receiveMaximum,
			}
		}
		cs.receiveMaximum = uint16(receiveMaximum)
	}

	if value, exists := settingsMap["connectiontimeout"]; exists {
		connectionTimeout, err := duration.Parse(value)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "invalid ConnectionTimeout in connection string",
				PropertyName:  "ConnectionTimeout",
				PropertyValue: connectionTimeout,
			}
		}
		cs.connectionTimeout = connectionTimeout.ToTimeDuration()
	}

	// Provide a random clientID by default.
	if cs.clientID == "" {
		cs.clientID = randomClientID()
	}

	// Ensure receiveMaximum is set correctly.
	if cs.receiveMaximum == 0 {
		cs.receiveMaximum = defaultReceiveMaximum
	}

	// Ensure AuthInterval is set correctly.
	if cs.authOptions.AuthInterval == 0 {
		cs.authOptions.AuthInterval = defaultAuthInterval
	}

	return nil
}

// validate validates connection config after the client is set up.
func (cs *connectionSettings) validate() error {
	if _, err := url.Parse(cs.serverURL); err != nil {
		return &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "server URL is not valid",
			PropertyName:  "serverURL",
			PropertyValue: cs.serverURL,
		}
	}

	if cs.keepAlive.Seconds() > float64(maxKeepAlive) {
		return &errors.Error{
			Kind: errors.ConfigurationInvalid,
			Message: fmt.Sprintf(
				"keepAlive cannot be more than %d seconds",
				maxKeepAlive,
			),
			PropertyName:  "keepAlive",
			PropertyValue: cs.keepAlive,
		}
	}

	if cs.sessionExpiry.Seconds() > float64(maxSessionExpiry) {
		return &errors.Error{
			Kind: errors.ConfigurationInvalid,
			Message: fmt.Sprintf(
				"sessionExpiry cannot be more than %d seconds",
				maxSessionExpiry,
			),
			PropertyName:  "sessionExpiry",
			PropertyValue: cs.sessionExpiry,
		}
	}

	if cs.authOptions.SatAuthFile != "" {
		data, err := readFileAsBytes(cs.authOptions.SatAuthFile)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "cannot read auth data from SatAuthFile",
				PropertyName:  "SatAuthFile",
				PropertyValue: cs.authOptions.SatAuthFile,
				NestedError:   err,
			}
		}

		cs.authOptions.AuthData = data
	}

	return cs.validateTLS()
}

// validateTLS validates and set TLS related config.
func (cs *connectionSettings) validateTLS() error {
	if cs.useTLS {
		if cs.tlsConfig == nil {
			cs.tlsConfig = &tls.Config{
				// Bypasses hostname check in TLS config
				// since sometimes we connect to localhost not the actual pod.
				InsecureSkipVerify: true, // #nosec G402
				MinVersion:         tls.VersionTLS12,
				MaxVersion:         tls.VersionTLS13,
			}
		}

		// Both certFile and keyFile must be provided together.
		// An error will be returned if only one of them is provided.
		if cs.certFile != "" || cs.keyFile != "" {
			var cert tls.Certificate
			var err error

			if cs.keyFilePassword != "" {
				cert, err = loadX509KeyPairWithPassword(
					cs.certFile,
					cs.keyFile,
					cs.keyFilePassword,
				)
			} else {
				cert, err = tls.LoadX509KeyPair(cs.certFile, cs.keyFile)
			}

			if err != nil {
				return &errors.Error{
					Kind:         errors.ConfigurationInvalid,
					Message:      "X509 key pair cannot be loaded",
					PropertyName: "certFile/keyFile",
					NestedError:  err,
				}
			}

			cs.tlsConfig.Certificates = []tls.Certificate{cert}
		}

		if cs.caFile != "" {
			caCertPool, err := loadCACertPool(cs.caFile)
			if err != nil {
				return &errors.Error{
					Kind: errors.ConfigurationInvalid,
					Message: "cannot load a CA certificate pool " +
						"from caFile",
					PropertyName:  "caFile",
					PropertyValue: cs.caFile,
					NestedError:   err,
				}
			}
			// Set RootCAs for server verification.
			cs.tlsConfig.RootCAs = caCertPool
		}
	} else if cs.certFile != "" ||
		cs.keyFile != "" ||
		cs.caFile != "" ||
		cs.tlsConfig != nil {
		return &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "TLS should not be set when useTLS flag is disabled",
			PropertyName:  "useTLS",
			PropertyValue: cs.useTLS,
		}
	}

	return nil
}

// assignIfExists assigns non-empty string values from settingsMap to the
// corresponding fields in connection settings.
func assignIfExists(
	settingsMap map[string]string,
	key string,
	field *string,
) {
	if value, exists := settingsMap[key]; exists && value != "" {
		*field = value
	}
}
