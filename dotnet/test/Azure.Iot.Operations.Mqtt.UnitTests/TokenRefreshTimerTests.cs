using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Azure.Iot.Operations.Mqtt.UnitTests
{
    public class TokenRefreshTimerTests
    {
        [Fact]
        public void TestGetTokenExpirySucceedsWithValidToken()
        {
            string fileName = Guid.NewGuid().ToString();
            Directory.CreateDirectory("./TestFiles/");
            File.WriteAllText("./TestFiles/" + fileName, GenerateJwtToken(DateTime.UtcNow.AddMinutes(60)));
            try
            {
                TokenRefreshTimer tokenRefreshTimer = new(new Mock<IMqttClient>().Object, "./TestFiles/" + fileName);
            }
            finally
            {
                try
                {
                    File.Delete("./TestFiles/" + fileName);
                }
                catch (Exception)
                {
                    // It's fine if deleting this file fails
                }
            }
        }

        [Fact]
        public void TestGetTokenExpiryThrowsForExpiredToken()
        {
            string fileName = Guid.NewGuid().ToString();
            Directory.CreateDirectory("./TestFiles/");
            File.WriteAllText("./TestFiles/" + fileName, GenerateJwtToken(DateTime.UtcNow.AddMinutes(-60)));
            try
            {
                Assert.Throws<ArgumentException>(() => new TokenRefreshTimer(new Mock<IMqttClient>().Object, "./TestFiles/" + fileName));
            }
            finally
            {
                try
                {
                    File.Delete("./TestFiles/" + fileName);
                }
                catch (Exception)
                {
                    // It's fine if deleting this file fails
                }
            }
        }

        [Fact]
        public async Task TestTokenRefresh()
        {
            var mockMqttClient = new Mock<IMqttClient>();
            mockMqttClient.Setup(mock => mock.IsConnected).Returns(true);
            string fileName = Guid.NewGuid().ToString();
            Directory.CreateDirectory("./TestFiles/");
            File.WriteAllText("./TestFiles/" + fileName, GenerateJwtToken(DateTime.UtcNow.AddSeconds(8)));
            try
            {
                TokenRefreshTimer tokenRefreshTimer = new(mockMqttClient.Object, "./TestFiles/" + fileName);
                string newToken = GenerateJwtToken(DateTime.UtcNow.AddMinutes(60));
                File.WriteAllText("./TestFiles/" + fileName, newToken); // refresh the token on disk
                await Task.Delay(TimeSpan.FromSeconds(6)); // wait a bit for the TokenRefreshTimer to run it's periodic renewal task
                mockMqttClient.Verify(
                    mock =>
                        mock.SendEnhancedAuthenticationExchangeDataAsync(
                            It.Is<MqttEnhancedAuthenticationExchangeData>(data => Enumerable.SequenceEqual(data.AuthenticationData!, Encoding.UTF8.GetBytes(newToken))),
                            It.Is<CancellationToken>(token => token == default)),
                    Times.Once());
            }
            finally
            {
                try
                {
                    File.Delete("./TestFiles/" + fileName);
                }
                catch (Exception)
                {
                    // It's fine if deleting this file fails
                }
            }
        }
        public string GenerateJwtToken(DateTime expiry)
        {
            // Test credentials. Do not use in any production setting
            string secretKey = Encoding.UTF8.GetString(new byte[128]);
            string issuer = "someIssuer";
            string audience = "someAudience";
            var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: null,
                expires: expiry,
                signingCredentials: credentials
            );

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }
    }
}
