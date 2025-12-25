using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

// UserManagementAPI/Services/JwtTokenService.cs
public class JwtTokenService
{
    private readonly string _issuer; // Token issuer
    private readonly string _audience; // Token audience
    private readonly string _key; // Secret key

    // Constructor to initialize JwtTokenService with issuer, audience, and key
    public JwtTokenService(string issuer, string audience, string key)
    {
        _issuer = issuer;
        _audience = audience;
        _key = key;
    }

    // Method to generate JWT token
    public string GenerateJwtToken(string username, string role)
    {
        // Create security key and signing credentials
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Define claims for the token
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username), // User's name claim
            new Claim("role", role) // User's role claim
        };

        // Create the JWT token
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24), // Token expiration time
            signingCredentials: credentials // Signing credentials
        );

        // Return the serialized token
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}