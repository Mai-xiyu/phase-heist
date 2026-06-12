using Godot;

namespace PhaseHeist;

public enum ObjectivePropKind
{
    None,
    Phone,        // 柜台电话（劫匪谈判）
    Megaphone,    // 警用扩音器（喇叭谈判）
    LootBag,      // 金库钱袋
    Recorder,     // 录音机
    Chair,        // 人质椅（伪装）
    Rack,         // 换装衣架
    Barricade,    // 警用拒马（堵点）
    BreachCharge, // 破门炸药包
    KnifeTable,   // 处决台
    Manhole,      // 下水道（井盖已在场景里，附加撬棍）
}

/// <summary>
/// 交互点实体道具：替代"地面大圆盘"，让交互对象在场景里有真实载体。
/// 全部为程序化小模型，挂在 BankObjective 节点下。
/// </summary>
public static class ObjectiveProps
{
    public static void Build(ObjectivePropKind kind, Node3D parent)
    {
        switch (kind)
        {
            case ObjectivePropKind.Phone:
                BuildPhone(parent);
                break;
            case ObjectivePropKind.Megaphone:
                BuildMegaphone(parent);
                break;
            case ObjectivePropKind.LootBag:
                BuildLootBag(parent);
                break;
            case ObjectivePropKind.Recorder:
                BuildRecorder(parent);
                break;
            case ObjectivePropKind.Chair:
                BuildChair(parent);
                break;
            case ObjectivePropKind.Rack:
                BuildRack(parent);
                break;
            case ObjectivePropKind.Barricade:
                BuildBarricade(parent);
                break;
            case ObjectivePropKind.BreachCharge:
                BuildBreachCharge(parent);
                break;
            case ObjectivePropKind.KnifeTable:
                BuildKnifeTable(parent);
                break;
            case ObjectivePropKind.Manhole:
                BuildCrowbar(parent);
                break;
        }
    }

    // 柜台电话：底座 + 听筒 + 螺旋线示意
    private static void BuildPhone(Node3D p)
    {
        Color body = new(0.12f, 0.12f, 0.14f);
        Box(p, "PhoneBase", new Vector3(0, 0.79f, -0.85f), new Vector3(0.26f, 0.10f, 0.18f), new Color(0.75f, 0.12f, 0.10f));
        Box(p, "Handset", new Vector3(0, 0.88f, -0.85f), new Vector3(0.30f, 0.05f, 0.07f), body);
        Box(p, "HandsetEarL", new Vector3(-0.13f, 0.905f, -0.85f), new Vector3(0.06f, 0.06f, 0.08f), body);
        Box(p, "HandsetEarR", new Vector3(0.13f, 0.905f, -0.85f), new Vector3(0.06f, 0.06f, 0.08f), body);
        // 小桌台（电话亭柱）
        Cyl(p, "PhonePole", new Vector3(0, 0.37f, -0.85f), 0.05f, 0.74f, new Color(0.30f, 0.30f, 0.33f));
        Box(p, "PhonePlate", new Vector3(0, 0.745f, -0.85f), new Vector3(0.42f, 0.04f, 0.34f), new Color(0.40f, 0.30f, 0.22f));
    }

