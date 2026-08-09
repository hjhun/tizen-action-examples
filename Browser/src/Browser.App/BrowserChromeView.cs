using Browser.Domain;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace Browser.App;

/// <summary>
/// Persistent, reference-canvas browser chrome. It deliberately owns no browser state: every
/// activation is forwarded to the app's shared navigation command path and the live WebView stays
/// mounted below this command band.
/// </summary>
internal sealed class BrowserChromeView
{
    private const string InitialAddress = "https://www.tizen.org/";
    private readonly Action<string> _navigate;
    private readonly View _back;
    private readonly View _forward;
    private readonly TextField _address;
    private readonly TextLabel _title;
    private readonly TextLabel _url;
    private readonly TextLabel _state;
    private readonly View _reload;
    private readonly View _tabs;
    private WebView? _webView;
    private BrowserShellFocusGraph _focusGraph = BrowserShellFocusGraph.Create(false, false);
    private readonly Dictionary<View, Action> _activations = new();

    internal BrowserChromeView(
        Action<string> navigate,
        Action? goBack = null,
        Action? goForward = null,
        Action? openTabs = null)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        Root = new View
        {
            Name = "BrowserPhysicalRoot",
            ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color("#17181BFF"),
            FocusableChildren = true,
        };
        Canvas = new View
        {
            Name = "BrowserReferenceCanvas",
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.DesignHeight),
            ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        Root.Add(Canvas);

