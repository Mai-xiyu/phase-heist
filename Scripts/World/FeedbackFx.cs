using Godot;
using System;

namespace PhaseHeist;

/// <summary>
/// Runtime-generated feedback effects: short procedural tones plus lightweight
/// visual bursts. This keeps the prototype self-contained until external audio/VFX
/// assets are selected.
/// </summary>
public partial class FeedbackFx : Node3D
{
    private const int SampleRate = 22050;

    public void PlayDoorLock(Vector3 worldPosition)
    {
        PlayTone(worldPosition, 84.0f, 0.18f, -7.0f, 48.0f);
        Burst(worldPosition + new Vector3(0, 2.1f, 0), new Color(0.95f, 0.18f, 0.08f), 12, 1.2f);
    }

    public void PlayGunshot(Vector3 worldPosition, bool hit)
    {
        PlayTone(worldPosition, hit ? 920.0f : 760.0f, 0.07f, -2.0f, 180.0f);
        Burst(worldPosition, hit ? new Color(1.0f, 0.18f, 0.08f) : new Color(1.0f, 0.72f, 0.25f), 10, 0.85f);
    }

    public void PlayHostageEvent(Vector3 worldPosition, bool released)
    {
        PlayTone(worldPosition, released ? 440.0f : 110.0f, released ? 0.12f : 0.22f, -5.0f, released ? 660.0f : 70.0f);
        Burst(worldPosition + new Vector3(0, 1.1f, 0),
            released ? new Color(0.18f, 0.85f, 0.35f) : new Color(0.85f, 0.05f, 0.04f),
            released ? 8 : 14,
            released ? 0.75f : 1.0f);
    }

    public void PlaySirenPulse(Vector3 worldPosition)
    {
        PlayTone(worldPosition, 620.0f, 0.13f, -8.0f, 920.0f);
        Burst(worldPosition + new Vector3(0, 1.6f, 0), new Color(0.15f, 0.28f, 1.0f), 8, 1.0f);
    }

    public void PlaySettlement(bool robberWin)
    {
        Vector3 pos = new(0, 2.0f, 3.0f);
        PlayTone(pos, robberWin ? 520.0f : 180.0f, 0.22f, -6.0f, robberWin ? 780.0f : 120.0f);
        Burst(pos, robberWin ? new Color(0.95f, 0.62f, 0.15f) : new Color(0.25f, 0.50f, 1.0f), 18, 1.5f);
    }

    private void PlayTone(Vector3 worldPosition, float frequency, float seconds, float volumeDb, float secondFrequency = 0.0f)
    {
        if (!IsInsideTree())
        {
            return;
        }

        var player = new AudioStreamPlayer3D
        {
            Name = "FxTone",
            Stream = new AudioStreamGenerator
            {
                MixRate = SampleRate,
                BufferLength = seconds + 0.05f,
            },
            VolumeDb = volumeDb,
            MaxDistance = 55.0f,
            UnitSize = 8.0f,
        };
        AddChild(player);
        player.GlobalPosition = worldPosition;
        player.Play();

        if (player.GetStreamPlayback() is AudioStreamGeneratorPlayback playback)
        {
            int total = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var frames = new Vector2[total];
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)SampleRate;
                float fade = 1.0f - i / (float)total;
                float f = secondFrequency > 0.0f ? Mathf.Lerp(frequency, secondFrequency, i / (float)total) : frequency;
                float sample = Mathf.Sin(Mathf.Pi * 2.0f * f * t) * fade * fade * 0.42f;
                frames[i] = new Vector2(sample, sample);
            }

            playback.PushBuffer(frames);
        }

        GetTree().CreateTimer(seconds + 0.25f).Timeout += () =>
        {
            if (IsInstanceValid(player))
            {
                player.QueueFree();
            }
        };
    }

    private void Burst(Vector3 center, Color color, int count, float radius)
    {
        if (!IsInsideTree())
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Pi * 2.0f * i / Mathf.Max(1, count);
            float lift = 0.18f + 0.08f * (i % 4);
            Vector3 end = center + new Vector3(Mathf.Cos(angle) * radius, lift, Mathf.Sin(angle) * radius);

            var particle = new MeshInstance3D
            {
                Name = "FxParticle",
                Scale = Vector3.One * 0.16f,
                Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = true,
                    Emission = color,
                    EmissionEnergyMultiplier = 1.15f,
                },
            };
            AddChild(particle);
            particle.GlobalPosition = center;

            Tween tween = CreateTween().SetParallel(true);
            tween.TweenProperty(particle, "global_position", end, 0.42f).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(particle, "scale", Vector3.Zero, 0.42f).SetTrans(Tween.TransitionType.Cubic);

            GetTree().CreateTimer(0.48f).Timeout += () =>
            {
                if (IsInstanceValid(particle))
                {
                    particle.QueueFree();
                }
            };
        }
    }
}
