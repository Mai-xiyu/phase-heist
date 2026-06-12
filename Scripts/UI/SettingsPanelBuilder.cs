using Godot;
using PhaseHeist.UI;

namespace PhaseHeist;

/// <summary>
/// 设置面板（主菜单与游戏内 ESC 菜单共用）。
/// 改动即时生效并写入 user://settings.cfg。
/// </summary>
public static class SettingsPanelBuilder
{
    public static void Build(VBoxContainer parent)
    {
        AppState app = AppState.Instance!;

        // 语言
        parent.AddChild(Aui.Text(Loc.T("set.language"), 13, AuiTheme.MutedInk));
        var langOption = new OptionButton { CustomMinimumSize = new Vector2(220, 30) };
        langOption.AddItem("中文", 0);
        langOption.AddItem("English", 1);
        langOption.Selected = Loc.Language == "en" ? 1 : 0;
        langOption.ItemSelected += idx =>
        {
            Loc.SetLanguage(idx == 1 ? "en" : "zh");
            app.NotifySettingsChanged();
        };
        parent.AddChild(langOption);

        // 鼠标灵敏度
        parent.AddChild(Aui.Text(Loc.T("set.sensitivity"), 13, AuiTheme.MutedInk));
        parent.AddChild(Slider(app.SensitivityRaw, 5, 100, v =>
        {
            app.SensitivityRaw = (float)v;
            app.NotifySettingsChanged();
        }));

        // 主音量
        parent.AddChild(Aui.Text(Loc.T("set.master"), 13, AuiTheme.MutedInk));
        parent.AddChild(Slider(app.MasterVolume, 0, 100, v =>
        {
            app.MasterVolume = (float)v;
            app.NotifySettingsChanged();
        }));

        // 语音音量
        parent.AddChild(Aui.Text(Loc.T("set.voice"), 13, AuiTheme.MutedInk));
        parent.AddChild(Slider(app.VoiceVolume, 0, 100, v =>
        {
            app.VoiceVolume = (float)v;
            app.NotifySettingsChanged();
        }));

        // 语音模式
        parent.AddChild(Aui.Text(Loc.T("set.voicemode"), 13, AuiTheme.MutedInk));
        var voiceOption = new OptionButton { CustomMinimumSize = new Vector2(220, 30) };
        voiceOption.AddItem(Loc.T("set.voicemode.off"), 0);
        voiceOption.AddItem(Loc.T("set.voicemode.ptt"), 1);
        voiceOption.AddItem(Loc.T("set.voicemode.open"), 2);
        voiceOption.Selected = (int)app.VoiceChatMode;
        voiceOption.ItemSelected += idx =>
        {
            app.VoiceChatMode = (VoiceMode)(int)idx;
            app.NotifySettingsChanged();
        };
        parent.AddChild(voiceOption);

        // 全屏
        var fullscreen = new CheckBox { Text = Loc.T("set.fullscreen"), ButtonPressed = app.Fullscreen };
        fullscreen.Toggled += pressed =>
        {
            app.Fullscreen = pressed;
            app.NotifySettingsChanged();
        };
        parent.AddChild(fullscreen);
    }

    private static HSlider Slider(double value, double min, double max, System.Action<double> changed)
    {
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Value = value,
            CustomMinimumSize = new Vector2(260, 22),
        };
        slider.ValueChanged += v => changed(v);
        return slider;
    }
}
