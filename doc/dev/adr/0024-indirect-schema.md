# ADR 24: DTDL Support for Indirect Schemas

## Context

When DTDL was upgraded from v3 to v4, one key addition was support for [recursive schema definitions](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#complex-schema).
The motivation for this addition was to support several known use cases for self-referential definitions, such as those in [ONVIF](https://www.onvif.org/) and [OPC UA](https://opcfoundation.org/about/opc-technologies/opc-ua/).
However, the DTDL ProtocolCompiler &mdash; which generates code from models that use the [Mqtt extension](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.mqtt.v3.md) &mdash; is not consistently able to generate code that functions correctly for recursive schemas.

The problem most readily manifests when generating code in Rust.
A recursive definition in Rust requires an indirection to prevent an infinitely large in-place definition.
Consider the following DTDL model, which defines a Telemetry whose schema is a binary tree:

```json
{
  "@context": [
    "dtmi:dtdl:context;4",
    "dtmi:dtdl:extension:requirement;2",
    "dtmi:dtdl:extension:mqtt;3"
  ],
  "@id": "dtmi:test:TelemetryComplexSchemas;1",
  "@type": [ "Interface", "Mqtt" ],
  "payloadFormat": "Json/ecma/404",
  "telemetryTopic": "sample/{senderId}/telemetry",
  "schemas": [
    {
      "@id": "dtmi:test:treeNode;1",
      "@type": "Object",
      "fields": [
        {
          "name": "value",
          "schema": "double"
        },
        {
          "name": "left",
          "schema": "dtmi:test:treeNode;1"
        },
        {
          "name": "right",
          "schema": "dtmi:test:treeNode;1"
        }
      ]
    }
  ],
  "contents": [
    {
      "@type": "Telemetry",
      "name": "tree",
      "schema": "dtmi:test:treeNode;1"
    }
  ]
}
```

The ProtocolCompiler generates the following code for Rust:

```rust
#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct TreeNode {
    /// The 'left' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub left: Option<TreeNode>,

    /// The 'right' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub right: Option<TreeNode>,

    /// The 'value' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub value: Option<f64>,
}
```

This results in the following build error:

```dos
error[E0072]: recursive type `TreeNode` has infinite size
  --> src\telemetry_recursive_schemas\tree_node.rs:15:1
   |
15 | pub struct TreeNode {
   | ^^^^^^^^^^^^^^^^^^^
...
19 |     pub left: Option<TreeNode>,
   |                      -------- recursive without indirection
   |
help: insert some indirection (e.g., a `Box`, `Rc`, or `&`) to break the cycle
   |
19 |     pub left: Option<Box<TreeNode>>,
   |                      ++++        +
```

This is a simple example, but a more complex example might include a recursive reference via a long chain of schema types.
The infinite-size condition can be alleviated by the addition of one or more indirection points, but it would be wasteful to burden all generated Object fields with unnecessary `Box` wrappers merely to address this problem for the small number of models that include recursive definitions.
On the other hand, judiciously placing indirection points only where needed is a challenging task to automate.

What is not especially challenging is detecting recursive structures and flagging them as errors.
The ProtocolCompiler already contains logic for this; it is used to forcibly skip generating a WoT Thing Description, because Thing Descriptions cannot contain any self-referential definitions.
It will be up to the modeler to add indirection points as appropriate to remove all direct-recursion conditions in the model.

## Decision

The Mqtt extension will be updated to add a new adjunct type that provides an indication of schema indirection.
Since version 4 of this extension has not officially shipped, this change will be rolled into the pending Mqtt extension version 4.

### The Indirect adjunct type for Object fields

The new Indirect adjunct type may co-type a Field in a DTDL Object.
This adjunct type will enable the author of a DTDL model to indicate where indirection should be applied.
For example, the tree node in the DTDL model above could be modified to add an Indirect co-type to the "left" and "right" fields, as follows:

```json
{
  "@id": "dtmi:test:treeNode;1",
  "@type": "Object",
  "fields": [
    {
      "name": "value",
      "schema": "double"
    },
    {
      "@type": [ "Field", "Indirect" ],
      "name": "left",
      "schema": "dtmi:test:treeNode;1"
    },
    {
      "@type": [ "Field", "Indirect" ],
      "name": "right",
      "schema": "dtmi:test:treeNode;1"
    }
  ]
}
```

The ProtocolCompiler will be enhanced to support the Indirect adjunct type.
It will also be enhanced to fail code generation when any directly recursive structures are detected in the model.
This will affect code generation only in Rust, for which a `Box` will be wrapped around the field type, like so:

```rust
#[derive(Serialize, Deserialize, Debug, Clone, Builder)]
pub struct TreeNode {
    /// The 'left' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub left: Option<Box<TreeNode>>,

    /// The 'right' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub right: Option<Box<TreeNode>>,

    /// The 'value' Field.
    #[serde(skip_serializing_if = "Option::is_none")]
    #[builder(default = "None")]
    pub value: Option<f64>,
}
```

The Indirect adjunct type and the [Required](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.requirement.v1.md#required-adjunct-type) adjunct type will not be permitted to co-type the same Field.
This is particularly relevant for Go, because generated Go Fields always use pointers unless they are co-typed Required.
Since Required is disallowed when Indirect is specified, the Indirect adjunct type will prevent the Field pointer from being removed via a Required co-type.

### No new adjunct type for Array or Map

A related problem can occur when a DTDL Array or Map has a recursive definition, because these schema types do not result in explicit type definitions in generated code.
Consider the following DTDL Telemetry, whose schema is an Array of Array of double:

```json
{
  "@type": "Telemetry",
  "name": "doubleArray2D",
  "schema": {
    "@type": "Array",
    "elementSchema": {
      "@type": "Array",
      "elementSchema": "double"
    }
  }
}
```

The two Array definitions do not result in explicit type definitions.
Instead, they merely cause the type of the Telemetry to be declared with nested arrays.
In Rust, this is like so:

```rust
#[serde(rename = "doubleArray2D")]
#[serde(skip_serializing_if = "Option::is_none")]
#[builder(default = "None")]
pub double_array2d: Option<Vec<Vec<f64>>>,
```

In C#, like so:

```csharp
[JsonPropertyName("doubleArray2D")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public List<List<double>>? DoubleArray2d { get; set; } = default;
```

And in Go, like so:

```go
DoubleArray2d [][]float64 `json:"doubleArray2D,omitempty"`
```

Consequently, the ProtocolCompiler fails when processing a recursive DTDL Array definition such as this:

```json
{
  "@type": "Telemetry",
  "name": "recursiveArray",
  "schema": {
    "@id": "dtmi:test:recursiveArray;1",
    "@type": "Array",
    "elementSchema": "dtmi:test:recursiveArray;1"
  }
}
```

The failure occurs because the ProtocolCompiler attempts to generate an infinitely long declaration in generated code:

```rust
pub recursive_array: Option<Vec<Vec<Vec<Vec<Vec<Vec<Vec<...
```

```csharp
public List<List<List<List<List<List<List<...
```

```go
RecursiveArray [][][][][][][]...
```

This is also true for a DTDL Map definition, which generates a declaration for a Rust `HashMap`, a Go `map`, and a C# `Dictionary`, respectively, with no explicitly defined type.

However, no change to DTDL or to the Mqtt extension is required to solve this problem.
A model author can readily address this situation by using a DTDL Object as the element schema of the Array, where the Object has a single field whose schema is the Array:

```json
{
  "@type": "Telemetry",
  "name": "array",
  "schema": {
    "@id": "dtmi:test:recursiveArray;1",
    "@type": "Array",
    "elementSchema": {
      "@type": "Object",
      "fields": [
        {
          "name": "elements",
          "schema": "dtmi:test:recursiveArray;1"
        }
      ]
    }
  }
}
```

Note that no Indirect co-type is required for the Object field, because the Array acts as an indirection in the generated definition.
This also holds for a Map.
An Indirect co-type is required only when the path from an Object back to itself traverses only Objects.
