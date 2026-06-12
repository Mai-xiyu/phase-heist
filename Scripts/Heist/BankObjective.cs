using Godot;

namespace PhaseHeist;

/// <summary>
/// 银行交互点：近距离按 E 触发（无需瞄准）。
/// 视觉 = 实体道具（ObjectiveProps）+ 地面细环 + 悬浮旋转菱形标记；
/// 对当前本地玩家不可用时整体变暗。
/// 可用性判定与服务端裁决共用 BankActionRules。
/// </summary>
public partial class BankObjective : Area3D, IInteractable
{
    [Export] public BankActionType ActionType { get; set; }
    [Export] public string PromptKey { get; set; } = string.Empty;
    [Export] public float InteractRadius { get; set; } = 1.6f;
    [Export] public Color BaseColor { get; set; } = new(0.9f, 0.9f, 0.9f);

    private Label3D? _label;
    private MeshInstance3D? _ring;
    private MeshInstance3D? _gem;
    private double _visualRefreshAcc;
    private bool _lastAvailable = true;
    private float _gemPhase;

    public override void _Ready()
    {
        AddToGroup("objectives");
        CollisionLayer = GameLayers.Interactable;
        CollisionMask = GameLayers.Player;

        _label = GetNodeOrNull<Label3D>("Label");

        // 地面细环（半径 0.55，替代大圆盘）
        _ring = new MeshInstance3D
        {
            Name = "Ring",
            Position = new Vector3(0, 0.03f, 0),
            Mesh = new TorusMesh
            {
                InnerRadius = 0.46f,
                OuterRadius = 0.56f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(BaseColor, 0.85f),
                    EmissionEnabled = true,
                    Emission = BaseColor,
                    EmissionEnergyMultiplier = 0.6f,
                },
            },
        };
        AddChild(_ring);

        // 悬浮菱形标记（旋转 + 上下浮动）
        _gem = new MeshInstance3D
        {
            Name = "Gem",
            Position = new Vector3(0, 1.45f, 0),
            RotationDegrees = new Vector3(45, 0, 45),
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.16f, 0.16f, 0.16f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = BaseColor,
                    EmissionEnabled = true,
                    Emission = BaseColor,
                    EmissionEnergyMultiplier = 1.0f,
                },
            },
        };
        AddChild(_gem);

        RefreshLabel();
        Loc.LanguageChanged += RefreshLabel;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshLabel;
    }

    public override void _Process(double delta)
    {
        // 菱形动画（仅可用时转动）
        if (_gem != null && _lastAvailable)
        {
            _gemPhase += (float)delta;
            _gem.RotationDegrees = new Vector3(45, _gemPhase * 70.0f, 45);
            _gem.Position = new Vector3(0, 1.45f + Mathf.Sin(_gemPhase * 2.2f) * 0.07f, 0);
        }

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
        if (_ring?.Mesh is TorusMesh ringMesh && ringMesh.Material is StandardMaterial3D ringMat)
        {
            ringMat.EmissionEnergyMultiplier = available ? 0.6f : 0.04f;
            ringMat.AlbedoColor = new Color(BaseColor, available ? 0.85f : 0.18f);
        }

        if (_gem != null)
        {
            _gem.Visible = available;
        }

        if (_label != null)
        {
            _label.Modulate = available ? Colors.White : new Color(1, 1, 1, 0.30f);
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
