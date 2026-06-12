using Godot;
using System.Collections.Generic;

namespace PhaseHeist;

public struct HumanPalette
{
    public Color Suit;
    public Color Trim;
    public Color Skin;
    public Color Accent;

    public static HumanPalette Police => new()
    {
        Suit = new Color(0.10f, 0.16f, 0.38f),
        Trim = new Color(0.05f, 0.08f, 0.16f),
        Skin = new Color(0.85f, 0.66f, 0.50f),
        Accent = new Color(0.20f, 0.45f, 0.95f),
    };

    public static HumanPalette RobberDisguise(int level) => (level % 3) switch
    {
        1 => new HumanPalette
        {
            Suit = new Color(0.30f, 0.42f, 0.34f),
            Trim = new Color(0.14f, 0.20f, 0.16f),
            Skin = new Color(0.80f, 0.62f, 0.46f),
            Accent = new Color(0.78f, 0.60f, 0.22f),
        },
        2 => new HumanPalette
        {
            Suit = new Color(0.44f, 0.32f, 0.22f),
            Trim = new Color(0.22f, 0.15f, 0.10f),
            Skin = new Color(0.90f, 0.70f, 0.54f),
            Accent = new Color(0.85f, 0.45f, 0.18f),
        },
        _ => new HumanPalette
        {
            Suit = new Color(0.34f, 0.35f, 0.40f),
            Trim = new Color(0.16f, 0.17f, 0.20f),
            Skin = new Color(0.86f, 0.66f, 0.48f),
            Accent = new Color(0.92f, 0.60f, 0.16f),
        },
    };

    public static HumanPalette FakeHostage => new()
    {
        Suit = new Color(0.86f, 0.66f, 0.20f),
        Trim = new Color(0.42f, 0.30f, 0.08f),
        Skin = new Color(0.86f, 0.66f, 0.48f),
        Accent = new Color(0.95f, 0.85f, 0.30f),
    };

    public static HumanPalette Arrested => new()
    {
        Suit = new Color(0.20f, 0.20f, 0.22f),
        Trim = new Color(0.10f, 0.10f, 0.11f),
        Skin = new Color(0.70f, 0.55f, 0.42f),
        Accent = new Color(0.45f, 0.45f, 0.48f),
    };

    public static HumanPalette Civilian(int seed)
    {
        Color[] suits =
        {
            new(0.36f, 0.42f, 0.50f),
            new(0.50f, 0.40f, 0.32f),
            new(0.30f, 0.44f, 0.36f),
            new(0.46f, 0.30f, 0.36f),
            new(0.28f, 0.32f, 0.46f),
        };
        Color suit = suits[System.Math.Abs(seed) % suits.Length];
        return new HumanPalette
        {
            Suit = suit,
            Trim = suit.Darkened(0.45f),
            Skin = new Color(0.85f, 0.66f, 0.50f),
            Accent = suit.Lightened(0.25f),
        };
    }

    public static HumanPalette Hostage => new()
    {
        Suit = new Color(0.80f, 0.62f, 0.22f),
        Trim = new Color(0.40f, 0.30f, 0.10f),
        Skin = new Color(0.85f, 0.66f, 0.50f),
        Accent = new Color(0.95f, 0.80f, 0.30f),
    };
}

/// <summary>
/// 人形骨架引用：四肢枢轴用于行走摆动，材质引用用于换装重涂。
/// </summary>
public class HumanRig
{
    public Node3D Root = null!;
    public Node3D ArmLeft = null!;
    public Node3D ArmRight = null!;
    public Node3D LegLeft = null!;
    public Node3D LegRight = null!;
    public MeshInstance3D SpeakDot = null!;
    public readonly List<MeshInstance3D> SuitParts = new();
    public readonly List<MeshInstance3D> TrimParts = new();
    public readonly List<MeshInstance3D> SkinParts = new();
    public readonly List<MeshInstance3D> AccentParts = new();

    public void ApplyPalette(HumanPalette palette)
    {
        Paint(SuitParts, palette.Suit, false);
        Paint(TrimParts, palette.Trim, false);
        Paint(SkinParts, palette.Skin, false);
        Paint(AccentParts, palette.Accent, true);
    }

    private static void Paint(List<MeshInstance3D> parts, Color color, bool emissive)
    {
        foreach (MeshInstance3D part in parts)
        {
            if (part.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = color;
                mat.EmissionEnabled = emissive;
                mat.Emission = color;
                mat.EmissionEnergyMultiplier = emissive ? 0.45f : 0.0f;
            }
        }
    }
}

