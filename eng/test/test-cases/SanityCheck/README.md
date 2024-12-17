# Sanity checks for METL execution systems

When the SDK in some language passes all test cases exercised by the test execution system for that language, the SDK is nominally considered to be functionally correct.
However, an absence of test failures could instead result from a test execution system that does not check all of the conditions it is supposed to check.
To detect this situation, the present folder hierarchy contains a collection of test cases that are expected to fail.

Each test case expects a value for one of the [METL check keys](../../../../doc/dev/generated/MetlSpec.md#keyvalue-kinds) that does not match the value that is actually expected for a correctly interpreted test of a correct implementation.
To use these cases for validating a test execution system, point the tester to this root folder instead of [the correct test-case root](../Protocol/).
Run the tester, and manually ensure that all test cases other than the designated canaries evince test failures.
There is one 'canary' test case that should pass for each set of tests; this ensures that the other cases are not all failing for some trivial reason such as an incorrect file-system path.

> Note: The folders herein do not duplicate the `defaults.toml` files in the correct test-case folder hierarchy.
When temporarily modifying a tester to point to these test cases, no change should be made to the path for the defaults file, only to the path for the test-case files.

## Pointers to file-system paths in testers

| language | CommandExecutor | CommandInvoker | TelemetryReceiver | TelemetrySender |
| --- | --- | --- | --- | --- |
| C# | [CommandExecutorTester.cs](../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/CommandExecutorTester.cs#L23) | [CommandInvokerTester.cs](../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/CommandInvokerTester.cs#L17) | [TelemetryReceiverTester.cs](../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/TelemetryReceiverTester.cs#L25) | [TelemetrySenderTester.cs](../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/TelemetrySenderTester.cs#L19) |
| Go | [command_executor_tester.go](../../../../go/test/protocol/command_executor_tester.go#L41) | [command_invoker_tester.go](../../../../go/test/protocol/command_invoker_tester.go#L37) | [telemetry_receiver_tester.go](../../../../go/test/protocol/telemetry_receiver_tester.go#L39) | [telemetry_sender_tester.go](../../../../go/test/protocol/telemetry_sender_tester.go#L37) |
