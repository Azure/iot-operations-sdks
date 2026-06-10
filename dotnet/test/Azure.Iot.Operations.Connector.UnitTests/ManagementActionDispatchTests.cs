// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests;

/// <summary>
/// Unit tests for <see cref="ManagementActionOrchestrator.InvokeHandlerAsync"/>,
/// the SDK's per-request dispatch primitive. Verifies that invocations are routed
/// to <see cref="IManagementActionHandler.HandleAsync"/> regardless of action type,
/// that handler exceptions become <c>InternalError</c> application errors, and that
/// <see cref="OperationCanceledException"/> is propagated rather than translated.
/// </summary>
public class ManagementActionDispatchTests
{
    [Theory]
    [InlineData(AssetManagementGroupActionType.Call)]
    [InlineData(AssetManagementGroupActionType.Read)]
    [InlineData(AssetManagementGroupActionType.Write)]
    public async Task InvokeHandler_RoutesToHandleAsync_RegardlessOfActionType(AssetManagementGroupActionType actionType)
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);
        var expected = SuccessResponse();
        ManagementActionInvokedEventArgs? receivedArgs = null;

        handler
            .Setup(h => h.HandleAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<ManagementActionInvokedEventArgs, CancellationToken>((a, _) => receivedArgs = a)
            .ReturnsAsync(expected);

        ManagementActionResponse response = await ManagementActionOrchestrator.InvokeHandlerAsync(
            handler.Object,
            BuildArgs(actionType),
            logger: null,
            CancellationToken.None);

        Assert.Same(expected, response);
        Assert.Null(response.ApplicationError);
        Assert.NotNull(receivedArgs);
        // ActionType must be observable on the args so handlers that branch on it can do so.
        Assert.Equal(actionType, receivedArgs!.ActionType);
        handler.VerifyAll();
        handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeHandler_HandlerThrows_ReturnsInternalErrorApplicationError()
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        ManagementActionResponse response = await ManagementActionOrchestrator.InvokeHandlerAsync(
            handler.Object,
            BuildArgs(AssetManagementGroupActionType.Call),
            logger: null,
            CancellationToken.None);

        Assert.NotNull(response.ApplicationError);
        Assert.Equal("InternalError", response.ApplicationError!.ErrorCode);
        Assert.Contains("boom", response.ApplicationError.ErrorPayload);
        Assert.Equal("application/json", response.ContentType);
        Assert.Equal(0, response.Payload.Length);
    }

    [Fact]
    public async Task InvokeHandler_HandlerThrowsOperationCanceled_PropagatesCancellation()
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        handler.Setup(h => h.HandleAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // OperationCanceledException must NOT be translated to an InternalError; it has to surface
        // so the surrounding loop can observe shutdown.
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ManagementActionOrchestrator.InvokeHandlerAsync(
                handler.Object,
                BuildArgs(AssetManagementGroupActionType.Read),
                logger: null,
                cts.Token));
    }

    private static ManagementActionInvokedEventArgs BuildArgs(AssetManagementGroupActionType actionType) => new()
    {
        GroupName = "device-control",
        ActionName = "test-action",
        ActionType = actionType,
        Payload = ReadOnlySequence<byte>.Empty,
        ContentType = "application/json",
        AssetName = "asset-1",
        DeviceName = "device-1",
    };

    private static ManagementActionResponse SuccessResponse() => new()
    {
        Payload = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }),
        ContentType = "application/json",
    };
}
