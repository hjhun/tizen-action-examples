using Browser.Domain;
using Browser.UseCases;
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
    private readonly Action _reloadAction;
    private readonly Action _closeTabsAction;
    private readonly Action _retryAction;
    private readonly Action _recoveryBackAction;
    private readonly Action _createTabAction;
    private readonly Action _confirmCloseAction;
    private readonly Action _cancelCloseAction;
    private readonly Action<string> _selectTabAction;
    private readonly Action<string> _requestCloseAction;
    private readonly View _back;
    private readonly View _forward;
    private readonly TextField _address;
    private readonly TextLabel _title;
    private readonly TextLabel _url;
    private readonly TextLabel _state;
    private readonly View _reload;
    private readonly View _tabs;
    private readonly View _bottomDock;
    private readonly View _progressFill;
    private View? _recoverySurface;
    private TextLabel? _recoveryTitle;
    private TextLabel? _recoveryMessage;
    private View? _retry;
    private View? _recoveryBack;
    private View? _editAddress;
    private View? _homeSurface;
    private View? _homeOpenGuide;
    private View? _homeEditAddress;
    private View? _tabsSurface;
    private View? _tabsBack;
    private TextLabel? _tabsCount;
    private View? _tabListViewport;
    private View? _newTab;
    private View? _closeModal;
    private TextLabel? _closeModalTitle;
    private View? _cancelClose;
    private View? _confirmClose;
    private readonly List<View> _dynamicTabViews = [];
    private readonly List<(View Row, View Open, View Close, int Index, string TabId)> _tabRows = [];
    private BrowserTabWorkspace? _workspace;
    private BrowserNavigationState _navigationState = BrowserNavigationState.Initial;
    private WebView? _webView;
    private BrowserShellFocusGraph _focusGraph = BrowserShellFocusGraph.Create(false, false);
    private readonly Dictionary<View, Action> _activations = new();

    internal BrowserChromeView(
        Action<string> navigate,
        Action? goBack = null,
        Action? goForward = null,
        Action? openTabs = null,
        Action? closeTabs = null,
        Action? reload = null,
        Action? retry = null,
        Action? recoveryBack = null,
        Action? createTab = null,
        Action<string>? selectTab = null,
        Action<string>? requestClose = null,
        Action? confirmClose = null,
        Action? cancelClose = null)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        _reloadAction = reload ?? ReloadAddress;
        _closeTabsAction = closeTabs ?? (() => { });
        _retryAction = retry ?? ReloadAddress;
        _recoveryBackAction = recoveryBack ?? FocusAddress;
        _createTabAction = createTab ?? (() => { });
        _selectTabAction = selectTab ?? (_ => { });
        _requestCloseAction = requestClose ?? (_ => { });
        _confirmCloseAction = confirmClose ?? (() => { });
        _cancelCloseAction = cancelClose ?? (() => { });
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
            Name = "BrowserAddressHeader",
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.HeaderHeight),
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        Canvas.Add(header);

        var productMark = new View
        {
            Name = "BrowserProductMark",
            Position = new Position(52, 37),
            Size = new Size(44, 44),
            BackgroundColor = Color.Transparent,
            CornerRadius = 14.0f,
            BorderlineWidth = 3.0f,
            BorderlineColor = new Color("#1B1B1FFF"),
        };
        productMark.Add(new View
        {
            Position = new Position(10, 12),
            Size = new Size(24, 3),
            BackgroundColor = new Color("#0877E8FF"),
            CornerRadius = 2.0f,
        });
        productMark.Add(new View
        {
            Position = new Position(10, 22),
            Size = new Size(24, 3),
            BackgroundColor = new Color("#0877E8FF"),
            CornerRadius = 2.0f,
        });
        header.Add(productMark);
        header.Add(Label("Browser", "#1B1B1FFF", 6.0f, new Position(110, 0), new Size(164, BrowserShellMetrics.HeaderHeight), HorizontalAlignment.Begin));

        _address = new TextField
        {
            Name = "BrowserAddress",
            Text = InitialAddress,
            PlaceholderText = "Search or enter address",
            PlaceholderTextColor = new Color("#61616AFF"),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(298, 24),
            Size = new Size(1478, 70),
            BackgroundColor = new Color("#E9E9EDFF"),
            CornerRadius = 27.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#E9E9EDFF"),
            AccessibilityName = "Address or search. Press Enter to load.",
        };
        _address.FocusGained += (_, _) => ApplyAddressFocusStyle(true);
        _address.FocusLost += (_, _) => ApplyAddressFocusStyle(false);
        header.Add(_address);

        _reload = CreateControl("Reload", "Reload current page", "↻", new Position(1796, 24), new Size(72, 70), _reloadAction);
        _reload.CornerRadius = 25.0f;
        header.Add(_reload);

        _bottomDock = new View
        {
            Name = "BrowserNavigationDock",
            Position = new Position(BrowserShellMetrics.DockLeft, BrowserShellMetrics.DockTop),
            Size = new Size(BrowserShellMetrics.DockWidth, BrowserShellMetrics.DockHeight),
            BackgroundColor = Color.White,
            CornerRadius = 34.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#D7D7DCFF"),
            FocusableChildren = true,
        };
        _back = CreateDisabledControl("Back", "Back unavailable", "←  Back", new Position(10, 10), new Size(230, 70), goBack);
        _forward = CreateDisabledControl("Forward", "Forward unavailable", "→  Forward", new Position(250, 10), new Size(230, 70), goForward);
        _tabs = CreateControl("Tabs", "Open tabs. 1 tab.", "▣  Tabs  1", new Position(490, 10), new Size(240, 70), openTabs ?? (() => { }));
        _bottomDock.Add(_back);
        _bottomDock.Add(_forward);
        _bottomDock.Add(_tabs);
        Canvas.Add(_bottomDock);

        _title = Label("Loading page", "#1B1B1FFF", 1.0f, new Position(0, 0), new Size(1, 1), HorizontalAlignment.Begin);
        _url = Label(InitialAddress, "#61616AFF", 1.0f, new Position(0, 0), new Size(1, 1), HorizontalAlignment.Begin);
        _state = Label("LOADING", "#61616AFF", 1.0f, new Position(0, 0), new Size(1, 1), HorizontalAlignment.End);
        Canvas.Add(_title);
        Canvas.Add(_url);
        Canvas.Add(_state);
        _title.Hide();
        _url.Hide();
        _state.Hide();
        Canvas.Add(new View
        {
            Name = "BrowserProgressTrack",
            Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.HeaderHeight),
            Size = new Size(BrowserShellMetrics.ContentWidth, BrowserShellMetrics.ProgressHeight),
            BackgroundColor = Color.Transparent,
        });
        _progressFill = new View
        {
            Name = "BrowserProgressFill",
            Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.HeaderHeight),
            Size = new Size(0, BrowserShellMetrics.ProgressHeight),
            BackgroundColor = new Color("#0877E8FF"),
        };
        Canvas.Add(_progressFill);
        _progressFill.Hide();
        BuildHomeSurface();
        BuildRecoverySurface();
        BuildTabsSurface();
        BuildCloseModal();
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
        BringOverlaysToFront();
    }

    internal void UpdatePhysicalSize(float width, float height) => Root.Size = new Size(width, height);

    internal void SetHistoryAvailability(bool canGoBack, bool canGoForward)
    {
        SetControlEnabled(_back, canGoBack, canGoBack ? "Go back" : "Back unavailable");
        SetControlEnabled(_forward, canGoForward, canGoForward ? "Go forward" : "Forward unavailable");
        _focusGraph = BrowserShellFocusGraph.Create(canGoBack, canGoForward, _reload.Focusable);
    }

    internal void UpdateNavigationState(BrowserNavigationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _navigationState = state;
        var visual = BrowserNavigationVisualState.From(state);

        SetControlEnabled(_reload, visual.ReloadEnabled, visual.ShowsProgress ? "Reload unavailable while loading" : "Reload current page");
        SetHistoryAvailability(!visual.ShowsProgress && state.History.CanGoBack, !visual.ShowsProgress && state.History.CanGoForward);
        if (visual.ShowsProgress) _progressFill.Show(); else _progressFill.Hide();
        _progressFill.Size = new Size(visual.ShowsProgress ? 720.0f : 0.0f, BrowserShellMetrics.ProgressHeight);

        if (state.Page is not null && state.Phase == BrowserNavigationPhase.Page)
        {
            UpdatePage(state.Page);
        }
        else
        {
            _title.Text = visual.Title;
            _url.Text = state.PublicUrl ?? state.Error ?? InitialAddress;
            _state.Text = visual.Status;
        }

        if (_recoverySurface is not null)
        {
            if (visual.ShowsRecovery) _recoverySurface.Show(); else _recoverySurface.Hide();
        }

        if (_homeSurface is not null)
        {
            var showHome = state.Phase == BrowserNavigationPhase.Home &&
                           _workspace?.Surface == BrowserWorkspaceSurface.Page &&
                           _workspace.SelectedTab.Page is null;
            if (showHome) _homeSurface.Show(); else _homeSurface.Hide();
        }

        if (visual.ShowsProgress && IsRecoveryControl(FocusManager.Instance.GetCurrentFocusView()))
        {
            FocusAddress();
        }

        if (visual.ShowsRecovery)
        {
            if (_recoveryTitle is not null) _recoveryTitle.Text = _title.Text;
            if (_recoveryMessage is not null) _recoveryMessage.Text = state.Error ?? "The page could not be loaded.";
            if (_retry is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_retry);
            }
        }

        ApplyModalInputBoundary();
    }

    internal void UpdateWorkspace(BrowserTabWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var priorWorkspace = _workspace;
        _workspace = workspace;
        var workspaceVisual = BrowserWorkspaceVisualState.From(workspace);
        _tabs.AccessibilityName = $"Open tabs. {workspace.Tabs.Count} tabs.";

        var countLabel = workspace.Tabs.Count == 1 ? "▣  Tabs  1" : $"▣  Tabs  {workspace.Tabs.Count}";
        if (_tabs.GetChildAt(0) is TextLabel tabsLabel)
        {
            tabsLabel.Text = countLabel;
        }

        if (_tabsCount is not null)
        {
            _tabsCount.Text = $"{workspace.Tabs.Count} normal · maximum 20";
        }

        if (_tabsSurface is not null)
        {
            if (workspaceVisual.ShowsTabs) _tabsSurface.Show(); else _tabsSurface.Hide();
        }

        if (_homeSurface is not null)
        {
            if (workspaceVisual.ShowsHome) _homeSurface.Show(); else _homeSurface.Hide();
        }

        RenderTabRows(workspace);
        if (_closeModal is not null)
        {
            if (workspaceVisual.ShowsCloseConfirmation)
            {
                if (_closeModalTitle is not null)
                {
                    _closeModalTitle.Text = $"Close “{workspace.PendingCloseTitle}”?";
                }

                _closeModal.Show();
                if (_cancelClose is not null) FocusManager.Instance.SetCurrentFocusView(_cancelClose);
            }
            else
            {
                _closeModal.Hide();
                if (priorWorkspace?.Surface != workspace.Surface ||
                    priorWorkspace?.SelectedTabId != workspace.SelectedTabId ||
                    priorWorkspace?.PreferredFocus != workspace.PreferredFocus ||
                    priorWorkspace?.PreferredFocusTabId != workspace.PreferredFocusTabId)
                {
                    RestoreWorkspaceFocus(workspace);
                }
            }
        }

        ApplyModalInputBoundary();
    }

    internal void SetTabMutationBusy(bool busy)
    {
        if (!busy)
        {
            if (_workspace is not null)
            {
                UpdateWorkspace(_workspace);
            }

            return;
        }

        if (_newTab is not null) SetControlEnabled(_newTab, false, "Updating tabs");
        foreach (var row in _tabRows)
        {
            SetControlEnabled(row.Open, false, "Updating tabs");
            SetControlEnabled(row.Close, false, "Updating tabs");
        }

        if (_cancelClose is not null) SetControlEnabled(_cancelClose, false, "Updating tabs");
        if (_confirmClose is not null) SetControlEnabled(_confirmClose, false, "Updating tabs");
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
        if (_workspace?.Surface == BrowserWorkspaceSurface.CloseConfirmation && TryHandleModalKey(keyName))
        {
            return true;
        }

        if (_workspace?.Surface == BrowserWorkspaceSurface.Tabs && TryHandleTabsKey(keyName))
        {
            return true;
        }

        if (keyName is "Left" or "Right")
        {
            var delta = keyName == "Left" ? -1 : 1;
            if (IsHomeControl(FocusManager.Instance.GetCurrentFocusView()))
            {
                MoveHomeFocus(delta);
            }
            else if (IsRecoveryControl(FocusManager.Instance.GetCurrentFocusView()))
            {
                MoveRecoveryFocus(delta);
            }
            else
            {
                MoveCommandFocus(delta);
            }
            return true;
        }

        var currentFocus = FocusManager.Instance.GetCurrentFocusView();
        if (keyName == "Down" && (ReferenceEquals(currentFocus, _address) || ReferenceEquals(currentFocus, _reload)))
        {
            if (_homeSurface?.Visibility == true && _homeOpenGuide is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_homeOpenGuide);
            }
            else if (_recoverySurface?.Visibility == true && _retry is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_retry);
            }
            else
            {
                FocusTarget(_focusGraph.MoveDown(TargetFor(FocusManager.Instance.GetCurrentFocusView())));
            }
            return true;
        }

        if (keyName == "Down" && (ReferenceEquals(currentFocus, _webView) ||
                                   IsRecoveryControl(currentFocus) || IsHomeControl(currentFocus)))
        {
            FocusTarget(BrowserShellFocusTarget.Tabs);
            return true;
        }

        if (keyName == "Up" && (ReferenceEquals(currentFocus, _webView) ||
                                IsRecoveryControl(currentFocus) || IsHomeControl(currentFocus)))
        {
            FocusTarget(_focusGraph.MoveUp(BrowserShellFocusTarget.WebContent));
            return true;
        }

        if (keyName == "Up" && IsDockControl(currentFocus))
        {
            if (_homeSurface?.Visibility == true && _homeOpenGuide is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_homeOpenGuide);
            }
            else if (_recoverySurface?.Visibility == true && _retry is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_retry);
            }
            else
            {
                FocusTarget(_focusGraph.MoveUp(TargetFor(currentFocus)));
            }

            return true;
        }

        if (keyName == "Down" && IsDockControl(currentFocus))
        {
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

    private bool IsDockControl(View? view) =>
        ReferenceEquals(view, _back) || ReferenceEquals(view, _forward) || ReferenceEquals(view, _tabs);

    private bool IsHomeControl(View? view) =>
        ReferenceEquals(view, _homeOpenGuide) || ReferenceEquals(view, _homeEditAddress);

    private void MoveHomeFocus(int delta)
    {
        if (_homeOpenGuide is null || _homeEditAddress is null)
        {
            return;
        }

        var current = ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _homeEditAddress)
            ? BrowserHomeFocusTarget.EditAddress
            : BrowserHomeFocusTarget.OpenGuide;
        var target = BrowserHomeFocusGraph.Move(current, delta) == BrowserHomeFocusTarget.EditAddress
            ? _homeEditAddress
            : _homeOpenGuide;
        FocusManager.Instance.SetCurrentFocusView(target);
    }

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
        _address.BackgroundColor = focused ? Color.White : new Color("#E9E9EDFF");
        _address.BorderlineColor = new Color(focused ? "#0868D7FF" : "#E9E9EDFF");
        _address.BorderlineWidth = focused ? 4.0f : 2.0f;
    }

    private void ReloadAddress()
    {
        var address = string.IsNullOrWhiteSpace(_address.Text) ? InitialAddress : _address.Text;
        _navigate(address);
    }

    private void SubmitAddress()
    {
        var address = _address.Text?.Trim();
        _navigate(address ?? string.Empty);
    }

    private void BuildHomeSurface()
    {
        _homeSurface = new View
        {
            Name = "BrowserHomeSurface",
            Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.ContentTop),
            Size = new Size(BrowserShellMetrics.ContentWidth, BrowserShellMetrics.ContentHeight),
            BackgroundColor = Color.White,
            FocusableChildren = true,
        };
        _homeSurface.Add(Label("START PAGE", "#0877E8FF", 4.8f, new Position(360, 214), new Size(1120, 48), HorizontalAlignment.Center));
        _homeSurface.Add(Label("Browse the web.", "#1B1B1FFF", 15.0f, new Position(260, 260), new Size(1320, 116), HorizontalAlignment.Center));
        var homeCopy = Paragraph(
            "Search or enter an address above. Pages open in the system WebView. Use remote, keyboard, pointer, or touch.",
            "#45454FFF",
            6.6f,
            new Position(330, 390),
            new Size(1180, 124));
        homeCopy.HorizontalAlignment = HorizontalAlignment.Center;
        _homeSurface.Add(homeCopy);
        _homeOpenGuide = CreateControl("OpenGuide", "Open the public Tizen guide", "Open Tizen guide", new Position(576, 534), new Size(326, 76), () => _navigate(InitialAddress));
        _homeEditAddress = CreateControl("HomeEditAddress", "Focus address or search", "Enter an address", new Position(926, 534), new Size(326, 76), FocusAddress);
        _homeSurface.Add(_homeOpenGuide);
        _homeSurface.Add(_homeEditAddress);
        _homeSurface.Add(Label(
            "✓   Normal browsing only · public title and address metadata",
            "#256D3BFF",
            4.8f,
            new Position(420, 642),
            new Size(1000, 58),
            HorizontalAlignment.Center));
        Canvas.Add(_homeSurface);
    }

    private void BuildRecoverySurface()
    {
        _recoverySurface = new View
        {
            Name = "BrowserRecoverySurface",
            Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.ContentTop),
            Size = new Size(BrowserShellMetrics.ContentWidth, BrowserShellMetrics.ContentHeight),
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        _recoveryTitle = Label("Page unavailable", "#1B1B1FFF", 13.0f, new Position(164, 132), new Size(1488, 96), HorizontalAlignment.Begin);
        _recoveryMessage = Label("The page could not be loaded.", "#61616AFF", 7.0f, new Position(164, 238), new Size(1488, 76), HorizontalAlignment.Begin);
        _retry = CreateControl("Retry", "Retry navigation", "Retry", new Position(164, 366), new Size(240, 76), _retryAction);
        _recoveryBack = CreateControl("RecoveryBack", "Return to the previous page", "Back", new Position(426, 366), new Size(240, 76), _recoveryBackAction);
        _editAddress = CreateControl("EditAddress", "Edit address or search", "Edit address", new Position(688, 366), new Size(280, 76), FocusAddress);
        _recoverySurface.Add(_recoveryTitle);
        _recoverySurface.Add(_recoveryMessage);
        _recoverySurface.Add(_retry);
        _recoverySurface.Add(_recoveryBack);
        _recoverySurface.Add(_editAddress);
        Canvas.Add(_recoverySurface);
        _recoverySurface.Hide();
    }

    private void BuildTabsSurface()
    {
        _tabsSurface = new View
        {
            Name = "BrowserTabsSurface",
            Position = new Position(0, 0),
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.DesignHeight),
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        _tabsBack = CreateControl("TabsBack", "Return to browser", "‹", new Position(110, 36), new Size(66, 66), _closeTabsAction);
        _tabsBack.CornerRadius = 23.0f;
        _tabsSurface.Add(_tabsBack);
        _tabsSurface.Add(Label("Tabs", "#1B1B1FFF", 14.0f, new Position(210, 20), new Size(620, 104), HorizontalAlignment.Begin));
        _tabsCount = Label("1 normal · maximum 20", "#61616AFF", 5.2f, new Position(1410, 32), new Size(400, 80), HorizontalAlignment.Center);
        _tabsCount.BackgroundColor = new Color("#ECECF0FF");
        _tabsCount.CornerRadius = 30.0f;
        _tabsSurface.Add(_tabsCount);
        _tabListViewport = new View
        {
            Name = "BrowserTabListViewport",
            Position = new Position(300, 190),
            Size = new Size(1320, 680),
            ClippingMode = ClippingModeType.ClipChildren,
            FocusableChildren = true,
        };
        _tabsSurface.Add(_tabListViewport);
        _newTab = CreateControl("NewTab", "Create a new normal tab", "New tab", new Position(300, 900), new Size(220, 76), _createTabAction);
        _tabsSurface.Add(_newTab);
        Canvas.Add(_tabsSurface);
        _tabsSurface.Hide();
    }

    private void BuildCloseModal()
    {
        _closeModal = new View
        {
            Name = "BrowserCloseConfirmation",
            Position = new Position(0, 0),
            Size = new Size(BrowserShellMetrics.DesignWidth, BrowserShellMetrics.DesignHeight),
            BackgroundColor = new Color("#00000099"),
            FocusableChildren = true,
        };
        _closeModal.TouchEvent += (_, _) => true;
        var card = new View
        {
            Name = "BrowserCloseConfirmationCard",
            Position = new Position(580, 398),
            Size = new Size(760, 284),
            BackgroundColor = Color.White,
            CornerRadius = 34.0f,
            FocusableChildren = true,
        };
        _closeModalTitle = Label("Close tab?", "#1B1B1FFF", 10.0f, new Position(48, 24), new Size(664, 78), HorizontalAlignment.Begin);
        card.Add(_closeModalTitle);
        card.Add(Paragraph("This tab will be removed.", "#61616AFF", 5.4f, new Position(48, 108), new Size(664, 64)));
        card.Add(new View
        {
            Position = new Position(0, 180),
            Size = new Size(760, 2),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        card.Add(new View
        {
            Position = new Position(379, 182),
            Size = new Size(2, 102),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        _cancelClose = CreateControl("CancelClose", "Cancel closing tab", "Cancel", new Position(0, 182), new Size(380, 102), _cancelCloseAction);
        _cancelClose.CornerRadius = 0.0f;
        _confirmClose = CreateControl("ConfirmClose", "Close this tab", "Close", new Position(381, 182), new Size(379, 102), _confirmCloseAction);
        _confirmClose.CornerRadius = 0.0f;
        SetControlEnabled(_cancelClose, true, "Cancel closing tab");
        SetControlEnabled(_confirmClose, true, "Close this tab");
        if (_confirmClose.GetChildAt(0) is TextLabel confirmLabel)
        {
            confirmLabel.TextColor = new Color("#D92D2DFF");
        }
        card.Add(_cancelClose);
        card.Add(_confirmClose);
        _closeModal.Add(card);
        Canvas.Add(_closeModal);
        _closeModal.Hide();
    }

    private void BringOverlaysToFront()
    {
        foreach (var overlay in new[] { _homeSurface, _recoverySurface, _bottomDock, _tabsSurface, _closeModal })
        {
            if (overlay is null)
            {
                continue;
            }

            Canvas.Remove(overlay);
            Canvas.Add(overlay);
        }
    }

    private void RenderTabRows(BrowserTabWorkspace workspace)
    {
        if (_tabListViewport is null || _newTab is null)
        {
            return;
        }

        foreach (var row in _tabRows)
        {
            _activations.Remove(row.Open);
            _activations.Remove(row.Close);
        }

        foreach (var view in _dynamicTabViews)
        {
            _tabListViewport.Remove(view);
            view.Dispose();
        }

        _dynamicTabViews.Clear();
        _tabRows.Clear();
        for (var index = 0; index < workspace.Tabs.Count; index++)
        {
            var tab = workspace.Tabs[index];
            var selected = tab.Id == workspace.SelectedTabId;
            var row = new View
            {
                Name = $"TabRow-{tab.Id}",
                Position = new Position(0, index * 156),
                Size = new Size(1320, 142),
                BackgroundColor = new Color(selected ? "#EDF5FFFF" : "#FFFFFFFF"),
                CornerRadius = 26.0f,
                BorderlineWidth = 2.0f,
                BorderlineColor = new Color(selected ? "#B7D7FBFF" : "#DEDEE4FF"),
                FocusableChildren = true,
            };
            var title = BrowserTabVisualText.Title(tab);
            var publicUrl = tab.Page?.Url ?? "Start page";
            var rail = new View
            {
                Position = new Position(0, 0),
                Size = new Size(selected ? 9 : 0, 142),
                BackgroundColor = new Color("#0877E8FF"),
                CornerRadius = 5.0f,
            };
            var preview = new View
            {
                Position = new Position(24, 17),
                Size = new Size(118, 108),
                BackgroundColor = new Color(selected ? "#FFFFFFFF" : "#E7EDF6FF"),
                CornerRadius = 18.0f,
            };
            preview.Add(Label(title[..1].ToUpperInvariant(), "#0877E8FF", 8.0f, new Position(0, 0), new Size(118, 108), HorizontalAlignment.Center));
            var open = CreateControl(
                $"Open-{tab.Id}",
                $"Open {title}{(selected ? ", current tab" : string.Empty)}",
                string.Empty,
                new Position(160, 0),
                new Size(1060, 142),
                () => _selectTabAction(tab.Id));
            open.BackgroundColor = new Color(selected ? "#EDF5FFFF" : "#FFFFFFFF");
            open.CornerRadius = 22.0f;
            open.Add(Label(title, "#1B1B1FFF", 7.0f, new Position(26, 20), new Size(990, 52), HorizontalAlignment.Begin));
            open.Add(Label(publicUrl, "#61616AFF", 5.2f, new Position(26, 72), new Size(990, 46), HorizontalAlignment.Begin));
            var close = CreateControl(
                $"Close-{tab.Id}",
                $"Close {title}",
                "×",
                new Position(1236, 38),
                new Size(66, 66),
                () => _requestCloseAction(tab.Id));
            close.CornerRadius = 33.0f;
            SetControlEnabled(close, workspace.Tabs.Count > 1, workspace.Tabs.Count > 1 ? $"Close {title}" : "Last tab cannot be closed");
            var capturedIndex = index;
            open.FocusGained += (_, _) => EnsureTabVisible(capturedIndex);
            close.FocusGained += (_, _) => EnsureTabVisible(capturedIndex);
            row.Add(rail);
            row.Add(preview);
            row.Add(open);
            row.Add(close);
            _tabListViewport.Add(row);
            _dynamicTabViews.Add(row);
            _tabRows.Add((row, open, close, index, tab.Id));
        }

        SetControlEnabled(
            _newTab,
            workspace.CanCreateTab,
            workspace.CanCreateTab ? "Create a new normal tab" : "Tab limit reached. Maximum 20 tabs.");
        var preferredIndex = workspace.Tabs.ToList().FindIndex(tab => tab.Id == (workspace.PreferredFocusTabId ?? workspace.SelectedTabId));
        EnsureTabVisible(Math.Max(0, preferredIndex));
    }

    private void EnsureTabVisible(int index)
    {
        if (_tabRows.Count == 0)
        {
            return;
        }

        const int visibleRows = 4;
        var firstVisible = Math.Clamp(index - 2, 0, Math.Max(0, _tabRows.Count - visibleRows));
        foreach (var row in _tabRows)
        {
            row.Row.Position = new Position(0, (row.Index - firstVisible) * 156);
        }
    }

    private void RestoreWorkspaceFocus(BrowserTabWorkspace workspace)
    {
        if (workspace.Surface != BrowserWorkspaceSurface.Tabs)
        {
            if (workspace.PreferredFocus == BrowserWorkspaceFocus.Address)
            {
                FocusAddress();
            }

            return;
        }

        var row = _tabRows.FirstOrDefault(item => item.TabId == (workspace.PreferredFocusTabId ?? workspace.SelectedTabId));
        var target = workspace.PreferredFocus == BrowserWorkspaceFocus.InvokingClose && row.Close?.Focusable == true
            ? row.Close
            : row.Open;
        if (target is not null)
        {
            EnsureTabVisible(row.Index);
            FocusManager.Instance.SetCurrentFocusView(target);
        }
    }

    private void ApplyModalInputBoundary()
    {
        var modal = _workspace?.Surface == BrowserWorkspaceSurface.CloseConfirmation;
        _address.Focusable = !modal;
        _address.EnableEditing = !modal;
        if (modal)
        {
            _back.Focusable = false;
            _forward.Focusable = false;
            _reload.Focusable = false;
            _tabs.Focusable = false;
            if (_tabsBack is not null) _tabsBack.Focusable = false;
            return;
        }

        var visual = BrowserNavigationVisualState.From(_navigationState);
        SetControlEnabled(_reload, visual.ReloadEnabled, visual.ShowsProgress ? "Reload unavailable while loading" : "Reload current page");
        SetHistoryAvailability(!visual.ShowsProgress && _navigationState.History.CanGoBack, !visual.ShowsProgress && _navigationState.History.CanGoForward);
        SetControlEnabled(_tabs, true, _workspace is null ? "Open tabs" : $"Open tabs. {_workspace.Tabs.Count} tabs.");
        if (_tabsBack is not null) SetControlEnabled(_tabsBack, true, "Return to browser");
    }

    private bool IsRecoveryControl(View? view) =>
        ReferenceEquals(view, _retry) || ReferenceEquals(view, _recoveryBack) || ReferenceEquals(view, _editAddress);

    private void MoveRecoveryFocus(int delta)
    {
        var current = FocusManager.Instance.GetCurrentFocusView();
        var currentTarget = ReferenceEquals(current, _recoveryBack)
            ? BrowserRecoveryFocusTarget.Back
            : ReferenceEquals(current, _editAddress)
                ? BrowserRecoveryFocusTarget.EditAddress
                : BrowserRecoveryFocusTarget.Retry;
        var target = BrowserRecoveryFocusGraph.Move(currentTarget, delta) switch
        {
            BrowserRecoveryFocusTarget.Back => _recoveryBack,
            BrowserRecoveryFocusTarget.EditAddress => _editAddress,
            _ => _retry,
        };
        if (target is not null)
        {
            FocusManager.Instance.SetCurrentFocusView(target);
        }
    }

    private bool TryHandleModalKey(string keyName)
    {
        if (_cancelClose is null || _confirmClose is null)
        {
            return false;
        }

        if (keyName is "Left" or "Right")
        {
            var target = keyName == "Left" ? _cancelClose : _confirmClose;
            FocusManager.Instance.SetCurrentFocusView(target);
            return true;
        }

        if (keyName is "Up" or "Down")
        {
            return true;
        }

        if (keyName is "Return" or "Enter" or "XF86Select")
        {
            if (_activations.TryGetValue(FocusManager.Instance.GetCurrentFocusView(), out var activate))
            {
                activate();
            }

            return true;
        }

        return false;
    }

    private bool TryHandleTabsKey(string keyName)
    {
        var current = FocusManager.Instance.GetCurrentFocusView();
        var rowIndex = _tabRows.FindIndex(row => ReferenceEquals(row.Open, current) || ReferenceEquals(row.Close, current));
        if (keyName is "Left" or "Right")
        {
            if (rowIndex < 0)
            {
                return ReferenceEquals(current, _newTab) || ReferenceEquals(current, _tabsBack);
            }

            var row = _tabRows[rowIndex];
            var target = keyName == "Right" && ReferenceEquals(current, row.Open) && row.Close.Focusable
                ? row.Close
                : row.Open;
            FocusManager.Instance.SetCurrentFocusView(target);
            return true;
        }

        if (keyName is "Up" or "Down")
        {
            if (_tabRows.Count == 0)
            {
                return true;
            }

            if (ReferenceEquals(current, _tabsBack))
            {
                if (keyName == "Down")
                {
                    RestoreWorkspaceFocus(_workspace!);
                }

                return true;
            }

            if (ReferenceEquals(current, _newTab))
            {
                if (keyName == "Up")
                {
                    var last = _tabRows[^1];
                    EnsureTabVisible(last.Index);
                    FocusManager.Instance.SetCurrentFocusView(last.Open);
                }

                return true;
            }

            if (rowIndex < 0)
            {
                RestoreWorkspaceFocus(_workspace!);
                return true;
            }

            var nextIndex = rowIndex + (keyName == "Up" ? -1 : 1);
            if (nextIndex < 0)
            {
                if (_tabsBack?.Focusable == true)
                {
                    FocusManager.Instance.SetCurrentFocusView(_tabsBack);
                }

                return true;
            }
            else if (nextIndex >= _tabRows.Count)
            {
                if (_newTab?.Focusable == true)
                {
                    FocusManager.Instance.SetCurrentFocusView(_newTab);
                }

                return true;
            }

            var next = _tabRows[nextIndex];
            var target = ReferenceEquals(current, _tabRows[rowIndex].Close) && next.Close.Focusable ? next.Close : next.Open;
            EnsureTabVisible(next.Index);
            FocusManager.Instance.SetCurrentFocusView(target);
            return true;
        }

        return false;
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
        if (IsDockControlName(control.Name))
        {
            control.BackgroundColor = new Color(enabled ? "#00000000" : "#F4F4F6FF");
            control.BorderlineColor = Color.Transparent;
            control.BorderlineWidth = 0.0f;
        }
        else if (IsModalActionName(control.Name))
        {
            control.BackgroundColor = Color.White;
            control.BorderlineColor = Color.Transparent;
            control.BorderlineWidth = 0.0f;
        }
        else
        {
            control.BackgroundColor = new Color(enabled ? "#FFFFFFFF" : "#F4F4F6FF");
            control.BorderlineColor = new Color(enabled ? "#DEDEE5FF" : "#E8E8ECFF");
            control.BorderlineWidth = 2.0f;
        }

        control.Opacity = enabled ? 1.0f : 0.55f;
    }

    private static void ApplyControlFocusStyle(View control, bool focused)
    {
        if (IsDockControlName(control.Name))
        {
            control.BackgroundColor = new Color(focused ? "#EDF5FFFF" : "#00000000");
            control.BorderlineColor = new Color(focused ? "#0868D7FF" : "#00000000");
            control.BorderlineWidth = focused ? 4.0f : 0.0f;
        }
        else if (IsModalActionName(control.Name))
        {
            control.BackgroundColor = Color.White;
            control.BorderlineColor = new Color(focused ? "#0868D7FF" : "#00000000");
            control.BorderlineWidth = focused ? 5.0f : 0.0f;
        }
        else
        {
            control.BorderlineColor = new Color(focused ? "#0868D7FF" : "#DEDEE5FF");
            control.BorderlineWidth = focused ? 4.0f : 2.0f;
        }

        var scaleFocused = focused && !IsModalActionName(control.Name);
        control.Scale = scaleFocused ? new Vector3(1.025f, 1.025f, 1.0f) : new Vector3(1.0f, 1.0f, 1.0f);
    }

    private static bool IsDockControlName(string? name) => name is "Back" or "Forward" or "Tabs";

    private static bool IsModalActionName(string? name) => name is "CancelClose" or "ConfirmClose";

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

    private static TextLabel Paragraph(string text, string color, float pointSize, Position position, Size size) => new(text)
    {
        Position = position,
        Size = size,
        TextColor = new Color(color),
        PointSize = pointSize,
        HorizontalAlignment = HorizontalAlignment.Begin,
        VerticalAlignment = VerticalAlignment.Top,
        Ellipsis = true,
        MultiLine = true,
    };
}
