# ADR 31: WoT Thing Model New Properties for Digital Operations Vocabulary

## Context

[ADR 26](./0026-wot-protocol-binding.md) defined an informal AIO Protocol Binding using the RDF term prefix `dtv:`, and [ADR 29](./0029-wot-platform-binding.md) defined an informal AIO Platform Binding using the RDF term prefix `aov:`.

Since these ADRs were written, two related changes have been made to the WoT modeling vocabulary used by the AIO ProtocolCompiler:

1. The two RDF term prefixes `dtv:` and `aov:` have been unified under a single new prefix `dov:` (the "Digital Operations Vocabulary"), bound to a new URI `http://azure.com/DigitalOperations/vocab#`.
   Both previous prefixes continue to be accepted for the specific set of terms where each had been employed prior to this change, so that legacy models remain valid; however, new vocabulary terms are defined only under `dov:`.

2. Common Information Model (CIM) work has identified the need for additional Thing Model expressiveness that is not covered by the existing AIO Platform Binding.
   In particular, modelers need a way to:
   * Carry free-form metadata at the Thing Model level and at the individual affordance level.
   * Declare named groups of affordances and assign individual affordances (actions, properties, and events) to those groups.
   * Attach a stable cross-modeling-system identifier ("propertyIRI") to each affordance.

The present ADR documents the new vocabulary terms, the changes to existing terms, and the validation rules and interrelationships that the ProtocolCompiler now enforces on Thing Models that use them.
This ADR partially supersedes the choice of prefix in [ADR 26](./0026-wot-protocol-binding.md) and [ADR 29](./0029-wot-platform-binding.md), and it extends the term `aov:memberOf` defined in [ADR 29](./0029-wot-platform-binding.md) so that it applies to property and event affordances in addition to action affordances.

## Decision

### URI and RDF term prefix

