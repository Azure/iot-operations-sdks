# ADR13: Language-Appropriate Generated Names

## Status

APPROVED

## Context

At present, the ProtocolCompiler generates names for types, methods, files, and folders that are not uniformly consistent with the casing conventions of the language in which the code is generated.
Moreover, some names are not consistent with the casing conventions of any known language (e.g., "dtmi_myCompany_MyApplication__1").

This situation arose in part because the original target language for the ProtocolCompiler was C#, and as other languages were added piecemeal, there was never a clean re-architecting of the naming mechanisms in the codebase.

Another factor driving the design was a strong emphasis on avoiding name collisions, which came at the expense of usability and conventionality.
Some of this emphasis is no longer relevant due to changes in other aspects of the design.
For instance, generated code now derives from a single DTDL Interface, so there is less importance in incorporating every character of the Interface's DTMI into a namespace for the generated code.

## Decision

The decision has multiple aspects.

**First**, the ProtocolCompiler will be modified to generate names that conform to language conventions.
The relevant conventions are as follows.

|Category|C#|Go|Rust|
|---|---|---|---|
|type|PascalCase|PascalCase|PascalCase|
|field|PascalCase|PascalCase|snake_case|
|method|PascalCase|PascalCase|snake_case|
|variable|camelCase|camelCase|snake_case|
|file|PascalCase|snake_case|snake_case|
|folder|PascalCase|lowercase|snake_case|

**Second**, although most generated names will derive from names and identifiers in the user's model, an exception is the name of the output folder, which is given directly as a CLI parameter to the ProtocolCompiler.
This parameter value will be used as the output folder name with no modification, so if the user does not specify a language-appropriate name, it is assumed to be intentional.

The output folder name may directly determine other names.
The .NET project name and the Rust package name both conventionally match the name of the folder containing the project/package.
This convention will largely be respected by the ProtocolCompiler; however, changes will be made to ensure that the project/package name is generally legal when used as a hierarchical name in source files.
In particular, for each dot-delimeted segment, the character set will be restricted to alphanumerics and underscore, and the first character must be non-numeric.
Thus:

* All non-alphanumeric characters will be mapped to underscore.
* Multiple underscores in sequence will be shortened to a single underscore.
* If the first character is numeric, it will be preceded by an underscore.

**Third**, all remaining names will derive from the DTDL model.
Names derived from the model's DTMI will use only the final label.
For example, given the identifier "dtmi:myCompany:MyApplication;1", the label "MyApplication" will be extracted and used for deriving names.
No other parts of the DTMI will affect the generated name.

**Fourth**, every name in DTDL and each label within a DTMI is a non-empty string containing only letters, digits, and underscores.
The first character must be a letter, and the last character may not be an underscore.
There is no guarantee that these strings will adhere to any standard casing rule.
Each such string will be canonicalized into a list of lowercase components by breaking the string on either:

* a lowercase-to-uppercase transition
* a sequence of one or more underscores

The list will be reassembled into the desired case via the following rules:

* Lowercase: Conjoin the elements in the list
* Pascal case: Capitalize each element, and conjoin the list
* Camel case: Capitalize each element but the first, and conjoin the list
* Snake case: Conjoin the elements with intervening underscores

### Casing Examples

The following table illustrates the application of canonicalization and reassembly for an exemplary set of strings.

|DTDL Name / DTMI Label|As Snake|As Pascal|As Camel|As Lower|
|----|----|----|----|----|
|UPPERCASE|uppercase|Uppercase|uppercase|uppercase|
|lowercase|lowercase|Lowercase|lowercase|lowercase|
|Capitalized|capitalized|Capitalized|capitalized|capitalized|
|PascalCase|pascal_case|PascalCase|pascalCase|pascalcase|
|camelCase|camel_case|CamelCase|camelCase|camelcase|
|snake_case|snake_case|SnakeCase|snakeCase|snakecase|
|SCREAMING_SNAKE_CASE|screaming_snake_case|ScreamingSnakeCase|screamingSnakeCase|screamingsnakecase|
|Capital_Snake_Case|capital_snake_case|CapitalSnakeCase|capitalSnakeCase|capitalsnakecase|
|DigitEnd9|digit_end9|DigitEnd9|digitEnd9|digitend9|
|Digit9Mid|digit9mid|Digit9mid|digit9mid|digit9mid|
|snake_99|snake_99|Snake99|snake99|snake99|
|double__lower|double_lower|DoubleLower|doubleLower|doublelower|
|double__UPPER|double_upper|DoubleUpper|doubleUpper|doubleupper|
|foo1_bar|foo1_bar|Foo1Bar|foo1Bar|foo1bar|
|foo_1bar|foo_1bar|Foo1bar|foo1bar|foo1bar|
|foo1_1bar|foo1_1bar|Foo11bar|foo11bar|foo11bar|
|foo2__bar|foo2_bar|Foo2Bar|foo2Bar|foo2bar|
|foo_2_bar|foo_2_bar|Foo2Bar|foo2Bar|foo2bar|
|foo__2bar|foo_2bar|Foo2bar|foo2bar|foo2bar|
|foo2__2bar|foo2_2bar|Foo22bar|foo22bar|foo22bar|

### Generation Example

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
|class/package/module|MyApplication|myapplication|my_application|
|wrapper file|MyApplication.g.cs|wrapper.go|(none)|
|wrapper type|MyApplication.Client|MyApplicationClient|(none)|
|wrapper method|SetColorAsync|(none)|(none)|
|schema file|SetColorRequestPayload.g.cs|set_color_request_payload.go|set_color_request_payload.rs|
|schema type|SetColorRequestPayload|SetColorRequestPayload|SetColorRequestPayload|
|schema field|NewColor|NewColor|new_color|
|envoy file|SetColorCommandInvoker.g.cs|set_color_command_invoker.go|set_color_command_invoker.rs|
|envoy type|SetColorCommandInvoker|SetColorCommandInvoker|SetColorCommandInvoker|
