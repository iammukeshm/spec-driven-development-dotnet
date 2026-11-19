using System.Text.RegularExpressions;

namespace Catalog.Api;

public class ProductTag
{
    public int ProductId { get; set; }
    public required string Tag { get; set; }
}

public record AddTagRequest(string? Tag);

public static partial class TagRules
{
    public const int MaxTagsPerProduct = 5;
    public const int MaxTagLength = 30;

    public static string Normalize(string? tag) => (tag ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValid(string tag) => TagPattern().IsMatch(tag);

    [GeneratedRegex(@"\A[a-z0-9-]{1,30}\z")]
    private static partial Regex TagPattern();
}
