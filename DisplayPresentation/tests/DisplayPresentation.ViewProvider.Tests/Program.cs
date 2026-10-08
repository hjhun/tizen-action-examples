using System.Text.Json;
using DisplayPresentation.Domain;
using DisplayPresentation.ViewActionProvider;

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

var provider = new DisplayPresentationViewService();
var input = new PresentationInput("quoted \"template\"\n한글", "document\n\\path\"雪");
DisplayPresentationViewActionProviderHost.ClearPublishedViews();
DisplayPresentationViewActionProviderHost.PublishVisibleSurface(
    "current", input, 10, 20, 1, 2, 300, 200, true);
Assert(provider.GetAnnotatedViews(out var views).Success && views.Count == 1,
    "Only the published current surface must appear.");
var view = views[0];
using (var json = JsonDocument.Parse(view.Annotation.EntityInfo))
{
    var wrapper = json.RootElement;
    Assert(wrapper.EnumerateObject().Count() == 1, "Preserve the legacy annotation wrapper.");
    var presentation = wrapper.GetProperty("TizenEntityPresentation");
    Assert(presentation.EnumerateObject().Count() == 2
        && presentation.GetProperty("Template").GetString() == input.Template
        && presentation.GetProperty("Document").GetString() == input.Document,
        "Preserve semantic JSON including quotes, newlines, escapes, and Unicode.");
}
Assert(view.ScreenBounds.X == 10 && view.ScreenBounds.Y == 20
    && view.ScreenBounds.Width == 300 && view.ScreenBounds.Height == 200
    && view.WindowBounds.X == 1 && view.WindowBounds.Y == 2,
    "Composition must preserve supplied actual geometry.");
Assert(provider.GetFocusedView(out var focused).Success && focused.Id == view.Id,
    "Composition must preserve current focus.");
DisplayPresentationViewActionProviderHost.PublishViews([
    new("new-view", "new-surface", "Current", input, 40, 50, null, null, 60, 70, false),
]);
var absentGeometry = provider.GetAnnotatedViews(out var replaced);
Assert(!absentGeometry.Success && absentGeometry.Reason.StartsWith("unavailable:")
    && replaced.Count == 0, "Absent required geometry must reject the whole result.");
Assert(!provider.FindById("new-view", out var unavailableView).Success
    && unavailableView.Id == "" && unavailableView.Extra == ""
    && unavailableView.Type == "" && unavailableView.Description == ""
    && unavailableView.ScreenBounds is not null && unavailableView.WindowBounds is not null
    && unavailableView.Annotation is not null,
    "Unavailable lookup must initialize all required failure fields.");
Assert(!provider.FindById(view.Id, out _).Success && !provider.GetFocusedView(out _).Success,
    "Removed surfaces and old focus must not remain visible.");
foreach (var coordinates in new (double? X, double? Y)[] { (null, null), (1, null), (null, 2) })
{
    DisplayPresentationViewActionProviderHost.PublishViews([
        new("valid", "valid-surface", "Valid", input, 10, 20, 1, 2, 30, 40, false),
        new("unavailable", "unavailable-surface", "Unavailable", input, 10, 20,
            coordinates.X, coordinates.Y, 30, 40, true),
    ]);
    var mixed = provider.GetAnnotatedViews(out var mixedViews);
    Assert(!mixed.Success && mixed.Reason.StartsWith("unavailable:") && mixedViews.Count == 0,
        "Mixed measured and unmeasured views must not publish a successful subset.");
    var unavailableFocus = provider.GetFocusedView(out var unavailableFocused);
    Assert(!unavailableFocus.Success && unavailableFocus.Reason.StartsWith("unavailable:")
        && unavailableFocused.Id == "" && unavailableFocused.WindowBounds is not null
        && unavailableFocused.ScreenBounds is not null && unavailableFocused.Annotation is not null,
        "Unavailable focused geometry must produce a valid failure View.");
    Assert(provider.FindById("valid", out var preserved).Success
        && preserved.WindowBounds.X == 1 && preserved.WindowBounds.Y == 2,
        "Rejected queries must preserve the current measured snapshot.");
    var unavailableLookup = provider.FindById("unavailable", out var rejected);
    Assert(!unavailableLookup.Success && unavailableLookup.Reason.StartsWith("unavailable:")
        && rejected.Id == "" && rejected.WindowBounds is not null,
        "Rejected queries must preserve the unmeasured view as explicitly unavailable.");
}
DisplayPresentationViewActionProviderHost.ClearPublishedViews();
Assert(provider.GetAnnotatedViews(out var cleared).Success && cleared.Count == 0,
    "Clear must remove the actual current surface.");
Console.WriteLine("PASS: Display composition JSON, geometry, focus, replacement, and clear (no native UI/Parcel claim).");
