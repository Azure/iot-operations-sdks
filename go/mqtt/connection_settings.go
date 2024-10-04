package mqtt

import (
	"crypto/tls"
	"fmt"
	"net/url"
	"os"
	"strconv"
	"strings"

	"github.com/sosodev/duration"
)

// TODO: Uncomment and adjust auth-related connection settings code once the
// auth interfaces in the session client are determined.

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
	// if cs.authOptions == nil {
	// 	cs.authOptions = &AuthOptions{}
	// }

	if settingsMap["hostname"] == "" {
		return &InvalidArgumentError{message: "HostName must not be empty"}
	}

	if settingsMap["tcpport"] == "" {
		return &InvalidArgumentError{message: "TcpPort must not be empty"}
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

	// if settingsMap["authmethod"] != "" || settingsMap["satAuthFile"] != "" {
	// 	assignIfExists(
	// 		settingsMap,
	// 		"authmethod",
	// 		&cs.authOptions.AuthMethod,
	// 	)
	// 	assignIfExists(
	// 		settingsMap,
	// 		"satauthfile",
	// 		&cs.authOptions.SatAuthFile,
	// 	)
	// }

	cs.caRequireRevocationCheck = settingsMap["carequirerevocationcheck"] ==
		"true"

	if value, exists := settingsMap["keepalive"]; exists {
		keepAlive, err := duration.Parse(value)
		if err != nil {
			return &InvalidArgumentError{
				message:      "invalid KeepAlive in connection string",
				WrappedError: err,
			}
		}
		cs.keepAlive = keepAlive.ToTimeDuration()
	}

	if value, exists := settingsMap["sessionexpiry"]; exists {
		sessionExpiry, err := duration.Parse(value)
		if err != nil {
			return &InvalidArgumentError{
				message:      "invalid SessionExpiry in connection string",
				WrappedError: err,
			}
		}
		cs.sessionExpiry = sessionExpiry.ToTimeDuration()
	}

	// if value, exists := settingsMap["authinterval"]; exists {
	// 	authinterval, err := duration.Parse(value)
	// 	if err != nil {
	// 		return &InvalidArgumentError{
	// 			message: "invalid AuthInterval in connection string",
	// 			WrappedError: err,
	// 		}
	// 	}
	// 	cs.authOptions.AuthInterval = authinterval.ToTimeDuration()
	// }

	if value, exists := settingsMap["receivemaximum"]; exists {
		receiveMaximum, err := strconv.ParseUint(value, 10, 16)
		if err != nil {
			return &InvalidArgumentError{
				message:      "invalid ReceiveMaximum in connection string",
				WrappedError: err,
			}
		}
		cs.receiveMaximum = uint16(receiveMaximum)
	}

	if value, exists := settingsMap["connectiontimeout"]; exists {
		connectionTimeout, err := duration.Parse(value)
		if err != nil {
			return &InvalidArgumentError{
				message:      "invalid ConnectionTimeout in connection string",
				WrappedError: err,
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
	// if cs.authOptions.AuthInterval == 0 {
	// 	cs.authOptions.AuthInterval = defaultAuthInterval
	// }

	return nil
}

// validate validates connection config after the client is set up.
func (cs *connectionSettings) validate() error {
	if _, err := url.Parse(cs.serverURL); err != nil {
		return &InvalidArgumentError{
			message:      "server URL is not valid",
			WrappedError: err,
		}
	}

	if cs.keepAlive.Seconds() > float64(maxKeepAlive) {
		return &InvalidArgumentError{
			message: fmt.Sprintf(
				"keepAlive cannot be more than %d seconds",
				maxKeepAlive,
			),
		}
	}

	if cs.sessionExpiry.Seconds() > float64(maxSessionExpiry) {
		return &InvalidArgumentError{
			message: fmt.Sprintf(
				"sessionExpiry cannot be more than %d seconds",
				maxSessionExpiry,
			),
		}
	}

	// if cs.authOptions.SatAuthFile != "" {
	// 	data, err := readFileAsBytes(cs.authOptions.SatAuthFile)
	// 	if err != nil {
	// 		return &InvalidArgumentError{
	// 			message:      "cannot read auth data from SatAuthFile",
	// 			WrappedError: err,
	// 		}
	// 	}

	// 	cs.authOptions.AuthData = data
	// }

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
				return &InvalidArgumentError{
					message:      "X509 key pair cannot be loaded",
					WrappedError: err,
				}
			}

			cs.tlsConfig.Certificates = []tls.Certificate{cert}
		}

		if cs.caFile != "" {
			caCertPool, err := loadCACertPool(cs.caFile)
			if err != nil {
				return &InvalidArgumentError{
					message:      "cannot load a CA certificate pool from caFile",
					WrappedError: err,
				}
			}
			// Set RootCAs for server verification.
			cs.tlsConfig.RootCAs = caCertPool
		}
	} else if cs.certFile != "" ||
		cs.keyFile != "" ||
		cs.caFile != "" ||
		cs.tlsConfig != nil {
		return &InvalidArgumentError{
			message: "TLS should not be set when useTLS flag is disabled",
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
