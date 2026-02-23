# ADR28: Health Status Reporting SDK Logic

## Context: 

Health Statuses will now be reported for the Inbound Endpoint, Dataset, Event, Stream, and Management Action. This document details what convenience logic the Service SDKs should provide and what logic the Connector SDKs should provide to support this.

## Decision: 

### Service SDK:

public API:

- fns to get a HealthReporterSender for each relevant component on the ADR Client. Example: `new_device_endpoint_health_reporter(&adr_client, device_ref, message_expiry, background_report_interval, cancellation_token) -> health_reporter::HealthReporterSender`
    - the `cancellation_token` should be called if the component is deleted or will no longer be used and health reporting should end.
- on the HealthReporterSender, `report(RuntimeHealth)` - used to report the current runtime health event of the component. This fn should be okay to call as frequently as desired. This fn is REQUIRED to be called by the application any time the health status changes to ensure that the service has the latest information. If the health status does not change, the underlying client will report the last known health status at the `background_report_interval`'s frequency unless `pause()` is called
- on the HealthReporterSender, `pause()` - this is used to pause background reporting of the health status until a new status is reported. This must be called when the previous health status may no longer be applicable (for example, after a definition update). This function is critical to allow supporting the component to get aggregated to an "Unknown" state on the service
- potential API: fn to report a status on all children of a device endpoint. TBD if needed at this time, or if it encourages bad practices

Internal logic:

- The health reporter maintains state of what the last reported health status from the application was (even if each event isn't reported to the service).
- It maintains a timer between the last time a status was reported to the service. If this elapses, the last status event from the application (with an updated timestamp to now()) is reported to the service if there is a cached health status.
- If the application reports a health status before the timer elapses,
    - if the new status has an older version than the last cached status, it is ignored (this does not reset the timer)
    - if the new status has an older timestamp than the last cached status, it is ignored (this does not reset the timer)
    - if the new status is equal to the last cached status in all fields except for timestamp, the cached status's timestamp is updated to this one, but no report is made to the service (this does not reset the timer)
    - if the timestamp of the new status is >= the time that the timer should elapse, this status will be reported (this is to protect against continuous reports that don't allow the timer to time out. However, we want the timer to be starved out in this case, because we'd rather report the absolute latest status rather than using the last status for the timeout report). The timer will be reset once this is reported and this will now be the cached status
    - if none of the first 3 scenarios caused the message to not be reported, the message is cached and reported and the timer is reset
- If the application calls to pause background reporting, the cached status is cleared (so any new status reported will always be != and get reported). If there is no cached status, then the timer doesn't have anything to report if it lapses (implementation on this can vary). Once a new status is reported, there is now a cached status again, so background reporting can work again. On new, there's no cached status, so background reporting is off by default until the first status is reported

Connector SDK:

Fns to report the health status for the relevant components on their status reporter:
- `report_health_event(PartialRuntimeHealth)` - this takes the message, reason code and status, but the status reporter internally populates the timestamp and version. The version is cached and can be refreshed by calling the refresh APIs (see below). This ensures that reported health events have the correct version.
- `pause_health_reporting()` - same as underlying component
- `refresh_health_version()` -  updates the cached version for use in future health events. Should be called when an update is received to lock in the new version.
- `pause_and_refresh_health_version()` - Combines the functionality of the pause and refresh APIs for convenience.

Logic:
- There is a health reporter created for each component (e.g. each dataset, stream, endpoint, and event).
- The cancellation token for the health reporter is called if the component is deleted
