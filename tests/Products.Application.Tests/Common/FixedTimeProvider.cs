namespace Products.Application.Tests.Common;

/// <summary>
/// A <see cref="TimeProvider"/> that always reports the same instant.
/// </summary>
/// <remarks>
/// The handlers take <see cref="TimeProvider"/> rather than calling
/// <c>DateTimeOffset.UtcNow</c>, which is what makes timestamp assertions exact
/// instead of "within a tolerance". Hand-rolled rather than pulling in
/// Microsoft.Extensions.TimeProvider.Testing: overriding one method does not
/// justify another dependency.
/// </remarks>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>The canonical instant used across the test suite.</summary>
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 9, 14, 10, 30, 0, TimeSpan.Zero);

    /// <summary>A provider fixed at <see cref="DefaultNow"/>.</summary>
    public static FixedTimeProvider Default => new(DefaultNow);

    public override DateTimeOffset GetUtcNow() => now;
}
