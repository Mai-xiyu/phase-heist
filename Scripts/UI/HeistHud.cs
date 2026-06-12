using Godot;
using PhaseHeist;
using PhaseHeist.UI;

public partial class HeistHud : CanvasLayer
{
    private Control? _root;
    private Label? _statusLabel;
    private Label? _phaseLabel;
    private Label? _objectiveLabel;
    private Label? _roleLabel;
    private Label? _routeLabel;
    private Label? _scoreLabel;
    private Label? _topClockLabel;
    private Label? _topPhaseLabel;
    private Label? _topTeamLabel;
    private Label? _hostageLabel;
    private Label? _lootLabel;
    private Label? _escapeStatsLabel;
    private Label? _voiceLabel;
    private ProgressBar? _healthBar;
    private ProgressBar? _breachBar;
    private ProgressBar? _timeBar;
    private AuiToastDeck? _toastDeck;
    private Control? _escapeOverlay;
    private Control? _settingsOverlay;
    private Control? _settlementOverlay;
    private Label? _settlementTitle;
    private Label? _settlementBody;
    private PanelContainer? _interactPanel;
    private Label? _interactLabel;
    private PlayerController? _localPlayer;
    private VoiceChatManager? _voice;
    private string _lastStatus = string.Empty;
    private bool _escapeOpen;
    private bool _settlementOpen;

    public override void _Ready()
    {
        BuildUi();
        CallDeferred(nameof(ConnectSignals));
        Loc.LanguageChanged += RebuildUi;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RebuildUi;
        DisconnectSignals();
    }

    public override void _Process(double delta)
    {
        _localPlayer ??= FindLocalPlayer();
        _voice ??= GetTree().CurrentScene?.GetNodeOrNull<VoiceChatManager>("VoiceChat");
        if (_localPlayer == null)
        {
            return;
        }

        HeistGameMode? mode = HeistService.Instance?.GameMode;
        float remaining = mode?.RemainingSeconds ?? 0.0f;
        float total = mode?.NegotiationSeconds ?? 600.0f;
        float breachUnlock = total * (mode?.PoliceEntryUnlockRatio ?? 0.30f);

        _phaseLabel!.Text = mode?.DoorsLocked == true
            ? Loc.TF("hud.countdown", FormatClock(remaining), Loc.T(mode.PoliceCanBreach ? "hud.breach.ok" : "hud.breach.no"))
            : Loc.T("hud.street");
        _topClockLabel!.Text = mode?.DoorsLocked == true ? FormatClock(remaining) : "--:--";
        _topPhaseLabel!.Text = mode == null ? Loc.T("hud.phase.init") : DisplayPhase(mode.CurrentPhase);

        _timeBar!.Value = total <= 0.0f ? 0.0f : Mathf.Clamp(remaining / total * 100.0f, 0, 100);
        _breachBar!.Value = mode?.PoliceCanBreach == true
            ? 100
            : Mathf.Clamp((mode?.ElapsedLockdownSeconds ?? 0.0f) / Mathf.Max(1.0f, breachUnlock) * 100.0f, 0, 100);
        _healthBar!.Value = _localPlayer.Health;

        string team = Loc.T(_localPlayer.Team == PlayerTeam.Police ? "team.police" : "team.robber");
        string weapon = Loc.T(_localPlayer.HasWeapon ? "hud.gun" : "hud.nogun");
        string disguise = _localPlayer.IsFakeHostage
            ? Loc.T("hud.fakehostage")
            : _localPlayer.Team == PlayerTeam.Robber
                ? Loc.TF("hud.disguise", _localPlayer.DisguiseLevel)
                : Loc.T("hud.outside");
        _roleLabel!.Text = $"{team} | {weapon} | {disguise}";
        _topTeamLabel!.Text = $"{team} / {weapon}";

        bool speaking = (_voice?.SelfSpeaking ?? false) || _localPlayer.IsSpeaking;
        _voiceLabel!.Text = speaking ? Loc.T("hud.speaking") : Loc.T("hud.voice.ptt");
        _voiceLabel.Modulate = speaking ? new Color(0.25f, 0.95f, 0.45f) : new Color(1, 1, 1, 0.55f);

        RefreshInteractPrompt();
        RefreshObjective();
        RefreshEscapeMenu();
    }

