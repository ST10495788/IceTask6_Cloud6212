
using Ice_Task3_NotesAPI.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ice_Task3_NotesAPI.Services;

public class AuthService
{
    private readonly UserService _userService;
    private readonly IConfiguration _configuration;

    public AuthService( UserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    public async Task<User> RegisterAsync( string displayName, string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        if (await _userService.GetByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException("An account with this email type has already been created or it exists.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid().ToString(),
            DisplayName = displayName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            PartitionKey = "Users"
        };

        await _userService.CreateAsync(user);
        return user;
    }

    public async Task<string?> LoginAsync( string email, string password)
    {
        var user = await _userService.GetByEmailAsync(email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }
        return GenerateToken(user);
    }

    private string GenerateToken(User user)
    {
        string? secret = _configuration["Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException( "Jwt:Secret must be configured with at least 32 bytes.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials( key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken( issuer: _configuration["Jwt:Issuer"], audience: _configuration["Jwt:Audience"],
            claims: claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
