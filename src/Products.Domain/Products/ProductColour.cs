namespace Products.Domain.Products;

/// <summary>
/// The set of colours a product may be.
/// </summary>
/// <remarks>
/// Modelled as an enum rather than a free-text string so that "Red", "red" and
/// "RED" cannot become three different things in the database, and so the
/// filter endpoint has a closed, documented set of valid values.
/// <para>
/// The enum is persisted <em>by name</em>, not by ordinal — see
/// <c>ProductConfiguration</c>. Storing ordinals would make inserting a new
/// colour into the middle of this list silently re-label existing rows.
/// </para>
/// <para>
/// The brief uses the British spelling "colour"; the HTTP surface uses the
/// American "color" to match the more common convention in JSON APIs. The type
/// name keeps the brief's spelling, the wire contract uses <c>color</c>.
/// </para>
/// </remarks>
public enum ProductColour
{
    Red = 1,
    Green = 2,
    Blue = 3,
    Yellow = 4,
    Orange = 5,
    Purple = 6,
    Pink = 7,
    Brown = 8,
    Black = 9,
    White = 10,
    Grey = 11,
    Silver = 12,
    Gold = 13,
}
