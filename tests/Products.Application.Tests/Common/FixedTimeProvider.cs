namespace Products.Application.Tests.Common;

/// <summary>
/// A <see cref="TimeProvider"/> that always reports the same instant.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>The canonical instant used across the test suite.</summary>
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 9, 14, 10, 30, 0, TimeSpan.Zero);

    /// <summary>A provider fixed at <see cref="DefaultNow"/>.</summary>
    public static FixedTimeProvider Default => new(DefaultNow);

    public override DateTimeOffset GetUtcNow() => now;
}
