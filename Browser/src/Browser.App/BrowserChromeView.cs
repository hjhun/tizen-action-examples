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
    private readonly Action _homeAction;
    private readonly Action _createHomeTabAction;
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
    private readonly View _addressShell;
    private readonly TextField _address;
    private readonly TextLabel _title;
    private readonly TextLabel _url;
    private readonly TextLabel _state;
    private readonly View _reload;
    private readonly View _home;
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
    private View? _homeTizenDocs;
    private View? _homeTizenOrg;
    private View? _homeNewTab;
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
        Action? goHome = null,
        Action? openTabs = null,
        Action? closeTabs = null,
        Action? reload = null,
        Action? retry = null,
        Action? recoveryBack = null,
        Action? createTab = null,
        Action? createHomeTab = null,
        Action<string>? selectTab = null,
        Action<string>? requestClose = null,
        Action? confirmClose = null,
        Action? cancelClose = null)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        _reloadAction = reload ?? ReloadAddress;
        _homeAction = goHome ?? (() => { });
        _createHomeTabAction = createHomeTab ?? (() => { });
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
            Position = new Position(40, 24),
            Size = new Size(36, 36),
            BackgroundColor = Color.Transparent,
            CornerRadius = 12.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#1B1B1FFF"),
        };
        productMark.Add(new View
        {
            Position = new Position(8, 10),
            Size = new Size(20, 2),
            BackgroundColor = new Color("#0B76E8FF"),
            CornerRadius = 1.0f,
        });
        productMark.Add(new View
        {
            Position = new Position(8, 19),
            Size = new Size(20, 2),
            BackgroundColor = new Color("#0B76E8FF"),
            CornerRadius = 1.0f,
        });
        header.Add(productMark);
        header.Add(Label("Internet", "#1B1B1FFF", BrowserTypographyMetrics.ProductPointSize, new Position(88, 0), new Size(150, BrowserShellMetrics.HeaderHeight), HorizontalAlignment.Begin));

        _addressShell = new View
        {
            Name = "BrowserAddressShell",
            Position = new Position(BrowserAddressMetrics.ShellLeft, BrowserAddressMetrics.ShellTop),
            Size = new Size(BrowserAddressMetrics.ShellWidth, BrowserAddressMetrics.ShellHeight),
            BackgroundColor = new Color("#ECECF1FF"),
            CornerRadius = 22.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#ECECF1FF"),
            FocusableChildren = true,
        };
        _address = new TextField
        {
            Name = "BrowserAddress",
            Text = string.Empty,
            PlaceholderText = "Search or enter address",
            PlaceholderTextColor = new Color("#66666FFF"),
            PointSize = BrowserTypographyMetrics.AddressPointSize,
            EnableEditing = true,
            Focusable = true,
            Position = new Position(BrowserAddressMetrics.TextInsetX, BrowserAddressMetrics.TextTopOffset),
            Size = new Size(BrowserAddressMetrics.TextWidth, BrowserAddressMetrics.TextHeight),
            BackgroundColor = Color.Transparent,
            CornerRadius = 0.0f,
            BorderlineWidth = 0.0f,
            BorderlineColor = Color.Transparent,
            AccessibilityName = "Address or search. Press Enter to load.",
        };
        _address.FocusGained += (_, _) => ApplyAddressFocusStyle(true);
        _address.FocusLost += (_, _) => ApplyAddressFocusStyle(false);
        _addressShell.TouchEvent += (_, eventArgs) =>
        {
            var state = eventArgs.Touch.GetState(0);
            var pressStarted = state is PointStateType.Down or PointStateType.Started;
            var modal = _workspace?.Surface == BrowserWorkspaceSurface.CloseConfirmation;
            if (BrowserAddressInteractionPolicy.ShouldRequestEditing(pressStarted, modal))
            {
                FocusManager.Instance.SetCurrentFocusView(_address);
            }

            return pressStarted;
        };
        _addressShell.Add(_address);
        header.Add(_addressShell);

        _reload = CreateControl("Reload", "Reload current page", "↻", new Position(1822, 13), new Size(58, 58), _reloadAction);
        _reload.CornerRadius = 19.0f;
        header.Add(_reload);

        _bottomDock = new View
        {
            Name = "BrowserNavigationDock",
            Position = new Position(BrowserShellMetrics.DockLeft, BrowserShellMetrics.DockTop),
            Size = new Size(BrowserShellMetrics.DockWidth, BrowserShellMetrics.DockHeight),
            BackgroundColor = Color.White,
            CornerRadius = 24.0f,
            BorderlineWidth = 1.0f,
            BorderlineColor = new Color("#D7D7DCFF"),
            FocusableChildren = true,
        };
        _back = CreateDisabledControl("Back", "Back unavailable", "←", new Position(6, 6), new Size(104, 52), goBack);
        _forward = CreateDisabledControl("Forward", "Forward unavailable", "→", new Position(114, 6), new Size(104, 52), goForward);
        _home = CreateControl("Home", "Open local start page", "Home", new Position(222, 6), new Size(104, 52), OpenHome);
        _tabs = CreateControl("Tabs", "Open tabs. 1 tab.", "Tabs 1", new Position(330, 6), new Size(104, 52), openTabs ?? (() => { }));
        _bottomDock.Add(_back);
        _bottomDock.Add(_forward);
        _bottomDock.Add(_home);
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
        FocusManager.Instance.SetCurrentFocusView(_homeTizenDocs ?? _home);
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
        var homeControlWasFocused = IsHomeControl(FocusManager.Instance.GetCurrentFocusView());
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
                           _workspace?.Surface == BrowserWorkspaceSurface.Page;
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

        if (BrowserHiddenHomeFocusPolicy.ShouldFocusWebView(homeControlWasFocused, state.Phase))
        {
            FocusWebContent();
        }

        ApplyModalInputBoundary();
    }

    internal void UpdateWorkspace(BrowserTabWorkspace workspace, bool restoreFocus = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var priorWorkspace = _workspace;
        _workspace = workspace;
        var workspaceVisual = BrowserWorkspaceVisualState.From(workspace);
        _tabs.AccessibilityName = $"Open tabs. {workspace.Tabs.Count} tabs.";

        var countLabel = workspace.Tabs.Count == 1 ? "Tabs 1" : $"Tabs {workspace.Tabs.Count}";
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
                if (restoreFocus &&
                    (priorWorkspace?.Surface != workspace.Surface ||
                     priorWorkspace?.SelectedTabId != workspace.SelectedTabId ||
                     priorWorkspace?.PreferredFocus != workspace.PreferredFocus ||
                     priorWorkspace?.PreferredFocusTabId != workspace.PreferredFocusTabId))
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
            if (_homeSurface?.Visibility == true && _homeTizenDocs is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_homeTizenDocs);
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
            FocusTarget(BrowserShellFocusTarget.Home);
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
            if (_homeSurface?.Visibility == true && _homeTizenDocs is not null)
            {
                FocusManager.Instance.SetCurrentFocusView(_homeTizenDocs);
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

    internal void FocusWebContent()
    {
        if (_webView is { } webView &&
            _workspace?.Surface == BrowserWorkspaceSurface.Page &&
            _navigationState.Phase == BrowserNavigationPhase.Page)
        {
            FocusManager.Instance.SetCurrentFocusView(webView);
        }
    }

    internal void FocusHomeEntry()
    {
        var showsHomeSurface = _navigationState.Phase == BrowserNavigationPhase.Home &&
                               _workspace?.Surface == BrowserWorkspaceSurface.Page;
        var target = BrowserInitialFocusPolicy.Resolve(showsHomeSurface) == BrowserInitialFocusTarget.HomeQuickAccess
            ? _homeTizenDocs ?? _home
            : _home;
        FocusManager.Instance.SetCurrentFocusView(target);
    }

    private void MoveCommandFocus(int delta)
    {
        var current = FocusManager.Instance.GetCurrentFocusView();
        FocusTarget(_focusGraph.MoveHorizontal(TargetFor(current), delta));
    }

    private bool IsCommandControl(View? view) =>
        ReferenceEquals(view, _back) || ReferenceEquals(view, _forward) || ReferenceEquals(view, _home) ||
        ReferenceEquals(view, _reload) || ReferenceEquals(view, _address) || ReferenceEquals(view, _tabs);

    private bool IsDockControl(View? view) =>
        ReferenceEquals(view, _back) || ReferenceEquals(view, _forward) || ReferenceEquals(view, _home) || ReferenceEquals(view, _tabs);

    private bool IsHomeControl(View? view) =>
        ReferenceEquals(view, _homeTizenDocs) || ReferenceEquals(view, _homeTizenOrg) || ReferenceEquals(view, _homeNewTab);

    private void MoveHomeFocus(int delta)
    {
        if (_homeTizenDocs is null || _homeTizenOrg is null || _homeNewTab is null)
        {
            return;
        }

        var current = ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _homeTizenOrg)
            ? BrowserHomeFocusTarget.TizenOrg
            : ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _homeNewTab)
                ? BrowserHomeFocusTarget.NewTab
                : BrowserHomeFocusTarget.TizenDocs;
        var target = BrowserHomeFocusGraph.Move(current, delta) switch
        {
            BrowserHomeFocusTarget.TizenOrg => _homeTizenOrg,
            BrowserHomeFocusTarget.NewTab => _homeNewTab,
            _ => _homeTizenDocs,
        };
        FocusManager.Instance.SetCurrentFocusView(target);
    }

    private BrowserShellFocusTarget TargetFor(View? view)
    {
        if (ReferenceEquals(view, _back)) return BrowserShellFocusTarget.Back;
        if (ReferenceEquals(view, _forward)) return BrowserShellFocusTarget.Forward;
        if (ReferenceEquals(view, _home)) return BrowserShellFocusTarget.Home;
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
            BrowserShellFocusTarget.Home => _home,
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
        _addressShell.BackgroundColor = focused ? Color.White : new Color("#ECECF1FF");
        _addressShell.BorderlineColor = new Color(focused ? "#0B76E8FF" : "#ECECF1FF");
        _addressShell.BorderlineWidth = focused ? BrowserAddressMetrics.FocusOutlineWidth : 2.0f;
    }

    private void OpenHome()
    {
        _address.Text = string.Empty;
        _homeAction();
        FocusHomeEntry();
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
        _homeSurface.Add(Label("QUICK ACCESS", "#0B76E8FF", 3.0f, new Position(360, 300), new Size(1120, 42), HorizontalAlignment.Center));
        _homeSurface.Add(Label("Where would you like to go?", "#17171BFF", BrowserTypographyMetrics.HomeTitlePointSize, new Position(260, 338), new Size(1320, 84), HorizontalAlignment.Center));
        var homeCopy = Paragraph(
            "Search with the address bar or choose a private local shortcut.",
            "#66666FFF",
            BrowserTypographyMetrics.BodyPointSize,
            new Position(330, 425),
            new Size(1180, 62));
        homeCopy.HorizontalAlignment = HorizontalAlignment.Center;
        _homeSurface.Add(homeCopy);
        _homeTizenDocs = CreateQuickAccessCard(
            "HomeTizenDocs", "Open Tizen Docs", "Tizen Docs", "docs.tizen.org", new Position(532, 505),
            () => _navigate("https://docs.tizen.org/"));
        _homeTizenOrg = CreateQuickAccessCard(
            "HomeTizenOrg", "Open Tizen.org", "Tizen.org", "www.tizen.org", new Position(796, 505),
            () => _navigate(InitialAddress));
        _homeNewTab = CreateQuickAccessCard(
            "HomeNewTab", "Open a new tab", "New tab", "Private local start page", new Position(1060, 505),
            _createHomeTabAction);
        _homeSurface.Add(_homeTizenDocs);
        _homeSurface.Add(_homeTizenOrg);
        _homeSurface.Add(_homeNewTab);
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
        _recoveryTitle = Label("Page unavailable", "#17171BFF", BrowserTypographyMetrics.HomeTitlePointSize, new Position(164, 170), new Size(1488, 78), HorizontalAlignment.Begin);
        _recoveryMessage = Label("The page could not be loaded.", "#66666FFF", BrowserTypographyMetrics.BodyPointSize, new Position(164, 252), new Size(1488, 62), HorizontalAlignment.Begin);
        _retry = CreateControl("Retry", "Retry navigation", "Retry", new Position(164, 350), new Size(210, 64), _retryAction);
        _recoveryBack = CreateControl("RecoveryBack", "Return to the previous page", "Back", new Position(394, 350), new Size(210, 64), _recoveryBackAction);
        _editAddress = CreateControl("EditAddress", "Edit address or search", "Edit address", new Position(624, 350), new Size(250, 64), FocusAddress);
        foreach (var control in new[] { _retry, _recoveryBack, _editAddress })
        {
            if (control.GetChildAt(0) is TextLabel label) label.PointSize = BrowserTypographyMetrics.ActionPointSize;
        }
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
        _tabsBack = CreateControl("TabsBack", "Return to browser", "‹", new Position(110, 52), new Size(58, 58), _closeTabsAction);
        _tabsBack.CornerRadius = 19.0f;
        _tabsSurface.Add(_tabsBack);
        _tabsSurface.Add(Label("Tabs", "#17171BFF", BrowserTypographyMetrics.TabsTitlePointSize, new Position(188, 38), new Size(620, 76), HorizontalAlignment.Begin));
        _tabsCount = Label("1 normal · maximum 20", "#66666FFF", BrowserTypographyMetrics.TabMetaPointSize, new Position(1556, 59), new Size(254, 44), HorizontalAlignment.Center);
        _tabsCount.BackgroundColor = new Color("#ECECF0FF");
        _tabsCount.CornerRadius = 22.0f;
        _tabsSurface.Add(_tabsCount);
        _tabListViewport = new View
        {
            Name = "BrowserTabGridViewport",
            Position = new Position(BrowserTabsMetrics.GridLeft, BrowserTabsMetrics.GridTop),
            Size = new Size(1500, 730),
            ClippingMode = ClippingModeType.ClipChildren,
            FocusableChildren = true,
        };
        _tabsSurface.Add(_tabListViewport);
        _newTab = CreateControl("NewTab", "Create a new normal tab", "New tab", new Position(1576, 900), new Size(134, 60), _createTabAction);
        if (_newTab.GetChildAt(0) is TextLabel newTabLabel) newTabLabel.PointSize = BrowserTypographyMetrics.ActionPointSize;
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
            Position = new Position(620, 418),
            Size = new Size(680, 244),
            BackgroundColor = Color.White,
            CornerRadius = 28.0f,
            FocusableChildren = true,
        };
        _closeModalTitle = Label("Close this tab?", "#17171BFF", BrowserTypographyMetrics.DialogTitlePointSize, new Position(42, 20), new Size(596, 64), HorizontalAlignment.Begin);
        card.Add(_closeModalTitle);
        card.Add(Paragraph("This tab will be removed.", "#66666FFF", BrowserTypographyMetrics.ActionPointSize, new Position(42, 88), new Size(596, 50)));
        card.Add(new View
        {
            Position = new Position(0, 156),
            Size = new Size(680, 1),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        card.Add(new View
        {
            Position = new Position(339, 157),
            Size = new Size(1, 87),
            BackgroundColor = new Color("#DEDEE5FF"),
        });
        _cancelClose = CreateControl("CancelClose", "Cancel closing tab", "Cancel", new Position(0, 157), new Size(339, 87), _cancelCloseAction);
        _cancelClose.CornerRadius = 0.0f;
        _confirmClose = CreateControl("ConfirmClose", "Close this tab", "Close", new Position(340, 157), new Size(340, 87), _confirmCloseAction);
        _confirmClose.CornerRadius = 0.0f;
        if (_cancelClose.GetChildAt(0) is TextLabel cancelLabel) cancelLabel.PointSize = BrowserTypographyMetrics.ActionPointSize;
        if (_confirmClose.GetChildAt(0) is TextLabel closeLabel) closeLabel.PointSize = BrowserTypographyMetrics.ActionPointSize;
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
            var column = index % BrowserTabsMetrics.ColumnCount;
            var gridRow = index / BrowserTabsMetrics.ColumnCount;
            var row = new View
            {
                Name = $"TabCard-{tab.Id}",
                Position = new Position(
                    column * (BrowserTabsMetrics.CardWidth + BrowserTabsMetrics.ColumnGap),
                    gridRow * (BrowserTabsMetrics.CardHeight + BrowserTabsMetrics.RowGap)),
                Size = new Size(BrowserTabsMetrics.CardWidth, BrowserTabsMetrics.CardHeight),
                BackgroundColor = new Color(selected ? "#EAF4FFFF" : "#FFFFFFFF"),
                CornerRadius = 24.0f,
                BorderlineWidth = 1.0f,
                BorderlineColor = new Color(selected ? "#B7D7FBFF" : "#DEDEE5FF"),
                FocusableChildren = true,
            };
            var title = BrowserTabVisualText.Title(tab);
            var publicUrl = tab.Page?.Url ?? "Start page";
            var rail = new View
            {
                Position = new Position(0, 0),
                Size = new Size(selected ? 7 : 0, BrowserTabsMetrics.CardHeight),
                BackgroundColor = new Color("#0B76E8FF"),
                CornerRadius = 4.0f,
            };
            var preview = new View
            {
                Position = new Position(20, 16),
                Size = new Size(168, 182),
                BackgroundColor = new Color("#F3F4F8FF"),
                CornerRadius = 18.0f,
                BorderlineWidth = 1.0f,
                BorderlineColor = new Color("#E6E7ECFF"),
            };
            var previewChrome = new View
            {
                Position = new Position(0, 0),
                Size = new Size(168, 28),
                BackgroundColor = new Color("#E7E9EEFF"),
                CornerRadius = 18.0f,
            };
            preview.Add(previewChrome);
            preview.Add(new View
            {
                Position = new Position(16, 48),
                Size = new Size(92, 10),
                BackgroundColor = new Color("#79B9FFFF"),
                CornerRadius = 5.0f,
            });
            for (var line = 0; line < 3; line++)
            {
                preview.Add(new View
                {
                    Position = new Position(16, 76 + (line * 18)),
                    Size = new Size(line == 2 ? 104 : 128, 7),
                    BackgroundColor = new Color("#D5D9E2FF"),
                    CornerRadius = 4.0f,
                });
            }
            var open = CreateControl(
                $"Open-{tab.Id}",
                $"Open {title}{(selected ? ", current tab" : string.Empty)}",
                string.Empty,
                new Position(204, 0),
                new Size(456, BrowserTabsMetrics.CardHeight),
                () => _selectTabAction(tab.Id));
            open.BackgroundColor = Color.Transparent;
            open.CornerRadius = 18.0f;
            open.Add(Label(title, "#17171BFF", BrowserTypographyMetrics.TabTitlePointSize, new Position(18, 48), new Size(420, 52), HorizontalAlignment.Begin));
            open.Add(Label(publicUrl, "#66666FFF", BrowserTypographyMetrics.TabMetaPointSize, new Position(18, 106), new Size(420, 42), HorizontalAlignment.Begin));
            var close = CreateControl(
                $"Close-{tab.Id}",
                $"Close {title}",
                "×",
                new Position(664, 81),
                new Size(52, 52),
                () => _requestCloseAction(tab.Id));
            close.CornerRadius = 26.0f;
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

        const int visibleGridRows = 3;
        var totalGridRows = (int)Math.Ceiling(_tabRows.Count / (double)BrowserTabsMetrics.ColumnCount);
        var focusedGridRow = index / BrowserTabsMetrics.ColumnCount;
        var firstVisibleGridRow = Math.Clamp(focusedGridRow - 1, 0, Math.Max(0, totalGridRows - visibleGridRows));
        foreach (var row in _tabRows)
        {
            var column = row.Index % BrowserTabsMetrics.ColumnCount;
            var gridRow = row.Index / BrowserTabsMetrics.ColumnCount;
            row.Row.Position = new Position(
                column * (BrowserTabsMetrics.CardWidth + BrowserTabsMetrics.ColumnGap),
                (gridRow - firstVisibleGridRow) * (BrowserTabsMetrics.CardHeight + BrowserTabsMetrics.RowGap));
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
            else if (workspace.PreferredFocus == BrowserWorkspaceFocus.HomeQuickAccess)
            {
                FocusHomeEntry();
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
            _home.Focusable = false;
            _reload.Focusable = false;
            _tabs.Focusable = false;
            if (_tabsBack is not null) _tabsBack.Focusable = false;
            return;
        }

        var visual = BrowserNavigationVisualState.From(_navigationState);
        SetControlEnabled(_reload, visual.ReloadEnabled, visual.ShowsProgress ? "Reload unavailable while loading" : "Reload current page");
        SetHistoryAvailability(!visual.ShowsProgress && _navigationState.History.CanGoBack, !visual.ShowsProgress && _navigationState.History.CanGoForward);
        SetControlEnabled(_home, true, "Open local start page");
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
            View target;
            if (keyName == "Right")
            {
                if (ReferenceEquals(current, row.Open) && row.Close.Focusable)
                {
                    target = row.Close;
                }
                else if (rowIndex % BrowserTabsMetrics.ColumnCount == 0 && rowIndex + 1 < _tabRows.Count)
                {
                    target = _tabRows[rowIndex + 1].Open;
                }
                else
                {
                    target = current!;
                }
            }
            else if (ReferenceEquals(current, row.Close))
            {
                target = row.Open;
            }
            else if (rowIndex % BrowserTabsMetrics.ColumnCount == 1)
            {
                target = _tabRows[rowIndex - 1].Open;
            }
            else
            {
                target = current!;
            }

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

            var nextIndex = rowIndex + (keyName == "Up" ? -BrowserTabsMetrics.ColumnCount : BrowserTabsMetrics.ColumnCount);
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

    private View CreateQuickAccessCard(
        string name,
        string accessibilityName,
        string title,
        string metadata,
        Position position,
        Action activate)
    {
        var card = CreateControl(name, accessibilityName, string.Empty, position, new Size(248, 108), activate);
        card.BackgroundColor = new Color("#F0F1F5FF");
        card.BorderlineWidth = 1.0f;
        card.BorderlineColor = new Color("#DEDEE5FF");
        card.CornerRadius = 22.0f;
        card.Add(Label(title, "#17171BFF", BrowserTypographyMetrics.ProductPointSize, new Position(16, 15), new Size(216, 40), HorizontalAlignment.Begin));
        card.Add(Label(metadata, "#66666FFF", BrowserTypographyMetrics.TabMetaPointSize, new Position(16, 55), new Size(216, 36), HorizontalAlignment.Begin));
        return card;
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
            control.BorderlineWidth = focused ? 3.0f : 0.0f;
        }
        else if (IsModalActionName(control.Name))
        {
            control.BackgroundColor = Color.White;
            control.BorderlineColor = new Color(focused ? "#0868D7FF" : "#00000000");
            control.BorderlineWidth = focused ? 3.0f : 0.0f;
        }
        else
        {
            control.BorderlineColor = new Color(focused ? "#0868D7FF" : "#DEDEE5FF");
            control.BorderlineWidth = focused ? 3.0f : 1.0f;
        }

        var scaleFocused = focused && !IsModalActionName(control.Name);
        control.Scale = scaleFocused ? new Vector3(1.015f, 1.015f, 1.0f) : new Vector3(1.0f, 1.0f, 1.0f);
    }

    private static bool IsDockControlName(string? name) => name is "Back" or "Forward" or "Home" or "Tabs";

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
