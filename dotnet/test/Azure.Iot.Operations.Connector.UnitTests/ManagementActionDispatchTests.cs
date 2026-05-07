// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests;

/// <summary>
/// Unit tests for <see cref="ManagementActionConnectorWorker.InvokeHandlerAsync"/>,
/// the SDK's per-request dispatch primitive. Pinned because the YAML
/// <c>actionType</c> field is the only signal the SDK consumes to route to
/// the right <see cref="IManagementActionHandler"/> method, and the
/// <c>UnsupportedActionType</c> / <c>InternalError</c> branches aren't
/// reachable from the integration tests.
/// </summary>
public class ManagementActionDispatchTests
{
    [Theory]
    [InlineData(AssetManagementGroupActionType.Call)]
    [InlineData(AssetManagementGroupActionType.Read)]
    [InlineData(AssetManagementGroupActionType.Write)]
    public async Task InvokeHandler_RoutesToMatchingHandlerMethod(AssetManagementGroupActionType actionType)
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);
        var expected = SuccessResponse();

        switch (actionType)
        {
            case AssetManagementGroupActionType.Call:
                handler.Setup(h => h.HandleCallAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);
                break;
            case AssetManagementGroupActionType.Read:
                handler.Setup(h => h.HandleReadAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);
                break;
            case AssetManagementGroupActionType.Write:
                handler.Setup(h => h.HandleWriteAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);
                break;
        }

        ManagementActionResponse response = await ManagementActionConnectorWorker.InvokeHandlerAsync(
            handler.Object,
            BuildArgs(actionType),
            logger: null,
            CancellationToken.None);

        Assert.Same(expected, response);
        Assert.Null(response.ApplicationError);
        handler.VerifyAll();
        handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeHandler_UnsupportedActionType_ReturnsApplicationError()
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);

        // Cast a value outside the defined enum range to exercise the default branch.
        var bogusType = (AssetManagementGroupActionType)999;

        ManagementActionResponse response = await ManagementActionConnectorWorker.InvokeHandlerAsync(
            handler.Object,
            BuildArgs(bogusType),
            logger: null,
            CancellationToken.None);

        Assert.NotNull(response.ApplicationError);
        Assert.Equal("UnsupportedActionType", response.ApplicationError!.ErrorCode);
        Assert.Contains("999", response.ApplicationError.ErrorPayload);
        Assert.Equal("application/json", response.ContentType);
        Assert.Equal(0, response.Payload.Length);
        handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeHandler_HandlerThrows_ReturnsInternalErrorApplicationError()
    {
        var handler = new Mock<IManagementActionHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleCallAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        ManagementActionResponse response = await ManagementActionConnectorWorker.InvokeHandlerAsync(
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
        handler.Setup(h => h.HandleReadAsync(It.IsAny<ManagementActionInvokedEventArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // OperationCanceledException must NOT be translated to an InternalError; it has to surface
        // so the surrounding loop can observe shutdown.
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ManagementActionConnectorWorker.InvokeHandlerAsync(
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
        CloudEvent = null,
    };
}