    /// <summary>屏幕中下方的「按 E 交互」提示，跟随最近可用交互点。</summary>
    private void RefreshInteractPrompt()
    {
        if (_interactPanel == null || _interactLabel == null)
        {
            return;
        }

        // 驾驶中：显示下车提示 + 实时车速
        if (_localPlayer is { InVehicle: true, CurrentVehicle: not null })
        {
            int kmh = Mathf.RoundToInt(Mathf.Abs(_localPlayer.CurrentVehicle.CurrentSpeed) * 3.6f);
            _interactPanel.Visible = true;
            _interactLabel.Text = Loc.TF("hud.interact", Loc.T("hud.exitcar")) + "  ·  " + Loc.TF("hud.kmh", kmh);
            return;
        }

        IInteractable? target = _localPlayer?.CurrentInteractable;
        if (target == null)
        {
            _interactPanel.Visible = false;
            return;
        }

        _interactPanel.Visible = true;
        _interactLabel.Text = Loc.TF("hud.interact", target.GetInteractPrompt());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("release_mouse"))
        {
            if (_settingsOverlay is { Visible: true })
            {
                CloseSettings();
            }
            else
            {
                ToggleEscapeMenu();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    public void SetStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }

        if (!string.IsNullOrWhiteSpace(text) && text != _lastStatus)
        {
            _toastDeck?.ShowToast(text, AuiTone.Neutral);
            _lastStatus = text;
        }
    }

    public void ToggleEscapeMenu(bool? open = null)
    {
        _escapeOpen = open ?? !_escapeOpen;
        if (_escapeOverlay != null)
        {
            _escapeOverlay.Visible = _escapeOpen;
        }

        Game.SetUiBlock("escmenu", _escapeOpen);
        Input.MouseMode = _escapeOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        RefreshEscapeMenu();
    }

    // ---------- 构建 ----------

    private void RebuildUi()
    {
        bool wasOpen = _escapeOpen;
        DisconnectSignals();
        _root?.QueueFree();
        _root = null;
        BuildUi();
        ConnectSignals();
        _escapeOpen = false;

        if (wasOpen)
        {
            ToggleEscapeMenu(true);
        }
    }

    private void BuildUi()
    {
        _root = Aui.Root("HeistHudRoot");
        AddChild(_root);
        Aui.BuildCrosshair(_root);
        BuildTopBar(_root);
        BuildTacticalCard(_root);
        BuildHintStrip(_root);
        BuildToastDeck(_root);
        BuildInteractPrompt(_root);
        BuildEscapeMenu(_root);
        BuildSettingsOverlay(_root);
        BuildSettlementPanel(_root);
    }

    private void BuildInteractPrompt(Control root)
    {
        _interactPanel = Aui.Panel(AuiTone.Success, true, "InteractPrompt");
        _interactPanel.AnchorLeft = 0.5f;
        _interactPanel.AnchorRight = 0.5f;
        _interactPanel.AnchorTop = 1;
        _interactPanel.AnchorBottom = 1;
        Aui.Dock(_interactPanel, -250, -136, 250, -88);
        _interactPanel.Visible = false;
        root.AddChild(_interactPanel);

        _interactLabel = Aui.Text("[E]", 16, AuiTheme.Ink, true);
        _interactLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _interactPanel.AddChild(_interactLabel);
    }

    private void BuildTopBar(Control root)
    {
        PanelContainer top = Aui.Panel(AuiTone.Neutral, true, "TopStatusBar");
        top.AnchorLeft = 0.5f;
        top.AnchorRight = 0.5f;
        Aui.Dock(top, -330, 10, 330, 54);
        root.AddChild(top);

        HBoxContainer row = Aui.HStack(14, "TopStatusRow");
        row.Alignment = BoxContainer.AlignmentMode.Center;
        top.AddChild(row);

        _topPhaseLabel = Aui.Text(Loc.T("phase.street"), 16, AuiTheme.Robber, true);
        _topClockLabel = Aui.Text("--:--", 22, AuiTheme.Danger, true);
        _topTeamLabel = Aui.Text(Loc.T("hud.team.none"), 13, AuiTheme.MutedInk, true);
        _voiceLabel = Aui.Text(Loc.T("hud.voice.ptt"), 12, AuiTheme.MutedInk, true);

        row.AddChild(Aui.Pill(Loc.T("hud.brand"), AuiTone.Robber));
        row.AddChild(TopDivider());
        row.AddChild(_topPhaseLabel);
        row.AddChild(_topClockLabel);
        row.AddChild(TopDivider());
        row.AddChild(_topTeamLabel);
        row.AddChild(TopDivider());
        row.AddChild(_voiceLabel);
    }

