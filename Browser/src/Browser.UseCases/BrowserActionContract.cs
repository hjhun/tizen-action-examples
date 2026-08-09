using Browser.Domain;
using System.Text.Json;

namespace Browser.UseCases;

/// <summary>
/// Host-runnable validation and projection rules used by the generated Browser Action adapter.
/// Generated Tizen DTO mapping remains at the provider boundary.
/// </summary>
public static class BrowserActionContract
{
    public const int MaximumResolverIds = 50;
    public const int MaximumIdLength = 256;
    public const int MaximumPresentationCharacters = 256 * 1024;
    public const string BasicCatalogId = "https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json";
    public const string A2UiMimeType = "application/a2ui+json";
    private const string A2UiVersion = "v0.9.1";
    private const string SurfaceId = "browser-page";
    private const int MaximumDisplayCharacters = 256;

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
        ids is not null && ids.Count is > 0 and <= MaximumResolverIds &&
        ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= MaximumIdLength);

    public static BrowserPresentationProfiles CreatePresentations(BrowserPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var snapshot = BrowserPresentationSnapshot.Create(page);
        var canonicalMessages = new[]
        {
            JsonSerializer.Serialize(new
            {
                version = A2UiVersion,
                createSurface = new
                {
                    surfaceId = SurfaceId,
                    catalogId = BasicCatalogId,
                },
            }),
            JsonSerializer.Serialize(new
            {
                version = A2UiVersion,
                updateComponents = new
                {
                    surfaceId = SurfaceId,
                    components = new object[]
                    {
                        new { id = "root", component = "Column", children = new[] { "context", "title", "url", "details" } },
                        new { id = "context", component = "Text", text = new { path = "/browser/context" }, variant = "caption" },
                        new { id = "title", component = "Text", text = new { path = "/browser/title" }, variant = "h2" },
                        new { id = "url", component = "Text", text = new { path = "/browser/url" }, variant = "body" },
                        new { id = "details", component = "Text", text = new { path = "/browser/details" }, variant = "body" },
                    },
                },
            }),
            JsonSerializer.Serialize(new
            {
                version = A2UiVersion,
                updateDataModel = new
                {
                    surfaceId = SurfaceId,
                    path = "/",
                    value = new
                    {
                        browser = new
                        {
                            id = snapshot.Id,
                            context = snapshot.Context,
                            url = snapshot.Url,
                            title = snapshot.Title,
                            details = snapshot.Details,
                        },
                    },
                },
            }),
        };
        var deleteMessage = JsonSerializer.Serialize(new
        {
            version = A2UiVersion,
            deleteSurface = new { surfaceId = SurfaceId },
        });

        var legacy = new BrowserPresentation(
            JsonSerializer.Serialize(new
            {
                surfaceUpdate = new
                {
                    surfaceId = SurfaceId,
                    components = new object[]
                    {
                        new { id = "root", component = new { Column = new { children = new[] { "context", "title", "url", "details" } } } },
                        new { id = "context", component = new { Text = new { text = new { path = "/context" }, role = "label" } } },
                        new { id = "title", component = new { Text = new { text = new { path = "/title" }, role = "headline" } } },
                        new { id = "url", component = new { Text = new { text = new { path = "/url" }, role = "supporting" } } },
                        new { id = "details", component = new { Text = new { text = new { path = "/details" }, role = "body" } } },
                    },
                },
            }),
            JsonSerializer.Serialize(new
            {
                dataModelUpdate = new
                {
                    surfaceId = SurfaceId,
                    path = "/",
                    value = new
                    {
                        context = snapshot.Context,
                        title = snapshot.Title,
                        url = snapshot.Url,
                        details = snapshot.Details,
                    },
                },
            }));

        var canonical = new BrowserCanonicalPresentation(
            A2UiVersion,
            A2UiMimeType,
            BasicCatalogId,
            SurfaceId,
            canonicalMessages,
            deleteMessage);
        var characters = canonical.Messages.Sum(message => message.Length) +
            canonical.DeleteMessage.Length + legacy.Template.Length + legacy.Document.Length;
        if (characters > MaximumPresentationCharacters)
        {
            throw new InvalidOperationException("The bounded Browser presentation exceeded its payload budget.");
        }

        return new BrowserPresentationProfiles(canonical, legacy);
    }

    public static BrowserPresentation CreateLegacyDisplayPresentation(BrowserPage page) =>
        CreatePresentations(page).LegacyDisplayCompatibility;

    public static bool RepresentsSamePage(BrowserPage left, BrowserPage right) =>
        left.Id == right.Id && left.Url == right.Url && left.Title == right.Title && left.Details == right.Details;

    private sealed record BrowserPresentationSnapshot(string Id, string Context, string Url, string Title, string Details)
    {
        internal static BrowserPresentationSnapshot Create(BrowserPage page) => new(
            Bound(page.Id),
            "Browser page",
            Bound(page.Url),
            Bound(page.Title),
            Bound(page.Details));

        private static string Bound(string value) =>
            value.Length <= MaximumDisplayCharacters ? value : value[..MaximumDisplayCharacters];
    }
}

public sealed record BrowserPresentation(string Template, string Document);

public sealed record BrowserCanonicalPresentation(
    string Version,
    string MimeType,
    string CatalogId,
    string SurfaceId,
    IReadOnlyList<string> Messages,
    string DeleteMessage);

public sealed record BrowserPresentationProfiles(
    BrowserCanonicalPresentation Canonical,
    BrowserPresentation LegacyDisplayCompatibility);
