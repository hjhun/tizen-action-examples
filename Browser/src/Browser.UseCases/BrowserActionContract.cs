using Browser.Domain;

namespace Browser.UseCases;

/// <summary>
/// Host-runnable validation and projection rules used by the generated Browser Action adapter.
/// Generated Tizen DTO mapping remains at the provider boundary.
/// </summary>
public static class BrowserActionContract
{
    public const int MaximumResolverIds = 100;
    public const int MaximumIdLength = 256;

    public static bool TryCreatePage(string? id, string? url, string? title, string? details, out BrowserPage page)
    {
        page = null!;
        try
        {
            page = BrowserPage.Create(id ?? string.Empty, url ?? string.Empty, title ?? string.Empty, details ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool HasValidResolverIds(IReadOnlyCollection<string>? ids) =>
        ids is not null && ids.Count <= MaximumResolverIds &&
        ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= MaximumIdLength);

    public static BrowserPresentation CreatePresentation(BrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new BrowserPresentation(
            "{\"surfaceUpdate\":{\"surfaceId\":\"browser-page\",\"components\":[]}}",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                dataModelUpdate = new { browser = new { page.Id, page.Url, page.Title, page.Details } }
            }));
    }
}

public sealed record BrowserPresentation(string Template, string Document);
