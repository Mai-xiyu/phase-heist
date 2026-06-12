using Godot;
using System;
using PhaseHeist;

public partial class PlayerController : CharacterBody3D
{
    private const float Gravity = 14.0f;
    private const float WalkSpeed = 4.2f;
    private const float SprintSpeed = 6.4f;
    private const float JumpVelocity = 4.8f;
    private const float InteractRange = 3.0f;

    [Export] public int MaxHealth { get; set; } = 100;

    /// <summary>本地受控玩家实例（HUD / 交互点可用性查询用）。</summary>
    public static PlayerController? Local { get; private set; }

    public int PeerId { get; set; } = 1;
    public Vector3 SpawnPoint { get; set; } = Vector3.Zero;
    public int Health { get; private set; }
    public PlayerTeam Team { get; private set; } = PlayerTeam.Robber;
    public string DisplayName { get; private set; } = "Player";
    public bool IsLocalControlled { get; private set; }
    public bool HasWeapon { get; private set; }
    public bool IsInsideBank { get; private set; }
    public bool IsFakeHostage { get; private set; }
    public bool IsArrested { get; private set; }
    public bool CarryingLoot { get; private set; }
    public int DisguiseLevel { get; private set; }
    public int WeaponDamage => _weapon?.Damage ?? Balance.WeaponDamage;
    public bool IsSpeaking => Time.GetUnixTimeFromSystem() < _speakUntil;

    /// <summary>当前范围内最近的可用交互对象（HUD 提示 + E 键触发）。</summary>
    public IInteractable? CurrentInteractable { get; private set; }

    /// <summary>正在驾驶的载具；null = 步行。</summary>
    public DriveableCar? CurrentVehicle { get; private set; }
    public bool InVehicle => CurrentVehicle != null;

    private Node3D? _head;
    private Camera3D? _camera;
    private RayCast3D? _aimRay;
    private SampleWeapon? _weapon;
    private Label3D? _nameTag;
    private Node3D? _modelRoot;
    private HumanRig? _rig;
    private Game? _game;
    private Vector3 _remoteTargetPosition;
    private Vector3 _remoteTargetRotation;
    private float _remoteHeadPitch;
    private double _speakUntil;
    private float _walkPhase;

    public override void _Ready()
    {
        AddToGroup("players");
        Health = MaxHealth;
        GlobalPosition = SpawnPoint;
        _remoteTargetPosition = GlobalPosition;
        _remoteTargetRotation = Rotation;

        CollisionLayer = GameLayers.Player;
        CollisionMask = GameLayers.World;

        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _aimRay = GetNode<RayCast3D>("Head/Camera3D/AimRay");
        _weapon = GetNode<SampleWeapon>("Head/Camera3D/Weapon");
        _nameTag = GetNode<Label3D>("NameTag");
        _modelRoot = GetNode<Node3D>("Model");
        _game = GetTree().CurrentScene as Game;

        _rig = PlayerModelBuilder.Build(_modelRoot, PaletteForState());

        SetMultiplayerAuthority(PeerId);
        _weapon!.Configure(this, _aimRay!);
        RefreshLocalState();
        UpdateNameTag();
        Loc.LanguageChanged += UpdateNameTag;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= UpdateNameTag;

        if (Local == this)
        {
            Local = null;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsLocalControlled || Game.UiBlocking)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured && _head != null)
        {
            float sensitivity = AppState.Instance?.MouseSensitivity ?? 0.0025f;
            RotateY(-motion.Relative.X * sensitivity);
            float nextPitch = Mathf.Clamp(_head.Rotation.X - motion.Relative.Y * sensitivity, -1.35f, 1.35f);
            _head.Rotation = new Vector3(nextPitch, 0, 0);
        }

        if (@event.IsActionPressed("interact"))
        {
            if (InVehicle)
            {
                _game?.RequestExitVehicle(PeerId);
            }
            else
            {
                TryInteract();
            }
        }

        if (@event.IsActionPressed("ability") || @event.IsActionPressed("context_alt"))
        {
            if (!InVehicle)
            {
                TryContextAction();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        RefreshLocalState();

        if (InVehicle)
        {
            // 乘车：跟随载具，挂起步行逻辑
            GlobalPosition = CurrentVehicle!.GlobalPosition + Vector3.Up * 0.4f;
            Velocity = Vector3.Zero;

            if (IsLocalControlled)
            {
                SendTransformSnapshot();
            }

            return;
        }

        if (IsLocalControlled)
        {
            ProcessLocalMovement(delta);
            ProcessLocalWeapon();
            ScanNearbyInteractable();
            SendTransformSnapshot();
        }
        else
        {
            InterpolateRemoteState(delta);
        }

        AnimateModel(delta);
    }

    // ---------- 载具 ----------

    public void EnterVehicle(DriveableCar car)
    {
        CurrentVehicle = car;
        CurrentInteractable = null;
        SetCollisionDisabled(true);

        if (IsLocalControlled)
        {
            car.ActivateCamera(true);
        }

        if (_nameTag != null)
        {
            _nameTag.Visible = false;
        }
    }

    public void ExitVehicle(DriveableCar car)
    {
        CurrentVehicle = null;
        SetCollisionDisabled(false);
        TeleportTo(car.ExitPosition());

        if (IsLocalControlled)
        {
            car.ActivateCamera(false);
        }

        if (_nameTag != null)
        {
            _nameTag.Visible = true;
        }
    }

    private void SetCollisionDisabled(bool disabled)
    {
        if (GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is CollisionShape3D shape)
        {
            shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, disabled);
        }
    }