The AIO Protocol Binding (formerly `dtv:`) and the AIO Platform Binding (formerly `aov:`) are unified under a single RDF term prefix `dov:`, derived from the phrase "Digital Operations Vocabulary" and constructed in accordance with the "v suffix" notation [recommended](https://www.w3.org/TR/wot-binding-templates/#creating-a-new-protocol-binding-template-subspecification) for vocabulary creation.

| Prefix | URI | Status |
| --- | --- | --- |
| `dov:` | `http://azure.com/DigitalOperations/vocab#` | Preferred; required for all new terms |
| `aov:` | `http://azure.com/IoT/operations/tm#` | Legacy; accepted only for the specific terms enumerated below that previously used this prefix |
| `dtv:` | `http://azure.com/DigitalTwins/dtmi#` | Legacy; accepted only for the specific terms enumerated below that previously used this prefix |

Every Thing Model that uses any term from the Digital Operations Vocabulary must declare the `dov` local term in its `@context` and bind it to the URI `http://azure.com/DigitalOperations/vocab#`.
A model that uses only legacy `aov:` or `dtv:` terms may continue to declare only the corresponding legacy local term(s), but mixed usage requires every prefix that the model actually employs to be declared.

The specific terms for which each legacy prefix is still accepted, in addition to the preferred `dov:` form, are:

| Preferred (`dov:`) term | Legacy form | Applies to |
| --- | --- | --- |
| `dov:isComposite` | `aov:isComposite` | Thing Model root |
| `dov:isEvent` | `aov:isEvent` | Thing Model root |
| `dov:typeRef` | `aov:typeRef` | Thing Model root, data schemas |
| `dov:namespace` | `aov:namespace` | affordances, data schemas |
| `dov:contains` | `aov:contains` | property, event affordances |
| `dov:containedIn` | `aov:containedIn` | property, event affordances |
| `dov:withUnit` | `aov:withUnit` | property, event affordances |
| `dov:memberOf` | `aov:memberOf` | action, property, event affordances |
| `dov:scaleFactor` | `aov:scaleFactor` | numeric data schemas |
| `dov:decimalPlaces` | `aov:decimalPlaces` | numeric data schemas |
| `dov:refName` | `aov:refName` | `links` element |
| `dov:refType` | `aov:refType` | `links` element |
| `dov:topic` | `dtv:topic` | `forms` element |
| `dov:serviceGroupId` | `dtv:serviceGroupId` | `forms` element |
| `dov:headerCode` | `dtv:headerCode` | `forms` element |
| `dov:headerInfo` | `dtv:headerInfo` | `forms` element |
| `dov:includeInherited` | `dtv:includeInherited` | `forms` element |
| `dov:placeholder` | `dtv:placeholder` | property, event affordances |
| `dov:ref` | `dtv:ref` | data schemas |
| `dov:additionalProperties` | `dtv:additionalProperties` | object data schemas |
| `dov:errorMessage` | `dtv:errorMessage` | object data schemas |

Terms introduced by the present ADR (`dov:metadata`, `dov:propertyGroups`, `dov:eventGroups`, `dov:actionGroups`, `dov:propertyIRI`, `dov:actionConfiguration`, `dov:propertyConfiguration`, `dov:eventConfiguration`) have no legacy form and must be written with the `dov:` prefix.

### New Thing-level properties

Four new properties are defined at the root of a Thing Model.

| Term | JSON type | Meaning |
| --- | --- | --- |
| `dov:metadata` | object (free-form) | Arbitrary metadata about the Thing Model, opaque to the ProtocolCompiler. |
| `dov:propertyGroups` | array of group objects | Declares the set of named groups to which property affordances may belong. |
| `dov:eventGroups` | array of group objects | Declares the set of named groups to which event affordances may belong. |
| `dov:actionGroups` | array of group objects | Declares the set of named groups to which action affordances may belong. |

Each element of `dov:propertyGroups`, `dov:eventGroups`, and `dov:actionGroups` is an _affordance group object_ that contains at least the property `title`, whose value is a non-empty string that names the group.
Additional WoT-defined properties (e.g. `description`, `descriptions`) may appear on a group object but are not interpreted by the ProtocolCompiler.

### New affordance-level properties

The following properties are newly defined or newly extended for use within action, property, and event affordances.

| Term | Applies to | JSON type | Meaning |
| --- | --- | --- | --- |
| `dov:memberOf` (and legacy `aov:memberOf`) | action, property, event | string | The `title` of the affordance group to which this affordance belongs. |
| `dov:propertyIRI` | action, property, event | string | A stable IRI identifying the modeled concept that this affordance represents. |
| `dov:actionConfiguration` | action | object (free-form) | Arbitrary configuration data associated with the action, opaque to the ProtocolCompiler. |
| `dov:propertyConfiguration` | property | object (free-form) | Arbitrary configuration data associated with the property, opaque to the ProtocolCompiler. |
| `dov:eventConfiguration` | event | object (free-form) | Arbitrary configuration data associated with the event, opaque to the ProtocolCompiler. |

`dov:memberOf` extends the term that was originally defined in [ADR 29](./0029-wot-platform-binding.md) as `aov:memberOf` and limited to action affordances.
It is now permitted on property and event affordances as well; the only change for action affordances is the recommended prefix.

### Validation rules

The ProtocolCompiler's `ThingValidator` enforces the following constraints on the new and extended properties.

#### Context requirements

* Any use of a `dov:` term (including `dov:metadata`, `dov:propertyGroups`, `dov:eventGroups`, `dov:actionGroups`, `dov:memberOf`, `dov:propertyIRI`, and the three affordance-`Configuration` properties) requires the Digital Operations Vocabulary local term `dov` to be bound to `http://azure.com/DigitalOperations/vocab#` in the model's `@context`.
* A use of any legacy `aov:` term requires the AIO Platform Binding local term `aov` to be bound to `http://azure.com/IoT/operations/tm#`.
* A use of any legacy `dtv:` term requires the AIO Protocol Binding local term `dtv` to be bound to `http://azure.com/DigitalTwins/dtmi#`.

#### `dov:propertyGroups`, `dov:eventGroups`, `dov:actionGroups`

* Each element of each groups array must be an object that contains a `title` property whose value is a non-empty string.
* Within a single groups array, no two elements may have the same `title` value (duplicate group names are rejected).
* Across all three groups arrays, no two elements may share the same `title` value (the namespace of group titles is global to the Thing Model).
This rule ensures that the value of `dov:memberOf` on an affordance unambiguously identifies a single group.

#### `dov:memberOf`

* When present on an action affordance, the value must equal the `title` of some element of `dov:actionGroups`.
* When present on a property affordance, the value must equal the `title` of some element of `dov:propertyGroups`.
* When present on an event affordance, the value must equal the `title` of some element of `dov:eventGroups`.
* The value must be a non-empty string.

#### `dov:propertyIRI`

* The value must be a non-empty string.
* The property is permitted on action, property, and event affordances independently; there is no requirement that the IRI be unique within the Thing Model.

#### `dov:actionConfiguration`, `dov:propertyConfiguration`, `dov:eventConfiguration`

* Each is a free-form JSON object whose internal structure is not validated by the ProtocolCompiler.
* The properties are mutually exclusive by domain: `dov:actionConfiguration` is only meaningful within an action affordance, `dov:propertyConfiguration` only within a property affordance, and `dov:eventConfiguration` only within an event affordance.

#### `dov:metadata`

* A free-form JSON object whose internal structure is not validated by the ProtocolCompiler.

#### Generalization of `optional` and `dov:withUnit` references

In conjunction with the changes above, two existing string-valued properties that reference affordances by name have been generalized to a uniform 2-level JSON Pointer syntax of the form `/<collection>/<name>`:

* The string elements of the Thing-level `optional` array must designate a 2-level path whose first level is `actions`, `properties`, or `events`, and whose second level is the name of an affordance present in the corresponding collection.
* The value of `dov:withUnit` (and legacy `aov:withUnit`) must designate a 2-level path whose first level is `properties` and whose second level is the name of a property whose schema type is `string`.

Proper JSON Pointer syntax with a leading `/` (e.g. `/properties/HeadAtPeakPoint_Units`) is preferred and supersedes the literal `properties/` prefix described in Example 4 of [ADR 29](./0029-wot-platform-binding.md).
For backward compatibility, however, the legacy form without a leading `/` (e.g. `properties/HeadAtPeakPoint_Units`) is still accepted by the validator: empty path segments produced by a leading `/` are simply ignored, so both forms resolve to the same 2-level path.

### Examples

#### Example 1: Thing-level metadata and affordance groups

The Thing Model below illustrates `dov:metadata`, `dov:propertyGroups`, `dov:eventGroups`, `dov:actionGroups`, and the use of `dov:memberOf` on properties, events, and actions.
Note that all group titles ("Telemetry", "Configuration", "Alerts", "Maintenance") are distinct across the three groups arrays.

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "dov": "http://azure.com/DigitalOperations/vocab#" }
  ],
  "@type": "tm:ThingModel",
  "title": "Pump",
  "dov:metadata": {
    "vendor": "Contoso",
    "modelLine": "P-200",
    "revision": "2026.04"
  },
  "dov:propertyGroups": [
    { "title": "Telemetry",     "description": "Continuously measured values." },
    { "title": "Configuration", "description": "Operator-settable parameters." }
  ],
  "dov:eventGroups": [
    { "title": "Alerts", "description": "Abnormal-condition notifications." }
  ],
  "dov:actionGroups": [
    { "title": "Maintenance", "description": "Service operations." }
  ],
  "properties": {
    "DischargePressure": {
      "type": "number",
      "dov:memberOf": "Telemetry",
      "forms": [ { "dov:topic": "pumps/p1/pressure" } ]
    },
    "PressureSetpoint": {
      "type": "number",
      "dov:memberOf": "Configuration",
      "forms": [ { "dov:topic": "pumps/p1/setpoint/{action}", "op": [ "readproperty", "writeproperty" ] } ]
    }
  },
  "events": {
    "OverPressure": {
      "data": { "type": "number" },
      "dov:memberOf": "Alerts",
      "forms": [ { "dov:topic": "pumps/p1/alerts/overpressure" } ]
    }
  },
  "actions": {
    "Flush": {
      "dov:memberOf": "Maintenance",
      "forms": [ { "dov:topic": "pumps/p1/maintenance/flush", "op": "invokeaction" } ]
    }
  }
}
```

The following model fragments would be rejected by the validator:

```json
"dov:propertyGroups": [
  { "title": "Telemetry" },
  { "title": "Telemetry" }            // rejected: duplicate title within propertyGroups
]
```

```json
"dov:propertyGroups": [ { "title": "Shared" } ],
"dov:eventGroups":    [ { "title": "Shared" } ]   // rejected: duplicate title across groups arrays
```

```json
"properties": {
  "P1": { "dov:memberOf": "Alerts", ... }   // rejected: "Alerts" is the title of an eventGroup, not a propertyGroup
}
```

#### Example 2: `dov:propertyIRI` and per-affordance configuration objects

The Thing Model below illustrates `dov:propertyIRI` and the three configuration properties.
The free-form `dov:propertyConfiguration`, `dov:eventConfiguration`, and `dov:actionConfiguration` objects are passed through verbatim by the ProtocolCompiler and may be consumed by downstream tooling.

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "dov": "http://azure.com/DigitalOperations/vocab#" }
  ],
  "@type": "tm:ThingModel",
  "title": "Boiler",
  "properties": {
    "Temperature": {
      "type": "number",
      "dov:propertyIRI": "http://example.com/cim/Boiler#Temperature",
      "dov:propertyConfiguration": {
        "samplingIntervalMs": 1000,
        "deadband": 0.5
      },
      "forms": [ { "dov:topic": "boilers/b1/temperature" } ]
    }
  },
  "events": {
    "FlameOut": {
      "data": { "type": "string" },
      "dov:propertyIRI": "http://example.com/cim/Boiler#FlameOut",
      "dov:eventConfiguration": {
        "severity": "critical",
        "retain": true
      },
      "forms": [ { "dov:topic": "boilers/b1/events/flameout" } ]
    }
  },
  "actions": {
    "Purge": {
      "dov:propertyIRI": "http://example.com/cim/Boiler#Purge",
      "dov:actionConfiguration": {
        "durationSeconds": 30,
        "requiresInterlock": true
      },
      "forms": [ { "dov:topic": "boilers/b1/actions/purge", "op": "invokeaction" } ]
    }
  }
}
```

