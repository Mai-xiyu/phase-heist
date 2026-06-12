using Godot;

namespace PhaseHeist;

public interface IVoiceCodec
{
    string Name { get; }
    int SampleRate { get; }
    int FrameSamples { get; }
    byte[] Encode(float[] samples);
    Vector2[] Decode(byte[] data);
}

/// <summary>
/// PCM16 fallback codec. Opus can replace this interface later without touching the
/// capture, RPC relay, or 3D playback code.
/// </summary>
public sealed class Pcm16VoiceCodec : IVoiceCodec
{
    public Pcm16VoiceCodec(int sampleRate, int frameSamples)
    {
        SampleRate = sampleRate;
        FrameSamples = frameSamples;
    }

    public string Name => "PCM16 fallback";
    public int SampleRate { get; }
    public int FrameSamples { get; }

    public byte[] Encode(float[] samples)
    {
        byte[] data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short v = (short)Mathf.Clamp(samples[i] * 32767.0f, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        return data;
    }

    public Vector2[] Decode(byte[] data)
    {
        int count = data.Length / 2;
        var frames = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            short v = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            float f = v / 32768.0f;
            frames[i] = new Vector2(f, f);
        }

        return frames;
    }
}

/// <summary>
/// Lightweight send-side cleanup. This is not WebRTC-grade acoustic echo
/// cancellation; it is a deterministic high-pass/noise gate/attenuator that keeps
/// the voice path stable until a native Opus/WebRTC module is added.
/// </summary>
public sealed class VoiceEchoSuppressor
{
    private const float HighPassFeedback = 0.985f;
    private const float NoiseGate = 0.0045f;

    private float _lastInput;
    private float _lastOutput;

    public void ProcessInPlace(float[] samples, bool remotePlaybackActive)
    {
        float playbackAttenuation = remotePlaybackActive ? 0.62f : 1.0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float input = samples[i];
            float highPassed = input - _lastInput + HighPassFeedback * _lastOutput;
            _lastInput = input;
            _lastOutput = highPassed;

            float cleaned = Mathf.Abs(highPassed) < NoiseGate ? 0.0f : highPassed * playbackAttenuation;
            samples[i] = Mathf.Clamp(cleaned, -1.0f, 1.0f);
        }
    }
}