    // ---------- 状态设置 ----------

    public void SetDisplayName(string name)
    {
        DisplayName = name;
        UpdateNameTag();
    }

    public void SetTeam(PlayerTeam team)
    {
        Team = team;
        HasWeapon = team == PlayerTeam.Police;
        IsFakeHostage = false;
        IsArrested = false;
        RefreshVisuals();
    }

    public void SetInsideBank(bool insideBank)
    {
        IsInsideBank = insideBank;
        UpdateNameTag();
    }

    public void SetHasWeapon(bool hasWeapon)
    {
        HasWeapon = hasWeapon;
        RefreshVisuals();
    }

    public void SetFakeHostage(bool fakeHostage)
    {
        IsFakeHostage = fakeHostage;
        RefreshVisuals();
    }

    public void SetCarryingLoot(bool carrying)
    {
        CarryingLoot = carrying;
        UpdateNameTag();
    }

    public void ChangeDisguise()
    {
        if (Team != PlayerTeam.Robber)
        {
            return;
        }

        DisguiseLevel++;
        IsFakeHostage = false;
        HasWeapon = false;
        RefreshVisuals();
    }

    public void ApplyArrest()
    {
        IsArrested = true;
        Health = MaxHealth;
        CarryingLoot = false;
        HasWeapon = false;
        IsInsideBank = false;
        TeleportTo(MapLocations.PoliceSpawn(PeerId) + new Vector3(0, 0, 2.0f));
        RefreshVisuals();
    }

    public void TeleportTo(Vector3 position)
    {
        GlobalPosition = position;
        Velocity = Vector3.Zero;
        _remoteTargetPosition = position;
    }

    public void ApplyDamage(int damage, int sourcePeerId)
    {
        if (damage <= 0 || Health <= 0)
        {
            return;
        }

        Health = Math.Max(Health - damage, 0);
        UpdateNameTag();

        if (Health == 0)
        {
            Respawn();
        }
    }

    public void ShowShotFeedback(bool hit)
    {
        _weapon?.ShowMuzzleFlash(hit);
    }

    public void NotifySpeaking()
    {
        _speakUntil = Time.GetUnixTimeFromSystem() + 0.4;
    }

    public void SetRemoteState(Vector3 position, Vector3 rotation, float headPitch)
    {
        _remoteTargetPosition = position;
        _remoteTargetRotation = rotation;
        _remoteHeadPitch = headPitch;
    }

    public void RequestFire(int hitPeerId)
    {
        _game?.RequestWeaponFire(PeerId, hitPeerId);
    }

    // ---------- 本地驱动 ----------

    private void ProcessLocalMovement(double delta)
    {
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * (float)delta;
        }
        else if (!Game.UiBlocking && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Vector2 input = Game.UiBlocking
            ? Vector2.Zero
            : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();

        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        velocity.X = direction.X * speed;
        velocity.Z = direction.Z * speed;
        Velocity = velocity;
        MoveAndSlide();
    }

    private void ProcessLocalWeapon()
    {
        if (_weapon != null && HasWeapon && !Game.UiBlocking && Input.IsActionPressed("fire"))
        {
            _weapon.TryFire();
        }
    }

    /// <summary>近距交互：扫描范围内最近且可用的交互对象（目标点 + 载具），无需瞄准。</summary>
    private void ScanNearbyInteractable()
    {
        IInteractable? best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = GlobalPosition;

        ScanGroup("objectives", origin, ref best, ref bestDistance);
        ScanGroup("vehicles", origin, ref best, ref bestDistance);
        ScanGroup("hostages", origin, ref best, ref bestDistance);

        CurrentInteractable = best;
    }

    private void ScanGroup(string group, Vector3 origin, ref IInteractable? best, ref float bestDistance)
    {
        foreach (Node node in GetTree().GetNodesInGroup(group))
        {
            if (node is not Node3D node3d || node is not IInteractable interactable)
            {
                continue;
            }

            float reach = interactable.InteractRadius + Balance.InteractExtraReach;
            float distance = origin.DistanceTo(node3d.GlobalPosition);
            if (distance > reach || distance >= bestDistance)
            {
                continue;
            }

            if (!interactable.CanInteract(this))
            {
                continue;
            }

            best = interactable;
            bestDistance = distance;
        }
    }

