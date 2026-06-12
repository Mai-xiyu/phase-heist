using Godot;

namespace PhaseHeist;

/// <summary>对局内系统定位器。</summary>
public partial class HeistService : Node
{
    public static HeistService? Instance { get; private set; }

    public HeistGameMode? GameMode { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterGameMode(HeistGameMode mode) => GameMode = mode;
}