    private static Control TopDivider()
    {
        return new ColorRect
        {
            Color = new Color(1, 1, 1, 0.10f),
            CustomMinimumSize = new Vector2(1, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private void BuildTacticalCard(Control root)
    {
        PanelContainer card = Aui.Card(Loc.T("hud.tactical"), AuiTone.Robber, out VBoxContainer body, true);
        card.AnchorTop = 1;
        card.AnchorBottom = 1;
        Aui.Dock(card, 12, -356, 352, -16);
        root.AddChild(card);

        _statusLabel = Aui.Text(Loc.T("menu.title"), 13, AuiTheme.Ink, true, wrap: true);
        _phaseLabel = Aui.Text(Loc.T("phase.street"), 12, AuiTheme.MutedInk, false, wrap: true);
        _objectiveLabel = Aui.Text(string.Empty, 12, AuiTheme.Ink, false, wrap: true);
        body.AddChild(_statusLabel);
        body.AddChild(_phaseLabel);
        body.AddChild(_objectiveLabel);

        HBoxContainer fields = Aui.HStack(6, "TacticalFields");
        _roleLabel = Aui.Stat("-", AuiTone.Neutral);
        _routeLabel = Aui.Stat("-", AuiTone.Police);
        _hostageLabel = Aui.Stat("-", AuiTone.Warning);
        _lootLabel = Aui.Stat("-", AuiTone.Robber);
        fields.AddChild(_roleLabel);
        fields.AddChild(_routeLabel);
        fields.AddChild(_hostageLabel);
        fields.AddChild(_lootLabel);
        body.AddChild(fields);

        body.AddChild(Aui.BarRow(Loc.T("hud.health"), AuiTone.Success, out _healthBar!));
        body.AddChild(Aui.BarRow(Loc.T("hud.breachbar"), AuiTone.Police, out _breachBar!));
        body.AddChild(Aui.BarRow(Loc.T("hud.timebar"), AuiTone.Danger, out _timeBar!));

        _scoreLabel = Aui.Text("-", 12, AuiTheme.MutedInk);
        body.AddChild(_scoreLabel);
    }

    private void BuildHintStrip(Control root)
    {
        PanelContainer hint = Aui.Panel(AuiTone.Neutral, false, "HintStrip");
        hint.AnchorLeft = 0.5f;
        hint.AnchorRight = 0.5f;
        hint.AnchorTop = 1;
        hint.AnchorBottom = 1;
        Aui.Dock(hint, -300, -58, 392, -16);
        root.AddChild(hint);

        HBoxContainer row = Aui.HStack(8, "HintRow");
        hint.AddChild(row);
        row.AddChild(Aui.Pill(Loc.T("hud.hint.move"), AuiTone.Neutral));
        row.AddChild(Aui.Pill(Loc.T("hud.hint.sprint"), AuiTone.Neutral));
        row.AddChild(Aui.Pill(Loc.T("hud.hint.interact"), AuiTone.Success));
        row.AddChild(Aui.Pill(Loc.T("hud.hint.context"), AuiTone.Warning));
        row.AddChild(Aui.Pill(Loc.T("hud.hint.fire"), AuiTone.Danger));
        row.AddChild(Aui.Pill(Loc.T("hud.hint.menu"), AuiTone.Police));
    }

    private void BuildToastDeck(Control root)
    {
        _toastDeck = new AuiToastDeck
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -382,
            OffsetTop = 92,
            OffsetRight = -16,
            OffsetBottom = 310,
        };
        root.AddChild(_toastDeck);
    }

    private void BuildEscapeMenu(Control root)
    {
        _escapeOverlay = Aui.Overlay("EscapeMenu");
        _escapeOverlay.Visible = false;
        root.AddChild(_escapeOverlay);

        PanelContainer menu = Aui.Card(Loc.T("esc.title"), AuiTone.Police, out VBoxContainer body, true);
        menu.AnchorLeft = 0.5f;
        menu.AnchorTop = 0.5f;
        menu.AnchorRight = 0.5f;
        menu.AnchorBottom = 0.5f;
        Aui.Dock(menu, -260, -225, 260, 225);
        _escapeOverlay.AddChild(menu);

        _escapeStatsLabel = Aui.Text("-", 13, AuiTheme.Ink);
        body.AddChild(_escapeStatsLabel);
        body.AddChild(Aui.Divider(AuiTone.Police));
        body.AddChild(Aui.Button(Loc.T("esc.resume"), AuiTone.Success, () => ToggleEscapeMenu(false)));
        body.AddChild(Aui.Button(Loc.T("esc.settings"), AuiTone.Neutral, OpenSettings));
        body.AddChild(Aui.Button(Loc.T("esc.reselect"), AuiTone.Warning, () =>
        {
            ToggleEscapeMenu(false);
            (GetTree().CurrentScene as Game)?.OpenTeamPanel();
        }));
        body.AddChild(Aui.Button(Loc.T("esc.mainmenu"), AuiTone.Police, () =>
        {
            Game.SetUiBlock("escmenu", false);
            (GetTree().CurrentScene as Game)?.ReturnToMainMenu();
        }));
        body.AddChild(Aui.Button(Loc.T("esc.quit"), AuiTone.Danger, () => GetTree().Quit()));
    }

    private void BuildSettingsOverlay(Control root)
    {
        _settingsOverlay = Aui.Overlay("SettingsOverlay");
        _settingsOverlay.Visible = false;
        root.AddChild(_settingsOverlay);

        PanelContainer panel = Aui.Card(Loc.T("set.title"), AuiTone.Neutral, out VBoxContainer body, true);
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        Aui.Dock(panel, -240, -280, 240, 280);
        _settingsOverlay.AddChild(panel);

        SettingsPanelBuilder.Build(body);
        body.AddChild(Aui.Divider());
        body.AddChild(Aui.Button(Loc.T("set.back"), AuiTone.Neutral, CloseSettings));
    }

    private void BuildSettlementPanel(Control root)
    {
        _settlementOverlay = Aui.Overlay("SettlementOverlay");
        _settlementOverlay.Visible = false;
        root.AddChild(_settlementOverlay);

        PanelContainer panel = Aui.Card(Loc.T("settlement.title"), AuiTone.Robber, out VBoxContainer body, true);
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        Aui.Dock(panel, -310, -235, 310, 235);
        _settlementOverlay.AddChild(panel);

        _settlementTitle = Aui.Text(Loc.T("settlement.title"), 24, AuiTheme.Robber, true);
        _settlementBody = Aui.Text("-", 14, AuiTheme.Ink);
        body.AddChild(_settlementTitle);
        body.AddChild(_settlementBody);
        body.AddChild(Aui.Divider(AuiTone.Robber));
        body.AddChild(Aui.Button(Loc.T("settlement.close"), AuiTone.Neutral, HideSettlement));
        body.AddChild(Aui.Button(Loc.T("esc.mainmenu"), AuiTone.Police, () =>
        {
            HideSettlement();
            (GetTree().CurrentScene as Game)?.ReturnToMainMenu();
        }));
    }

    private void OpenSettings()
    {
        if (_settingsOverlay != null && _escapeOverlay != null)
        {
            _escapeOverlay.Visible = false;
            _settingsOverlay.Visible = true;
        }
    }

    private void CloseSettings()
    {
        if (_settingsOverlay != null && _escapeOverlay != null)
        {
            _settingsOverlay.Visible = false;
            _escapeOverlay.Visible = _escapeOpen;
        }
    }

    // ---------- 数据刷新 ----------

    private void ConnectSignals()
    {
        if (HeistService.Instance?.GameMode != null)
        {
            HeistService.Instance.GameMode.PhaseChanged += OnHeistPhaseChanged;
            HeistService.Instance.GameMode.RulesChanged += OnRulesChanged;
        }
    }

    private void DisconnectSignals()
    {
        if (HeistService.Instance?.GameMode != null)
        {
            HeistService.Instance.GameMode.PhaseChanged -= OnHeistPhaseChanged;
            HeistService.Instance.GameMode.RulesChanged -= OnRulesChanged;
        }
    }

    private void RefreshObjective()
    {
        HeistGameMode? mode = HeistService.Instance?.GameMode;
        if (mode == null)
        {
            _objectiveLabel!.Text = Loc.T("hud.phase.init");
            return;
        }

        string loot = Loc.T(mode.LootTaken ? "hud.loot.taken" : "hud.loot.not");
        string route = mode.BlockedRoute switch
        {
            EscapeRoute.SideDoor => Loc.T("hud.route.side"),
            EscapeRoute.Sewer => Loc.T("hud.route.sewer"),
            _ => Loc.T("hud.route.none"),
        };

        _objectiveLabel!.Text =
            Loc.TF("hud.phase.label", DisplayPhase(mode.CurrentPhase)) + "\n" +
            loot + "\n" +
            Loc.TF("hud.hostages", mode.HostagesRemaining, mode.HostagesReleased, mode.HostagesKilled);
        _routeLabel!.Text = route;
        _hostageLabel!.Text = Loc.TF("hud.hostage.short", mode.HostagesRemaining);
        _lootLabel!.Text = Loc.T(mode.LootTaken ? "hud.loot.short.taken" : "hud.loot.short.not");
        _scoreLabel!.Text = Loc.TF("hud.score", mode.PoliceScore, mode.RobberScore);
    }

    private void RefreshEscapeMenu()
    {
        if (_escapeStatsLabel == null || !_escapeOpen)
        {
            return;
        }

        HeistGameMode? mode = HeistService.Instance?.GameMode;
        string phase = mode == null ? Loc.T("hud.phase.init") : DisplayPhase(mode.CurrentPhase);
        string score = mode == null ? "-" : Loc.TF("hud.score", mode.PoliceScore, mode.RobberScore);
        string team = _localPlayer == null
            ? "-"
            : Loc.T(_localPlayer.Team == PlayerTeam.Police ? "team.police" : "team.robber");
        string weapon = _localPlayer == null
            ? "-"
            : Loc.T(_localPlayer.HasWeapon ? "hud.gun" : "hud.nogun");

        _escapeStatsLabel.Text = Loc.TF("esc.stats", phase, team, _localPlayer?.Health ?? 0, weapon, score);
    }

    private void OnHeistPhaseChanged(int phaseInt)
    {
        var phase = (HeistPhase)phaseInt;
        SetStatus(DisplayPhase(phase));

        if (phase is HeistPhase.Scored or HeistPhase.Failed)
        {
            ShowSettlement(phase);
        }
        else if (_settlementOpen)
        {
            HideSettlement();
        }
    }

    private void OnRulesChanged()
    {
        RefreshObjective();
    }

    private static PlayerController? FindLocalPlayer()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return null;
        }

        foreach (Node node in tree.GetNodesInGroup("players"))
        {
            if (node is PlayerController player && player.IsLocalControlled)
            {
                return player;
            }
        }

        return null;
    }

