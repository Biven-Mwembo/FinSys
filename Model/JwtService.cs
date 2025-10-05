using System;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using FinSys.Models;

namespace FinSys.Services
{
    public class JwtService
    {
        // FIX: The secret key MUST match the key used in Program.cs for validation to succeed.
        // In a real application, this would be injected via configuration (IConfiguration).
        private readonly string _secretKey = "super_secret_key_12345";
        private readonly string _issuer = "FinSysAPI";
        private readonly string _audience = "FinSysClient";
        private readonly int _expiryHours = 24;

        /// <summary>
        /// Generates a signed JWT for the authenticated user.
        /// </summary>
        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            // Convert secret key to byte array
            var key = Encoding.ASCII.GetBytes(_secretKey);

            // Define the token contents (Claims)
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    // ClaimTypes.NameIdentifier is crucial; controllers use it to get the user ID
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                }),

                // Define metadata
                Expires = DateTime.UtcNow.AddHours(_expiryHours),
                Issuer = _issuer,
                Audience = _audience,

                // Define signing credentials
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