    private void TryInteract()
    {
        CurrentInteractable?.Interact(this);
    }

    private void TryContextAction()
    {
        HeistGameMode? mode = HeistService.Instance?.GameMode;
        if (Team == PlayerTeam.Police)
        {
            _game?.RequestBankAction(PeerId, mode?.DoorsLocked == true
                ? BankActionType.Negotiate
                : BankActionType.PoliceShoutDown);
            return;
        }

        if (mode?.DoorsLocked != true)
        {
            _game?.RequestBankAction(PeerId, BankActionType.ChangeDisguise);
            return;
        }

        _game?.RequestBankAction(PeerId, IsFakeHostage
            ? BankActionType.RecordMessage
            : BankActionType.FakeHostage);
    }

    private void SendTransformSnapshot()
    {
        if (_head == null)
        {
            return;
        }

        _game?.SendPlayerTransform(PeerId, GlobalPosition, Rotation, _head.Rotation.X);
    }

    private void InterpolateRemoteState(double delta)
    {
        GlobalPosition = GlobalPosition.Lerp(_remoteTargetPosition, 12.0f * (float)delta);
        Rotation = Rotation.Lerp(_remoteTargetRotation, 12.0f * (float)delta);

        if (_head != null)
        {
            _head.Rotation = new Vector3(Mathf.Lerp(_head.Rotation.X, _remoteHeadPitch, 12.0f * (float)delta), 0, 0);
        }
    }

    private void AnimateModel(double delta)
    {
        if (_rig == null)
        {
            return;
        }

        Vector3 horizontal = IsLocalControlled
            ? new Vector3(Velocity.X, 0, Velocity.Z)
            : (_remoteTargetPosition - GlobalPosition) with { Y = 0 } * 12.0f;
        float speed = horizontal.Length();
        float speed01 = Mathf.Clamp(speed / SprintSpeed, 0.0f, 1.0f);
        _walkPhase += speed * (float)delta * 2.4f;

        PlayerModelBuilder.AnimateWalk(_rig, _walkPhase, speed01);
        _rig.SpeakDot.Visible = IsSpeaking;
    }

    private void Respawn()
    {
        Health = MaxHealth;
        TeleportTo(SpawnPoint + new Vector3(0, 0.2f, 0));
        CarryingLoot = false;

        // 平衡：劫匪死亡掉枪（需重新抢金库点装备）；警察保留配枪
        if (Team == PlayerTeam.Robber)
        {
            HasWeapon = false;
        }

        RefreshVisuals();
    }

    // ---------- 视觉 ----------

    private void RefreshVisuals()
    {
        _rig?.ApplyPalette(PaletteForState());
        UpdateNameTag();
    }

    private HumanPalette PaletteForState()
    {
        if (IsArrested)
        {
            return HumanPalette.Arrested;
        }

        if (IsFakeHostage)
        {
            return HumanPalette.FakeHostage;
        }

        return Team == PlayerTeam.Police
            ? HumanPalette.Police
            : HumanPalette.RobberDisguise(DisguiseLevel);
    }

    private void RefreshLocalState()
    {
        IsLocalControlled = IsLocalPlayer();

        if (IsLocalControlled)
        {
            Local = this;
        }

        if (_camera != null)
        {
            // 驾驶时让位给车载相机
            _camera.Current = IsLocalControlled && !InVehicle;
        }

        if (_modelRoot != null)
        {
            _modelRoot.Visible = !IsLocalControlled && !InVehicle;
        }

        if (_weapon != null)
        {
            _weapon.Visible = HasWeapon && IsLocalControlled && !InVehicle;
        }
    }

    private bool IsLocalPlayer()
    {
        if (Multiplayer.MultiplayerPeer == null)
        {
            return PeerId == 1;
        }

        return Multiplayer.GetUniqueId() == PeerId;
    }

    private void UpdateNameTag()
    {
        if (_nameTag == null)
        {
            return;
        }

        string you = IsLocalControlled ? $" {Loc.T("tag.you")}" : string.Empty;
        string team = Loc.T(Team == PlayerTeam.Police ? "team.police" : "team.robber");
        string weapon = Loc.T(HasWeapon ? "hud.gun" : "hud.nogun");
        string extra = IsFakeHostage
            ? Loc.T("tag.fake")
            : Team == PlayerTeam.Robber ? " " + Loc.TF("hud.disguise", DisguiseLevel) : string.Empty;
        string loot = CarryingLoot ? Loc.T("tag.loot") : string.Empty;
        string arrested = IsArrested ? Loc.T("tag.arrested") : string.Empty;

        _nameTag.Text = $"{DisplayName}{you}\n{team} HP {Health} {weapon}{extra}{loot}{arrested}";
    }
}
