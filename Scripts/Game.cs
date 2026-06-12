using Godot;
using System;
using System.Collections.Generic;
using PhaseHeist;
using PhaseHeist.UI;

public partial class Game : Node3D
{
    private const int MaxClients = 16;
    private const int OfflinePeerId = 1;

    [Export] public PackedScene? PlayerScene { get; set; }
    [Export] public NodePath PlayersRootPath { get; set; } = new("Players");

    /// <summary>任一全屏 UI（阵营选择 / ESC 菜单）打开时为 true，玩家输入挂起。</summary>
    public static bool UiBlocking { get; private set; }
    private static readonly HashSet<string> UiBlocks = new();

    private readonly Dictionary<int, PlayerController> _players = new();
    private readonly Dictionary<int, PlayerTeam> _pendingTeams = new();
    private readonly Dictionary<int, string> _playerNames = new();
    private readonly Dictionary<int, double> _shoutCooldownEnds = new();

    private Node3D? _playersRoot;
    private Control? _teamPanel;
    private Label? _teamTitle;
    private Label? _teamRobberDesc;
    private Label? _teamPoliceDesc;
    private Button? _teamRobberBtn;
    private Button? _teamPoliceBtn;
    private HeistHud? _hud;
    private CityMapBuilder? _map;
    private VoiceChatManager? _voice;
    private FeedbackFx? _fx;

    public PlayerController? LocalPlayer => _players.TryGetValue(LocalPeerId(), out PlayerController? p) ? p : null;

    public FeedbackFx? Fx => _fx;

    public static void SetUiBlock(string source, bool blocked)
    {
        if (blocked)
        {
            UiBlocks.Add(source);
        }
        else
        {
            UiBlocks.Remove(source);
        }

        UiBlocking = UiBlocks.Count > 0;
    }

    public override void _Ready()
    {
        UiBlocks.Clear();
        UiBlocking = false;

        _playersRoot = GetNode<Node3D>(PlayersRootPath);
        EnsureDefaultInputMap();
        BuildSystems();
        BuildTeamPanel();
        ConnectMultiplayerSignals();
        Loc.LanguageChanged += RefreshTeamPanelText;
        ApplyStartupIntent();
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshTeamPanelText;

        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
    }

    // ---------- 启动 ----------

    private void BuildSystems()
    {
        var service = new HeistService { Name = "HeistService" };
        AddChild(service);

        var mode = new HeistGameMode { Name = "HeistGameMode" };
        service.AddChild(mode);

        _map = new CityMapBuilder { Name = "CityBlock" };
        AddChild(_map);

        _fx = new FeedbackFx { Name = "FeedbackFx" };
        AddChild(_fx);

        _hud = new HeistHud { Name = "HeistHud" };
        AddChild(_hud);

        _voice = new VoiceChatManager { Name = "VoiceChat" };
        _voice.Configure(this);
        AddChild(_voice);
    }

    private void ApplyStartupIntent()
    {
        NetIntent intent = AppState.Instance?.PendingIntent ?? NetIntent.None;
        if (AppState.Instance != null)
        {
            AppState.Instance.PendingIntent = NetIntent.None;
        }

        switch (intent)
        {
            case NetIntent.Host:
                HostGame();
                break;
            case NetIntent.Join:
                JoinGame();
                break;
            default:
                StartSolo();
                break;
        }
    }

    private void StartSolo()
    {
        CloseNetworkPeer();
        ClearPlayers();
        SpawnPlayer(OfflinePeerId);
        SubmitLocalIdentity();
        ShowTeamPanel();
        SetLocalStatus(Loc.T("st.solo"));
    }

