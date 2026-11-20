// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.UnitTests;

public class RpcCallAsyncTests
{
    [Fact]
    public async Task AwaitReturnsResponse()
    {
        var expectedResponse = new TestResponse();
        RpcCallAsync<TestResponse> rpcCall = new(
            Task.FromResult(new ExtendedResponse<TestResponse> { Response = expectedResponse }),
            Guid.NewGuid());

        TestResponse actualResponse = await rpcCall;

        Assert.Same(expectedResponse, actualResponse);
    }

    [Fact]
    public async Task AwaitPreservesOriginalException()
    {
        var expectedException = new InvalidOperationException("Expected failure");
        RpcCallAsync<TestResponse> rpcCall = new(
            Task.FromException<ExtendedResponse<TestResponse>>(expectedException),
            Guid.NewGuid());

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await rpcCall);

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task AwaitPreservesCancellation()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        RpcCallAsync<TestResponse> rpcCall = new(
            Task.FromCanceled<ExtendedResponse<TestResponse>>(cancellationTokenSource.Token),
            Guid.NewGuid());

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await rpcCall);

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
    }

    private sealed class TestResponse
    {
    }
}
