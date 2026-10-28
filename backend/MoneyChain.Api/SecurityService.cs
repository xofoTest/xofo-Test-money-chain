using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace MoneyChain.Api;

public class SecurityService
{
    private readonly PasswordHasher<Profile> _hasher = new();
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;

    public SecurityService(IConfiguration config)
    {
        // Demo signing key for the local prototype only. Override via Jwt:Key in real use.
        _key = config["Jwt:Key"] ?? "MoneyChainSAS-local-prototype-signing-key-0123456789";
        _issuer = config["Jwt:Issuer"] ?? "moneychain";
        _audience = config["Jwt:Audience"] ?? "moneychain";
    }

    public string HashPassword(string password) => _hasher.HashPassword(new Profile(), password);

    public bool VerifyPassword(string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(new Profile(), hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public string CreateToken(Profile profile)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, profile.Email),
            new("role", profile.Role),
            new("member_code", profile.MemberCode),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static Guid UserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new DomainException("Unauthenticated", 401));

    public static string UserRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role") ?? "member";
}
