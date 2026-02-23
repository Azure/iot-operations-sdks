# Protocol compiler

The `Azure.IoT.Operations.ProtocolCompiler` accepts one or more WoT Thing Model files as input, and it outputs specializations of the AIO SDK client and server classes in the requested programming language.
It can also accept JSON Schema files as input, either independently or in conjunction with WoT Thing Model files.

## Install the compiler

1. Install [.NET](https://dotnet.microsoft.com/download)

1. Install the protocol compiler using the `dotnet` CLI:

    ```bash
    dotnet tool install -g Azure.IoT.Operations.ProtocolCompiler --add-source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json
    ```

1. The compiler can now be executed using the command `Azure.Iot.Operations.ProtocolCompiler`

## Compiler options

The compiler provides the following options:

```
--things <FILEPATH ...>        File(s) containing WoT Thing Model(s) to process for full generation
--clientThings <FILEPATH ...>  File(s) containing WoT Thing Model(s) to process for client-side generation
--serverThings <FILEPATH ...>  File(s) containing WoT Thing Model(s) to process for server-side generation
--schemas <FILESPEC ...>       Filespec(s) of files containing schema definitions (each may include wildcards)
--typeNamer <FILEPATH>         File containing JSON config for deriving type names from JSON Schema names
--outDir <DIRPATH>             Directory for receiving generated code [default: .]
--workingDir <DIRPATH>         Directory for storing temporary files (relative to outDir unless path is rooted) [default: schemas]
--namespace <NAMESPACE>        Namespace for generated code [csharp default: "Generated", rust default: "generated"]
--common <NAMESPACE>           Namespace for common code [csharp default: "", rust default: "common_types"]
--sdkPath <FILEPATH | URL>     Local path or feed URL for Azure.Iot.Operations.Protocol SDK
--lang <csharp|rust|none>      Programming language for generated code
--prefixSchemas                Apply Thing Model prefixes to schema type names (to avoid collisions across Thing Models)
--noProj                       Do not generate code in a project
--defaultImpl                  Generate default implementations of user-level callbacks
```

Notes:

```
For --lang csharp, the --namespace and --common options must have PascalCase name segments separated by dots
For --lang rust, the --namespace and --common options must have a single snake_case name segment
For --lang none, the --namespace and --common options must not be used
```

The repo contains [a small collection of valid Thing Models](./test/thing-models/valid/) that may be helpful for experimenting with this tool.

## Generation hierarchy

The `---outDir`, `--workingDir`, `--namespace`, and `--common` options work together to control the generated hierarchy of folders and namespaces, as can be seen in the following examples.

### Generated C# hierarchy

CLI options: `--lang csharp --outDir output/ProjName --workingDir Working --namespace FromModel --common Common`

Generated hierarchy:

```
─ output
  └─ ProjName
     ├─ ProjName.csproj
     ├─ Working
     │  └─ (generated JSON Schema files)
     ├─ FromModel
     │  └─ (C# source files generated from model)
     └─ Common
        └─ (C# source files independent of model)
```

### Generated Rust hierarchy

CLI options: `--lang rust --outDir output/proj_name --workingDir working --namespace from_model --common common`

Generated hierarchy:

```
─ output
  └─ proj_name
     ├─ Carge.toml
     ├─ working
     │  └─ (generated JSON Schema files)
     └─ src
        ├─ lib.rs
        ├─ from_model.rs
        ├─ from_model
        │  └─ (Rust source files generated from model)
        ├─ common.rs
        └─ common
           └─ (Rust source files independent of model)
```

## Compilation scenarios

The following outlines the different options needed to resolve the input files, depending on their formats, interrelationships, and desired output.

### WoT Thing Models

If the input is exclusively Thing Models, these can be specified via the `--things` option.
Multiple files are allowed, and each file may contain one or more Thing Models.

```bash
Azure.Iot.Operations.ProtocolCompiler --things <FILEPATH ...>
```

### WoT Thing Models for different endpoints

To generate only client-side code, or only server-side code, or a non-uniform mix of client and server code, use the `--clientThings` and/or `--serverThings` option(s).

```bash
Azure.Iot.Operations.ProtocolCompiler --clientThings <FILEPATH ...> --serverThings <FILEPATH ...>
```

Models passed in via `--clientThings` will not generate any server-side code, and models passed in via `--serverThings` will not generate any client-side code.
These options can be used in conjunction with the `--things` option for use cases that also involve common code across the client and server sides.

### WoT Thing Models with references to external JSON Schema definitions

A Thing Model may reference an external JSON Schema definition by employing the `dtv:ref` property defined by the WoT Protocol Binding for AIO.
Any such schema definition must be passed in via the `--schemas` option.

```bash
Azure.Iot.Operations.ProtocolCompiler --things <FILEPATH ...> --schemas <FILESPEC ...>
```

The `FILESPEC` may contain wildcards, but glob patterns are not supported.

### JSON Schema definitions with no Thing Models

To generate programming-language code from JSON Schema definitions without supplying a WoT Thing Model, the `--schemas` option can be used without any of the `--things`, `--clientThings`, or `--serverThings` options.

```bash
Azure.Iot.Operations.ProtocolCompiler --schemas <FILESPEC ...>
```

When used in this manner, no client-specific or server-specific code is generated.
The only generated code is translations of the JSON Schema definitions into the requested programming language.

### Thing Models not intended for code generation

Just as it is possible to generate code from JSON Schema definitions without the need for a Thing Model, it is possible to generate JSON Schema definitions from a Thing Model without generating any code.
This is achieved by specifying a language of `none`.

```bash
Azure.Iot.Operations.ProtocolCompiler --things <FILEPATH ...> --lang none
```

The generation stops after schema definitions in the Thing Model(s) are converted into JSON Schema definitions.

## Model validation

All provided WoT Thing Models must conform to the [JSON Schema for validating Thing Models](https://github.com/w3c/wot-thing-description/blob/main/validation/tm-json-schema-validation.json) and must also employ the WoT Protocol Binding for AIO.
The compiler validates the syntax, grammar, vocabulary, and usage in the submitted models.
These validations include the following.

* The `@context` includes the WoT remote context specifier and the AIO Protocol Binding local context specifier.
* All `forms` elements are used appropriately, either in the model root or in affordance definitions.
* Schema definitions are structurally correct for the specified type.
* All defined `actions` can be invoked; all defined `properties` can be read; and all defined `events` can be subscribed.
* Properties defined by the AIO Protocol Binding (e.g., `dtv:topic`, `dtv:serviceGroupId`, and `dtv:ref`) are used correctly.
* If the AIO Platform Binding is used in the model, its local context specifier is included, and all defined properties (e.g., `aov:isComposite` and `aov:namespace`) are used correctly.

## Migrating from the DTDL ProtocolCompiler

The usage of the WoT ProtocolCompiler differs from that of the [DTDL ProtocolCompiler](../codegen/) in a few important ways.

* `--outDir`, `--sdkPath`, `--noProj`, and `--defaultImpl` are unchanged.
* `--workingDir` and `--namespace` are unchanged except for their default values.
* `--lang` no longer supports "go" and it now allows "none" for generating only schemas but no code.
* `--things` is analogous to `--modelFile` but for WoT Thing Models instead of DTDL models.
* Generating code from multiple models is now supported; consequently:
  * `--modelId` has been removed; there is no need to identify a single model as a generation source.
  * `--prefixSchemas` has been added to avoid collisions when the same name is used in different Thing Models.
* `--dmrRoot` has been removed; retrieval of referenced models is not supported.
* `--clientOnly` and `--serverOnly` have been replaced by the more flexible `--clientThings` and `--serverThings`:
  * `--clientThings Foo.json` is equivalent to `--modelFile Foo.json --clientOnly`
  * `--serverThings Bar.json` is equivalent to `--modelFile Bar.json --serverOnly`
  * `--clientThings Foo.json --serverThings Bar.json` was not expressible using the old CLI options.
* JSON Schema definitions are now accepted as first-class input; consequently:
  * `--schemas` specifies JSON Schema files as input.
  * `--typeNamer` specifies ruls for naming types generated from JSON Schema definitions.
* `--common` is new; previously, common code was placed in a fixed location with a fixed name.
