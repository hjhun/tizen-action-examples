namespace PhotoGallery.Domain;

public sealed record PhotoSearchCriteria(string Keyword, DateTimeOffset? FromInclusive, DateTimeOffset? UntilExclusive, int Limit)
{
    public const int MaximumKeywordLength = 256;
    public const int MaximumResultCount = 200;

    public static PhotoSearchCriteria Create(string? keyword, DateTimeOffset? fromInclusive, DateTimeOffset? untilExclusive, int limit)
    {
        var trimmedKeyword = keyword?.Trim() ?? string.Empty;
        if (trimmedKeyword.Length > MaximumKeywordLength)
        {
            throw new ArgumentException($"The search keyword must not exceed {MaximumKeywordLength} characters.", nameof(keyword));
        }

        if (fromInclusive is not null && untilExclusive is not null && untilExclusive <= fromInclusive)
        {
            throw new ArgumentException("The search end must be after its start.", nameof(untilExclusive));
        }

        if (limit is < 1 or > MaximumResultCount)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"The result limit must be between 1 and {MaximumResultCount}.");
        }

        return new PhotoSearchCriteria(trimmedKeyword, fromInclusive, untilExclusive, limit);
    }
}