    // 警用扩音器：喇叭锥 + 手柄，架在三脚桩上
    private static void BuildMegaphone(Node3D p)
    {
        Cyl(p, "MegaStand", new Vector3(0, 0.5f, 0), 0.05f, 1.0f, new Color(0.22f, 0.24f, 0.30f));
        Box(p, "MegaPlate", new Vector3(0, 1.02f, 0), new Vector3(0.34f, 0.05f, 0.30f), new Color(0.16f, 0.20f, 0.40f));

        var cone = new MeshInstance3D
        {
            Name = "MegaCone",
            Position = new Vector3(0, 1.22f, -0.10f),
            Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.17f, Height = 0.34f },
            MaterialOverride = Mat(new Color(0.92f, 0.92f, 0.95f), metallic: 0.4f),
        };
        p.AddChild(cone);
        Box(p, "MegaGrip", new Vector3(0, 1.10f, 0.10f), new Vector3(0.05f, 0.16f, 0.06f), new Color(0.10f, 0.10f, 0.12f));
        Box(p, "MegaMouth", new Vector3(0, 1.22f, -0.30f), new Vector3(0.10f, 0.10f, 0.05f), new Color(0.85f, 0.30f, 0.10f), emissive: true);
    }

    // 金库钱袋堆：三只鼓包袋 + 散钞
    private static void BuildLootBag(Node3D p)
    {
        Color bag = new(0.30f, 0.42f, 0.28f);
        Sack(p, "BagA", new Vector3(0.0f, 0, 0.0f), 0.34f, bag);
        Sack(p, "BagB", new Vector3(0.42f, 0, 0.18f), 0.28f, bag.Darkened(0.12f));
        Sack(p, "BagC", new Vector3(-0.36f, 0, 0.22f), 0.26f, bag.Lightened(0.08f));
        Box(p, "CashA", new Vector3(0.15f, 0.04f, -0.35f), new Vector3(0.16f, 0.05f, 0.09f), new Color(0.55f, 0.75f, 0.50f), emissive: true);
        Box(p, "CashB", new Vector3(-0.20f, 0.04f, -0.30f), new Vector3(0.16f, 0.05f, 0.09f), new Color(0.55f, 0.75f, 0.50f), emissive: true);
    }

    private static void Sack(Node3D p, string name, Vector3 basePos, float radius, Color color)
    {
        var body = new MeshInstance3D
        {
            Name = name,
            Position = basePos + new Vector3(0, radius * 0.85f, 0),
            Scale = new Vector3(1, 0.85f, 1),
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2 },
            MaterialOverride = Mat(color),
        };
        p.AddChild(body);
        var knot = new MeshInstance3D
        {
            Name = name + "Knot",
            Position = basePos + new Vector3(0, radius * 1.75f, 0),
            Mesh = new SphereMesh { Radius = radius * 0.22f, Height = radius * 0.44f },
            MaterialOverride = Mat(color.Darkened(0.3f)),
        };
        p.AddChild(knot);
    }

    // 录音机：主机 + 双卷盘 + 红色录制灯
    private static void BuildRecorder(Node3D p)
    {
        Box(p, "RecTable", new Vector3(0, 0.42f, 0), new Vector3(0.7f, 0.06f, 0.5f), new Color(0.38f, 0.28f, 0.20f));
        Cyl(p, "RecLegA", new Vector3(-0.28f, 0.2f, -0.18f), 0.03f, 0.4f, new Color(0.2f, 0.2f, 0.22f));
        Cyl(p, "RecLegB", new Vector3(0.28f, 0.2f, -0.18f), 0.03f, 0.4f, new Color(0.2f, 0.2f, 0.22f));
        Cyl(p, "RecLegC", new Vector3(-0.28f, 0.2f, 0.18f), 0.03f, 0.4f, new Color(0.2f, 0.2f, 0.22f));
        Cyl(p, "RecLegD", new Vector3(0.28f, 0.2f, 0.18f), 0.03f, 0.4f, new Color(0.2f, 0.2f, 0.22f));

        Box(p, "RecBody", new Vector3(0, 0.52f, 0), new Vector3(0.46f, 0.12f, 0.32f), new Color(0.55f, 0.52f, 0.46f));
        Reel(p, "ReelL", new Vector3(-0.11f, 0.60f, 0));
        Reel(p, "ReelR", new Vector3(0.11f, 0.60f, 0));
        Box(p, "RecLight", new Vector3(0.18f, 0.55f, 0.14f), new Vector3(0.04f, 0.04f, 0.04f), new Color(0.95f, 0.10f, 0.08f), emissive: true);
    }

    private static void Reel(Node3D p, string name, Vector3 pos)
    {
        p.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.085f, Height = 0.03f },
            MaterialOverride = Mat(new Color(0.15f, 0.15f, 0.17f)),
        });
    }

    // 人质椅：座面 + 靠背 + 四腿 + 散落束带
    private static void BuildChair(Node3D p)
    {
        Color wood = new(0.42f, 0.30f, 0.20f);
        Box(p, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.46f, 0.05f, 0.44f), wood);
        Box(p, "Back", new Vector3(0, 0.78f, 0.20f), new Vector3(0.46f, 0.62f, 0.05f), wood);
        Box(p, "LegA", new Vector3(-0.19f, 0.22f, -0.18f), new Vector3(0.05f, 0.44f, 0.05f), wood.Darkened(0.2f));
        Box(p, "LegB", new Vector3(0.19f, 0.22f, -0.18f), new Vector3(0.05f, 0.44f, 0.05f), wood.Darkened(0.2f));
        Box(p, "LegC", new Vector3(-0.19f, 0.22f, 0.18f), new Vector3(0.05f, 0.44f, 0.05f), wood.Darkened(0.2f));
        Box(p, "LegD", new Vector3(0.19f, 0.22f, 0.18f), new Vector3(0.05f, 0.44f, 0.05f), wood.Darkened(0.2f));
        Box(p, "ZipTie", new Vector3(0.1f, 0.03f, -0.3f), new Vector3(0.18f, 0.02f, 0.04f), new Color(0.9f, 0.9f, 0.92f));
    }

    // 换装衣架：横杆 + 三件挂衣
    private static void BuildRack(Node3D p)
    {
        Color steel = new(0.55f, 0.56f, 0.60f);
        Cyl(p, "RackPoleL", new Vector3(-0.55f, 0.8f, 0), 0.035f, 1.6f, steel);
        Cyl(p, "RackPoleR", new Vector3(0.55f, 0.8f, 0), 0.035f, 1.6f, steel);
        var bar = new MeshInstance3D
        {
            Name = "RackBar",
            Position = new Vector3(0, 1.55f, 0),
            Rotation = new Vector3(0, 0, Mathf.Pi / 2),
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 1.15f },
            MaterialOverride = Mat(steel),
        };
        p.AddChild(bar);

        Color[] clothes = { new(0.34f, 0.35f, 0.40f), new(0.30f, 0.42f, 0.34f), new(0.44f, 0.32f, 0.22f) };
        for (int i = 0; i < clothes.Length; i++)
        {
            Box(p, $"Cloth_{i}", new Vector3(-0.3f + i * 0.3f, 1.18f, 0), new Vector3(0.24f, 0.66f, 0.06f), clothes[i]);
        }
    }

    // 警用拒马：双脚架 + 蓝白条纹板
    private static void BuildBarricade(Node3D p)
    {
        Color frame = new(0.25f, 0.27f, 0.32f);
        Box(p, "BarrLegL", new Vector3(-0.85f, 0.5f, 0), new Vector3(0.08f, 1.0f, 0.5f), frame);
        Box(p, "BarrLegR", new Vector3(0.85f, 0.5f, 0), new Vector3(0.08f, 1.0f, 0.5f), frame);
        Box(p, "BarrBoardTop", new Vector3(0, 0.85f, 0), new Vector3(1.85f, 0.22f, 0.06f), new Color(0.18f, 0.34f, 0.85f));
        Box(p, "BarrBoardMid", new Vector3(0, 0.55f, 0), new Vector3(1.85f, 0.22f, 0.06f), new Color(0.92f, 0.93f, 0.95f));
        for (int i = 0; i < 4; i++)
        {
            Box(p, $"BarrStripe_{i}", new Vector3(-0.66f + i * 0.44f, 0.85f, 0.035f), new Vector3(0.16f, 0.20f, 0.012f), new Color(0.92f, 0.93f, 0.95f));
        }
        Box(p, "BarrLamp", new Vector3(0, 1.02f, 0), new Vector3(0.12f, 0.10f, 0.10f), new Color(1.0f, 0.55f, 0.10f), emissive: true);
    }

    // 破门炸药包：门贴板 + 雷管 + 红灯
    private static void BuildBreachCharge(Node3D p)
    {
        Box(p, "ChargeCase", new Vector3(0, 1.1f, 0), new Vector3(0.34f, 0.44f, 0.10f), new Color(0.16f, 0.18f, 0.16f));
        Box(p, "ChargeX1", new Vector3(0, 1.1f, 0.055f), new Vector3(0.30f, 0.06f, 0.012f), new Color(0.80f, 0.66f, 0.20f));
        Box(p, "ChargeX2", new Vector3(0, 1.1f, 0.055f), new Vector3(0.06f, 0.40f, 0.012f), new Color(0.80f, 0.66f, 0.20f));
        Box(p, "ChargeLed", new Vector3(0.10f, 1.28f, 0.055f), new Vector3(0.04f, 0.04f, 0.03f), new Color(0.95f, 0.12f, 0.08f), emissive: true);
    }

    // 处决台：矮桌 + 刀 + 暗痕
    private static void BuildKnifeTable(Node3D p)
    {
        Box(p, "KnifeTable", new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.08f, 0.5f), new Color(0.20f, 0.16f, 0.14f));
        Box(p, "KnifeLegL", new Vector3(-0.32f, 0.18f, 0), new Vector3(0.07f, 0.36f, 0.4f), new Color(0.14f, 0.12f, 0.10f));
        Box(p, "KnifeLegR", new Vector3(0.32f, 0.18f, 0), new Vector3(0.07f, 0.36f, 0.4f), new Color(0.14f, 0.12f, 0.10f));
        Box(p, "Blade", new Vector3(0.05f, 0.46f, 0.02f), new Vector3(0.34f, 0.015f, 0.05f), new Color(0.78f, 0.80f, 0.84f));
        Box(p, "Hilt", new Vector3(-0.18f, 0.465f, 0.02f), new Vector3(0.12f, 0.035f, 0.06f), new Color(0.10f, 0.08f, 0.08f));
        Box(p, "Stain", new Vector3(0.12f, 0.445f, -0.12f), new Vector3(0.2f, 0.005f, 0.14f), new Color(0.30f, 0.04f, 0.03f));
    }

    // 井盖旁撬棍
    private static void BuildCrowbar(Node3D p)
    {
        var bar = new MeshInstance3D
        {
            Name = "Crowbar",
            Position = new Vector3(0.7f, 0.05f, 0.3f),
            Rotation = new Vector3(0, 0.6f, Mathf.Pi / 2),
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.8f },
            MaterialOverride = Mat(new Color(0.70f, 0.16f, 0.10f), metallic: 0.5f),
        };
        p.AddChild(bar);
    }

    // ---------- 基础件 ----------

    private static void Box(Node3D p, string name, Vector3 pos, Vector3 size, Color color, bool emissive = false)
    {
        p.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Mat(color, emissive),
        });
    }

    private static void Cyl(Node3D p, string name, Vector3 pos, float radius, float height, Color color)
    {
        p.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
            MaterialOverride = Mat(color),
        });
    }

    private static StandardMaterial3D Mat(Color color, bool emissive = false, float metallic = 0.05f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.62f,
            Metallic = metallic,
            EmissionEnabled = emissive,
            Emission = color,
            EmissionEnergyMultiplier = emissive ? 0.8f : 0.0f,
        };
    }
}
