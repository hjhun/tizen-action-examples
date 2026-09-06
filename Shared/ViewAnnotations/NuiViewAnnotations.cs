using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NuiButton = Tizen.NUI.Components.Button;

namespace ActionExamples.ViewAnnotations;

internal static class NuiViewAnnotations
{
    internal static IEnumerable<(View View, string Key)> Descendants(View root, string path = "root")
    {
        if (!root.Visibility) yield break;
        yield return (root, path);
        for (uint i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChildAt(i);
            var key = string.IsNullOrEmpty(child.Name) ? $"child{i}" : child.Name;
            foreach (var item in Descendants(child, $"{path}/{key}")) yield return item;
        }
    }

    internal static IEnumerable<(View View, string Key)> Controls(View root) => Descendants(root).Where(x => x.View.Focusable);

    internal static void Observe(View root, Action refresh)
    {
        root.Relayout += (_, _) => refresh();
        foreach (var (view, _) in Controls(root))
        {
            view.FocusGained += (_, _) => refresh();
            view.FocusLost += (_, _) => refresh();
            if (view is TextField field) field.TextChanged += (_, _) => refresh();
            else if (view is TextEditor editor) editor.TextChanged += (_, _) => refresh();
            else if (view is NuiButton button) button.Clicked += (_, _) => refresh();
        }
    }

    internal static View? Focused(View root)
    {
        // Touch/pointer text input may hold key focus independently of D-pad focus.
        return Controls(root).Select(x => x.View).FirstOrDefault(x => (x is TextField or TextEditor) && x.KeyInputFocus)
            ?? FocusManager.Instance.GetCurrentFocusView();
    }

    internal static object ControlState(View view, string key) => new
    {
        control = key,
        text = Bounded(view switch
        {
            TextField field => field.Text,
            TextEditor editor => editor.Text,
            NuiButton button => button.Text,
            _ => view.AccessibilityName,
        }),
        selected = view is NuiButton buttonView ? (bool?)buttonView.IsSelected : null,
    };

    internal static string Description(View view, string key) => Bounded(view switch
    {
        TextField field => field.PlaceholderText,
        TextEditor editor => editor.PlaceholderText,
        NuiButton button => button.Text,
        _ => string.IsNullOrEmpty(view.AccessibilityName) ? key : view.AccessibilityName,
    });

    internal static CurrentViewSnapshot? Measure(View view, View activeRoot, View? focused, CurrentViewSnapshot snapshot)
    {
        try
        {
            if (!BelongsTo(view, activeRoot)) return null;
            var bounds = view.CalculateScreenPositionSize();
            // Do not publish reference-canvas sizes or an unmeasured native actor as real bounds.
            if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) ||
                !float.IsFinite(bounds.Z) || !float.IsFinite(bounds.W) || bounds.Z <= 0 || bounds.W <= 0) return null;
            double? windowX = null, windowY = null;
            try
            {
                using var origin = Window.Default.WindowPosition;
                windowX = bounds.X - origin.X;
                windowY = bounds.Y - origin.Y;
            }
            catch { /* Window origin is optional; measured screen bounds remain valid. */ }
            return snapshot with
            {
                ScreenX = bounds.X, ScreenY = bounds.Y, Width = bounds.Z, Height = bounds.W,
                WindowX = windowX, WindowY = windowY, IsFocused = view == focused, IsEnabled = view.IsEnabled,
            };
        }
        catch { return null; } // A disposed actor cannot describe the current frame.
    }

    internal static bool BelongsTo(View view, View root)
    {
        for (View? current = view; current is not null; current = current.GetParent() as View)
        {
            if (!current.Visibility) return false;
            if (current == root) return true;
        }
        return false;
    }

    private static string Bounded(string? text) => text is null ? string.Empty : text[..Math.Min(text.Length, 4096)];
}
