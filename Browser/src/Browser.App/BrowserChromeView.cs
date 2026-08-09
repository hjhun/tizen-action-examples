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
        header.Add(Label("Browser", "#1B1B1FFF", 6.0f, new Position(108, 0), new Size(154, BrowserShellMetrics.HeaderHeight), HorizontalAlignment.Begin));
        _back = CreateDisabledControl("Back", "Back unavailable", "←", new Position(278, 33), new Size(66, 66), goBack);
        header.Add(_back);
        _forward = CreateDisabledControl("Forward", "Forward unavailable", "→", new Position(354, 33), new Size(66, 66), goForward);
        header.Add(_forward);
        _reload = CreateControl("Reload", "Reload current page", "↻", new Position(430, 33), new Size(66, 66), _reloadAction);
        header.Add(_reload);

        _address = new TextField
        {
            Name = "BrowserAddress",
            Text = InitialAddress,
            PlaceholderText = "Address or search",
            PlaceholderTextColor = new Color("#61616AFF"),
            EnableEditing = true,
            Focusable = true,
            Position = new Position(518, 31),
            Size = new Size(1168, 70),
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
        _progressFill = new View
        {
            Name = "BrowserProgressFill",
            Position = new Position(0, BrowserShellMetrics.HeaderHeight + BrowserShellMetrics.ContextHeight),
            Size = new Size(0, BrowserShellMetrics.ProgressHeight),
            BackgroundColor = new Color("#134F9EFF"),
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

        var countLabel = workspace.Tabs.Count == 1 ? "Tab   1" : $"Tabs   {workspace.Tabs.Count}";
        if (_tabs.GetChildAt(0) is TextLabel tabsLabel)
        {
            tabsLabel.Text = countLabel;
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

        if (keyName == "Down" && IsCommandControl(FocusManager.Instance.GetCurrentFocusView()))
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

        if (keyName == "Up" && (ReferenceEquals(FocusManager.Instance.GetCurrentFocusView(), _webView) ||
                                IsRecoveryControl(FocusManager.Instance.GetCurrentFocusView()) ||
                                IsHomeControl(FocusManager.Instance.GetCurrentFocusView())))
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
        _address.BackgroundColor = focused ? Color.White : new Color("#F3F3F6FF");
        _address.BorderlineColor = new Color(focused ? "#134F9EFF" : "#777782FF");
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
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        _homeSurface.Add(Label("BROWSE THE OPEN WEB", "#134F9EFF", 4.8f, new Position(112, 116), new Size(820, 48), HorizontalAlignment.Begin));
        _homeSurface.Add(Label("A clear place to start.", "#1B1B1FFF", 13.5f, new Position(112, 164), new Size(840, 118), HorizontalAlignment.Begin));
        _homeSurface.Add(Paragraph(
            "Enter an address or search above. Pages open in the system WebView. Use remote, keyboard, pointer, or touch.",
            "#45454FFF",
            6.5f,
            new Position(112, 292),
            new Size(830, 178)));
        _homeOpenGuide = CreateControl("OpenGuide", "Open the public Tizen guide", "Open Tizen guide", new Position(112, 506), new Size(286, 76), () => _navigate(InitialAddress));
        _homeEditAddress = CreateControl("HomeEditAddress", "Focus address or search", "Enter an address", new Position(420, 506), new Size(286, 76), FocusAddress);
        _homeSurface.Add(_homeOpenGuide);
        _homeSurface.Add(_homeEditAddress);

        var privacyCard = new View
        {
            Name = "BrowserPrivacyCard",
            Position = new Position(1048, 176),
            Size = new Size(628, 380),
            BackgroundColor = Color.White,
            CornerRadius = 26.0f,
            BorderlineWidth = 2.0f,
            BorderlineColor = new Color("#DEDEE5FF"),
        };
        privacyCard.Add(Label("✓", "#20743DFF", 12.0f, new Position(48, 34), new Size(72, 72), HorizontalAlignment.Center));
        privacyCard.Add(Label("Normal browsing only", "#1B1B1FFF", 7.5f, new Position(48, 116), new Size(532, 70), HorizontalAlignment.Begin));
        privacyCard.Add(Paragraph(
            "Public titles and addresses only. No credentials, form values, page content, or private history.",
            "#61616AFF",
            5.4f,
            new Position(48, 198),
            new Size(532, 134)));
        _homeSurface.Add(privacyCard);
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
            Position = new Position(BrowserShellMetrics.ContentLeft, BrowserShellMetrics.ContentTop),
            Size = new Size(BrowserShellMetrics.ContentWidth, BrowserShellMetrics.ContentHeight),
            BackgroundColor = new Color("#F7F7FAFF"),
            FocusableChildren = true,
        };
        _tabsSurface.Add(Label("Tabs", "#1B1B1FFF", 14.0f, new Position(108, 38), new Size(520, 88), HorizontalAlignment.Begin));
        _tabsSurface.Add(Label("Normal browsing · maximum 20", "#61616AFF", 5.5f, new Position(624, 38), new Size(700, 88), HorizontalAlignment.Begin));
        _tabListViewport = new View
        {
            Name = "BrowserTabListViewport",
            Position = new Position(108, 142),
            Size = new Size(1600, 512),
            ClippingMode = ClippingModeType.ClipChildren,
            FocusableChildren = true,
        };
        _tabsSurface.Add(_tabListViewport);
        _newTab = CreateControl("NewTab", "Create a new normal tab", "New tab", new Position(108, 684), new Size(260, 76), _createTabAction);
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
            Position = new Position(550, 300),
            Size = new Size(820, 480),
            BackgroundColor = Color.White,
            CornerRadius = 30.0f,
            FocusableChildren = true,
        };
        _closeModalTitle = Label("Close tab?", "#1B1B1FFF", 12.0f, new Position(72, 58), new Size(676, 120), HorizontalAlignment.Begin);
        card.Add(_closeModalTitle);
        card.Add(Paragraph("This tab's public metadata will be removed from the normal session.", "#61616AFF", 5.4f, new Position(72, 178), new Size(676, 120)));
        _cancelClose = CreateControl("CancelClose", "Cancel closing tab", "Cancel", new Position(72, 328), new Size(310, 82), _cancelCloseAction);
        _confirmClose = CreateControl("ConfirmClose", "Close this tab", "Close", new Position(406, 328), new Size(310, 82), _confirmCloseAction);
        card.Add(_cancelClose);
        card.Add(_confirmClose);
        _closeModal.Add(card);
        Canvas.Add(_closeModal);
        _closeModal.Hide();
    }

    private void BringOverlaysToFront()
    {
        foreach (var overlay in new[] { _homeSurface, _recoverySurface, _tabsSurface, _closeModal })
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
                Position = new Position(0, index * 94),
                Size = new Size(1600, 82),
                BackgroundColor = new Color(selected ? "#EAF2FFFF" : "#FFFFFFFF"),
                CornerRadius = 18.0f,
                FocusableChildren = true,
            };
            var title = TabTitle(tab);
            var publicUrl = tab.Page?.Url ?? "Start page";
            var open = CreateControl(
                $"Open-{tab.Id}",
                $"Open {title}{(selected ? ", current tab" : string.Empty)}",
                $"{title}    {publicUrl}",
                new Position(0, 0),
                new Size(1458, 82),
                () => _selectTabAction(tab.Id));
            open.BackgroundColor = new Color(selected ? "#EAF2FFFF" : "#FFFFFFFF");
            var close = CreateControl(
                $"Close-{tab.Id}",
                $"Close {title}",
                "×",
                new Position(1476, 0),
                new Size(124, 82),
                () => _requestCloseAction(tab.Id));
            SetControlEnabled(close, workspace.Tabs.Count > 1, workspace.Tabs.Count > 1 ? $"Close {title}" : "Last tab cannot be closed");
            var capturedIndex = index;
            open.FocusGained += (_, _) => EnsureTabVisible(capturedIndex);
            close.FocusGained += (_, _) => EnsureTabVisible(capturedIndex);
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

        const int visibleRows = 5;
        var firstVisible = Math.Clamp(index - 2, 0, Math.Max(0, _tabRows.Count - visibleRows));
        foreach (var row in _tabRows)
        {
            row.Row.Position = new Position(0, (row.Index - firstVisible) * 94);
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
            return;
        }

        var visual = BrowserNavigationVisualState.From(_navigationState);
        SetControlEnabled(_reload, visual.ReloadEnabled, visual.ShowsProgress ? "Reload unavailable while loading" : "Reload current page");
        SetHistoryAvailability(!visual.ShowsProgress && _navigationState.History.CanGoBack, !visual.ShowsProgress && _navigationState.History.CanGoForward);
        SetControlEnabled(_tabs, true, _workspace is null ? "Open tabs" : $"Open tabs. {_workspace.Tabs.Count} tabs.");
    }

    private static string TabTitle(BrowserTab tab)
    {
        var title = tab.Page?.Title ?? "New tab";
        return title.Length <= 80 ? title : title[..80];
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
                return ReferenceEquals(current, _newTab);
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
                nextIndex = 0;
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