    private void HostGame()
    {
        CloseNetworkPeer();
        ClearPlayers();

        int port = AppState.Instance?.PendingPort ?? 24565;
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(port, MaxClients);
        if (error != Error.Ok)
        {
            SetLocalStatus(Loc.TF("st.hostfail", error));
            StartSolo();
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        SpawnPlayer(Multiplayer.GetUniqueId());
        SubmitLocalIdentity();
        ShowTeamPanel();
        SetLocalStatus(Loc.TF("st.hosting", port));
    }

    private void JoinGame()
    {
        CloseNetworkPeer();
        ClearPlayers();

        string address = AppState.Instance?.PendingAddress ?? "127.0.0.1";
        int port = AppState.Instance?.PendingPort ?? 24565;
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(address, port);
        if (error != Error.Ok)
        {
            ReturnToMainMenu(Loc.TF("st.joinfail", error));
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        SetLocalStatus(Loc.TF("st.connecting", address, port));
    }

    public void ReturnToMainMenu(string statusMessage = "")
    {
        if (AppState.Instance != null)
        {
            AppState.Instance.LastNetworkError = statusMessage;
        }

        CloseNetworkPeer();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    // ---------- 阵营面板 ----------

    private void BuildTeamPanel()
    {
        var canvas = new CanvasLayer { Name = "TeamUi", Layer = 3 };
        AddChild(canvas);

        var panel = Aui.Panel(AuiTone.Neutral, true, "TeamPanel");
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        Aui.Dock(panel, -250, -160, 250, 160);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        canvas.AddChild(panel);
        _teamPanel = panel;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var col = Aui.VStack(10, "TeamStack");
        margin.AddChild(col);

        _teamTitle = Aui.Text(Loc.T("team.title"), 20, AuiTheme.Ink, true);
        col.AddChild(_teamTitle);
        _teamRobberDesc = Aui.Text(Loc.T("team.robber.desc"), 13, AuiTheme.Robber, false, wrap: true);
        col.AddChild(_teamRobberDesc);
        _teamPoliceDesc = Aui.Text(Loc.T("team.police.desc"), 13, AuiTheme.Police, false, wrap: true);
        col.AddChild(_teamPoliceDesc);

        _teamRobberBtn = Aui.Button(Loc.T("team.robber"), AuiTone.Robber, () => SelectTeam(PlayerTeam.Robber));
        col.AddChild(_teamRobberBtn);
        _teamPoliceBtn = Aui.Button(Loc.T("team.police"), AuiTone.Police, () => SelectTeam(PlayerTeam.Police));
        col.AddChild(_teamPoliceBtn);
    }

    private void RefreshTeamPanelText()
    {
        if (_teamTitle != null)
        {
            _teamTitle.Text = Loc.T("team.title");
        }
        if (_teamRobberDesc != null)
        {
            _teamRobberDesc.Text = Loc.T("team.robber.desc");
        }
        if (_teamPoliceDesc != null)
        {
            _teamPoliceDesc.Text = Loc.T("team.police.desc");
        }
        if (_teamRobberBtn != null)
        {
            _teamRobberBtn.Text = Loc.T("team.robber");
        }
        if (_teamPoliceBtn != null)
        {
            _teamPoliceBtn.Text = Loc.T("team.police");
        }
    }

    private void ShowTeamPanel()
    {
        if (_teamPanel != null)
        {
            _teamPanel.Visible = true;
            SetUiBlock("team", true);
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void OpenTeamPanel()
    {
        ShowTeamPanel();
    }

    private void SelectTeam(PlayerTeam team)
    {
        RequestTeamSelect(LocalPeerId(), team);

        if (_teamPanel != null)
        {
            _teamPanel.Visible = false;
        }

        SetUiBlock("team", false);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        SetLocalStatus(Loc.T(team == PlayerTeam.Police ? "st.team.police" : "st.team.robber"));
    }

    // ---------- 公共查询 ----------

    public bool HasNetworkPeerPublic() => Multiplayer.MultiplayerPeer != null;

    public int LocalPeerId() => HasNetworkPeerPublic() ? Multiplayer.GetUniqueId() : OfflinePeerId;

    public PlayerController? FindPlayerPublic(int peerId)
    {
        return _players.TryGetValue(peerId, out PlayerController? player) ? player : null;
    }

    public string NameForPeer(int peerId)
    {
        return _playerNames.TryGetValue(peerId, out string? name) ? name : $"P{peerId}";
    }

    // ---------- 请求入口（客户端 → 服务端） ----------

    public void RequestTeamSelect(int peerId, PlayerTeam team)
    {
        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerSelectTeam), peerId, (int)team);
            return;
        }

        ServerSelectTeam(peerId, (int)team);
    }

    public void RequestBankAction(int peerId, BankActionType action)
    {
        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerBankAction), peerId, (int)action);
            return;
        }

