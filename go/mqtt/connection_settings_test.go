// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

// TODO: Redo connection settings tests once the design of connection settings
// becomes clear

// func TestFromConnectionString(t *testing.T) {
// 	tests := []struct {
// 		name         string
// 		connString   string
// 		connSettings *connectionSettings
// 		throwError   bool
// 	}{
// 		{
// 			name: "Valid connection string",
// 			connString: "ClientId=testClient;" +
// 				"Username=testUser;" +
// 				"Password=testPass;" +
// 				"UseTLS=true;" +
// 				"HostName=test.mqtt.com;" +
// 				"TcpPort=8883;" +
// 				"KeepAlive=PT60S;" +
// 				"SessionExpiry=PT30M;" +
// 				"receiveMaximum=10;" +
// 				"CertFile=cert.pem;" +
// 				"KeyFile=key.pem;" +
// 				"CAFile=ca.pem;" +
// 				"AuthMethod=basic;" +
// 				"SatAuthFile=auth.data;" +
// 				"AuthInterval=PT10S",
// 			connSettings: &connectionSettings{
// 				clientID:       "testClient",
// 				username:       "testUser",
// 				password:       []byte("testPass"),
// 				useTLS:         true,
// 				serverURL:      "tls://test.mqtt.com:8883",
// 				keepAlive:      60 * time.Second,
// 				sessionExpiry:  30 * time.Minute,
// 				receiveMaximum: 10,
// 				certFile:       "cert.pem",
// 				keyFile:        "key.pem",
// 				caFile:         "ca.pem",
// 				authOptions: &AuthOptions{
// 					AuthMethod:       "basic",
// 					SatAuthFile:      "auth.data",
// 					AuthData:         []byte(nil),
// 					AuthDataProvider: nil,
// 					AuthInterval:     10 * time.Second,
// 					AuthHandler:      nil,
// 				},
// 			},
// 			throwError: false,
// 		},
// 		{
// 			name: "Valid connection string but invalid TLS connection settings",
// 			connString: "ClientId=testClient;" +
// 				"Username=testUser;" +
// 				"Password=testPass;" +
// 				"UseTLS=false;" +
// 				"HostName=localhost;" +
// 				"TcpPort=1883;" +
// 				"KeepAlive=P1D;" +
// 				"CertFile=cert.pem;" +
// 				"KeyFile=key.pem;" +
// 				"CAFile=ca.pem;" +
// 				"AuthMethod=K8S-SAT;" +
// 				"SatAuthFile=authData.txt",
// 			connSettings: &connectionSettings{
// 				clientID:       "testClient",
// 				username:       "testUser",
// 				password:       []byte("testPass"),
// 				useTLS:         false,
// 				serverURL:      "tcp://localhost:1883",
// 				keepAlive:      24 * time.Hour,
// 				receiveMaximum: 65535,
// 				certFile:       "cert.pem",
// 				keyFile:        "key.pem",
// 				caFile:         "ca.pem",
// 				authOptions: &AuthOptions{
// 					AuthMethod:       "K8S-SAT",
// 					SatAuthFile:      "authData.txt",
// 					AuthData:         []byte(nil),
// 					AuthDataProvider: nil,
// 					AuthInterval:     20 * time.Minute,
// 					AuthHandler:      nil,
// 				},
// 			},
// 			throwError: false,
// 		},
// 		{
// 			name: "Invalid connection string without hostName",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Invalid connection string segment",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;" +
// 				"invalidsegment",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Invalid keepalive value",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;" +
// 				"usetls=true;" +
// 				"hostname=test.mqtt.com;" +
// 				"tcpport=8883;" +
// 				"keepalive=invalid",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Invalid sessionExpiry value",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;" +
// 				"usetls=true;" +
// 				"hostname=test.mqtt.com;" +
// 				"tcpport=8883;" +
// 				"SessionExpiry=invalid",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Invalid receiveMaximum value",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;" +
// 				"usetls=true;" +
// 				"hostname=test.mqtt.com;" +
// 				"tcpport=8883;" +
// 				"receiveMaximum=invalid",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Valid connection string with connection timeout",
// 			connString: "ClientId=testClient;" +
// 				"Username=testUser;" +
// 				"Password=testPass;" +
// 				"UseTLS=true;" +
// 				"HostName=test.mqtt.com;" +
// 				"TcpPort=8883;" +
// 				"ConnectionTimeout=PT10S",
// 			connSettings: &connectionSettings{
// 				clientID:          "testClient",
// 				username:          "testUser",
// 				password:          []byte("testPass"),
// 				useTLS:            true,
// 				serverURL:         "tls://test.mqtt.com:8883",
// 				connectionTimeout: 10 * time.Second,
// 				receiveMaximum:    65535,
// 				// authOptions: &AuthOptions{
// 				// 	AuthMethod:       "",
// 				// 	SatAuthFile:      "",
// 				// 	AuthData:         []byte(nil),
// 				// 	AuthDataProvider: nil,
// 				// 	AuthInterval:     20 * time.Minute,
// 				// 	AuthHandler:      nil,
// 				// },
// 			},
// 			throwError: false,
// 		},
// 		{
// 			name: "Invalid connection timeout value",
// 			connString: "clientId=testClient;" +
// 				"username=testUser;" +
// 				"password=testPass;" +
// 				"usetls=true;" +
// 				"hostname=test.mqtt.com;" +
// 				"tcpport=8883;" +
// 				"connectiontimeout=invalid",
// 			connSettings: nil,
// 			throwError:   true,
// 		},
// 		{
// 			name: "Valid connection string with password file",
// 			connString: "ClientId=testClient;" +
// 				"Username=testUser;" +
// 				"PasswordFile=password.txt;" +
// 				"UseTLS=true;" +
// 				"HostName=test.mqtt.com;" +
// 				"TcpPort=8883;" +
// 				"KeepAlive=PT60S",
// 			connSettings: &connectionSettings{
// 				clientID:       "testClient",
// 				username:       "testUser",
// 				passwordFile:   "password.txt",
// 				useTLS:         true,
// 				serverURL:      "tls://test.mqtt.com:8883",
// 				keepAlive:      60 * time.Second,
// 				receiveMaximum: 65535,
// 				authOptions: &AuthOptions{
// 					AuthMethod:       "",
// 					SatAuthFile:      "",
// 					AuthData:         []byte(nil),
// 					AuthDataProvider: nil,
// 					AuthInterval:     20 * time.Minute,
// 					AuthHandler:      nil,
// 				},
// 			},
// 			throwError: false,
// 		},
// 		{
// 			name: "Valid connection string with CA require revocation check",
// 			connString: "ClientId=testClient;" +
// 				"Username=testUser;" +
// 				"Password=testPass;" +
// 				"UseTLS=true;" +
// 				"HostName=test.mqtt.com;" +
// 				"TcpPort=8883;" +
// 				"CARequireRevocationCheck=true;" +
// 				"KeepAlive=PT60S",
// 			connSettings: &connectionSettings{
// 				clientID:                 "testClient",
// 				username:                 "testUser",
// 				password:                 []byte("testPass"),
// 				useTLS:                   true,
// 				serverURL:                "tls://test.mqtt.com:8883",
// 				caRequireRevocationCheck: true,
// 				keepAlive:                60 * time.Second,
// 				receiveMaximum:           65535,
// 				authOptions: &AuthOptions{
// 					AuthMethod:       "",
// 					SatAuthFile:      "",
// 					AuthData:         []byte(nil),
// 					AuthDataProvider: nil,
// 					AuthInterval:     20 * time.Minute,
// 					AuthHandler:      nil,
// 				},
// 			},
// 			throwError: false,
// 		},
// 	}

