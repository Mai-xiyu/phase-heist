using Godot;

namespace PhaseHeist;

/// <summary>
/// 银行交互点：近距离按 E 触发（无需瞄准）。
/// 视觉 = 地面圆盘 + 竖直光柱；对当前本地玩家不可用时自动变暗。
/// 可用性判定与服务端裁决共用 BankActionRules。
/// </summary>
public partial class BankObjective : Area3D, IInteractable
{
    [Export] public BankActionType ActionType { get; set; }
    [Export] public string PromptKey { get; set; } = string.Empty;
    [Export] public float InteractRadius { get; set; } = 1.6f;

    private Label3D? _label;
    private MeshInstance3D? _marker;
    private MeshInstance3D? _beam;
    private Color _baseColor = Colors.White;
    private double _visualRefreshAcc;
    private bool _lastAvailable = true;

    public override void _Ready()
    {
        AddToGroup("objectives");
        CollisionLayer = GameLayers.Interactable;
        CollisionMask = GameLayers.Player;

        _label = GetNodeOrNull<Label3D>("Label");
        _marker = GetNodeOrNull<MeshInstance3D>("Marker");

        if (_marker?.Mesh is CylinderMesh disc && disc.Material is StandardMaterial3D discMat)
        {
            _baseColor = discMat.AlbedoColor;
        }

        // 竖直光柱：远处可见
        _beam = new MeshInstance3D
        {
            Name = "Beam",
            Position = new Vector3(0, 1.3f, 0),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.10f,
                BottomRadius = 0.16f,
                Height = 2.6f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(_baseColor, 0.30f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    EmissionEnabled = true,
                    Emission = _baseColor,
                    EmissionEnergyMultiplier = 0.8f,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            },
        };
        AddChild(_beam);

        RefreshLabel();
        Loc.LanguageChanged += RefreshLabel;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshLabel;
    }

    public override void _Process(double delta)
    {
        // 0.2s 节流刷新可用性视觉
        _visualRefreshAcc += delta;
        if (_visualRefreshAcc < 0.2)
        {
            return;
        }

        _visualRefreshAcc = 0;
        PlayerController? local = PlayerController.Local;
        bool available = local != null && IsAvailableFor(local);
        if (available == _lastAvailable)
        {
            return;
        }

        _lastAvailable = available;
        ApplyAvailabilityVisual(available);
    }

    public bool IsAvailableFor(PlayerController player)
    {
        HeistGameMode? mode = HeistService.Instance?.GameMode;
        if (mode == null || mode.CurrentPhase is HeistPhase.Scored or HeistPhase.Failed)
        {
            return false;
        }

        return BankActionRules.Check(ActionType, player, mode, out _);
    }

    public bool CanInteract(Node3D actor)
    {
        return actor is PlayerController player && IsAvailableFor(player);
    }

    public void Interact(Node3D actor)
    {
        if (actor is not PlayerController player)
        {
            return;
        }

        if (GetTree().CurrentScene is Game game)
        {
            game.RequestBankAction(player.PeerId, ActionType);
        }
    }

    public string GetInteractPrompt()
    {
        return string.IsNullOrWhiteSpace(PromptKey) ? $"{ActionType}" : Loc.T(PromptKey);
    }

    private void ApplyAvailabilityVisual(bool available)
    {
        float emission = available ? 0.8f : 0.06f;
        float alpha = available ? 0.30f : 0.07f;

        if (_beam?.Mesh is CylinderMesh beamMesh && beamMesh.Material is StandardMaterial3D beamMat)
        {
            beamMat.EmissionEnergyMultiplier = emission;
            beamMat.AlbedoColor = new Color(_baseColor, alpha);
        }

        if (_marker?.Mesh is CylinderMesh discMesh && discMesh.Material is StandardMaterial3D discMat)
        {
            discMat.EmissionEnergyMultiplier = available ? 0.4f : 0.05f;
        }

        if (_label != null)
        {
            _label.Modulate = available ? Colors.White : new Color(1, 1, 1, 0.35f);
        }
    }

    private void RefreshLabel()
    {
        if (_label != null)
        {
            _label.Text = GetInteractPrompt();
        }
    }
}
