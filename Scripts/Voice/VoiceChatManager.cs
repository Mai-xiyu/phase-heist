using Godot;
using System;
using System.Collections.Generic;

namespace PhaseHeist;

/// <summary>
/// 近距离语音：麦克风采集（Record 总线 + AudioEffectCapture）
/// → 下采样 16 kHz 单声道 → codec → Game RPC（不可靠）中继
/// → 远端玩家身上的 AudioStreamGenerator 3D 回放。
/// </summary>
public partial class VoiceChatManager : Node
{
    private const int TargetRate = 16000;
    private const int FrameSamples = 480; // 30 ms, 960-byte PCM16 payload below ENet MTU
    private const float OpenMicRmsGate = 0.015f;

    private Game? _game;
    private AudioEffectCapture? _capture;
    private AudioStreamPlayer? _micPlayer;
    private int _recordBusIdx = -1;
    private readonly List<float> _pending = new();
    private double _resampleAcc;
    private readonly Dictionary<int, AudioStreamGeneratorPlayback> _playbacks = new();
    private readonly Dictionary<int, AudioStreamPlayer3D> _players3d = new();
    private readonly Dictionary<int, AudioStreamGeneratorPlayback> _radioPlaybacks = new();
    private readonly Dictionary<int, AudioStreamPlayer> _radioPlayers = new();
    private readonly IVoiceCodec _codec = new Pcm16VoiceCodec(TargetRate, FrameSamples);
    private readonly VoiceEchoSuppressor _echoSuppressor = new();

    public bool SelfSpeaking { get; private set; }
    public string CodecName => _codec.Name;
    private double _selfSpeakUntil;

    public void Configure(Game game)
    {
        _game = game;
    }

    public override void _Ready()
    {
        if (AppState.Instance != null)
        {
            AppState.Instance.SettingsChanged += OnSettingsChanged;
        }

        SetupRadioBus();
        ReconfigureMic();
    }

    /// <summary>
    /// 警用无线电总线：带通 + 过载失真 + 压缩，模拟对讲机电音。
    /// 警察之间的语音走此总线（全图 2D），劫匪听不到。
    /// </summary>
    private static void SetupRadioBus()
    {
        if (AudioServer.GetBusIndex("Radio") >= 0)
        {
            return;
        }

        int idx = AudioServer.BusCount;
        AudioServer.AddBus(idx);
        AudioServer.SetBusName(idx, "Radio");
        AudioServer.SetBusSend(idx, "Master");

        AudioServer.AddBusEffect(idx, new AudioEffectHighPassFilter { CutoffHz = 420 });
        AudioServer.AddBusEffect(idx, new AudioEffectLowPassFilter { CutoffHz = 3100 });
        AudioServer.AddBusEffect(idx, new AudioEffectDistortion
        {
            Mode = AudioEffectDistortion.ModeEnum.Overdrive,
            Drive = 0.28f,
            PostGain = -2.0f,
        });
        AudioServer.AddBusEffect(idx, new AudioEffectCompressor
        {
            Threshold = -18.0f,
            Ratio = 5.0f,
        });
    }

    public override void _ExitTree()
    {
        if (AppState.Instance != null)
        {
            AppState.Instance.SettingsChanged -= OnSettingsChanged;
        }
    }

    public override void _Process(double delta)
    {
        SelfSpeaking = Time.GetUnixTimeFromSystem() < _selfSpeakUntil;

        if (_capture == null || _game == null)
        {
            return;
        }

        VoiceMode mode = AppState.Instance?.VoiceChatMode ?? VoiceMode.Off;
        bool transmitting = mode switch
        {
            VoiceMode.PushToTalk => Input.IsActionPressed("voice_ptt"),
            VoiceMode.OpenMic => true,
            _ => false,
        };

        // 始终读空缓冲，避免积压旧音频
        int available = _capture.GetFramesAvailable();
        if (available > 0)
        {
            Vector2[] frames = _capture.GetBuffer(available);
            if (transmitting && _game.HasNetworkPeerPublic())
            {
                Downsample(frames);
            }
            else
            {
                _pending.Clear();
                _resampleAcc = 0;
            }
        }

        while (_pending.Count >= FrameSamples)
        {
            float[] chunk = _pending.GetRange(0, FrameSamples).ToArray();
            _pending.RemoveRange(0, FrameSamples);
            _echoSuppressor.ProcessInPlace(chunk, HasRemotePlayback());

            if (AppState.Instance?.VoiceChatMode == VoiceMode.OpenMic && Rms(chunk) < OpenMicRmsGate)
            {
                continue;
            }

            _game.SendVoiceFrame(_codec.Encode(chunk));
            _selfSpeakUntil = Time.GetUnixTimeFromSystem() + 0.35;
            _game.LocalPlayer?.NotifySpeaking();
        }
    }

    /// <summary>
    /// 远端语音帧入口（Game RPC 调用）。
    /// 路由规则：警察→警察 = 全图无线电（电音、劫匪听不到此通道）；
    /// 其余组合 = 3D 近距离语音（警察当面喊话劫匪仍能听见）。
    /// </summary>
    public void Receive(int peerId, byte[] data)
    {
        if (_game == null || peerId == _game.LocalPeerId())
        {
            return;
        }

        PlayerController? player = _game.FindPlayerPublic(peerId);
        if (player == null)
        {
            return;
        }

        player.NotifySpeaking();

        bool radioChannel = player.Team == PlayerTeam.Police &&
                            _game.LocalPlayer?.Team == PlayerTeam.Police;

        AudioStreamGeneratorPlayback? playback = radioChannel
            ? EnsureRadioPlayback(peerId)
            : EnsurePlayback(peerId, player);
        if (playback == null)
        {
            return;
        }

        Vector2[] frames = _codec.Decode(data);
        if (playback.GetFramesAvailable() >= frames.Length)
        {
            playback.PushBuffer(frames);
        }
    }

