# ADR 30: Service Preview Feature Testing

## Context

Often when a service feature is first introduced (like health status reporting APIs in the Akri service), we on the SDK team implement the SDK side of that feature in a feature branch and do an RC release from that branch. We do this so that partner teams can try out the service feature using our SDK before the service formally releases the feature.

Currently, this repo only deploys old versions of the MQTT broker + Akri service at the gate, so our automated tests cannot include testing preview service features. 

Lately, we have been discussing how these SDK RC bits end up in shipped connectors, so we need to test these RC bits and their associated service features the same way we test non-preview features in main. This would ensure that any shipped RC versions are compatible with the preview service feature.

## Decision

Within a feature branch for a preview service feature, each language's CI pipeline should target a version of the MQTT broker and Akri that contain this preview service feature. This is done [here](https://github.com/Azure/iot-operations-sdks/blob/ad91f6392e1003f9b183333fd28ec5227a5fe65a/.github/actions/configure-aio/action.yml#L56).

With this approach, we can write integration tests for the preview feature while still testing more stable bits in main and other feature branches.

The only callout here is that we don't want to merge the feature branch into main while the feature branch targets a preview MQTT broker or Akri version. So a new pre-requisite to merge to main should be to update the feature branch to target a stable version of the MQTT broker and Akri. This has an added benefit of making us stay up to date with MQTT broker + Akri versions which we have not been doing so far.

## Consequences

With the above proposal, we would need to do the following things:

1) Edit the https://github.com/Azure/iot-operations-sdks-action repo such that we can pass in arbitrary version numbers for MQTT + Akri to deploy. This allows each service feature branch to test a unique set of service bits (which may be necessary if the service is developing multiple features in parallel)

## Other approaches considered

- Test the RC SDK bits in the AIO e2e test pipeline
  - This approach is a bit late in identifying issues. We have to do the RC release before we could run the AIO e2e test pipeline with it.