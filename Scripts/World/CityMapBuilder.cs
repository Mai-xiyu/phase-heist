using Godot;
using System.Collections.Generic;

namespace PhaseHeist;

/// <summary>
/// 真实尺度城市街区（约 120m × 74m）。
/// 原点 = 银行营业厅中心；+Z 朝南（临街），-Z 朝北（后巷）。
/// 关键尺寸（米，参照现实）：
///   银行主楼 36×26，墙高 5，门洞宽 1.6–4.2；
///   营业厅进深 12，柜台长 20 高 1.1；
///   金库内室约 7×9，围墙厚 0.8；
///   南侧双车道街道宽 8，人行广场进深 16；
///   停车位 2.6×5.5，轿车 4.5×1.8×1.45。
/// </summary>
public partial class CityMapBuilder : Node3D
{
    private const float WallH = 5.0f;
    private const float WallT = 0.4f;

    // Kenney 模型缩放（模型原始单位 ≈ 1m 网格，按需放大到真实尺度）
    private const float TruckScale = 2.2f;
    private const float TreeScale = 3.2f;
    private const float FountainScale = 5.0f;
    private const float FacadeBuildingScale = 8.0f;

    private readonly List<(Label3D label, string key)> _locLabels = new();

    public override void _Ready()
    {
        BuildGround();
        BuildStreets();
        BuildPlaza();
        BuildParkingLot();
        BuildAlleys();
        BuildBankShell();
        BuildBankInterior();
        BuildVault();
        BuildFurniture();
        BuildExteriorProps();
        BuildPerimeter();
        BuildObjectives();
        BuildNpcs();
        BuildLighting();
        BuildRoomLabels();

        Loc.LanguageChanged += RefreshLabels;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshLabels;
    }

    // ---------- 地面与街道 ----------

    private void BuildGround()
    {
        // 总地块 120 × 74
        AddStatic("Ground", new Vector3(0, -0.15f, -3), new Vector3(120, 0.3f, 74), new Color(0.16f, 0.17f, 0.18f));
    }

    private void BuildStreets()
    {
        // 南侧双车道沥青路：Z 22..30
        AddVisual("RoadSouth", new Vector3(0, 0.02f, 26), new Vector3(120, 0.05f, 8), new Color(0.10f, 0.10f, 0.11f));

        // 车道中线虚线
        for (int x = -56; x <= 56; x += 6)
        {
            AddVisual($"LaneDash_{x}", new Vector3(x, 0.06f, 26), new Vector3(2.4f, 0.02f, 0.18f), new Color(0.85f, 0.82f, 0.70f));
        }

        // 路缘石
        AddStatic("CurbSouth", new Vector3(0, 0.10f, 21.9f), new Vector3(120, 0.2f, 0.35f), new Color(0.45f, 0.46f, 0.48f));

        // 斑马线（银行正门前）
        for (int i = 0; i < 6; i++)
        {
            AddVisual($"Zebra_{i}", new Vector3(-2.5f + i, 0.06f, 26), new Vector3(0.55f, 0.02f, 7.2f), new Color(0.80f, 0.80f, 0.78f));
        }
    }

    private void BuildPlaza()
    {
        // 银行前人行广场：Z 6..22
        AddVisual("Plaza", new Vector3(0, 0.015f, 14), new Vector3(120, 0.04f, 16), new Color(0.30f, 0.29f, 0.28f));

        // 广场铺装分隔线
        for (int x = -56; x <= 56; x += 8)
        {
            AddVisual($"PlazaLine_{x}", new Vector3(x, 0.045f, 14), new Vector3(0.08f, 0.02f, 16), new Color(0.22f, 0.21f, 0.20f));
        }

        // 警戒线（蓝白胶带视觉 + 矮护栏）
        AddVisual("PoliceLine", new Vector3(0, 0.55f, 22), new Vector3(64, 0.06f, 0.06f), new Color(0.18f, 0.35f, 0.95f));
        for (int x = -32; x <= 32; x += 4)
        {
            if (x % 12 == 0)
            {
                continue; // 留出可通行缺口
            }

            AddStatic($"Barrier_{x}", new Vector3(x, 0.5f, 22), new Vector3(2.6f, 1.0f, 0.12f), new Color(0.85f, 0.86f, 0.88f));
        }

        // 喷泉（Kenney 模型 + 圆柱碰撞；模型缺失时退回程序化）
        if (AssetLibrary.AddModel(this, AssetLibrary.PavementFountain, "FountainModel",
                new Vector3(12, 0.02f, 14), Vector3.One * FountainScale, Vector3.Zero) == null)
        {
            AddStatic("FountainBase", new Vector3(12, 0.3f, 14), new Vector3(5, 0.6f, 5), new Color(0.42f, 0.43f, 0.45f));
            AddVisual("FountainWater", new Vector3(12, 0.65f, 14), new Vector3(4.2f, 0.08f, 4.2f), new Color(0.20f, 0.55f, 0.80f));
        }
        AddInvisibleCylinder("FountainCollision", new Vector3(12, 0, 14), 2.4f, 1.4f);

        // 花坛 + 行道树（树模型自带草毯基座，沉入花坛顶面避免悬浮）
        Vector3[] planters = { new(-14, 0, 12), new(-14, 0, 18), new(24, 0, 12), new(24, 0, 18) };
        foreach (Vector3 p in planters)
        {
            AddStatic($"Planter_{p.X}_{p.Z}", new Vector3(p.X, 0.25f, p.Z), new Vector3(2.4f, 0.5f, 2.4f), new Color(0.36f, 0.30f, 0.26f));
            AddTree($"PlanterTree_{p.X}_{p.Z}", new Vector3(p.X, 0.46f, p.Z), tall: false);
        }

        float[] streetTreeXs = { -34, -24, 32, 42 };
        foreach (float x in streetTreeXs)
        {
            AddTree($"StreetTree_{x}", new Vector3(x, 0, 20.2f), tall: true);
        }
    }

