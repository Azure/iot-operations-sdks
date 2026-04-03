# ADR 30: Service Preview Feature Testing

## Context

Often when a service feature is first introduced (like health status reporting APIs in the Akri service), we on the SDK team implement the SDK side of that feature in a feature branch and do an RC release from that branch. We do this so that partner teams can try out the service feature using our SDK before the service formally releases the feature.

Currently, this repo only deploys old versions of the MQTT broker + Akri service at the gate, so our automated tests cannot include testing preview service features. 

Lately, we have been discussing how these SDK RC bits end up in shipped connectors, so we need to test these RC bits and their associated service features the same way we test non-preview features in main. This would ensure that any shipped RC versions are compatible with the preview service feature.

## Decision

From a branch management side, we should start distinguishing feature branches between those that are for upcoming service features vs those that are not such as:

```
serviceFeature/akriHealthStatusReporting
```

vs

```
feature/mqttNetRefactor
```

With this, we can set up our GitHub branch protection rules differently for these types of feature branches. We will enforce that:

 - serviceFeature/* branches will run integration tests against preview versions of the broker + akri 
 - feature/* branches will run integration tests against stable versions of the broker + akri 

With this approach, we can write integration tests for the preview feature while still testing more stable bits in main and other feature branches.

## Consequences

With the above proposal, we would need to do the following things:

1) Edit the https://github.com/Azure/iot-operations-sdks-action repo such that we can pass in arbitrary version numbers for MQ + Akri to deploy. This allows each service feature branch to test a unique set of service bits (which may be necessary if the service is developing multiple features in parallel)
2) Edit the branch protection rules as discussed above

## Other approaches considered

Test the RC SDK bits in the AIO e2e test pipeline
 - This approach is a bit late in identifying issues. We have to do the RC release before we could run the AIO e2e test pipeline with it.