#### Example 3: Generalized `optional` and `dov:withUnit` JSON Pointers

The Thing Model below illustrates the 2-level JSON Pointer syntax that is now required for `optional` array elements and for `dov:withUnit` values.
The `optional` array marks the action `Calibrate` and the property `BatchId` as optional; both pointers must resolve to existing affordances.
The event `HeadAtPeakPoint` declares its unit via a JSON Pointer to the string-typed property `HeadAtPeakPoint_Units`.

```json
{
  "@context": [
    "https://www.w3.org/2022/wot/td/v1.1",
    { "dov": "http://azure.com/DigitalOperations/vocab#" },
    { "qudt": "http://qudt.org/schema/qudt/" }
  ],
  "@type": "tm:ThingModel",
  "title": "Pump",
  "optional": [
    "/actions/Calibrate",
    "/properties/BatchId"
  ],
  "properties": {
    "BatchId": {
      "type": "string",
      "forms": [ { "dov:topic": "pumps/p1/batchId" } ]
    },
    "HeadAtPeakPoint_Units": {
      "type": "string",
      "readOnly": true,
      "forms": [ { "dov:topic": "pumps/p1/headAtPeakPoint_Units/read" } ]
    }
  },
  "events": {
    "HeadAtPeakPoint": {
      "data": { "type": "number" },
      "qudt:hasQuantityKind": "quantitykind:Length",
      "dov:withUnit": "/properties/HeadAtPeakPoint_Units",
      "forms": [ { "dov:topic": "pumps/p1/headAtPeakPoint" } ]
    }
  },
  "actions": {
    "Calibrate": {
      "forms": [ { "dov:topic": "pumps/p1/actions/calibrate", "op": "invokeaction" } ]
    }
  }
}
```