    private AudioStreamGeneratorPlayback? EnsureRadioPlayback(int peerId)
    {
        if (_radioPlayers.TryGetValue(peerId, out AudioStreamPlayer? existing) && IsInstanceValid(existing))
        {
            return _radioPlaybacks.TryGetValue(peerId, out var pb) ? pb : null;
        }

        var output = new AudioStreamPlayer
        {
            Name = $"RadioVoice_{peerId}",
            Stream = new AudioStreamGenerator
            {
                MixRate = TargetRate,
                BufferLength = 0.4f,
            },
            Bus = "Radio",
            VolumeDb = VoiceVolumeDb() - 3.0f,
        };
        AddChild(output);
        output.Play();

        if (output.GetStreamPlayback() is AudioStreamGeneratorPlayback playback)
        {
            _radioPlayers[peerId] = output;
            _radioPlaybacks[peerId] = playback;
            return playback;
        }

        return null;
    }

    private AudioStreamGeneratorPlayback? EnsurePlayback(int peerId, PlayerController player)
    {
        if (_players3d.TryGetValue(peerId, out AudioStreamPlayer3D? existing) && IsInstanceValid(existing))
        {
            return _playbacks.TryGetValue(peerId, out var pb) ? pb : null;
        }

        var output = new AudioStreamPlayer3D
        {
            Name = "VoiceOutput",
            Stream = new AudioStreamGenerator
            {
                MixRate = TargetRate,
                BufferLength = 0.4f,
            },
            UnitSize = 6.0f,
            MaxDistance = 45.0f,
            VolumeDb = VoiceVolumeDb(),
        };
        player.AddChild(output);
        output.Play();

        if (output.GetStreamPlayback() is AudioStreamGeneratorPlayback playback)
        {
            _players3d[peerId] = output;
            _playbacks[peerId] = playback;
            return playback;
        }

        return null;
    }

    private void OnSettingsChanged()
    {
        ReconfigureMic();

        float db = VoiceVolumeDb();
        foreach (AudioStreamPlayer3D output in _players3d.Values)
        {
            if (IsInstanceValid(output))
            {
                output.VolumeDb = db;
            }
        }

        foreach (AudioStreamPlayer output in _radioPlayers.Values)
        {
            if (IsInstanceValid(output))
            {
                output.VolumeDb = db - 3.0f;
            }
        }
    }

    private static float VoiceVolumeDb()
    {
        float linear = Mathf.Clamp((AppState.Instance?.VoiceVolume ?? 100.0f) / 100.0f, 0.0f, 1.0f);
        return linear <= 0.001f ? -80.0f : Mathf.LinearToDb(linear);
    }

    private void ReconfigureMic()
    {
        bool wantMic = (AppState.Instance?.VoiceChatMode ?? VoiceMode.Off) != VoiceMode.Off;

        if (wantMic && _micPlayer == null)
        {
            SetupRecordBus();
            _micPlayer = new AudioStreamPlayer
            {
                Name = "MicCapture",
                Stream = new AudioStreamMicrophone(),
                Bus = "Record",
            };
            AddChild(_micPlayer);
            _micPlayer.Play();
        }
        else if (!wantMic && _micPlayer != null)
        {
            _micPlayer.Stop();
            _micPlayer.QueueFree();
            _micPlayer = null;
            _pending.Clear();
        }
    }

    private void SetupRecordBus()
    {
        _recordBusIdx = AudioServer.GetBusIndex("Record");
        if (_recordBusIdx < 0)
        {
            _recordBusIdx = AudioServer.BusCount;
            AudioServer.AddBus(_recordBusIdx);
            AudioServer.SetBusName(_recordBusIdx, "Record");
            AudioServer.SetBusMute(_recordBusIdx, true);
        }

        _capture = null;
        for (int i = 0; i < AudioServer.GetBusEffectCount(_recordBusIdx); i++)
        {
            if (AudioServer.GetBusEffect(_recordBusIdx, i) is AudioEffectCapture cap)
            {
                _capture = cap;
                break;
            }
        }

        if (_capture == null)
        {
            _capture = new AudioEffectCapture { BufferLength = 0.3f };
            AudioServer.AddBusEffect(_recordBusIdx, _capture);
        }
    }

    private void Downsample(Vector2[] frames)
    {
        double mixRate = AudioServer.GetMixRate();
        double step = mixRate / TargetRate;

        foreach (Vector2 frame in frames)
        {
            _resampleAcc += 1.0;
            while (_resampleAcc >= step)
            {
                _resampleAcc -= step;
                _pending.Add((frame.X + frame.Y) * 0.5f);
            }
        }

        // 防积压：最多保留 0.5s
        int cap = TargetRate / 2;
        if (_pending.Count > cap)
        {
            _pending.RemoveRange(0, _pending.Count - cap);
        }
    }

    private static float Rms(float[] samples)
    {
        float sum = 0;
        foreach (float s in samples)
        {
            sum += s * s;
        }

        return Mathf.Sqrt(sum / samples.Length);
    }

    private bool HasRemotePlayback()
    {
        foreach (AudioStreamPlayer3D output in _players3d.Values)
        {
            if (IsInstanceValid(output))
            {
                return true;
            }
        }

        return false;
    }
}