    private static string DisplayPhase(HeistPhase phase)
    {
        return phase switch
        {
            HeistPhase.StreetInfiltration => Loc.T("phase.street"),
            HeistPhase.Lockdown => Loc.T("phase.lockdown"),
            HeistPhase.Negotiation => Loc.T("phase.negotiation"),
            HeistPhase.Assault => Loc.T("phase.assault"),
            HeistPhase.Escape => Loc.T("phase.escape"),
            HeistPhase.Scored => Loc.T("phase.scored"),
            HeistPhase.Failed => Loc.T("phase.failed"),
            _ => phase.ToString(),
        };
    }

    private static string FormatClock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{whole / 60:00}:{whole % 60:00}";
    }

    private void ShowSettlement(HeistPhase phase)
    {
        HeistGameMode? mode = HeistService.Instance?.GameMode;
        if (_settlementOverlay == null || _settlementTitle == null || _settlementBody == null || mode == null)
        {
            return;
        }

        bool robberWin = phase == HeistPhase.Scored;
        bool firstOpen = !_settlementOpen;
        _settlementOpen = true;
        _settlementOverlay.Visible = true;
        _settlementTitle.Text = Loc.T(robberWin ? "settlement.robberwin" : "settlement.policewin");
        _settlementTitle.Modulate = robberWin ? AuiTheme.Robber : AuiTheme.Police;
        _settlementBody.Text = Loc.TF("settlement.body",
            mode.PoliceScore,
            mode.RobberScore,
            mode.HostagesRemaining,
            mode.HostagesReleased,
            mode.HostagesKilled,
            Loc.T(mode.LootTaken ? "hud.loot.taken" : "hud.loot.not"),
            mode.BlockedRoute switch
            {
                EscapeRoute.SideDoor => Loc.T("hud.route.side"),
                EscapeRoute.Sewer => Loc.T("hud.route.sewer"),
                _ => Loc.T("hud.route.none"),
            });
        if (firstOpen)
        {
            (GetTree().CurrentScene as Game)?.GetNodeOrNull<FeedbackFx>("FeedbackFx")?.PlaySettlement(robberWin);
        }
        Game.SetUiBlock("settlement", true);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void HideSettlement()
    {
        _settlementOpen = false;
        if (_settlementOverlay != null)
        {
            _settlementOverlay.Visible = false;
        }

        Game.SetUiBlock("settlement", false);
        if (!_escapeOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }
}
