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
    internal const float DesignWidth = 1920.0f;
    internal const float DesignHeight = 1080.0f;
    internal const float HeaderHeight = 120.0f;
    internal const float ContentLeft = 52.0f;
    internal const float ContentTop = 215.0f;
    internal const float ContentWidth = 1816.0f;
    internal const float ContentHeight = 821.0f;

    private const string InitialAddress = "https://www.tizen.org/";
    private readonly Action<string> _navigate;
    private readonly TextField _address;
    private readonly TextLabel _title;
    private readonly TextLabel _url;
    private readonly View _reload;
    private readonly View _tabs;
    private readonly Dictionary<View, Action> _activations = new();

    internal BrowserChromeView(Action<string> navigate)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        Canvas = new View
        {
            Name = "BrowserReferenceCanvas",
            Size = new Size(DesignWidth, DesignHeight),
            ParentOrigin = ParentOrigin.TopLeft,
            PivotPoint = PivotPoint.TopLeft,
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };

        var header = new View
        {
            Name = "BrowserCommandBand",
            Size = new Size(DesignWidth, HeaderHeight),
            BackgroundColor = Color.White,
            FocusableChildren = true,
        };
        Canvas.Add(header);

        header.Add(Label("Browser", "#1B1B1FFF", 9.0f, new Position(52, 0), new Size(190, HeaderHeight), HorizontalAlignment.Begin));
        header.Add(CreateDisabledControl("Back", "Back unavailable", "←", new Position(260, 27), new Size(66, 66)));
        header.Add(CreateDisabledControl("Forward", "Forward unavailable", "→", new Position(336, 27), new Size(66, 66)));
        _reload = CreateControl("Reload", "Reload current page", "↻", new Position(412, 27), new Size(66, 66), Reload);
        header.Add(_reload);

        _address = new TextField
        {
            Name = "BrowserAddress",
            Text = InitialAddress,
            PlaceholderText = "Address or search",
            PlaceholderTextColor = new Color("#61616AFF"),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(496, 27),
            Size = new Size(1044, 66),
            BackgroundColor = new Color("#F3F3F6FF"),
            CornerRadius = 16.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#777782FF"),
            AccessibilityName = "Address or search. Press Enter to load.",
        };
        header.Add(_address);

        _tabs = CreateControl("Tabs", "Open tabs", "Tabs 1", new Position(1560, 27), new Size(150, 66), () => { });
        header.Add(_tabs);
        header.Add(Label("1", "#1B1B1FFF", 5.0f, new Position(1668, 27), new Size(24, 66), HorizontalAlignment.Center));

        Canvas.Add(new View
        {
            Position = new Position(0, HeaderHeight - 1),
            Size = new Size(DesignWidth, 1),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        _title = Label("Loading page", "#1B1B1FFF", 7.0f, new Position(ContentLeft, 150), new Size(510, 45), HorizontalAlignment.Begin);
        _url = Label(InitialAddress, "#61616AFF", 5.5f, new Position(580, 150), new Size(1200, 45), HorizontalAlignment.Begin);
        Canvas.Add(_title);
        Canvas.Add(_url);
    }

    internal View Canvas { get; }

    internal TextField AddressField => _address;

    internal void AddWebView(WebView webView)
    {
        ArgumentNullException.ThrowIfNull(webView);
        webView.Name = "BrowserWebContent";
        webView.Position = new Position(ContentLeft, ContentTop);
        webView.Size = new Size(ContentWidth, ContentHeight);
        Canvas.Add(webView);
    }

    internal void UpdatePage(BrowserPage? page, string? error = null)
    {
        if (page is not null)
        {
            _title.Text = page.Title;
            _url.Text = page.Url;
            _address.Text = page.Url;
            return;
        }

        _title.Text = string.IsNullOrWhiteSpace(error) ? "Loading page" : "Page unavailable";
        _url.Text = string.IsNullOrWhiteSpace(error) ? InitialAddress : error;
    }

    internal bool TryHandleKey(string keyName)
    {
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

    private View CreateDisabledControl(string name, string accessibilityName, string text, Position position, Size size)
    {
        var control = CreateControl(name, accessibilityName, text, position, size, () => { });
        control.Focusable = false;
        control.BackgroundColor = new Color("#F4F4F6FF");
        control.BorderlineColor = new Color("#E8E8ECFF");
        return control;
    }

    private View CreateControl(string name, string accessibilityName, string text, Position position, Size size, Action activate)
    {
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
        control.TouchEvent += (_, eventArgs) =>
        {
            var state = eventArgs.Touch.GetState(0);
            if (state is PointStateType.Down or PointStateType.Started)
            {
                FocusManager.Instance.SetCurrentFocusView(control);
            }
            else if (state is PointStateType.Up or PointStateType.Finished)
            {
                activate();
            }

            return true;
        };
        _activations[control] = activate;
        return control;
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
