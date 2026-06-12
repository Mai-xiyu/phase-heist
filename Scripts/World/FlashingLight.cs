using Godot;

namespace PhaseHeist;

/// <summary>警灯交替闪烁：驱动父节点下名为 LightBlue / LightRed 的发光网格。</summary>
public partial class FlashingLight : Node
{
    [Export] public float Period { get; set; } = 0.9f;

    private StandardMaterial3D? _blue;
    private StandardMaterial3D? _red;
    private double _t;

    public override void _Ready()
    {
        if (GetParent() is not Node3D parent)
        {
            return;
        }

        _blue = (parent.GetNodeOrNull<MeshInstance3D>("LightBlue")?.MaterialOverride) as StandardMaterial3D;
        _red = (parent.GetNodeOrNull<MeshInstance3D>("LightRed")?.MaterialOverride) as StandardMaterial3D;
    }

    public override void _Process(double delta)
    {
        if (_blue == null || _red == null)
        {
            return;
        }

        _t += delta;
        bool phase = _t % Period < Period * 0.5;
        _blue.EmissionEnergyMultiplier = phase ? 1.8f : 0.15f;
        _red.EmissionEnergyMultiplier = phase ? 0.15f : 1.8f;
    }
}
