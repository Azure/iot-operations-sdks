# Codegen comparison: error response with no success output (Rust vs .NET)

The single Thing Model `wot/ErrorNoOutput.TM.json` defines one command,
`deleteThing`, that has **an error schema (`SampleError`) but no normal output
schema**.

The same TM was run through the protocol compiler for both languages:

    dotnet run --project wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler \
      --clientThings wot/ErrorNoOutput.TM.json --lang <rust|csharp> \
      --namespace ErrorNoOutput --noProj

## Result

| Language | Outcome |
|----------|---------|
| **.NET** | Generates fully — including the action invoker and service. On success it returns an empty `DeleteThingResponseSchema {}`; on failure it throws `SampleErrorException`. See `dotnet_generation_output.txt` and `dotnet/`. |
| **Rust** | **Generation fails** in the "Envoy generation" phase with `Exception: Object reference not set to an instance of an object` (a NullReferenceException). The schema types are generated, but the command invoker is never produced. See `rust_generation_output.txt` and `rust/`. |

## Why

Both languages generate an identical response wrapper containing only the error:

- `.NET`  — `dotnet/ErrorNoOutput/DeleteThingResponseSchema.g.cs`:
  `public SampleError? Error { get; set; }`  (no output field)
- `Rust`  — `rust/error_no_output/delete_thing_response_schema.rs`:
  `pub error: Option<SampleError>`           (no output field)

The .NET command/service templates fall back to the response/empty type when no
normal output schema is defined, so they generate cleanly. The Rust
command-invoker template assumes a normal result schema exists whenever an error
schema does, and dereferences it unconditionally — hence the NRE.

Note: `rust/error_no_output/` is missing `delete_thing_action_invoker.rs`
(the file that crashed), whereas `dotnet/ErrorNoOutput/` contains the full
`DeleteThingActionInvoker.g.cs` + service `ErrorNoOutput.g.cs`.
