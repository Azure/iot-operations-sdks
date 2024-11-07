// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"crypto/tls"
	"math"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/sosodev/duration"
)

// Connection string example:
// HostName=localhost;TcpPort=1883;UseTls=True;ClientId=Test.
func configFromConnectionString(
	connStr string,
) (*connectionConfig, error) {
	return configFromMap(parseToMap(connStr, ";"))
}

// Environment variable example:
// MQTT_HOST_NAME=localhost
// MQTT_TCP_PORT = 8883
// MQTT_USE_TLS = true.
func configFromEnv() (*connectionConfig, error) {
	envVars := os.Environ()
	return configFromMap(parseToMap(envVars, "="))
}

func parseToMap(input any, delimiter string) map[string]string {
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

// Determined from this spec:
// https://github.com/Azure/iot-operations-sdks/blob/cab472bbb6f74b65db2d2c94efe14f1f1f88ccbc/doc/reference/connection-settings.md
func configFromMap(settingsMap map[string]string) (*connectionConfig, error) {
	cleanStart := true
	if cleanStartStr := settingsMap["cleanstart"]; cleanStartStr != "" {
		var err error
		cleanStart, err = strconv.ParseBool(cleanStartStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse CleanStart as a boolean",
				wrapped: err,
			}
		}
	}

	var keepAlive uint16 = 60
	if keepAliveStr := settingsMap["keepalive"]; keepAliveStr != "" {
		parsedDuration, err := duration.Parse(keepAliveStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse KeepAlive as an ISO8601 duration",
				wrapped: err,
			}
		}
		seconds := parsedDuration.ToTimeDuration().Seconds()
		if seconds > math.MaxUint16 || seconds < 0 {
			return nil, &InvalidArgumentError{
				message: "KeepAlive is outside of the valid MQTT range",
			}
		}
		keepAlive = uint16(seconds)
	}

	clientID := internal.RandomClientID()
	if clientIDStr := settingsMap["clientid"]; clientIDStr != "" {
		clientID = clientIDStr
	}

	var sessionExpiryInterval uint32 = 3600
	if sessionExpiryStr := settingsMap["sessionexpiry"]; sessionExpiryStr != "" {
		parsedDuration, err := duration.Parse(sessionExpiryStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse SessionExpiry as an ISO8601 duration",
				wrapped: err,
			}
		}
		seconds := parsedDuration.ToTimeDuration().Seconds()
		if seconds > math.MaxUint32 || seconds < 0 {
			return nil, &InvalidArgumentError{
				message: "SessionExpiry is outside of the valid MQTT range",
			}
		}
		sessionExpiryInterval = uint32(seconds)
	}

	connectionTimeout := 30 * time.Second
	if connectionTimeoutStr := settingsMap["connectiontimeout"]; connectionTimeoutStr != "" {
		parsedDuration, err := duration.Parse(connectionTimeoutStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse ConnectionTimeout as an ISO8601 duration",
				wrapped: err,
			}
		}
		connectionTimeout = parsedDuration.ToTimeDuration()
	}

	var userName UserNameProvider = defaultUserName
	if usernameStr := settingsMap["username"]; usernameStr != "" {
		userName = ConstantUserName(usernameStr)
	}

	var password PasswordProvider = defaultPassword
	if passwordStr := settingsMap["password"]; passwordStr != "" {
		password = ConstantPassword([]byte(passwordStr))
	}
	if passwordFileStr := settingsMap["passwordfile"]; passwordFileStr != "" {
		if password != nil {
			return nil, &InvalidArgumentError{
				message: "Password and PasswordFile are both provided, but only one may be used",
			}
		}
		password = FilePassword(passwordFileStr)
	}

	var authProvider auth.Provider
	if satAuthFileStr := settingsMap["satauthfile"]; satAuthFileStr != "" {
		authProvider = auth.NewMQServiceAccountToken(satAuthFileStr)
	}

	hostname := settingsMap["hostname"]
	if hostname == "" {
		return nil, &InvalidArgumentError{message: "HostName must be provided"}
	}

	port := 8883
	if portStr := settingsMap["tcpport"]; portStr != "" {
		var err error
		port, err = strconv.Atoi(portStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse TcpPort as an integer",
				wrapped: err,
			}
		}
	}

	useTLS := true
	if useTLSStr := settingsMap["usetls"]; useTLSStr != "" {
		var err error
		useTLS, err = strconv.ParseBool(useTLSStr)
		if err != nil {
			return nil, &InvalidArgumentError{
				message: "unable to parse UseTls as a boolean",
				wrapped: err,
			}
		}
	}

	config := &connectionConfig{
		clientID:                  clientID,
		userNameProvider:          userName,
		passwordProvider:          password,
		authProvider:              authProvider,
		firstConnectionCleanStart: cleanStart,
		keepAlive:                 keepAlive,
		sessionExpiryInterval:     sessionExpiryInterval,
		connectionTimeout:         connectionTimeout,
		receiveMaximum:            math.MaxUint16,
	}

	certFileStr := settingsMap["certfile"]
	keyFileStr := settingsMap["keyfile"]
	caFileStr := settingsMap["cafile"]
	if !useTLS {
		if certFileStr != "" {
			return nil, &InvalidArgumentError{
				message: "CertFile must not be provided if UseTls is false",
			}
		}
		if keyFileStr != "" {
			return nil, &InvalidArgumentError{
				message: "KeyFile must not be provided if UseTls is false",
			}
		}
		if caFileStr != "" {
			return nil, &InvalidArgumentError{
				message: "CaFile must not be provided if UseTls is false",
			}
		}
		config.connectionProvider = TCPConnection(hostname, port)
		return config, nil
	}

	if (certFileStr != "" || keyFileStr != "") &&
		(certFileStr == "" || keyFileStr == "") {
		return nil, &InvalidArgumentError{
			message: "both CertFile and KeyFile must be provided if using X509 authentication",
		}
	}

	tlsConfigProvider := func(context.Context) (*tls.Config, error) {
		tlsConfig := &tls.Config{MinVersion: tls.VersionTLS12}
		if certFileStr != "" || keyFileStr != "" {
			var cert tls.Certificate
			var err error
			if keyFilePasswordStr := settingsMap["keyfilepassword"]; keyFilePasswordStr != "" {
				cert, err = loadX509KeyPairWithPassword(
					certFileStr,
					keyFileStr,
					keyFilePasswordStr,
				)
			} else {
				cert, err = tls.LoadX509KeyPair(certFileStr, keyFileStr)
			}
			if err != nil {
				return nil, &InvalidArgumentError{
					message: "unable to load X509 key pair",
					wrapped: err,
				}
			}
			tlsConfig.Certificates = []tls.Certificate{cert}
		}

		if caFileStr != "" {
			caCertPool, err := loadCACertPool(caFileStr)
			if err != nil {
				return nil, &InvalidArgumentError{
					message: "unable to load CA cert",
					wrapped: err,
				}
			}
			tlsConfig.RootCAs = caCertPool
		}
		return tlsConfig, nil
	}

	config.connectionProvider = TLSConnection(hostname, port, tlsConfigProvider)
	return config, nil
}