The following fragments illustrate validator rejections related to this example:

```json
"optional": [ "/actions/Calibrate" ]   // rejected if no "Calibrate" action exists
```

```json
"optional": [ "actions/Calibrate" ]    // accepted as legacy form (leading '/' is preferred but optional)
```

```json
"optional": [ "/relationships/Foo" ]   // rejected: first level must be actions, properties, or events
```

```json
"dov:withUnit": "/properties/DischargePressure"
                                            // rejected if DischargePressure is not of type string
```

### Summary of supersedence

| Decision in prior ADR | Status after this ADR |
| --- | --- |
| [ADR 26](./0026-wot-protocol-binding.md): RDF term prefix `dtv:` for the AIO Protocol Binding | Superseded by `dov:` for new terms; `dtv:` retained as legacy for the previously defined Protocol Binding terms enumerated above. |
| [ADR 29](./0029-wot-platform-binding.md): RDF term prefix `aov:` for the AIO Platform Binding | Superseded by `dov:` for new terms; `aov:` retained as legacy for the previously defined Platform Binding terms enumerated above. |
| [ADR 29](./0029-wot-platform-binding.md): `aov:memberOf` applies only to action affordances | Extended: `dov:memberOf` (and legacy `aov:memberOf`) applies to action, property, and event affordances; the referenced group must appear in the corresponding `dov:actionGroups`, `dov:propertyGroups`, or `dov:eventGroups` array. |
| [ADR 29](./0029-wot-platform-binding.md): `aov:withUnit` value of the form `properties/<Name>` | The preferred form is now a 2-level JSON Pointer (`/properties/<Name>`); the legacy form without a leading `/` is still accepted. |

## Consequences

### Positive

* A single Digital Operations Vocabulary prefix (`dov:`) simplifies authoring and reduces the number of `@context` declarations required in a typical Thing Model.
* Named affordance groups, with cross-array uniqueness of titles, enable downstream tooling to organize affordances into logical groupings without ambiguity.
* Free-form `dov:metadata` and the three per-affordance `*Configuration` objects provide structured extension points for CIM-driven tooling without further extending the validator.
* `dov:propertyIRI` carries a stable cross-modeling-system identifier on each affordance, enabling integration with external ontologies and registries.
* Generalizing `optional` and `dov:withUnit` to JSON Pointer syntax produces clearer, more uniformly validated references between Thing Model elements.

### Negative / Migration

* Previously, an `aov:memberOf` value on an action affordance was accepted without any corresponding group declaration in the Thing Model.
With the introduction of `dov:propertyGroups`, `dov:eventGroups`, and `dov:actionGroups`, the validator now requires that every `dov:memberOf` (or legacy `aov:memberOf`) value match the `title` of an element in the corresponding groups array.
Existing models that use `aov:memberOf` without declaring a matching group must be updated to add the appropriate `dov:actionGroups` (or, for the newly extended uses, `dov:propertyGroups` / `dov:eventGroups`) entry.