    private void AddTree(string name, Vector3 pos, bool tall)
    {
        string asset = tall ? AssetLibrary.TreesTall : AssetLibrary.Trees;
        if (AssetLibrary.AddModel(this, asset, name, pos, Vector3.One * TreeScale, Vector3.Zero) == null)
        {
            AddVisualSphere($"{name}_Fallback", pos + new Vector3(0, 1.6f, 0), 1.0f, new Color(0.18f, 0.38f, 0.16f));
        }

        AddInvisibleCylinder($"{name}_Col", pos, 0.25f, 2.5f);
    }

    private void BuildParkingLot()
    {
        // 西侧停车场：X -58..-26, Z 6..20
        AddVisual("ParkingSurface", new Vector3(-42, 0.025f, 13), new Vector3(32, 0.04f, 14), new Color(0.13f, 0.13f, 0.14f));

        // 车位线：2.6m 间距
        for (int i = 0; i <= 10; i++)
        {
            float x = -55.5f + i * 2.6f;
            AddVisual($"Stall_{i}", new Vector3(x, 0.05f, 9), new Vector3(0.10f, 0.02f, 5.5f), new Color(0.75f, 0.73f, 0.65f));
        }

        // 停放车辆：Kenney 卡车模型为主，程序化轿车补充
        BuildModelVehicle("TruckA", new Vector3(-54.2f, 0, 9.2f), 0, AssetLibrary.TruckRed);
        BuildModelVehicle("TruckB", new Vector3(-48.9f, 0, 9.2f), 0, AssetLibrary.TruckGreen);
        BuildModelVehicle("TruckC", new Vector3(-43.7f, 0, 9.2f), 0, AssetLibrary.TruckYellow);
        BuildModelVehicle("TruckD", new Vector3(-38.5f, 0, 9.2f), 0, AssetLibrary.TruckPurple);
        BuildModelVehicle("TruckF", new Vector3(-50f, 0, 17.0f), Mathf.Pi, AssetLibrary.TruckGreen);

        // 可驾驶民用车（E 上车）
        BuildDriveableCar("Drive_SedanA", new Vector3(-33.3f, 0, 9.2f), 0, new Color(0.34f, 0.42f, 0.30f), police: false);
        BuildDriveableCar("Drive_SedanB", new Vector3(-40f, 0, 17.0f), Mathf.Pi, new Color(0.50f, 0.50f, 0.55f), police: false);
    }