// 	for _, test := range tests {
// 		t.Run(test.name, func(t *testing.T) {
// 			settings := &connectionSettings{}
// 			err := settings.fromConnectionString(test.connString)
// 			if test.throwError {
// 				require.Error(t, err)
// 			} else {
// 				require.NoError(t, err)
// 				require.Equal(t, test.connSettings, settings)
// 			}
// 		})
// 	}
// }

// func TestFromEnvironmentVariables(t *testing.T) {
// 	tests := []struct {
// 		name         string
// 		envVars      map[string]string
// 		connSettings *connectionSettings
// 		throwError   bool
// 	}{
// 		{
// 			name: "Valid environment variables",
// 			envVars: map[string]string{
// 				"MQTT_HOST_NAME":                   "localhost",
// 				"MQTT_TCP_PORT":                    "8883",
// 				"MQTT_USE_TLS":                     "true",
// 				"MQTT_CA_FILE":                     "Connection/ca.txt",
// 				"MQTT_CA_REQUIRE_REVOCATION_CHECK": "false",
// 				"MQTT_CLEAN_START":                 "true",
// 				"MQTT_KEEP_ALIVE":                  "PT45S",
// 				"MQTT_CLIENT_ID":                   "clientId",
// 				"MQTT_SESSION_EXPIRY":              "PT1M",
// 				"MQTT_CONNECTION_TIMEOUT":          "PT3M",
// 				"MQTT_USERNAME":                    "username",
// 				"MQTT_PASSWORD":                    "password",
// 				"MQTT_PASSWORD_FILE":               "Connection/TestPwd.txt",
// 				"MQTT_CERT_FILE":                   "Connection/TestPwdPem.txt",
// 				"MQTT_KEY_FILE":                    "Connection/TestPwdKey.txt",
// 				"MQTT_KEY_FILE_PASSWORD":           "sdklite",
// 				"MQTT_SAT_AUTH_FILE":               "auth.data",
// 				"MQTT_AUTH_METHOD":                 "K8S-SAT",
// 			},
// 			connSettings: &connectionSettings{
// 				useTLS:                   true,
// 				serverURL:                "tls://localhost:8883",
// 				clientID:                 "clientId",
// 				username:                 "username",
// 				password:                 []byte("password"),
// 				passwordFile:             "Connection/TestPwd.txt",
// 				certFile:                 "Connection/TestPwdPem.txt",
// 				keyFile:                  "Connection/TestPwdKey.txt",
// 				keyFilePassword:          "sdklite",
// 				caFile:                   "Connection/ca.txt",
// 				caRequireRevocationCheck: false,
// 				keepAlive:                45 * time.Second,
// 				sessionExpiry:            1 * time.Minute,
// 				receiveMaximum:           65535,
// 				connectionTimeout:        3 * time.Minute,
// 				authOptions: &AuthOptions{
// 					AuthInterval: 20 * time.Minute,
// 					AuthMethod:   "K8S-SAT",
// 					SatAuthFile:  "auth.data",
// 				},
// 			},
// 			throwError: false,
// 		},
// 	}

// 	for _, test := range tests {
// 		t.Run(test.name, func(t *testing.T) {
// 			// Set environment variables
// 			for key, value := range test.envVars {
// 				os.Setenv(key, value)
// 			}
// 			// Unset environment variables after the test
// 			defer func() {
// 				for key := range test.envVars {
// 					os.Unsetenv(key)
// 				}
// 			}()

// 			settings := &connectionSettings{}
// 			err := settings.fromEnv()
// 			if test.throwError {
// 				require.Error(t, err)
// 			} else {
// 				require.NoError(t, err)
// 				require.Equal(t, test.connSettings, settings)
// 			}
// 		})
// 	}
// }
