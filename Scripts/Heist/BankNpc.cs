using Godot;

namespace PhaseHeist;

/// <summary>
/// 人群/人质 NPC：人形模型 + 轻量行为树式状态机（确定性 tick，无需网络同步）。
/// </summary>
public partial class BankNpc : Node3D
{
    private enum BrainState
    {
        Wander,
        Queue,
        HostageIdle,
        ReleasedFlee,
        Dead,
    }

    [Export] public BankNpcKind Kind { get; set; } = BankNpcKind.Civilian;
    [Export] public int Seed { get; set; }
    [Export] public float WanderRadius { get; set; } = 2.0f;

    private HumanRig? _rig;
    private Label3D? _label;
    private Vector3 _basePosition;
    private Vector3 _targetPosition;
    private float _phase;
    private double _nextThinkAt;
    private bool _alive = true;
    private bool _released;
    private BrainState _state;

    public bool IsAvailableHostage => Kind == BankNpcKind.Hostage && _alive && !_released;

    public override void _Ready()
    {
        AddToGroup(Kind == BankNpcKind.Hostage ? "hostages" : "crowd");
        _basePosition = GlobalPosition;
        _state = Kind == BankNpcKind.Hostage ? BrainState.HostageIdle : BrainState.Wander;
        _targetPosition = _basePosition;
        _nextThinkAt = 0.0;

        HumanPalette palette = Kind == BankNpcKind.Hostage
            ? HumanPalette.Hostage
            : HumanPalette.Civilian(Seed);
        _rig = PlayerModelBuilder.Build(this, palette);

        _label = new Label3D
        {
            Name = "Label",
            Position = new Vector3(0, 2.15f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 20,
        };
        AddChild(_label);

        RefreshVisual();
        Loc.LanguageChanged += RefreshVisual;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshVisual;
    }

    public override void _Process(double delta)
    {
        if (!_alive || _rig == null)
        {
            return;
        }

        double now = Time.GetUnixTimeFromSystem();
        if (now >= _nextThinkAt)
        {
            TickBrain(now);
        }

        switch (_state)
        {
            case BrainState.HostageIdle:
                _phase += (float)delta;
                PlayerModelBuilder.AnimateWalk(_rig, _phase * 0.6f, 0.05f);
                break;
            case BrainState.ReleasedFlee:
                MoveTowardTarget((float)delta, 3.0f);
                break;
            default:
                MoveTowardTarget((float)delta, _state == BrainState.Queue ? 1.2f : 1.6f);
                break;
        }
    }

    public void MarkReleased()
    {
        if (!_alive || _released)
        {
            return;
        }

        _released = true;
        _state = BrainState.ReleasedFlee;
        _targetPosition = ReleaseTarget();
        _nextThinkAt = Time.GetUnixTimeFromSystem() + 3.0;
        RefreshVisual();
    }

    public void MarkKilled()
    {
        if (!_alive)
        {
            return;
        }

        _alive = false;
        _state = BrainState.Dead;

        if (_rig != null)
        {
            _rig.Root.RotationDegrees = new Vector3(90, 0, 0);
            _rig.Root.Position = new Vector3(0, 0.2f, 0);
        }

        RefreshVisual();
    }

    private void TickBrain(double now)
    {
        _nextThinkAt = now + 0.9 + (Seed % 5) * 0.17;

        if (_state == BrainState.ReleasedFlee || _state == BrainState.Dead)
        {
            return;
        }

        if (Kind == BankNpcKind.Hostage)
        {
            _state = BrainState.HostageIdle;
            _targetPosition = _basePosition;
            return;
        }

        bool lockdown = HeistService.Instance?.GameMode?.DoorsLocked == true;
        if (lockdown && GlobalPosition.Z < 21.0f)
        {
            _state = BrainState.Wander;
            _targetPosition = new Vector3(
                _basePosition.X + ((Seed % 5) - 2) * 1.6f,
                _basePosition.Y,
                22.5f + (Seed % 4) * 1.2f);
            return;
        }

        if (Seed % 4 == 0)
        {
            _state = BrainState.Queue;
            _targetPosition = new Vector3(-7.0f + (Seed % 6) * 2.4f, _basePosition.Y, 11.0f + (Seed % 3) * 1.2f);
            return;
        }

        _state = BrainState.Wander;
        float ox = Mathf.Sin((float)now * 0.23f + Seed * 1.37f) * WanderRadius;
        float oz = Mathf.Cos((float)now * 0.19f + Seed * 2.11f) * WanderRadius;
        _targetPosition = _basePosition + new Vector3(ox, 0, oz);
    }

    private void MoveTowardTarget(float delta, float speed)
    {
        Vector3 desired = _targetPosition - GlobalPosition;
        desired.Y = 0.0f;
        Vector3 avoidance = AvoidPlayers();
        Vector3 move = desired + avoidance * 1.4f;
        move.Y = 0.0f;

        if (move.Length() < 0.05f)
        {
            PlayerModelBuilder.AnimateWalk(_rig!, _phase, 0.0f);
            return;
        }

        Vector3 dir = move.Normalized();
        GlobalPosition += dir * speed * delta;
        Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        _phase += delta * speed * 2.4f;
        PlayerModelBuilder.AnimateWalk(_rig!, _phase, Mathf.Clamp(speed / 3.0f, 0.15f, 0.8f));
    }

    private Vector3 AvoidPlayers()
    {
        if (!IsInsideTree())
        {
            return Vector3.Zero;
        }

        Vector3 avoid = Vector3.Zero;
        foreach (Node node in GetTree().GetNodesInGroup("players"))
        {
            if (node is not Node3D player)
            {
                continue;
            }

            Vector3 delta = GlobalPosition - player.GlobalPosition;
            delta.Y = 0.0f;
            float dist = delta.Length();
            if (dist is > 0.01f and < 1.4f)
            {
                avoid += delta.Normalized() * (1.4f - dist);
            }
        }

        return avoid;
    }

    private Vector3 ReleaseTarget()
    {
        return MapLocations.FrontDoorOutside + new Vector3(-8.0f + (Seed % 6) * 3.2f, 0, 12.0f + (Seed % 3));
    }

    private void RefreshVisual()
    {
        if (_rig != null)
        {
            if (!_alive)
            {
                _rig.ApplyPalette(HumanPalette.Arrested);
            }
            else if (_released)
            {
                var palette = HumanPalette.Hostage;
                palette.Accent = new Color(0.16f, 0.70f, 0.32f);
                palette.Suit = new Color(0.20f, 0.55f, 0.30f);
                _rig.ApplyPalette(palette);
            }
        }

        if (_label != null)
        {
            string key = !_alive ? "npc.dead" : _released ? "npc.released" : Kind == BankNpcKind.Hostage ? "npc.hostage" : "npc.civilian";
            _label.Text = Loc.T(key);
        }
    }
}