    /// <summary>可驾驶载具：名字跨端一致（网络寻址用）。</summary>
    private void BuildDriveableCar(string name, Vector3 pos, float rotY, Color color, bool police)
    {
        var car = new DriveableCar
        {
            Name = name,
            Position = pos with { Y = pos.Y + 0.05f },
            Rotation = new Vector3(0, rotY, 0),
        };

        CarVisualBuilder.AddCarVisual(car, color, police);
        car.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.85f, 0),
            Shape = new BoxShape3D { Size = new Vector3(4.5f, 1.3f, 1.85f) },
        });
        AddChild(car);
    }

    /// <summary>Kenney 车辆模型 + 碰撞盒；模型缺失退回程序化轿车。</summary>
    private void BuildModelVehicle(string name, Vector3 pos, float rotY, string asset)
    {
        var anchor = new StaticBody3D
        {
            Name = name,
            Position = pos,
            Rotation = new Vector3(0, rotY, 0),
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };

        Node3D? model = AssetLibrary.Instantiate(asset);
        if (model == null)
        {
            BuildCar(name + "_Fallback", pos, rotY, new Color(0.5f, 0.5f, 0.5f));
            return;
        }

        model.Name = "Model";
        model.Scale = Vector3.One * TruckScale;
        anchor.AddChild(model);
        anchor.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.9f, 0),
            Shape = new BoxShape3D { Size = new Vector3(4.6f, 1.8f, 2.0f) },
        });
        AddChild(anchor);
    }

    private void BuildAlleys()
    {
        // 东侧小巷：X 18..26
        AddVisual("AlleyEast", new Vector3(22, 0.02f, -7), new Vector3(8, 0.04f, 58), new Color(0.17f, 0.16f, 0.16f));

        // 后巷：Z -26..-20
        AddVisual("AlleyBack", new Vector3(0, 0.02f, -23), new Vector3(120, 0.04f, 6), new Color(0.15f, 0.15f, 0.15f));

        // 垃圾箱
        AddStatic("DumpsterA", new Vector3(14, 0.7f, -22.5f), new Vector3(2.4f, 1.4f, 1.4f), new Color(0.15f, 0.30f, 0.18f));
        AddStatic("DumpsterB", new Vector3(23.5f, 0.7f, -16), new Vector3(1.4f, 1.4f, 2.4f), new Color(0.30f, 0.18f, 0.12f));

        // 下水道井盖与坑口
        AddVisualCylinder("SewerLid", new Vector3(-8, 0.04f, -24.5f), 1.1f, 0.08f, new Color(0.06f, 0.06f, 0.07f));
        AddVisual("SewerGrate", new Vector3(-8, 0.10f, -24.5f), new Vector3(1.4f, 0.04f, 0.25f), new Color(0.30f, 0.30f, 0.32f));
    }

    // ---------- 银行主体 ----------

    private void BuildBankShell()
    {
        // 前墙 z=6：门洞 4.2m（X -2.1..2.1）
        AddWall("FrontWallW", new Vector3(-10.05f, 0, 6), new Vector3(15.9f, WallH, WallT));
        AddWall("FrontWallE", new Vector3(10.05f, 0, 6), new Vector3(15.9f, WallH, WallT));
        // 门楣
        AddStatic("FrontLintel", new Vector3(0, WallH - 0.4f, 6), new Vector3(4.6f, 0.8f, WallT), new Color(0.34f, 0.33f, 0.36f));

        // 大门（封锁时关闭）
        BuildMainDoor(new Vector3(0, 0, 6), new Vector3(4.2f, WallH - 0.8f, 0.45f));

        // 东墙 x=18：侧门洞 1.6m（Z -1.8..-0.2）
        AddWall("EastWallS", new Vector3(18, 0, 2.9f), new Vector3(WallT, WallH, 6.2f));
        AddWall("EastWallN", new Vector3(18, 0, -10.9f), new Vector3(WallT, WallH, 18.2f));

        // 后墙 z=-20：后门洞 1.6m（X -6.8..-5.2）
        AddWall("BackWallW", new Vector3(-12.4f, 0, -20), new Vector3(11.2f, WallH, WallT));
        AddWall("BackWallE", new Vector3(6.4f, 0, -20), new Vector3(23.2f, WallH, WallT));

        // 西墙 x=-18：完整
        AddWall("WestWall", new Vector3(-18, 0, -7), new Vector3(WallT, WallH, 26));

        // 前廊立柱（4 根，柱距 8m）
        for (int i = 0; i < 4; i++)
        {
            float x = -12 + i * 8;
            AddStaticCylinder($"Column_{i}", new Vector3(x, 0, 7.6f), 0.45f, WallH + 0.6f, new Color(0.55f, 0.53f, 0.50f));
        }

        // 檐口横梁
        AddVisual("Cornice", new Vector3(0, WallH + 0.45f, 7.6f), new Vector3(36.5f, 0.5f, 1.2f), new Color(0.40f, 0.39f, 0.42f));

        // 玻璃幕墙（视觉，位于门两侧）
        AddGlass("GlassW", new Vector3(-10, 2.6f, 6.06f), new Vector3(13.5f, 2.6f, 0.08f));
        AddGlass("GlassE", new Vector3(10, 2.6f, 6.06f), new Vector3(13.5f, 2.6f, 0.08f));

        // 台阶
        AddStatic("Step1", new Vector3(0, 0.07f, 7.0f), new Vector3(10, 0.14f, 1.4f), new Color(0.40f, 0.40f, 0.42f));
        AddStatic("Step2", new Vector3(0, 0.16f, 6.4f), new Vector3(8, 0.10f, 0.8f), new Color(0.44f, 0.44f, 0.46f));

        // 侧门 / 后门装饰门框（纯视觉）
        AssetLibrary.AddModel(this, AssetLibrary.DoorSlidingDouble, "SideDoorDeco",
            new Vector3(18, 0, -1.0f), Vector3.One * 1.3f, new Vector3(0, Mathf.Pi * 0.5f, 0));
        AssetLibrary.AddModel(this, AssetLibrary.DoorSlidingDouble, "BackDoorDeco",
            new Vector3(-6, 0, -20), Vector3.One * 1.3f, Vector3.Zero);
    }

    private void BuildBankInterior()
    {
        // 大厅/后区分隔墙 A：z=-6，门洞 x=-6（1.8m）与 x=10（2.2m）
        AddWall("WallA_W", new Vector3(-12.25f, 0, -6), new Vector3(10.7f, WallH, WallT));
        AddWall("WallA_M", new Vector3(2.0f, 0, -6), new Vector3(13.8f, WallH, WallT));
        AddWall("WallA_E", new Vector3(14.35f, 0, -6), new Vector3(6.5f, WallH, WallT));

        // 走廊南墙 B：z=-9（仅西半 X -17.6..0），门洞 x=-13 与 x=-4（各 1.6m）
        AddWall("WallB_W", new Vector3(-15.7f, 0, -9), new Vector3(3.8f, WallH, WallT));
        AddWall("WallB_M", new Vector3(-8.5f, 0, -9), new Vector3(7.4f, WallH, WallT));
        AddWall("WallB_E", new Vector3(-1.6f, 0, -9), new Vector3(3.2f, WallH, WallT));

        // 机房/办公室隔墙 x=-9
        AddWall("ServerDivider", new Vector3(-9, 0, -14.5f), new Vector3(WallT, WallH, 11));

        // 办公室/金库前厅隔墙 x=0
        AddWall("OfficeDivider", new Vector3(0, 0, -14.5f), new Vector3(WallT, WallH, 11));

        // 柜台（实体碰撞，可作掩体）
        AddStatic("TellerCounter", new Vector3(0, 0.55f, -3), new Vector3(20, 1.1f, 0.8f), new Color(0.36f, 0.26f, 0.18f));
        AddGlass("TellerGlass", new Vector3(0, 1.75f, -3), new Vector3(20, 1.1f, 0.06f));

        // 排队立柱
        for (int i = 0; i < 6; i++)
        {
            AddStaticCylinder($"Stanchion_{i}", new Vector3(-7.5f + i * 3, 0, 0.5f), 0.06f, 1.0f, new Color(0.65f, 0.55f, 0.20f));
        }
    }

    private void BuildVault()
    {
        // 金库围墙厚 0.8：西墙 x=9（门洞 z -12..-10），南墙 z=-9（X 9..17.6）
        AddThickWall("VaultWallW_N", new Vector3(9, 0, -16.0f), new Vector3(0.8f, WallH, 7.2f));
        AddThickWall("VaultWallW_S", new Vector3(9, 0, -9.5f), new Vector3(0.8f, WallH, 1.0f));
        AddThickWall("VaultWallS", new Vector3(13.3f, 0, -9), new Vector3(8.6f, WallH, 0.8f));

        // 内衬（北、东侧加厚）
        AddThickWall("VaultLinerN", new Vector3(13.3f, 0, -19.2f), new Vector3(8.6f, WallH, 0.8f));
        AddThickWall("VaultLinerE", new Vector3(17.2f, 0, -14.1f), new Vector3(0.8f, WallH, 10.2f));

        // 金库门（重型，常开视觉）
        var vaultDoor = new MeshInstance3D
        {
            Name = "VaultDoorVisual",
            Position = new Vector3(8.4f, 2.0f, -11.0f),
            RotationDegrees = new Vector3(0, 35, 0),
            Mesh = new BoxMesh { Size = new Vector3(0.5f, 3.6f, 2.0f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.56f, 0.60f),
                Metallic = 0.8f,
                Roughness = 0.35f,
            },
        };
        AddChild(vaultDoor);

        // 转轮
        AddVisualCylinder("VaultWheel", new Vector3(8.1f, 2.0f, -10.6f), 0.45f, 0.12f, new Color(0.75f, 0.74f, 0.70f), true);

        // 金库内：货架与金条堆
        AddStatic("VaultShelfN", new Vector3(13.3f, 1.0f, -18.2f), new Vector3(6.5f, 2.0f, 0.6f), new Color(0.30f, 0.30f, 0.33f));
        AddStatic("VaultShelfE", new Vector3(16.2f, 1.0f, -14), new Vector3(0.6f, 2.0f, 6.5f), new Color(0.30f, 0.30f, 0.33f));
        for (int i = 0; i < 4; i++)
        {
            AddVisual($"GoldStack_{i}", new Vector3(11.5f + (i % 2) * 3.4f, 0.28f, -16.5f + (i / 2) * 3.0f),
                new Vector3(1.0f, 0.55f, 0.7f), new Color(0.90f, 0.74f, 0.22f), true);
        }

        // 目标基座
        AddStaticCylinder("LootPedestal", new Vector3(13.3f, 0, -13.6f), 0.7f, 0.9f, new Color(0.25f, 0.25f, 0.28f));
    }

    private void BuildFurniture()
    {
        // 人质等候区（大厅西侧）：沙发 + 茶几 + 绿植
        AddStatic("SofaA", new Vector3(-13.5f, 0.4f, 1.0f), new Vector3(2.4f, 0.8f, 1.0f), new Color(0.30f, 0.20f, 0.30f));
        AddStatic("SofaB", new Vector3(-13.5f, 0.4f, 3.4f), new Vector3(2.4f, 0.8f, 1.0f), new Color(0.30f, 0.20f, 0.30f));
        AddStatic("CoffeeTable", new Vector3(-13.5f, 0.25f, 2.2f), new Vector3(1.2f, 0.5f, 0.8f), new Color(0.40f, 0.30f, 0.22f));
        AddVisualSphere("LobbyPlant", new Vector3(-16.5f, 1.2f, 4.5f), 0.7f, new Color(0.16f, 0.36f, 0.15f));

        // 办公室（X -9..0, Z -19.6..-9）：办公桌 ×2
        AddStatic("DeskA", new Vector3(-5.5f, 0.4f, -12), new Vector3(1.8f, 0.8f, 0.9f), new Color(0.42f, 0.32f, 0.22f));
        AddStatic("DeskB", new Vector3(-3.5f, 0.4f, -16), new Vector3(1.8f, 0.8f, 0.9f), new Color(0.42f, 0.32f, 0.22f));
        AddVisual("MonitorA", new Vector3(-5.5f, 1.05f, -12.2f), new Vector3(0.6f, 0.4f, 0.06f), new Color(0.05f, 0.08f, 0.10f), true);
        AddVisual("MonitorB", new Vector3(-3.5f, 1.05f, -16.2f), new Vector3(0.6f, 0.4f, 0.06f), new Color(0.05f, 0.08f, 0.10f), true);

        // 安防机房（X -17.6..-9）：机柜排
        for (int i = 0; i < 3; i++)
        {
            AddStatic($"Rack_{i}", new Vector3(-16.4f + i * 2.4f, 1.0f, -17.5f), new Vector3(1.0f, 2.0f, 0.8f), new Color(0.10f, 0.12f, 0.14f));
            AddVisual($"RackLed_{i}", new Vector3(-16.4f + i * 2.4f, 1.5f, -17.05f), new Vector3(0.7f, 0.5f, 0.03f), new Color(0.10f, 0.80f, 0.45f), true);
        }

        // 监控台
        AddStatic("SecurityDesk", new Vector3(-13, 0.4f, -10.5f), new Vector3(2.4f, 0.8f, 0.9f), new Color(0.20f, 0.22f, 0.26f));
    }

    private void BuildExteriorProps()
    {
        // 警车（一辆可驾驶巡逻车 + 一辆静态）与警用面包车（路面北侧）
        BuildDriveableCar("Drive_Patrol", new Vector3(-9, 0, 27), 0.08f, new Color(0.92f, 0.92f, 0.95f), police: true);
        BuildCar("PoliceCarB", new Vector3(4, 0, 27.5f), -0.06f, new Color(0.92f, 0.92f, 0.95f), true);
        BuildVan("PoliceVan", new Vector3(13, 0, 27), 0.1f, new Color(0.16f, 0.22f, 0.42f));

        // 路灯（南街 + 后巷）
        float[] lampXs = { -40, -20, 0, 20, 40 };
        foreach (float x in lampXs)
        {
            BuildStreetLamp($"LampS_{x}", new Vector3(x, 0, 21.2f));
        }
        BuildStreetLamp("LampBackA", new Vector3(-16, 0, -20.8f));
        BuildStreetLamp("LampBackB", new Vector3(10, 0, -20.8f));

        // 消防栓 / 邮筒点缀
        AddStaticCylinder("Hydrant", new Vector3(-22, 0, 20.5f), 0.18f, 0.8f, new Color(0.75f, 0.15f, 0.10f));
        AddStatic("MailBox", new Vector3(18.5f, 0.55f, 20.5f), new Vector3(0.6f, 1.1f, 0.6f), new Color(0.15f, 0.35f, 0.20f));

        // 银行招牌
        var sign = new Label3D
        {
            Name = "BankSign",
            Text = Loc.T("map.bank"),
            Position = new Vector3(0, WallH + 1.3f, 6.6f),
            FontSize = 220,
            PixelSize = 0.01f,
            Modulate = new Color(0.95f, 0.80f, 0.30f),
            OutlineSize = 24,
        };
        AddChild(sign);
        _locLabels.Add((sign, "map.bank"));
    }

    private void BuildPerimeter()
    {
        // 世界边界：隐形碰撞墙 + Kenney 临街建筑模型排
        AddInvisibleWall("BoundSouth", new Vector3(0, 6, 33), new Vector3(120, 12, 2));
        AddInvisibleWall("BoundNorth", new Vector3(0, 6, -29), new Vector3(120, 12, 2));
        AddInvisibleWall("BoundWest", new Vector3(-59, 6, -3), new Vector3(2, 12, 74));
        AddInvisibleWall("BoundEast", new Vector3(27, 6, -3), new Vector3(2, 12, 74));

        string[] facadePool =
        {
            AssetLibrary.BuildingSmallA,
            AssetLibrary.BuildingSmallB,
            AssetLibrary.BuildingSmallC,
            AssetLibrary.BuildingSmallD,
            AssetLibrary.BuildingGarage,
        };

        bool anyModel = false;
        int index = 0;

        // 南北两排（沿街）
        for (int x = -52; x <= 52; x += 13)
        {
            anyModel |= AssetLibrary.AddModel(this, facadePool[index++ % facadePool.Length],
                $"FacadeS_{x}", new Vector3(x, 0, 35.5f), Vector3.One * FacadeBuildingScale,
                new Vector3(0, Mathf.Pi, 0)) != null;
            anyModel |= AssetLibrary.AddModel(this, facadePool[index++ % facadePool.Length],
                $"FacadeN_{x}", new Vector3(x, 0, -31.5f), Vector3.One * FacadeBuildingScale,
                Vector3.Zero) != null;
        }

        // 东西两排
        for (int z = -24; z <= 24; z += 16)
        {
            anyModel |= AssetLibrary.AddModel(this, facadePool[index++ % facadePool.Length],
                $"FacadeW_{z}", new Vector3(-61.5f, 0, z), Vector3.One * FacadeBuildingScale,
                new Vector3(0, Mathf.Pi * 0.5f, 0)) != null;
            anyModel |= AssetLibrary.AddModel(this, facadePool[index++ % facadePool.Length],
                $"FacadeE_{z}", new Vector3(29.5f, 0, z), Vector3.One * FacadeBuildingScale,
                new Vector3(0, -Mathf.Pi * 0.5f, 0)) != null;
        }

        // 模型缺失时退回程序化立面墙
        if (!anyModel)
        {
            Color facadeA = new(0.24f, 0.22f, 0.24f);
            AddStatic("FacadeSouthFallback", new Vector3(0, 6, 33), new Vector3(120, 12, 2), facadeA);
            AddStatic("FacadeNorthFallback", new Vector3(0, 6, -29), new Vector3(120, 12, 2), facadeA);
            AddStatic("FacadeWestFallback", new Vector3(-59, 6, -3), new Vector3(2, 12, 74), facadeA);
            AddStatic("FacadeEastFallback", new Vector3(27, 6, -3), new Vector3(2, 12, 74), facadeA);
        }
    }

    // ---------- 交互点 / NPC / 灯光 ----------

    private void BuildObjectives()
    {
        AddObjective("EnterBank", BankActionType.EnterBank, "obj.enterbank", new Vector3(0, 0, 8.2f), 2.2f, new Color(0.22f, 0.65f, 0.30f));
        AddObjective("DisguiseRack", BankActionType.ChangeDisguise, "obj.disguise", new Vector3(-12, 0, 16), 1.8f, new Color(0.45f, 0.38f, 0.28f));
        AddObjective("PoliceShoutLine", BankActionType.PoliceShoutDown, "obj.shout", new Vector3(5, 0, 21), 2.0f, new Color(0.16f, 0.25f, 0.75f));

        AddObjective("RobberPhone", BankActionType.Negotiate, "obj.phone", new Vector3(3.5f, 0, 1.5f), 1.6f, new Color(0.10f, 0.45f, 0.50f));
        AddObjective("PoliceMegaphone", BankActionType.Negotiate, "obj.megaphone", new Vector3(-6, 0, 25), 1.6f, new Color(0.12f, 0.22f, 0.75f));

        AddObjective("VaultLoot", BankActionType.VaultLoot, "obj.vault", new Vector3(13.3f, 0, -13.6f), 2.0f, new Color(0.90f, 0.70f, 0.18f));

        AddObjective("ReleaseHostage", BankActionType.ReleaseHostage, "obj.release", new Vector3(-11, 0, 1.0f), 1.5f, new Color(0.18f, 0.65f, 0.32f));
        AddObjective("KillHostage", BankActionType.KillHostage, "obj.kill", new Vector3(-11, 0, 3.5f), 1.4f, new Color(0.70f, 0.10f, 0.08f));
        AddObjective("FakeHostage", BankActionType.FakeHostage, "obj.fake", new Vector3(-15.8f, 0, 0.0f), 1.4f, new Color(0.75f, 0.55f, 0.18f));
        AddObjective("RecordMessage", BankActionType.RecordMessage, "obj.record", new Vector3(-15.8f, 0, -3.0f), 1.4f, new Color(0.55f, 0.22f, 0.55f));

        AddObjective("PoliceFrontEntry", BankActionType.PoliceEntryFront, "obj.entryfront", new Vector3(0, 0, 10.8f), 1.5f, new Color(0.10f, 0.28f, 0.78f));
        AddObjective("PoliceSideEntry", BankActionType.PoliceEntrySide, "obj.entryside", MapLocations.SideDoorOutside with { Y = 0 }, 1.5f, new Color(0.10f, 0.28f, 0.78f));
        AddObjective("PoliceBackEntry", BankActionType.PoliceEntryBack, "obj.entryback", MapLocations.BackDoorOutside with { Y = 0 }, 1.5f, new Color(0.10f, 0.28f, 0.78f));

        AddObjective("BlockSideExit", BankActionType.BlockSideExit, "obj.blockside", MapLocations.BlockSidePos with { Y = 0 }, 1.6f, new Color(0.10f, 0.18f, 0.55f));
        AddObjective("BlockSewerExit", BankActionType.BlockSewerExit, "obj.blocksewer", MapLocations.BlockSewerPos with { Y = 0 }, 1.6f, new Color(0.10f, 0.18f, 0.55f));

        AddObjective("EscapeSideDoor", BankActionType.EscapeSideDoor, "obj.escapeside", new Vector3(22, 0, 8), 2.2f, new Color(0.88f, 0.52f, 0.12f));
        AddObjective("EscapeSewer", BankActionType.EscapeSewer, "obj.escapesewer", new Vector3(-8, 0, -24.5f), 2.2f, new Color(0.18f, 0.56f, 0.48f));
    }

    private void BuildNpcs()
    {
        // 广场人群（漫游）
        Vector3[] crowd =
        {
            new(-8, 0, 11), new(-4, 0, 14), new(-1, 0, 10), new(2.5f, 0, 15),
            new(6, 0, 11), new(9, 0, 17), new(-10, 0, 18), new(15, 0, 12),
            new(18, 0, 17), new(-6, 0, 19),
        };
        for (int i = 0; i < crowd.Length; i++)
        {
            AddNpc($"Crowd_{i}", BankNpcKind.Civilian, crowd[i], i, wanderRadius: 2.5f);
        }

        // 人质（大厅西侧等候区，静止）
        Vector3[] hostages =
        {
            new(-12.5f, 0, 0.2f), new(-14.5f, 0, 1.2f), new(-12.0f, 0, 2.2f),
            new(-14.8f, 0, 3.2f), new(-12.5f, 0, 4.2f), new(-16.2f, 0, 2.0f),
            new(-16.2f, 0, -1.0f),
        };
        for (int i = 0; i < hostages.Length; i++)
        {
            AddNpc($"Hostage_{i}", BankNpcKind.Hostage, hostages[i], 100 + i, wanderRadius: 0.0f);
        }
    }

    private void BuildLighting()
    {
        // 大厅吊灯
        AddOmni("HallLightW", new Vector3(-8, 4.4f, 0), new Color(1.0f, 0.92f, 0.78f), 2.2f, 12);
        AddOmni("HallLightE", new Vector3(8, 4.4f, 0), new Color(1.0f, 0.92f, 0.78f), 2.2f, 12);
        AddOmni("CorridorLight", new Vector3(-8, 4.2f, -7.5f), new Color(0.95f, 0.95f, 0.90f), 1.4f, 9);
        AddOmni("OfficeLight", new Vector3(-4.5f, 4.2f, -14), new Color(0.95f, 0.95f, 0.90f), 1.3f, 9);
        AddOmni("ServerLight", new Vector3(-13.5f, 4.2f, -14), new Color(0.45f, 0.85f, 0.80f), 1.3f, 9);
        AddOmni("VaultLight", new Vector3(13.3f, 4.0f, -14), new Color(1.0f, 0.85f, 0.55f), 1.6f, 10);
        AddOmni("AnteLight", new Vector3(4.5f, 4.2f, -13), new Color(0.95f, 0.95f, 0.90f), 1.2f, 9);
    }

    private void BuildRoomLabels()
    {
        AddRoomLabel("map.hall", new Vector3(0, 3.6f, 0));
        AddRoomLabel("map.vault", new Vector3(13.3f, 3.4f, -14));
        AddRoomLabel("map.offices", new Vector3(-4.5f, 3.4f, -14));
        AddRoomLabel("map.server", new Vector3(-13.5f, 3.4f, -14));
        AddRoomLabel("map.hostage", new Vector3(-13.5f, 2.8f, 2));
        AddRoomLabel("map.plaza", new Vector3(0, 3.0f, 14));
        AddRoomLabel("map.policeline", new Vector3(0, 2.2f, 22));
        AddRoomLabel("map.parking", new Vector3(-42, 3.0f, 13));
        AddRoomLabel("map.sideexit", new Vector3(22, 2.6f, 8));
        AddRoomLabel("map.sewer", new Vector3(-8, 2.2f, -24.5f));
        AddRoomLabel("map.street", new Vector3(0, 2.6f, 26));
    }

    private void RefreshLabels()
    {
        foreach ((Label3D label, string key) in _locLabels)
        {
            if (IsInstanceValid(label))
            {
                label.Text = Loc.T(key);
            }
        }
    }

    // ---------- 构件辅助 ----------

    private void BuildMainDoor(Vector3 pos, Vector3 size)
    {
        var door = new BankDoor
        {
            Name = "MainBankDoor",
            Position = pos,
            StartsClosed = false,
        };

        door.AddChild(new MeshInstance3D
        {
            Name = "Mesh",
            Position = new Vector3(0, size.Y * 0.5f, 0),
            Mesh = new BoxMesh { Size = size },
        });
        door.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Position = new Vector3(0, size.Y * 0.5f, 0),
            Shape = new BoxShape3D { Size = size },
        });
        var label = new Label3D
        {
            Name = "Label",
            Position = new Vector3(0, size.Y + 0.6f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 28,
        };
        door.AddChild(label);
        AddChild(door);
    }

    private void BuildCar(string name, Vector3 pos, float rotY, Color color, bool police = false)
    {
        var car = new StaticBody3D
        {
            Name = name,
            Position = pos,
            Rotation = new Vector3(0, rotY, 0),
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };

        CarVisualBuilder.AddCarVisual(car, color, police);
        car.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.7f, 0),
            Shape = new BoxShape3D { Size = new Vector3(4.5f, 1.4f, 1.8f) },
        });
        AddChild(car);
    }

    private void BuildVan(string name, Vector3 pos, float rotY, Color color)
    {
        var van = new StaticBody3D
        {
            Name = name,
            Position = pos,
            Rotation = new Vector3(0, rotY, 0),
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };
        van.AddChild(Mesh("Body", new Vector3(0, 1.1f, 0), new BoxMesh { Size = new Vector3(5.2f, 2.0f, 2.0f) }, color));
        van.AddChild(Mesh("Glass", new Vector3(2.2f, 1.55f, 0), new BoxMesh { Size = new Vector3(0.6f, 0.7f, 1.9f) }, new Color(0.30f, 0.40f, 0.48f)));
        van.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 1.1f, 0),
            Shape = new BoxShape3D { Size = new Vector3(5.2f, 2.2f, 2.0f) },
        });
        AddChild(van);
    }

    private void BuildStreetLamp(string name, Vector3 pos)
    {
        var lamp = new Node3D { Name = name, Position = pos };
        lamp.AddChild(Mesh("Pole", new Vector3(0, 3, 0), new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.10f, Height = 6 }, new Color(0.20f, 0.21f, 0.23f)));
        lamp.AddChild(Mesh("Arm", new Vector3(0, 5.9f, 0.6f), new BoxMesh { Size = new Vector3(0.12f, 0.12f, 1.4f) }, new Color(0.20f, 0.21f, 0.23f)));
        lamp.AddChild(Mesh("Bulb", new Vector3(0, 5.8f, 1.2f), new SphereMesh { Radius = 0.16f, Height = 0.32f }, new Color(1.0f, 0.90f, 0.65f), emissive: true));
        lamp.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 5.6f, 1.2f),
            LightColor = new Color(1.0f, 0.88f, 0.62f),
            LightEnergy = 1.3f,
            OmniRange = 13,
        });
        AddChild(lamp);
    }

    private void AddObjective(string name, BankActionType action, string promptKey, Vector3 pos, float radius, Color color)
    {
        var objective = new BankObjective
        {
            Name = name,
            Position = pos with { Y = 0.35f },
            ActionType = action,
            PromptKey = promptKey,
        };

        objective.AddChild(new MeshInstance3D
        {
            Name = "Marker",
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.08f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = true,
                    Emission = color,
                    EmissionEnergyMultiplier = 0.4f,
                },
            },
        });
        objective.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new SphereShape3D { Radius = radius },
        });
        objective.AddChild(new Label3D
        {
            Name = "Label",
            Position = new Vector3(0, 1.9f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 22,
        });
        AddChild(objective);
    }

    private void AddNpc(string name, BankNpcKind kind, Vector3 position, int seed, float wanderRadius)
    {
        var npc = new BankNpc
        {
            Name = name,
            Kind = kind,
            Position = position with { Y = 0.05f },
            Seed = seed,
            WanderRadius = wanderRadius,
        };
        AddChild(npc);
    }

    private void AddRoomLabel(string key, Vector3 position)
    {
        var label = new Label3D
        {
            Name = $"Label_{key.Replace('.', '_')}",
            Text = Loc.T(key),
            Position = position,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34,
            Modulate = new Color(0.85f, 0.90f, 0.92f, 0.85f),
            OutlineSize = 10,
        };
        AddChild(label);
        _locLabels.Add((label, key));
    }

    private void AddWall(string name, Vector3 pos, Vector3 size)
    {
        AddStatic(name, new Vector3(pos.X, size.Y * 0.5f, pos.Z), size, new Color(0.52f, 0.49f, 0.45f));
    }

    private void AddThickWall(string name, Vector3 pos, Vector3 size)
    {
        AddStatic(name, new Vector3(pos.X, size.Y * 0.5f, pos.Z), size, new Color(0.34f, 0.35f, 0.38f));
    }

    private void AddInvisibleWall(string name, Vector3 pos, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = pos,
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    private void AddInvisibleCylinder(string name, Vector3 basePos, float radius, float height)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = basePos with { Y = basePos.Y + height * 0.5f },
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } });
        AddChild(body);
    }

    private void AddStatic(string name, Vector3 pos, Vector3 size, Color color)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = pos,
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(Mesh("Mesh", Vector3.Zero, new BoxMesh { Size = size }, color));
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    private void AddStaticCylinder(string name, Vector3 basePos, float radius, float height, Color color)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = basePos with { Y = basePos.Y + height * 0.5f },
            CollisionLayer = GameLayers.World,
            CollisionMask = 0,
        };
        body.AddChild(Mesh("Mesh", Vector3.Zero, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height }, color));
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } });
        AddChild(body);
    }

    private void AddVisual(string name, Vector3 pos, Vector3 size, Color color, bool emissive = false)
    {
        AddChild(Mesh(name, pos, new BoxMesh { Size = size }, color, emissive));
    }

    private void AddVisualSphere(string name, Vector3 pos, float radius, Color color)
    {
        AddChild(Mesh(name, pos, new SphereMesh { Radius = radius, Height = radius * 2 }, color));
    }

    private void AddVisualCylinder(string name, Vector3 pos, float radius, float height, Color color, bool emissive = false)
    {
        AddChild(Mesh(name, pos, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height }, color, emissive));
    }

    private void AddGlass(string name, Vector3 pos, Vector3 size)
    {
        var mesh = Mesh(name, pos, new BoxMesh { Size = size }, new Color(0.45f, 0.65f, 0.75f, 0.35f));
        if (mesh.MaterialOverride is StandardMaterial3D mat)
        {
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.Roughness = 0.1f;
            mat.Metallic = 0.4f;
        }
        AddChild(mesh);
    }

    private void AddOmni(string name, Vector3 pos, Color color, float energy, float range)
    {
        AddChild(new OmniLight3D
        {
            Name = name,
            Position = pos,
            LightColor = color,
            LightEnergy = energy,
            OmniRange = range,
        });
    }

    private static MeshInstance3D Mesh(string name, Vector3 pos, Mesh mesh, Color color, bool emissive = false)
    {
        return new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = 0.8f,
                EmissionEnabled = emissive,
                Emission = color,
                EmissionEnergyMultiplier = emissive ? 0.55f : 0.0f,
            },
        };
    }
}
