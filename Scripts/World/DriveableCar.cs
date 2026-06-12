using Godot;

namespace PhaseHeist;

/// <summary>
/// 可驾驶载具（街机手感）：E 上车 → WASD 驾驶（W/S 油门刹车、A/D 转向）→ E 下车。
/// 占用权由服务端裁决；位姿由驾驶端上报、服务端转播。
/// 车头方向 = 本地 +X（与程序化车模型一致）。
/// </summary>
public partial class DriveableCar : CharacterBody3D, IInteractable
{
    [Export] public float MaxSpeed { get; set; } = 13.0f;     // ≈ 47 km/h
    [Export] public float ReverseMax { get; set; } = 5.0f;
    [Export] public float Accel { get; set; } = 9.0f;
    [Export] public float BrakeAccel { get; set; } = 18.0f;
    [Export] public float Friction { get; set; } = 6.0f;
    [Export] public float SteerSpeed { get; set; } = 1.7f;

    public int DriverPeerId { get; private set; }
    public bool Occupied => DriverPeerId != 0;
    public float CurrentSpeed { get; private set; }

    public float InteractRadius => 2.6f;

    private Camera3D? _camera;
    private Game? _game;
    private Vector3 _remoteTargetPosition;
    private float _remoteTargetYaw;

    public override void _Ready()
    {
        AddToGroup("vehicles");
        CollisionLayer = GameLayers.World;
        CollisionMask = GameLayers.World;
        _game = GetTree().CurrentScene as Game;
        _remoteTargetPosition = GlobalPosition;
        _remoteTargetYaw = Rotation.Y;

        // 第三人称跟车相机：吊臂朝车头（+X）方向看
        var pivot = new Node3D
        {
            Name = "CamPivot",
            Position = new Vector3(0, 1.4f, 0),
            RotationDegrees = new Vector3(0, -90, 0),
        };
        AddChild(pivot);
        _camera = new Camera3D
        {
            Name = "Camera3D",
            Position = new Vector3(0, 2.4f, 6.0f),
            RotationDegrees = new Vector3(-14, 0, 0),
            Current = false,
            Fov = 70,
        };
        pivot.AddChild(_camera);
    }

    public override void _PhysicsProcess(double delta)
    {
        bool localDriving = IsLocalDriver();

        if (localDriving)
        {
            SimulateDriving((float)delta);
            _game?.SendVehicleTransform(Name, GlobalPosition, Rotation.Y);
        }
        else if (Occupied)
        {
            // 远端驾驶：插值跟随
            GlobalPosition = GlobalPosition.Lerp(_remoteTargetPosition, 12.0f * (float)delta);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _remoteTargetYaw, 12.0f * (float)delta), 0);
        }
    }

    private void SimulateDriving(float dt)
    {
        float throttle = Game.UiBlocking
            ? 0.0f
            : Input.GetAxis("move_back", "move_forward");
        float steer = Game.UiBlocking
            ? 0.0f
            : Input.GetAxis("move_right", "move_left");

        // 纵向：油门 / 刹车 / 滑行摩擦
        if (Mathf.Abs(throttle) > 0.05f)
        {
            bool braking = Mathf.Sign(throttle) != Mathf.Sign(CurrentSpeed) && Mathf.Abs(CurrentSpeed) > 0.5f;
            CurrentSpeed += throttle * (braking ? BrakeAccel : Accel) * dt;
        }
        else
        {
            CurrentSpeed = Mathf.MoveToward(CurrentSpeed, 0, Friction * dt);
        }

        CurrentSpeed = Mathf.Clamp(CurrentSpeed, -ReverseMax, MaxSpeed);

        // 转向随速度衰减，低速更灵活；倒车反向
        if (Mathf.Abs(CurrentSpeed) > 0.3f)
        {
            float steerFactor = Mathf.Clamp(Mathf.Abs(CurrentSpeed) / 5.0f, 0.35f, 1.0f);
            RotateY(steer * SteerSpeed * steerFactor * Mathf.Sign(CurrentSpeed) * dt);
        }

        Vector3 forward = GlobalTransform.Basis.X;
        Vector3 velocity = forward * CurrentSpeed;
        velocity.Y = IsOnFloor() ? 0 : Velocity.Y - 14.0f * dt;
        Velocity = velocity;
        MoveAndSlide();

        // 碰撞减速
        if (GetSlideCollisionCount() > 0)
        {
            CurrentSpeed *= 0.82f;
        }
    }

    // ---------- 占用 ----------

    public void SetDriver(int peerId)
    {
        DriverPeerId = peerId;

        if (peerId == 0)
        {
            CurrentSpeed = 0;
            if (_camera != null)
            {
                _camera.Current = false;
            }
        }
    }

    public void ActivateCamera(bool active)
    {
        if (_camera != null)
        {
            _camera.Current = active;
        }
    }

    public void ApplyNetworkTransform(Vector3 position, float yaw)
    {
        _remoteTargetPosition = position;
        _remoteTargetYaw = yaw;
    }

    public Vector3 ExitPosition()
    {
        // 车左侧 2m（本地 +Z 是车左）
        return GlobalPosition + GlobalTransform.Basis.Z * 2.2f + Vector3.Up * 0.2f;
    }

    private bool IsLocalDriver()
    {
        if (!Occupied)
        {
            return false;
        }

        int localId = Multiplayer.MultiplayerPeer != null ? Multiplayer.GetUniqueId() : 1;
        return DriverPeerId == localId;
    }

    // ---------- 交互 ----------

    public bool CanInteract(Node3D actor)
    {
        return actor is PlayerController { InVehicle: false } && !Occupied;
    }

    public void Interact(Node3D actor)
    {
        if (actor is PlayerController player)
        {
            _game?.RequestEnterVehicle(player.PeerId, Name);
        }
    }

    public string GetInteractPrompt()
    {
        return Loc.T("obj.drive");
    }
}

