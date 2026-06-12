using Godot;
using System;

namespace PhaseHeist;

public enum NetIntent
{
    None,
    Solo,
    Host,
    Join,
}

public enum VoiceMode
{
    Off = 0,
    PushToTalk = 1,
    OpenMic = 2,
}

/// <summary>
/// Autoload：设置持久化（user://settings.cfg）+ 主菜单到对局的启动意图。
/// </summary>
public partial class AppState : Node
{
    private const string ConfigPath = "user://settings.cfg";

    public static AppState? Instance { get; private set; }

    public string PlayerName { get; set; } = $"Player{GD.RandRange(100, 999)}";
    public string LanguageCode { get; set; } = "zh";
    public float SensitivityRaw { get; set; } = 50.0f;
    public float MasterVolume { get; set; } = 80.0f;
    public float VoiceVolume { get; set; } = 100.0f;
    public VoiceMode VoiceChatMode { get; set; } = VoiceMode.PushToTalk;
    public bool Fullscreen { get; set; }

    public NetIntent PendingIntent { get; set; } = NetIntent.None;
    public string PendingAddress { get; set; } = "127.0.0.1";
    public int PendingPort { get; set; } = 24565;
    public string LastNetworkError { get; set; } = string.Empty;

    public event Action? SettingsChanged;

    public float MouseSensitivity => Mathf.Clamp(SensitivityRaw, 5.0f, 100.0f) / 20000.0f;

    public override void _EnterTree()
    {
        Instance = this;
        Load();
        Loc.SetLanguage(LanguageCode);
        ApplyAudioVideo();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void NotifySettingsChanged()
    {
        LanguageCode = Loc.Language;
        ApplyAudioVideo();
        Save();
        SettingsChanged?.Invoke();
    }

    public void ApplyAudioVideo()
    {
        int master = AudioServer.GetBusIndex("Master");
        if (master >= 0)
        {
            float linear = Mathf.Clamp(MasterVolume / 100.0f, 0.0f, 1.0f);
            AudioServer.SetBusVolumeDb(master, linear <= 0.001f ? -80.0f : Mathf.LinearToDb(linear));
        }

        var target = Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != target)
        {
            DisplayServer.WindowSetMode(target);
        }
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("player", "name", PlayerName);
        cfg.SetValue("player", "language", LanguageCode);
        cfg.SetValue("input", "sensitivity", SensitivityRaw);
        cfg.SetValue("audio", "master", MasterVolume);
        cfg.SetValue("audio", "voice", VoiceVolume);
        cfg.SetValue("audio", "voice_mode", (int)VoiceChatMode);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("network", "address", PendingAddress);
        cfg.SetValue("network", "port", PendingPort);
        cfg.Save(ConfigPath);
    }

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) != Error.Ok)
        {
            return;
        }

        PlayerName = (string)cfg.GetValue("player", "name", PlayerName);
        LanguageCode = (string)cfg.GetValue("player", "language", LanguageCode);
        SensitivityRaw = (float)cfg.GetValue("input", "sensitivity", SensitivityRaw);
        MasterVolume = (float)cfg.GetValue("audio", "master", MasterVolume);
        VoiceVolume = (float)cfg.GetValue("audio", "voice", VoiceVolume);
        VoiceChatMode = (VoiceMode)(int)cfg.GetValue("audio", "voice_mode", (int)VoiceChatMode);
        Fullscreen = (bool)cfg.GetValue("video", "fullscreen", Fullscreen);
        PendingAddress = (string)cfg.GetValue("network", "address", PendingAddress);
        PendingPort = (int)cfg.GetValue("network", "port", PendingPort);
    }
}
