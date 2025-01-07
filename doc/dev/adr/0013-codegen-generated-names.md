# ADR13: Language-Appropriate Generated Names

## Status

PROPOSED

## Context

At present, the ProtocolCompiler generates names for types, methods, files, and folders that are not uniformly consistent with the casing conventions of the language in which the code is generated.
Moreover, some names are not consistent with the casing conventions of any known language (e.g., "dtmi_myCompany_MyApplication__1").

This situation arose in part because the original target language for the ProtocolCompiler was C#, and as other languages were added piecemeal, there was never a clean re-archtichtecting of the naming mechanisms in the codebase.

Another factor driving the design was a strong emphasis on avoiding name collisions, which came at the expense of usablity and conventionality.
Some of this emphasis is no longer relevant due to changes in other aspects of the design.
For instance, generated code now derives from a single DTDL Interface, so there is less importance in incorporating every character of the Interface's DTMI into a namespace for the generated code.

## Decision

The ProtocolCompiler will be modified to generate names that conform to language conventions.
The relevant conventions are as follows.

|Category|C#|Go|Rust|
|---|---|---|---|
|type|PascalCase|PascalCase|PascalCase|
|field|PascalCase|PascalCase|snake_case|
|method|PascalCase|PascalCase|snake_case|
|variable|camelCase|camelCase|snake_case|
|file|PascalCase|snake_case|snake_case|
|folder|PascalCase|lowercase|snake_case|

Most generated names will derive from names and identifiers in the user's model.
An exception is the name of the output folder, which is given directly as a CLI parameter to the ProtocolCompiler.
This parameter value will be used as the output folder name with no modification, so if the user does not specify a language-appropriate name, it is assumed to be intentional.

The output folder name may directly determine other names.
The .NET project name and the Rust package name both conventionally match the name of the folder containing the project/package.
This convention will be respected by the ProtocolCompiler.

All other names will derive from the DTDL model.
For illustrative purposes, consider the following abridged (and incomplete) model.

```json
{
  "@id": "dtmi:myCompany:MyApplication;1",
  "@type": [ "Interface", "Mqtt" ],
  "contents": [
    {
      "@type": "Command",
      "name": "setColor",
      "request": {
        "name": "newColor",
        "schema": "string"
      }
    }
  ]
}
```

Assume the ProtocolCompiler is invoked with the following command lines, each of which specifies a a language-appropriate name for the output folder.

```dotnetcli
ProtocolCompiler --lang csharp --outDir CSharpGen
ProtocolCompiler --lang go --outDir gogen
ProtocolCompiler --lang rust --outDir rust_gen
```

The generated names will be as follows.

|Item|C#|Go|Rust|
|---|---|---|---|
|output folder|CSharpGen|gogen|rust_gen|
|project file|CSharpGen.csproj|(none)|Cargo.toml|
|codegen folder|MyApplication|myapplication|my_application|
|container/package/module|MyApplication|myapplication|my_application|
|wrapper file|MyApplication.g.cs|wrapper.go|(none)|
|wrapper type|MyApplication.Client|MyApplicationClient|(none)|
|wrapper method|SetColorAsync|(none)|(none)|
|schema file|SetColorRequestPayload.g.cs|set_color_request_payload.go|set_color_request_payload.rs|
|schema type|SetColorRequestPayload|SetColorRequestPayload|SetColorRequestPayload|
|schema field|NewColor|NewColor|new_color|
|envoy file|SetColorCommandInvoker.g.cs|set_color_command_invoker.go|set_color_command_invoker.rs|
|envoy type|SetColorCommandInvoker|SetColorCommandInvoker|SetColorCommandInvoker|
