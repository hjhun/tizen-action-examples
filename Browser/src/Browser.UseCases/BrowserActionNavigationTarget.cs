using Browser.Domain;

namespace Browser.UseCases;

public sealed record BrowserActionNavigationTarget(string SelectedTabId, string Url);

public static class BrowserActionNavigationTargetContract
{
    public static bool TryCreate(
        BrowserTabWorkspace? workspace,
        bool isApplicationVisible,
        BrowserPage requestedPage,
        out BrowserActionNavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(requestedPage);
        target = null!;
        if (!isApplicationVisible || workspace?.Surface != BrowserWorkspaceSurface.Page)
        {
            return false;
        }

        target = new BrowserActionNavigationTarget(workspace.SelectedTabId, requestedPage.Url);
        return true;
    }
}
