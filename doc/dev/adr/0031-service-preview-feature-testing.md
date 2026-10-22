# ADR 31: Service Preview Feature Testing

## Context

Often when a service feature is first introduced (like health status reporting APIs in the Akri service), we on the SDK team implement the SDK side of that feature and do a release. We do this so that partner teams can try out the service feature using our SDK before the service formally releases the feature.

Currently, this repo only deploys old versions of the MQTT broker + Akri service at the gate, so our automated tests cannot include testing preview service features. 

Lately, we have been discussing how these SDK bits end up in shipped connectors, so we need to test these bits and their associated service features the same way we test non-preview features. This would ensure that any shipped SDK versions are compatible with the preview service feature they claim to support.

## Decision

Within a branch (main, preview, or otherwise) that contains SDK code for a preview service feature, each language's CI pipeline should target a version of the MQTT broker and Akri that contain this preview service feature. This is done [here](https://github.com/Azure/iot-operations-sdks/blob/ad91f6392e1003f9b183333fd28ec5227a5fe65a/.github/actions/configure-aio/action.yml#L56).

We will allow for each language within each branch to target different broker versions as well. This accomodates for the case where one language may be ready to consume the preview service versions, but another language is not.

## Consequences

With the above proposal, we would need to do the following things:

1) Edit the iot-operations-sdks-action repo such that we can pass in arbitrary version numbers for MQTT + Akri to deploy. This allows each service feature branch to test a unique set of service bits (which may be necessary if the service is developing multiple features in parallel)

## Other approaches considered

- Test the RC SDK bits in the AIO e2e test pipeline
  - This approach is a bit late in identifying issues. We have to do the RC release before we could run the AIO e2e test pipeline with it.