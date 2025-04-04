// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Azure.Iot.Operations.Mqtt;

internal class TokenRefreshTimer : IDisposable
{
    private readonly Timer _refreshTimer = null!;
    private readonly string _tokenFilePath = null!;

    public TokenRefreshTimer(IMqttClient mqttClient, string tokenFilePath)
    {
        _tokenFilePath = tokenFilePath;
        int secondsToRefresh = GetTokenExpiry(File.ReadAllBytes(tokenFilePath));
        _refreshTimer = new Timer(RefreshToken, mqttClient, secondsToRefresh * 1000, Timeout.Infinite);
        Trace.TraceInformation("Refresh token Timer set to {0} seconds.", secondsToRefresh);
    }

    private static int GetTokenExpiry(byte[] token)
    {
        JwtSecurityToken jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(Encoding.UTF8.GetString(token));

        DateTime currentTime = DateTime.UtcNow;

        if (jwtToken.ValidTo <= currentTime)
        {
            throw new ArgumentException("Provided authentication token has already expired");
        }

        TimeSpan timeRemaining = jwtToken.ValidTo.Subtract(currentTime);

        return (int)timeRemaining.TotalSeconds - 5;
    }

    private void RefreshToken(object? state)
    {
        Trace.TraceInformation("Refreshing the authentication token");
        IMqttClient? mqttClient = state! as IMqttClient;
        if (mqttClient is not null && mqttClient.IsConnected)
        {
            byte[] token = File.ReadAllBytes(_tokenFilePath);
            Task.Run(async () =>
            {
                await mqttClient.SendEnhancedAuthenticationExchangeDataAsync(
                    new MqttEnhancedAuthenticationExchangeData()
                    {
                        AuthenticationData = token,
                        ReasonCode = MqttAuthenticateReasonCode.ReAuthenticate
                    });
            });

            try
            {
                int secondsToRefresh = GetTokenExpiry(token);
                _refreshTimer.Change(secondsToRefresh * 1000, Timeout.Infinite);
                Trace.TraceInformation("Refresh token Timer set to {0} seconds.", secondsToRefresh);
            }
            catch (ArgumentException e)
            {
                Trace.TraceError("Failed to get next token renewal due time {0}", e);
            }
        }
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
