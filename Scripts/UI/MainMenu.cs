using Godot;
using System;
using PhaseHeist;
using PhaseHeist.UI;

/// <summary>
/// 主菜单：标题页 / 联机页 / 设置页。
/// 通过 AppState.PendingIntent 把启动意图传给对局场景。
/// </summary>
public partial class MainMenu : Control
{
    private const string GameScene = "res://Scenes/Main.tscn";

    private Control? _homePage;
    private Control? _multiplayerPage;
    private Control? _settingsPage;
    private Label? _statusLabel;
    private LineEdit? _nameInput;
    private LineEdit? _addressInput;
    private SpinBox? _portInput;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        BuildAll();
        Loc.LanguageChanged += RebuildAll;

        if (HandleCommandLine())
        {
            return;
        }

        string lastError = AppState.Instance?.LastNetworkError ?? string.Empty;
        if (!string.IsNullOrEmpty(lastError))
        {
            SetStatus(lastError);
            if (AppState.Instance != null)
            {
                AppState.Instance.LastNetworkError = string.Empty;
            }
        }
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RebuildAll;
    }

    // ---------- 启动意图 ----------

    private bool HandleCommandLine()
    {
        AppState? app = AppState.Instance;
        if (app == null)
        {
            return false;
        }

        bool host = false;
        bool join = false;

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg["--port=".Length..], out int port))
            {
                app.PendingPort = Mathf.Clamp(port, 1024, 65535);
            }

            if (arg.StartsWith("--join=", StringComparison.OrdinalIgnoreCase))
            {
                app.PendingAddress = arg["--join=".Length..];
                join = true;
            }
            else if (arg.Equals("--join", StringComparison.OrdinalIgnoreCase))
            {
                join = true;
            }

            if (arg.Equals("--host", StringComparison.OrdinalIgnoreCase))
            {
                host = true;
            }
        }

        if (host)
        {
            StartWithIntent(NetIntent.Host);
            return true;
        }

        if (join)
        {
            StartWithIntent(NetIntent.Join);
            return true;
        }

        return false;
    }

    private void StartWithIntent(NetIntent intent)
    {
        if (AppState.Instance != null)
        {
            AppState.Instance.PendingIntent = intent;
            AppState.Instance.Save();
        }

        // _Ready 期间不能同步切场景，必须延迟到帧末
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, GameScene);
    }

    // ---------- UI 构建 ----------

    private void RebuildAll()
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }

        BuildAll();
    }

    private void BuildAll()
    {
        BuildBackground();
        _homePage = BuildHomePage();
        _multiplayerPage = BuildMultiplayerPage();
        _settingsPage = BuildSettingsPage();
        ShowPage(_homePage);
    }

    private void BuildBackground()
    {
        AddChild(new ColorRect
        {
            Name = "Bg",
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.045f, 0.055f, 0.07f),
        });

        // 顶部品牌色条与装饰
        AddChild(new ColorRect
        {
            Name = "TopStripe",
            AnchorRight = 1,
            OffsetBottom = 6,
            Color = AuiTheme.Robber,
        });
        AddChild(new ColorRect
        {
            Name = "BottomStripe",
            AnchorTop = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetTop = -6,
            Color = AuiTheme.Police,
        });
    }

    private Control BuildHomePage()
    {
        var page = NewPage("HomePage");

        var stack = CenterStack(page, 380, 430);

        var title = Aui.Text(Loc.T("menu.title"), 44, AuiTheme.Robber, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(title);

        var subtitle = Aui.Text(Loc.T("menu.subtitle"), 15, AuiTheme.MutedInk);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(subtitle);

        stack.AddChild(Spacer(18));
        stack.AddChild(MenuButton(Loc.T("menu.solo"), AuiTone.Robber, () => StartWithIntent(NetIntent.Solo)));
        stack.AddChild(MenuButton(Loc.T("menu.multiplayer"), AuiTone.Police, () => ShowPage(_multiplayerPage)));
        stack.AddChild(MenuButton(Loc.T("menu.settings"), AuiTone.Neutral, () => ShowPage(_settingsPage)));
        stack.AddChild(MenuButton(Loc.T("menu.quit"), AuiTone.Danger, () => GetTree().Quit()));
        stack.AddChild(Spacer(14));

        _statusLabel = Aui.Text(string.Empty, 13, AuiTheme.Warning);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(_statusLabel);

        var version = Aui.Text(Loc.T("menu.version"), 12, AuiTheme.MutedInk);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(version);

        return page;
    }

    private Control BuildMultiplayerPage()
    {
        var page = NewPage("MultiplayerPage");
        var stack = CenterStack(page, 400, 430);

        var title = Aui.Text(Loc.T("mp.title"), 30, AuiTheme.Police, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(title);
        stack.AddChild(Spacer(8));

        stack.AddChild(Aui.Text(Loc.T("mp.name"), 13, AuiTheme.MutedInk));
        _nameInput = new LineEdit
        {
            Text = AppState.Instance?.PlayerName ?? "Player",
            MaxLength = 18,
            CustomMinimumSize = new Vector2(0, 32),
        };
        stack.AddChild(_nameInput);

        stack.AddChild(Aui.Text(Loc.T("mp.address"), 13, AuiTheme.MutedInk));
        _addressInput = new LineEdit
        {
            Text = AppState.Instance?.PendingAddress ?? "127.0.0.1",
            CustomMinimumSize = new Vector2(0, 32),
        };
        stack.AddChild(_addressInput);

        stack.AddChild(Aui.Text(Loc.T("mp.port"), 13, AuiTheme.MutedInk));
        _portInput = new SpinBox
        {
            MinValue = 1024,
            MaxValue = 65535,
            Value = AppState.Instance?.PendingPort ?? 24565,
            CustomMinimumSize = new Vector2(0, 32),
        };
        stack.AddChild(_portInput);

        stack.AddChild(Spacer(10));
        stack.AddChild(MenuButton(Loc.T("mp.host"), AuiTone.Police, () => LaunchNetwork(NetIntent.Host)));
        stack.AddChild(MenuButton(Loc.T("mp.join"), AuiTone.Robber, () => LaunchNetwork(NetIntent.Join)));
        stack.AddChild(Spacer(6));

        var hint = Aui.Text(Loc.T("mp.hint"), 12, AuiTheme.MutedInk, false, wrap: true);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(hint);

        stack.AddChild(MenuButton(Loc.T("mp.back"), AuiTone.Neutral, () => ShowPage(_homePage)));
        return page;
    }

    private Control BuildSettingsPage()
    {
        var page = NewPage("SettingsPage");
        var stack = CenterStack(page, 400, 540);

        var title = Aui.Text(Loc.T("set.title"), 30, AuiTheme.Ink, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(title);
        stack.AddChild(Spacer(8));

        SettingsPanelBuilder.Build(stack);

        stack.AddChild(Spacer(10));
        stack.AddChild(MenuButton(Loc.T("set.back"), AuiTone.Neutral, () => ShowPage(_homePage)));
        return page;
    }

    private void LaunchNetwork(NetIntent intent)
    {
        AppState? app = AppState.Instance;
        if (app == null)
        {
            return;
        }

        app.PlayerName = string.IsNullOrWhiteSpace(_nameInput?.Text) ? app.PlayerName : _nameInput!.Text.Trim();
        app.PendingAddress = string.IsNullOrWhiteSpace(_addressInput?.Text) ? "127.0.0.1" : _addressInput!.Text.Trim();
        app.PendingPort = (int)(_portInput?.Value ?? 24565);
        StartWithIntent(intent);
    }

    // ---------- 工具 ----------

    private Control NewPage(string name)
    {
        var page = new Control
        {
            Name = name,
            AnchorRight = 1,
            AnchorBottom = 1,
            Visible = false,
        };
        AddChild(page);
        return page;
    }

    private static VBoxContainer CenterStack(Control page, float width, float height)
    {
        var panel = Aui.Panel(AuiTone.Neutral, true, "CenterPanel");
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        Aui.Dock(panel, -width / 2, -height / 2, width / 2, height / 2);
        page.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var stack = Aui.VStack(9, "PageStack");
        margin.AddChild(stack);
        return stack;
    }

    private static Button MenuButton(string text, AuiTone tone, Action pressed)
    {
        Button button = Aui.Button(text, tone, pressed);
        button.CustomMinimumSize = new Vector2(0, 40);
        return button;
    }

    private static Control Spacer(float height)
    {
        return new Control { CustomMinimumSize = new Vector2(0, height) };
    }

    private void ShowPage(Control? page)
    {
        foreach (Control? candidate in new[] { _homePage, _multiplayerPage, _settingsPage })
        {
            if (candidate != null)
            {
                candidate.Visible = candidate == page;
            }
        }
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }
    }
}