        ServerBankAction(peerId, (int)action);
    }

    public void RequestWeaponFire(int shooterPeerId, int hitPeerId)
    {
        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerWeaponFire), shooterPeerId, hitPeerId);
            return;
        }

        ServerWeaponFire(shooterPeerId, hitPeerId);
    }

    public void SendPlayerTransform(int peerId, Vector3 position, Vector3 rotation, float headPitch)
    {
        if (!HasNetworkPeerPublic())
        {
            return;
        }

        if (Multiplayer.IsServer())
        {
            Rpc(nameof(ReceivePlayerTransform), peerId, position, rotation, headPitch);
        }
        else
        {
            RpcId(1, nameof(ServerReceivePlayerTransform), peerId, position, rotation, headPitch);
        }
    }

    private void SubmitLocalIdentity()
    {
        string name = AppState.Instance?.PlayerName ?? "Player";
        int peerId = LocalPeerId();

        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerSetPlayerName), peerId, name);
            return;
        }

        ApplyPlayerNameRpc(peerId, name);
        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyPlayerNameRpc), peerId, name);
        }
    }

    // ---------- 名字同步 ----------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerSetPlayerName(int peerId, string name)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        name = SanitizeName(name);
        ApplyPlayerNameRpc(peerId, name);
        Rpc(nameof(ApplyPlayerNameRpc), peerId, name);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyPlayerNameRpc(int peerId, string name)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        _playerNames[peerId] = SanitizeName(name);
        FindPlayerPublic(peerId)?.SetDisplayName(_playerNames[peerId]);
    }

    private static string SanitizeName(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return "Player";
        }

        return name.Length > 18 ? name[..18] : name;
    }

    // ---------- 载具 ----------

    public void RequestEnterVehicle(int peerId, string vehicleName)
    {
        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerEnterVehicle), peerId, vehicleName);
            return;
        }

        ServerEnterVehicle(peerId, vehicleName);
    }

    public void RequestExitVehicle(int peerId)
    {
        if (MultiplayerGuard.IsClient(Multiplayer))
        {
            RpcId(1, nameof(ServerExitVehicle), peerId);
            return;
        }

        ServerExitVehicle(peerId);
    }

    public void SendVehicleTransform(string vehicleName, Vector3 position, float yaw)
    {
        if (!HasNetworkPeerPublic())
        {
            return;
        }

        if (Multiplayer.IsServer())
        {
            Rpc(nameof(ApplyVehicleTransformRpc), vehicleName, position, yaw);
        }
        else
        {
            RpcId(1, nameof(ServerVehicleTransform), vehicleName, position, yaw);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerEnterVehicle(int peerId, string vehicleName)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        DriveableCar? car = FindVehicle(vehicleName);
        if (car == null || car.Occupied ||
            !_players.TryGetValue(peerId, out PlayerController? player) || player.InVehicle)
        {
            return;
        }

        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyEnterVehicleRpc), peerId, vehicleName);
        }
        else
        {
            ApplyEnterVehicleRpc(peerId, vehicleName);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyEnterVehicleRpc(int peerId, string vehicleName)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        DriveableCar? car = FindVehicle(vehicleName);
        if (car == null || !_players.TryGetValue(peerId, out PlayerController? player))
        {
            return;
        }

        car.SetDriver(peerId);
        player.EnterVehicle(car);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerExitVehicle(int peerId)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        if (!_players.TryGetValue(peerId, out PlayerController? player) ||
            player.CurrentVehicle == null)
        {
            return;
        }

        string vehicleName = player.CurrentVehicle.Name;
        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyExitVehicleRpc), peerId, vehicleName);
        }
        else
        {
            ApplyExitVehicleRpc(peerId, vehicleName);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyExitVehicleRpc(int peerId, string vehicleName)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        DriveableCar? car = FindVehicle(vehicleName);
        if (car == null || !_players.TryGetValue(peerId, out PlayerController? player))
        {
            return;
        }

        car.SetDriver(0);
        player.ExitVehicle(car);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ServerVehicleTransform(string vehicleName, Vector3 position, float yaw)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        DriveableCar? car = FindVehicle(vehicleName);
        if (car == null || (sender != 0 && sender != car.DriverPeerId))
        {
            return;
        }

        car.ApplyNetworkTransform(position, yaw);
        Rpc(nameof(ApplyVehicleTransformRpc), vehicleName, position, yaw);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ApplyVehicleTransformRpc(string vehicleName, Vector3 position, float yaw)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        FindVehicle(vehicleName)?.ApplyNetworkTransform(position, yaw);
    }

    private DriveableCar? FindVehicle(string vehicleName)
    {
        foreach (Node node in GetTree().GetNodesInGroup("vehicles"))
        {
            if (node is DriveableCar car && car.Name == vehicleName)
            {
                return car;
            }
        }

        return null;
    }

    // ---------- 语音中继 ----------

    public void SendVoiceFrame(byte[] data)
    {
        if (!HasNetworkPeerPublic())
        {
            return;
        }

        if (Multiplayer.IsServer())
        {
            Rpc(nameof(ClientVoiceRpc), 1, data);
        }
        else
        {
            RpcId(1, nameof(ServerVoiceRelay), data);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ServerVoiceRelay(byte[] data)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender == 0)
        {
            return;
        }

        _voice?.Receive(sender, data);
        Rpc(nameof(ClientVoiceRpc), sender, data);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ClientVoiceRpc(int peerId, byte[] data)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != 1)
        {
            return;
        }

        _voice?.Receive(peerId, data);
    }

    // ---------- 阵营 ----------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerSelectTeam(int peerId, int teamInt)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyTeamRpc), peerId, teamInt);
        }
        else
        {
            ApplyTeamRpc(peerId, teamInt);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyTeamRpc(int peerId, int teamInt)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        PlayerTeam team = (PlayerTeam)teamInt;
        _pendingTeams[peerId] = team;

        if (_players.TryGetValue(peerId, out PlayerController? player))
        {
            player.SetTeam(team);
            player.SpawnPoint = GetSpawnPoint(peerId, team);
            player.TeleportTo(player.SpawnPoint);
        }
    }

    // ---------- 银行行为 ----------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerBankAction(int peerId, int actionInt)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        if (!_players.TryGetValue(peerId, out PlayerController? player))
        {
            return;
        }

        (string key, int arg) = HandleBankAction(player, (BankActionType)actionInt);
        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyBankStatusRpc), key, arg);
        }
        else
        {
            ApplyBankStatusRpc(key, arg);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyBankStatusRpc(string key, int arg)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        SetLocalStatus(Loc.TF(key, arg));
    }

    private (string, int) HandleBankAction(PlayerController player, BankActionType action)
    {
        HeistGameMode? mode = HeistService.Instance?.GameMode;
        if (mode == null)
        {
            return ("st.unknown", 0);
        }

        // 可用性统一裁决（与交互点提示同源），通过后各 case 只做效果
        if (!BankActionRules.Check(action, player, mode, out string deniedKey))
        {
            return (deniedKey, 0);
        }

        switch (action)
        {
            case BankActionType.EnterBank:
                player.SetInsideBank(true);
                player.SetHasWeapon(false);
                player.TeleportTo(MapLocations.LobbyInside);
                SetMainDoorClosed(true);
                if (HasNetworkPeerPublic() && Multiplayer.IsServer())
                {
                    Rpc(nameof(SyncDoorStateRpc), true);
                }

                _fx?.PlayDoorLock(MapLocations.FrontDoorOutside);
                mode.BeginLockdown();
                return ("st.enterbank", 0);

            case BankActionType.ChangeDisguise:
                player.ChangeDisguise();
                player.TeleportTo(MapLocations.RobberSpawn(player.PeerId));
                return ("st.disguise", player.DisguiseLevel);

            case BankActionType.PoliceShoutDown:
                return HandlePoliceShoutDown(player, mode);

            case BankActionType.Negotiate:
                if (!mode.RecordNegotiation(player.Team))
                {
                    return ("st.negotiate.cd", 0);
                }

                _fx?.PlaySirenPulse(player.GlobalPosition);
                return (player.Team == PlayerTeam.Police ? "st.negotiate.police" : "st.negotiate.robber", 0);

            case BankActionType.VaultLoot:
                player.SetCarryingLoot(true);
                player.SetHasWeapon(true);
                mode.NotifyLootTaken();
                _fx?.PlaySirenPulse(player.GlobalPosition);
                return ("st.vault.taken", 0);

            case BankActionType.ReleaseHostage:
                if (mode.ReleaseHostage())
                {
                    MarkHostage(released: true);
                    _fx?.PlayHostageEvent(player.GlobalPosition, released: true);
                    return ("st.host.release", 0);
                }

                return ("st.host.release.none", 0);

            case BankActionType.KillHostage:
                if (mode.KillHostage())
                {
                    MarkHostage(released: false);
                    _fx?.PlayHostageEvent(player.GlobalPosition, released: false);
                    return ("st.host.kill", 0);
                }

                return ("st.host.kill.none", 0);

            case BankActionType.FakeHostage:
                player.SetFakeHostage(true);
                mode.DisguiseAsHostage();
                return ("st.fake.done", 0);

            case BankActionType.RecordMessage:
                mode.PrepareRecording();
                return ("st.record.done", 0);

            case BankActionType.PoliceEntryFront:
                return HandlePoliceEntry(player, mode, "front", MapLocations.FrontDoorInside, MapLocations.FrontDoorOutside);

            case BankActionType.PoliceEntrySide:
                return HandlePoliceEntry(player, mode, "side", MapLocations.SideDoorInside, MapLocations.SideDoorOutside);

            case BankActionType.PoliceEntryBack:
                return HandlePoliceEntry(player, mode, "back", MapLocations.BackDoorInside, MapLocations.BackDoorOutside);

            case BankActionType.BlockSideExit:
                mode.BlockRoute(EscapeRoute.SideDoor);
                player.TeleportTo(MapLocations.BlockSidePos);
                _fx?.PlaySirenPulse(player.GlobalPosition);
                return ("st.block.side", 0);

            case BankActionType.BlockSewerExit:
                mode.BlockRoute(EscapeRoute.Sewer);
                player.TeleportTo(MapLocations.BlockSewerPos);
                _fx?.PlaySirenPulse(player.GlobalPosition);
                return ("st.block.sewer", 0);

            case BankActionType.EscapeSideDoor:
                return HandleRobberEscape(player, mode, EscapeRoute.SideDoor, MapLocations.SideEscapeExit);

            case BankActionType.EscapeSewer:
                return HandleRobberEscape(player, mode, EscapeRoute.Sewer, MapLocations.SewerExit);
        }

        return ("st.unknown", 0);
    }

    private (string, int) HandlePoliceShoutDown(PlayerController police, HeistGameMode mode)
    {
        double now = Time.GetUnixTimeFromSystem();
        if (_shoutCooldownEnds.TryGetValue(police.PeerId, out double ends) && now < ends)
        {
            return ("st.shout.cd", 0);
        }

        _shoutCooldownEnds[police.PeerId] = now + Balance.ShoutDownCooldownSeconds;

        PlayerController? robber = FindClosestPlayer(PlayerTeam.Robber, police.GlobalPosition, p => !p.IsInsideBank);
        if (robber == null)
        {
            return ("st.shout.none", 0);
        }

        robber.ChangeDisguise();
        robber.TeleportTo(MapLocations.RobberSpawn(robber.PeerId));
        return ("st.shout.hit", robber.PeerId);
    }

    private (string, int) HandlePoliceEntry(PlayerController police, HeistGameMode mode, string route, Vector3 inside, Vector3 outside)
    {
        if (RobberCoversEntry(inside))
        {
            police.TeleportTo(outside);
            return ($"st.entry.pushed.{route}", 0);
        }

        police.TeleportTo(inside);
        police.SetInsideBank(true);
        return ($"st.entry.ok.{route}", 0);
    }

    private (string, int) HandleRobberEscape(PlayerController robber, HeistGameMode mode, EscapeRoute route, Vector3 exitPosition)
    {
        if (mode.BlockedRoute == route && !robber.HasWeapon)
        {
            bool carryingLoot = robber.CarryingLoot;
            robber.ApplyArrest();
            mode.ArrestRobber(endsRound: carryingLoot);
            return ("st.escape.arrested", 0);
        }

        bool broke = mode.BlockedRoute == route;
        robber.TeleportTo(exitPosition);
        robber.SetInsideBank(false);
        robber.SetCarryingLoot(false);
        mode.CompleteEscape(route);

        return route == EscapeRoute.Sewer
            ? (broke ? "st.escape.sewer.broke" : "st.escape.sewer", 0)
            : (broke ? "st.escape.side.broke" : "st.escape.side", 0);
    }

    // ---------- 射击 ----------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ServerWeaponFire(int shooterPeerId, int hitPeerId)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != shooterPeerId)
        {
            return;
        }

        if (!_players.TryGetValue(shooterPeerId, out PlayerController? shooter) || !shooter.HasWeapon)
        {
            return;
        }

        if (hitPeerId == shooterPeerId || (hitPeerId != 0 && !_players.ContainsKey(hitPeerId)))
        {
            hitPeerId = 0;
        }

        int damage = shooter.WeaponDamage;
        if (HasNetworkPeerPublic())
        {
            Rpc(nameof(ApplyShotResultRpc), shooterPeerId, hitPeerId, damage);
        }
        else
        {
            ApplyShotResultRpc(shooterPeerId, hitPeerId, damage);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ApplyShotResultRpc(int shooterPeerId, int hitPeerId, int damage)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        PlayerController? shooter = FindPlayerPublic(shooterPeerId);
        shooter?.ShowShotFeedback(hitPeerId != 0);
        if (shooter != null)
        {
            _fx?.PlayGunshot(shooter.GlobalPosition + new Vector3(0, 1.35f, 0), hitPeerId != 0);
        }

        if (hitPeerId == 0)
        {
            return;
        }

        PlayerController? target = FindPlayerPublic(hitPeerId);
        if (target == null)
        {
            return;
        }

        if (shooter?.Team == PlayerTeam.Police && target.Team == PlayerTeam.Robber && !target.HasWeapon)
        {
            // 平衡：普通劫匪被捕只计分；携带金库目标的劫匪被捕直接终结回合
            bool carryingLoot = target.CarryingLoot;
            target.ApplyArrest();
            if (MultiplayerGuard.IsServerOrOffline(Multiplayer))
            {
                HeistService.Instance?.GameMode?.ArrestRobber(endsRound: carryingLoot);
            }

            SetLocalStatus(Loc.TF("st.arrest.player", hitPeerId, Balance.ScoreArrest));
        }
        else
        {
            // 击杀加分：跨阵营击倒才计
            bool lethal = target.Health <= damage;
            target.ApplyDamage(damage, shooterPeerId);

            if (lethal && shooter != null && shooter.Team != target.Team)
            {
                if (MultiplayerGuard.IsServerOrOffline(Multiplayer))
                {
                    HeistService.Instance?.GameMode?.AddKillScore(shooter.Team);
                }

                SetLocalStatus(Loc.TF(
                    target.Team == PlayerTeam.Robber ? "st.kill.robber" : "st.kill.police",
                    hitPeerId));
            }
        }
    }

    // ---------- 世界辅助 ----------

    private void SetMainDoorClosed(bool closed, bool immediate = false)
    {
        foreach (Node node in GetTree().GetNodesInGroup("bank_main_doors"))
        {
            if (node is BankDoor door)
            {
                door.SetClosed(closed, immediate);
            }
        }
    }

    private void MarkHostage(bool released)
    {
        foreach (Node node in GetTree().GetNodesInGroup("hostages"))
        {
            if (node is BankNpc hostage && hostage.IsAvailableHostage)
            {
                if (released)
                {
                    hostage.MarkReleased();
                }
                else
                {
                    hostage.MarkKilled();
                }

                return;
            }
        }
    }

    private bool RobberCoversEntry(Vector3 entryPosition)
    {
        foreach (PlayerController robber in _players.Values)
        {
            if (robber.Team == PlayerTeam.Robber &&
                robber.IsInsideBank &&
                !robber.IsFakeHostage &&
                robber.GlobalPosition.DistanceTo(entryPosition) < Balance.EntryCoverRadius)
            {
                return true;
            }
        }

        return false;
    }

    private PlayerController? FindClosestPlayer(PlayerTeam team, Vector3 origin, Func<PlayerController, bool>? predicate = null)
    {
        PlayerController? best = null;
        float bestDistance = 10.0f;

        foreach (PlayerController player in _players.Values)
        {
            if (player.Team != team || predicate?.Invoke(player) == false)
            {
                continue;
            }

            float distance = origin.DistanceTo(player.GlobalPosition);
            if (distance < bestDistance)
            {
                best = player;
                bestDistance = distance;
            }
        }

        return best;
    }

    // ---------- 网络生命周期 ----------

    private void ConnectMultiplayerSignals()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    private void OnConnectedToServer()
    {
        SetLocalStatus(Loc.TF("st.connected", Multiplayer.GetUniqueId()));
        SubmitLocalIdentity();
        ShowTeamPanel();
    }

    private void OnConnectionFailed()
    {
        ReturnToMainMenu(Loc.T("st.connfail"));
    }

    private void OnServerDisconnected()
    {
        ReturnToMainMenu(Loc.T("st.serverlost"));
    }

    private void OnPeerConnected(long peerId)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int newPeerId = checked((int)peerId);

        foreach (int existingPeerId in _players.Keys)
        {
            RpcId(newPeerId, nameof(SpawnPlayerRpc), existingPeerId);
            if (_pendingTeams.TryGetValue(existingPeerId, out PlayerTeam team))
            {
                RpcId(newPeerId, nameof(ApplyTeamRpc), existingPeerId, (int)team);
            }
            if (_playerNames.TryGetValue(existingPeerId, out string? name))
            {
                RpcId(newPeerId, nameof(ApplyPlayerNameRpc), existingPeerId, name);
            }
            if (_players[existingPeerId].CurrentVehicle is DriveableCar car)
            {
                RpcId(newPeerId, nameof(ApplyEnterVehicleRpc), existingPeerId, car.Name);
            }
        }

        Rpc(nameof(SpawnPlayerRpc), newPeerId);
        HeistService.Instance?.GameMode?.BroadcastState();
        SendDoorStateToPeer(newPeerId);
        ScheduleDoorStateResend(newPeerId);
        SetLocalStatus(Loc.TF("st.peerjoin", newPeerId));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    public void SyncDoorStateRpc(bool closed)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        SetMainDoorClosed(closed, immediate: true);
    }

    private void SendDoorStateToPeer(int peerId)
    {
        if (!HasNetworkPeerPublic() || !Multiplayer.IsServer() || !IsPeerConnected(peerId))
        {
            return;
        }

        RpcId(peerId, nameof(SyncDoorStateRpc), CurrentMainDoorClosedState());
    }

    private void ScheduleDoorStateResend(int peerId)
    {
        GetTree().CreateTimer(0.25).Timeout += () => SendDoorStateToPeer(peerId);
    }

    private bool CurrentMainDoorClosedState()
    {
        return HeistService.Instance?.GameMode?.DoorsLocked ?? IsMainDoorClosed();
    }

    private bool IsPeerConnected(int peerId)
    {
        if (!HasNetworkPeerPublic())
        {
            return peerId == OfflinePeerId;
        }

        foreach (int connectedPeer in Multiplayer.GetPeers())
        {
            if (connectedPeer == peerId)
            {
                return true;
            }
        }

        return peerId == Multiplayer.GetUniqueId();
    }

    private bool IsMainDoorClosed()
    {
        foreach (Node node in GetTree().GetNodesInGroup("bank_main_doors"))
        {
            if (node is BankDoor door)
            {
                return door.IsClosed;
            }
        }

        return false;
    }

    private void OnPeerDisconnected(long peerId)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        Rpc(nameof(DespawnPlayerRpc), checked((int)peerId));
        SetLocalStatus(Loc.TF("st.peerleft", peerId));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SpawnPlayerRpc(int peerId)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        SpawnPlayer(peerId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void DespawnPlayerRpc(int peerId)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        if (!_players.TryGetValue(peerId, out PlayerController? player))
        {
            return;
        }

        _players.Remove(peerId);
        player.QueueFree();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ServerReceivePlayerTransform(int peerId, Vector3 position, Vector3 rotation, float headPitch)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            return;
        }

        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (sender != 0 && sender != peerId)
        {
            return;
        }

        ApplyTransform(peerId, position, rotation, headPitch);
        Rpc(nameof(ReceivePlayerTransform), peerId, position, rotation, headPitch);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ReceivePlayerTransform(int peerId, Vector3 position, Vector3 rotation, float headPitch)
    {
        int sender = MultiplayerGuard.RemoteSenderIdOrZero(Multiplayer);
        if (HasNetworkPeerPublic() && sender != 0 && sender != 1)
        {
            return;
        }

        ApplyTransform(peerId, position, rotation, headPitch);
    }

    private void ApplyTransform(int peerId, Vector3 position, Vector3 rotation, float headPitch)
    {
        if (_players.TryGetValue(peerId, out PlayerController? player))
        {
            player.SetRemoteState(position, rotation, headPitch);
        }
    }

    // ---------- 玩家生成 ----------

    private void SpawnPlayer(int peerId)
    {
        if (PlayerScene == null || _playersRoot == null || _players.ContainsKey(peerId))
        {
            return;
        }

        var player = PlayerScene.Instantiate<PlayerController>();
        player.Name = $"Player_{peerId}";
        player.PeerId = peerId;
        PlayerTeam team = _pendingTeams.TryGetValue(peerId, out PlayerTeam pendingTeam)
            ? pendingTeam
            : PlayerTeam.Robber;
        player.SpawnPoint = GetSpawnPoint(peerId, team);

        _playersRoot.AddChild(player, true);
        _players[peerId] = player;
        player.SetTeam(team);
        player.SetDisplayName(NameForPeer(peerId));
    }

    private void ClearPlayers()
    {
        foreach (PlayerController player in _players.Values)
        {
            player.QueueFree();
        }

        _players.Clear();
    }

    private Vector3 GetSpawnPoint(int peerId, PlayerTeam team)
    {
        return team == PlayerTeam.Police
            ? MapLocations.PoliceSpawn(peerId)
            : MapLocations.RobberSpawn(peerId);
    }

    private void CloseNetworkPeer()
    {
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
    }

    private void SetLocalStatus(string text)
    {
        GD.Print(text);
        _hud?.SetStatus(text);
    }

    // ---------- 输入映射 ----------

    private static void EnsureDefaultInputMap()
    {
        AddKeyAction("move_forward", Key.W);
        AddKeyAction("move_back", Key.S);
        AddKeyAction("move_left", Key.A);
        AddKeyAction("move_right", Key.D);
        AddKeyAction("jump", Key.Space);
        AddKeyAction("sprint", Key.Shift);
        AddMouseAction("fire", MouseButton.Left);
        AddKeyAction("release_mouse", Key.Escape);
        AddKeyAction("interact", Key.E);
        AddKeyAction("ability", Key.F);
        AddKeyAction("context_alt", Key.Q);
        AddKeyAction("voice_ptt", Key.V);
    }

    private static void AddKeyAction(string action, Key key)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void AddMouseAction(string action, MouseButton button)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }
}