/// <summary>程序化车辆视觉（静态摆设与可驾驶载具共用）。</summary>
public static class CarVisualBuilder
{
    public static void AddCarVisual(Node3D parent, Color bodyColor, bool police)
    {
        Color glass = new(0.22f, 0.30f, 0.38f);
        Color dark = new(0.06f, 0.06f, 0.07f);
        Color trim = bodyColor.Darkened(0.30f);

        // 底盘 + 引擎盖 + 尾箱 + 座舱（带斜面观感的分段车身）
        AddBox(parent, "Chassis", new Vector3(0, 0.42f, 0), new Vector3(4.5f, 0.30f, 1.82f), dark);
        AddBox(parent, "BodyMain", new Vector3(0, 0.70f, 0), new Vector3(4.5f, 0.34f, 1.80f), bodyColor);
        AddBox(parent, "Hood", new Vector3(1.55f, 0.92f, 0), new Vector3(1.35f, 0.18f, 1.72f), police ? dark : bodyColor);
        AddBox(parent, "Trunk", new Vector3(-1.70f, 0.94f, 0), new Vector3(1.05f, 0.22f, 1.72f), bodyColor);
        AddBox(parent, "Cabin", new Vector3(-0.18f, 1.18f, 0), new Vector3(2.05f, 0.40f, 1.66f), police ? new Color(0.92f, 0.92f, 0.95f) : bodyColor);
        AddBox(parent, "GlassFront", new Vector3(0.92f, 1.16f, 0), new Vector3(0.46f, 0.34f, 1.58f), glass);
        AddBox(parent, "GlassRear", new Vector3(-1.28f, 1.16f, 0), new Vector3(0.36f, 0.32f, 1.58f), glass);
        AddBox(parent, "BumperF", new Vector3(2.30f, 0.52f, 0), new Vector3(0.16f, 0.24f, 1.80f), trim);
        AddBox(parent, "BumperR", new Vector3(-2.30f, 0.52f, 0), new Vector3(0.16f, 0.24f, 1.80f), trim);

        // 车灯
        AddBox(parent, "HeadL", new Vector3(2.27f, 0.78f, 0.62f), new Vector3(0.08f, 0.12f, 0.34f), new Color(1.0f, 0.95f, 0.75f), true);
        AddBox(parent, "HeadR", new Vector3(2.27f, 0.78f, -0.62f), new Vector3(0.08f, 0.12f, 0.34f), new Color(1.0f, 0.95f, 0.75f), true);
        AddBox(parent, "TailL", new Vector3(-2.27f, 0.80f, 0.62f), new Vector3(0.08f, 0.10f, 0.30f), new Color(0.85f, 0.10f, 0.08f), true);
        AddBox(parent, "TailR", new Vector3(-2.27f, 0.80f, -0.62f), new Vector3(0.08f, 0.10f, 0.30f), new Color(0.85f, 0.10f, 0.08f), true);

        // 车轮
        float[] wx = { -1.45f, 1.45f };
        float[] wz = { -0.86f, 0.86f };
        foreach (float x in wx)
        {
            foreach (float z in wz)
            {
                var wheel = new MeshInstance3D
                {
                    Name = $"Wheel_{x}_{z}",
                    Position = new Vector3(x, 0.33f, z),
                    Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
                    Mesh = new CylinderMesh { TopRadius = 0.33f, BottomRadius = 0.33f, Height = 0.24f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = dark, Roughness = 0.9f },
                };
                parent.AddChild(wheel);

                var hub = new MeshInstance3D
                {
                    Name = $"Hub_{x}_{z}",
                    Position = new Vector3(x, 0.33f, z + Mathf.Sign(z) * 0.125f),
                    Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
                    Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.14f, Height = 0.02f },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.62f), Metallic = 0.7f, Roughness = 0.35f },
                };
                parent.AddChild(hub);
            }
        }

        if (police)
        {
            // 小型警灯条（红蓝分色）
            AddBox(parent, "LightBarBase", new Vector3(-0.18f, 1.42f, 0), new Vector3(0.55f, 0.06f, 1.05f), dark);
            AddBox(parent, "LightBlue", new Vector3(-0.18f, 1.50f, 0.28f), new Vector3(0.45f, 0.10f, 0.40f), new Color(0.15f, 0.35f, 1.0f), true);
            AddBox(parent, "LightRed", new Vector3(-0.18f, 1.50f, -0.28f), new Vector3(0.45f, 0.10f, 0.40f), new Color(1.0f, 0.12f, 0.10f), true);
            // 车门蓝带
            AddBox(parent, "DoorStripeL", new Vector3(0, 0.70f, 0.915f), new Vector3(2.4f, 0.16f, 0.015f), new Color(0.14f, 0.28f, 0.72f));
            AddBox(parent, "DoorStripeR", new Vector3(0, 0.70f, -0.915f), new Vector3(2.4f, 0.16f, 0.015f), new Color(0.14f, 0.28f, 0.72f));
        }
    }

    private static void AddBox(Node3D parent, string name, Vector3 pos, Vector3 size, Color color, bool emissive = false)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = pos,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = emissive ? 0.4f : 0.55f,
                Metallic = emissive ? 0.0f : 0.25f,
                EmissionEnabled = emissive,
                Emission = color,
                EmissionEnergyMultiplier = emissive ? 0.7f : 0.0f,
            },
        });
    }
}