        var header = new View
        {
            Name = "BrowserCommandBand",
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.HeaderHeight),
            BackgroundColor = Color.White,
            FocusableChildren = true,
        };
        Canvas.Add(header);

        header.Add(new View
        {
            Name = "BrowserProductMark",
            Position = new Position(52, 45),
            Size = new Size(42, 42),
            BackgroundColor = new Color("#134F9EFF"),
            CornerRadius = 14.0f,
        });
        header.Add(Label("Browser", "#1B1B1FFF", 9.0f, new Position(108, 0), new Size(132, BrowserShellMetrics.HeaderHeight), HorizontalAlignment.Begin));
        _back = CreateDisabledControl("Back", "Back unavailable", "←", new Position(258, 33), new Size(66, 66), goBack);
        header.Add(_back);
        _forward = CreateDisabledControl("Forward", "Forward unavailable", "→", new Position(334, 33), new Size(66, 66), goForward);
        header.Add(_forward);
        _reload = CreateControl("Reload", "Reload current page", "↻", new Position(410, 33), new Size(66, 66), Reload);
        header.Add(_reload);

        _address = new TextField
        {
            Name = "BrowserAddress",
            Text = InitialAddress,
            PlaceholderText = "Address or search",
            PlaceholderTextColor = new Color("#61616AFF"),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(498, 31),
            Size = new Size(1188, 70),
            BackgroundColor = new Color("#F3F3F6FF"),
            CornerRadius = 18.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#777782FF"),
            AccessibilityName = "Address or search. Press Enter to load.",
        };
        _address.FocusGained += (_, _) => ApplyAddressFocusStyle(true);
        _address.FocusLost += (_, _) => ApplyAddressFocusStyle(false);
        header.Add(_address);

        _tabs = CreateControl("Tabs", "Open tabs. 1 tab.", "Tabs   1", new Position(1704, 33), new Size(164, 66), openTabs ?? (() => { }));
        header.Add(_tabs);

        Canvas.Add(new View
        {
            Position = new Position(0, BrowserShellMetrics.HeaderHeight - 1),
            Size = new Size(BrowserShellMetrics.DesignWidth, 1),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        _title = Label("Loading page", "#1B1B1FFF", 7.0f, new Position(52, 132), new Size(360, 92), HorizontalAlignment.Begin);
        _url = Label(InitialAddress, "#61616AFF", 5.5f, new Position(432, 132), new Size(1240, 92), HorizontalAlignment.Begin);
        _state = Label("LOADING", "#61616AFF", 4.8f, new Position(1716, 132), new Size(152, 92), HorizontalAlignment.End);
        Canvas.Add(_title);
        Canvas.Add(_url);
        Canvas.Add(_state);
        Canvas.Add(new View
        {
            Name = "BrowserProgressTrack",
            Position = new Position(0, BrowserShellMetrics.HeaderHeight + BrowserShellMetrics.ContextHeight),
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.ProgressHeight),
            BackgroundColor = new Color("#E5E5EAFF"),
        });
    }

    internal View Root { get; }

    internal View Canvas { get; }

    internal TextField AddressField => _address;

    internal void AddWebView(WebView webView)
    {
        ArgumentNullException.ThrowIfNull(webView);
        _webView = webView;
        webView.Name = "BrowserWebContent";
        webView.Focusable = true;
        webView.Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.ContentTop);
        webView.Size = new Size(BrowserShellMetrics.ContentWidth, BrowserShellMetrics.ContentHeight);
        Canvas.Add(webView);
    }

    internal void UpdatePhysicalSize(float width, float height) => Root.Size = new Size(width, height);

    internal void SetHistoryAvailability(bool canGoBack, bool canGoForward)
    {
        SetControlEnabled(_back, canGoBack, canGoBack ? "Go back" : "Back unavailable");
        SetControlEnabled(_forward, canGoForward, canGoForward ? "Go forward" : "Forward unavailable");
        _focusGraph = BrowserShellFocusGraph.Create(canGoBack, canGoForward);
    }

    internal void UpdatePage(BrowserPage? page, string? error = null)
    {
        if (page is not null)
        {
            _title.Text = page.Title;
            _url.Text = page.Url;
            _address.Text = page.Url;
            _state.Text = "READY";
            return;
        }

        _title.Text = string.IsNullOrWhiteSpace(error) ? "Loading page" : "Page unavailable";
        _url.Text = string.IsNullOrWhiteSpace(error) ? InitialAddress : error;
        _state.Text = string.IsNullOrWhiteSpace(error) ? "LOADING" : "ERROR";
    }

    internal bool TryHandleKey(string keyName)
    {
        if (keyName is "Left" or "Right")
        {
            MoveCommandFocus(keyName == "Left" ? -1 : 1);
            return true;
        }

        if (keyName == "Down" && IsCommandControl(FocusManager.Instance.GetCurrentFocusView()))
        {
            FocusTarget(_focusGraph.MoveDown(TargetFor(FocusManager.Instance.GetCurrentFocusView())));
            return true;
        }

        if (keyName == "Up" && ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _webView))
        {
            FocusTarget(_focusGraph.MoveUp(BrowserShellFocusTarget.WebContent));
            return true;
        }

        if (keyName is "Return" or "Enter" or "XF86Select")
        {
            if (ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _address))
            {
                SubmitAddress();
                return true;
            }

            if (_activations.TryGetValue(FocusManager.Instance.GetCurrentFocusView(), out var activate))
            {
                activate();
                return true;
            }
        }

        return false;
    }

    internal void FocusAddress() => FocusManager.Instance.SetCurrentFocusView(_address);

    private void MoveCommandFocus(int delta)
    {
        var current = FocusManager.Instance.GetCurrentFocusView();
        FocusTarget(_focusGraph.MoveHorizontal(TargetFor(current), delta));
    }

    private bool IsCommandControl(View? view) =>
        ReferenceEquals(view, _back) || ReferenceEquals(view, _forward) ||
        ReferenceEquals(view, _reload) || ReferenceEquals(view, _address) || ReferenceEquals(view, _tabs);

    private BrowserShellFocusTarget TargetFor(View? view)
    {
        if (ReferenceEquals(view, _back)) return BrowserShellFocusTarget.Back;
        if (ReferenceEquals(view, _forward)) return BrowserShellFocusTarget.Forward;
        if (ReferenceEquals(view, _reload)) return BrowserShellFocusTarget.Reload;
        if (ReferenceEquals(view, _tabs)) return BrowserShellFocusTarget.Tabs;
        if (ReferenceEquals(view, _webView)) return BrowserShellFocusTarget.WebContent;
        return BrowserShellFocusTarget.Address;
    }

    private void FocusTarget(BrowserShellFocusTarget target)
    {
        var view = target switch
        {
            BrowserShellFocusTarget.Back => _back,
            BrowserShellFocusTarget.Forward => _forward,
            BrowserShellFocusTarget.Reload => _reload,
            BrowserShellFocusTarget.Tabs => _tabs,
            BrowserShellFocusTarget.WebContent => _webView,
            _ => _address,
        };
        if (view is not null && view.Focusable)
        {
            FocusManager.Instance.SetCurrentFocusView(view);
        }
    }

    private void ApplyAddressFocusStyle(bool focused)
    {
        _address.BackgroundColor = focused ? Color.White : new Color("#F3F3F6FF");
        _address.BorderlineColor = new Color(focused ? "#134F9EFF" : "#777782FF");
        _address.BorderlineWidth = focused ? 4.0f : 2.0f;
    }

    private void Reload()
    {
        var address = string.IsNullOrWhiteSpace(_address.Text) ? InitialAddress : _address.Text;
        _navigate(address);
    }

    private void SubmitAddress()
    {
        var address = _address.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            _navigate(address);
        }
    }

    private View CreateDisabledControl(
        string name,
        string accessibilityName,
        string text,
        Position position,
        Size size,
        Action? activate)
    {
        var control = CreateControl(name, accessibilityName, text, position, size, activate ?? (() => { }));
        SetControlEnabled(control, false, accessibilityName);
        return control;
    }

    private View CreateControl(string name, string accessibilityName, string text, Position position, Size size, Action activate)
    {
        var pressed = false;
        var control = new View
        {
            Name = name,
            AccessibilityName = accessibilityName,
            Focusable = true,
            Position = position,
            Size = size,
            BackgroundColor = Color.White,
            CornerRadius = 14.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#134F9EFF"),
            Scale = new Vector3(1.0f, 1.0f, 1.0f),
        };
        control.Add(Label(text, "#1B1B1FFF", 6.0f, new Position(0, 0), size, HorizontalAlignment.Center));
        control.LeaveRequired = true;
        control.FocusGained += (_, _) => ApplyControlFocusStyle(control, true);
        control.FocusLost += (_, _) => ApplyControlFocusStyle(control, false);
        control.TouchEvent += (_, eventArgs) =>
        {
            var state = eventArgs.Touch.GetState(0);
            if (state is PointStateType.Down or PointStateType.Started)
            {
                pressed = true;
                FocusManager.Instance.SetCurrentFocusView(control);
            }
            else if (state is PointStateType.Up or PointStateType.Finished)
            {
                if (pressed && control.Focusable)
                {
                    activate();
                }

                pressed = false;
            }

            return true;
        };
        _activations[control] = activate;
        return control;
    }

    private static void SetControlEnabled(View control, bool enabled, string accessibilityName)
    {
        control.Focusable = enabled;
        control.AccessibilityName = accessibilityName;
        control.BackgroundColor = new Color(enabled ? "#FFFFFFFF" : "#F4F4F6FF");
        control.BorderlineColor = new Color(enabled ? "#DEDEE5FF" : "#E8E8ECFF");
        control.BorderlineWidth = 2.0f;
        control.Opacity = enabled ? 1.0f : 0.55f;
    }

    private static void ApplyControlFocusStyle(View control, bool focused)
    {
        control.BorderlineColor = new Color(focused ? "#134F9EFF" : "#DEDEE5FF");
        control.BorderlineWidth = focused ? 4.0f : 2.0f;
        control.Scale = focused ? new Vector3(1.025f, 1.025f, 1.0f) : new Vector3(1.0f, 1.0f, 1.0f);
    }

    private static TextLabel Label(string text, string color, float pointSize, Position position, Size size, HorizontalAlignment alignment) => new(text)
    {
        Position = position,
        Size = size,
        TextColor = new Color(color),
        PointSize = pointSize,
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
        Ellipsis = true,
        MultiLine = false,
    };
}
