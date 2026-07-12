using Domain.Models;
using Microsoft.Extensions.Configuration;
using Services.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Tests.Services
{
    public class TokenServicesTests
    {
        [Test]
        public async Task CreateJwtToken_GeneratesValidTokenWithCorrectClaims()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string> {
                {"JWT:KEY", "ToJestBardzoDlugiKluczSzyfrujacyKtoryMusiMiecMinimum16Znakow"},
                {"JWT:ISSUER", "TestIssuer"},
                {"JWT:AUDIENCE", "TestAudience"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            var tokenService = new TokenServices(configuration);

            var user = new ApplicationUser { 
                Id = Guid.NewGuid(), 
                Email = "test@test.pl", 
                UserName = "TestUser",
                FirstName = "Test",
                LastName = "Test",
            };
            
            var roles = new List<string> { "Admin", "User" };

            // Act
            var tokenString = tokenService.CreateJwtToken(user, roles);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);

            await Assert.That(jwtToken.Issuer).IsEqualTo("TestIssuer");
            await Assert.That(jwtToken.Claims.First(c => c.Type == ClaimTypes.Email).Value).IsEqualTo("test@test.pl");
            await Assert.That(jwtToken.Claims.Count(c => c.Type == ClaimTypes.Role)).IsEqualTo(2);
            await Assert.That(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Admin")).IsTrue();
        }
    }
}
