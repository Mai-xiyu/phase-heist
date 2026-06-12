using Godot;

namespace PhaseHeist.UI;

public partial class AuiToastDeck : VBoxContainer
{
    public override void _Ready()
    {
        Name = "AuiToastDeck";
        MouseFilter = Control.MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
    }

    public void ShowToast(string message, AuiTone tone = AuiTone.Neutral, double seconds = 3.0)
    {
        var toast = Aui.Panel(tone, true, "Toast");
        toast.CustomMinimumSize = new Vector2(320, 0);
        toast.AddChild(Aui.Text(message, 13, AuiTheme.Ink, true, wrap: true));
        AddChild(toast);

        SceneTreeTimer timer = GetTree().CreateTimer(seconds);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(toast))
            {
                toast.QueueFree();
            }
        };
    }
}

