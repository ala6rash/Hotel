using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login(LoginDto dto)
    {
        var result = await _auth.LoginAsync(dto);
        if (result == null)
            return Unauthorized(ApiResponse<LoginResponseDto>.Fail("Invalid email or password"));

        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Refresh(
        [FromBody] string refreshToken)
    {
        var result = await _auth.RefreshTokenAsync(refreshToken);
        if (result == null)
            return Unauthorized(ApiResponse<LoginResponseDto>.Fail("Invalid or expired refresh token"));

        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        await _auth.RevokeTokenAsync(userId);
        return Ok(ApiResponse<bool>.Ok(true, "Logged out successfully"));
    }
}
