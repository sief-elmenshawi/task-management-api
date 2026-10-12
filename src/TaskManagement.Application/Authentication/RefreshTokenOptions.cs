namespace TaskManagement.Application.Authentication;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// How long a token that was rotated (revoked by a successful refresh) is
    /// still considered a legitimate retry rather than a reuse attack. Defaults
    /// to 30 seconds.
    /// </summary>
    public int ReuseGraceSeconds { get; set; } = 30;
}