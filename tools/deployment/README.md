# Initializing a Kubernetes cluster and installing AIO

The following scripts will:

1. Install Step and Jetstack to manage certs
1. Install AIO
1. Deploy the `Broker` resource
1. Create the trust bundle ConfigMap for the broker
1. Deploy a TLS listener with sat-auth on 8883 for internal connections
1. Deploy a TLS listener with x509-auth on 38883 for external connections
1. Deploy an non-auth listener on 31883 for for external connection debugging
1. Create a pair of client crt/key in the repository root for authenticating the client samples