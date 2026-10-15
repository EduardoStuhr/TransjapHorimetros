using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Autentica um usuário corporativo e emite tokens JWT e de atualização.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        authenticationService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            cancellationToken);

    /// <summary>Rotaciona o token de atualização e emite um novo access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken) =>
        authenticationService.RefreshAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            cancellationToken);

    /// <summary>Revoga os tokens de atualização e encerra a sessão.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(
            request,
            currentUser.UserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            cancellationToken);
        return NoContent();
    }

    /// <summary>Retorna os dados do usuário corporativo autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<UserDto> GetMe(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Usuário não autenticado.");
        return authenticationService.GetMeAsync(userId, cancellationToken);
    }
}
