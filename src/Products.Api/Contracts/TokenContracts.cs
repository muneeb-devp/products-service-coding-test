namespace Products.Api.Contracts;

/// <summary>
/// Credentials for the demo token endpoint.
/// </summary>
/// <param name="Username">Demo username.</param>
/// <param name="Password">Demo password.</param>
public sealed record TokenRequest(string? Username, string? Password);

/// <summary>
/// An issued bearer token.
/// </summary>
/// <param name="AccessToken">The compact JWT, to be sent as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresAt">Absolute expiry (UTC).</param>
/// <param name="ExpiresInSeconds">Seconds until expiry, for clients that prefer a relative value.</param>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);
