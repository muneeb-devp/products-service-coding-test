using System.Security.Claims;
using System.Security.Cryptography;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Products.Api.Authentication;
using Products.Api.Contracts;
using Products.Api.Infrastructure;

namespace Products.Api.Controllers;

/// <summary>
/// Issues demo bearer tokens.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/auth")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController(
    ITokenService tokenService,
    IOptions<DemoCredentialsOptions> demoCredentials,
    TimeProvider timeProvider) : ControllerBase
{
    private readonly DemoCredentialsOptions _demoCredentials = demoCredentials.Value;

    /// <summary>Issues a short-lived JWT for the demo user.</summary>
    /// <param name="request">Demo credentials.</param>
    /// <response code="200">A signed bearer token and its expiry.</response>
    /// <response code="400">Username or password was missing.</response>
    /// <response code="401">The credentials were not recognised.</response>
    /// <response code="429">Too many token requests; retry after a short delay.</response>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<TokenResponse> CreateToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["credentials"] = ["Username and password are required."],
                }));
        }

        if (!AreCredentialsValid(request.Username, request.Password))
        {
            // One message for both "unknown user" and "wrong password". Telling
            // them apart hands an attacker a way to enumerate valid usernames.
            return Problem(
                title: "Invalid credentials",
                detail: "The supplied username or password is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = tokenService.CreateToken(
            request.Username,
            [
                new Claim(ClaimTypes.Name, request.Username),

                // A role claim, so the shape is already right for
                // [Authorize(Roles = "...")] when real authorisation arrives.
                new Claim(ClaimTypes.Role, "catalogue-manager"),
            ]);

        var expiresInSeconds = (int)(expiresAt - timeProvider.GetUtcNow()).TotalSeconds;

        return Ok(new TokenResponse(token, "Bearer", expiresAt, expiresInSeconds));
    }

    /// <summary>Echoes the claims in the caller's token.</summary>
    /// <response code="200">The authenticated subject and their claims.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser() => Ok(new
    {
        subject = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
        isAuthenticated = User.Identity?.IsAuthenticated ?? false,
        claims = User.Claims.Select(c => new { type = c.Type, value = c.Value }),
    });

    /// <summary>
    /// Compares credentials in fixed time.
    /// </summary>
    private bool AreCredentialsValid(string username, string password) =>
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(username),
            System.Text.Encoding.UTF8.GetBytes(_demoCredentials.Username)) &
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(password),
            System.Text.Encoding.UTF8.GetBytes(_demoCredentials.Password));
}
