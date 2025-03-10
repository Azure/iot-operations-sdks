using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol;
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
            File.WriteAllText("./TestFiles/TEST_JWT", GenerateJwtToken(DateTime.UtcNow.AddMinutes(60)));
            try
            {
                TokenRefreshTimer tokenRefreshTimer = new(new Mock<IMqttClient>().Object, "./TestFiles/TEST_JWT");
            }
            finally
            {
                try
                {
                    File.Delete("./TestFiles/TEST_JWT");
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
            File.WriteAllText("./TestFiles/TEST_JWT", GenerateJwtToken(DateTime.UtcNow.AddMinutes(-60)));
            try
            {
                Assert.Throws<ArgumentException>(() => new TokenRefreshTimer(new Mock<IMqttClient>().Object, "./TestFiles/TEST_JWT"));
            }
            finally
            {
                try
                {
                    File.Delete("./TestFiles/TEST_JWT");
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