/// <summary>
/// 用基础网格拼装 1.8m 人形模型（真实人体比例近似）。
/// 头顶 1.80，肩高 1.45，髋部 0.95，腿长 0.95。
/// </summary>
public static class PlayerModelBuilder
{
    public static HumanRig Build(Node3D parent, HumanPalette palette)
    {
        var rig = new HumanRig();
        var root = new Node3D { Name = "HumanModel" };
        parent.AddChild(root);
        rig.Root = root;

        // 躯干：髋 0.95 → 肩 1.45
        rig.SuitParts.Add(AddBox(root, "Torso", new Vector3(0, 1.20f, 0), new Vector3(0.42f, 0.52f, 0.24f)));
        rig.TrimParts.Add(AddBox(root, "Hips", new Vector3(0, 0.92f, 0), new Vector3(0.38f, 0.16f, 0.23f)));
        rig.AccentParts.Add(AddBox(root, "ChestBadge", new Vector3(0.10f, 1.32f, -0.13f), new Vector3(0.10f, 0.10f, 0.02f)));
        rig.TrimParts.Add(AddBox(root, "Belt", new Vector3(0, 1.00f, 0), new Vector3(0.40f, 0.05f, 0.25f)));

        // 头：颈 1.45 → 头心 1.62
        rig.SkinParts.Add(AddSphere(root, "Head", new Vector3(0, 1.62f, 0), 0.13f));
        rig.TrimParts.Add(AddBox(root, "Cap", new Vector3(0, 1.72f, 0), new Vector3(0.24f, 0.07f, 0.24f)));

        // 手臂枢轴在肩 (±0.26, 1.42)，臂长 0.55
        rig.ArmLeft = AddLimb(root, "ArmL", new Vector3(-0.26f, 1.42f, 0), 0.55f, 0.07f, rig.SuitParts, rig.SkinParts);
        rig.ArmRight = AddLimb(root, "ArmR", new Vector3(0.26f, 1.42f, 0), 0.55f, 0.07f, rig.SuitParts, rig.SkinParts);

        // 腿枢轴在髋 (±0.11, 0.95)，腿长 0.92
        rig.LegLeft = AddLimb(root, "LegL", new Vector3(-0.11f, 0.95f, 0), 0.92f, 0.085f, rig.TrimParts, rig.TrimParts);
        rig.LegRight = AddLimb(root, "LegR", new Vector3(0.11f, 0.95f, 0), 0.92f, 0.085f, rig.TrimParts, rig.TrimParts);

        // 说话指示灯
        rig.SpeakDot = AddSphere(root, "SpeakDot", new Vector3(0, 2.02f, 0), 0.055f);
        if (rig.SpeakDot.MaterialOverride is StandardMaterial3D dotMat)
        {
            dotMat.AlbedoColor = new Color(0.20f, 0.95f, 0.40f);
            dotMat.EmissionEnabled = true;
            dotMat.Emission = new Color(0.20f, 0.95f, 0.40f);
            dotMat.EmissionEnergyMultiplier = 1.4f;
        }
        rig.SpeakDot.Visible = false;

        rig.ApplyPalette(palette);
        return rig;
    }

    /// <summary>行走摆动。speed01：0 静止，1 全速。phase 由调用方累计。</summary>
    public static void AnimateWalk(HumanRig rig, float phase, float speed01)
    {
        float swing = Mathf.Sin(phase) * 0.55f * speed01;
        rig.LegLeft.Rotation = new Vector3(swing, 0, 0);
        rig.LegRight.Rotation = new Vector3(-swing, 0, 0);
        rig.ArmLeft.Rotation = new Vector3(-swing * 0.8f, 0, 0);
        rig.ArmRight.Rotation = new Vector3(swing * 0.8f, 0, 0);
    }

    private static Node3D AddLimb(
        Node3D parent,
        string name,
        Vector3 pivot,
        float length,
        float radius,
        List<MeshInstance3D> upperBucket,
        List<MeshInstance3D> lowerBucket)
    {
        var limb = new Node3D { Name = name, Position = pivot };
        parent.AddChild(limb);

        MeshInstance3D upper = AddCapsule(limb, $"{name}Upper", new Vector3(0, -length * 0.30f, 0), radius, length * 0.55f);
        upperBucket.Add(upper);
        MeshInstance3D lower = AddCapsule(limb, $"{name}Lower", new Vector3(0, -length * 0.78f, 0), radius * 0.85f, length * 0.5f);
        lowerBucket.Add(lower);
        return limb;
    }

    private static MeshInstance3D AddBox(Node3D parent, string name, Vector3 pos, Vector3 size)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { Roughness = 0.7f },
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static MeshInstance3D AddSphere(Node3D parent, string name, Vector3 pos, float radius)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2 },
            MaterialOverride = new StandardMaterial3D { Roughness = 0.6f },
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static MeshInstance3D AddCapsule(Node3D parent, string name, Vector3 pos, float radius, float height)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new CapsuleMesh { Radius = radius, Height = height },
            MaterialOverride = new StandardMaterial3D { Roughness = 0.7f },
        };
        parent.AddChild(mesh);
        return mesh;
    }
}
