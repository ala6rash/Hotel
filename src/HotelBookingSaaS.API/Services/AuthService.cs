using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HotelBookingSaaS.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto dto);
    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _db.Users
            .Include(u => u.Hotel)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        var (accessToken, expiry) = GenerateJwt(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = BCrypt.Net.BCrypt.HashPassword(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
        user.LastLoginDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiry,
            UserId = user.UserId,
            Email = user.Email,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = user.Role.ToString(),
            HotelId = user.HotelId
        };
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _db.Users
            .Where(u => u.RefreshToken != null &&
                        u.RefreshTokenExpiresAt > DateTime.UtcNow &&
                        u.IsActive)
            .ToListAsync();

        // Find user by verifying hashed refresh token
        var matched = user.FirstOrDefault(u =>
            BCrypt.Net.BCrypt.Verify(refreshToken, u.RefreshToken!));

        if (matched == null) return null;

        var (accessToken, expiry) = GenerateJwt(matched);
        var newRefresh = GenerateRefreshToken();
        matched.RefreshToken = BCrypt.Net.BCrypt.HashPassword(newRefresh);
        matched.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefresh,
            ExpiresAt = expiry,
            UserId = matched.UserId,
            Email = matched.Email,
            FullName = $"{matched.FirstName} {matched.LastName}",
            Role = matched.Role.ToString(),
            HotelId = matched.HotelId
        };
    }

    public async Task RevokeTokenAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync();
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private (string token, DateTime expiry) GenerateJwt(User user)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key not configured");
        var issuer = _config["Jwt:Issuer"] ?? "HotelSaaS";
        var audience = _config["Jwt:Audience"] ?? "HotelSaaS";
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("hotelId", user.HotelId?.ToString() ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
