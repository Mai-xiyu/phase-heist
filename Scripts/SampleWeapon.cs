using Godot;
using System;
using PhaseHeist;

/// <summary>命中扫描手枪。命中判定在客户端取样，伤害由服务端裁决。</summary>
public partial class SampleWeapon : Node3D
{
    [Export] public int Damage { get; set; } = Balance.WeaponDamage;
    [Export] public float FireRate { get; set; } = Balance.WeaponFireRate;
    [Export] public float Range { get; set; } = Balance.WeaponRange;

    private PlayerController? _owner;
    private RayCast3D? _aimRay;
    private MeshInstance3D? _muzzle;
    private double _cooldownSeconds;
    private double _flashSeconds;

    public override void _Ready()
    {
        _muzzle = GetNodeOrNull<MeshInstance3D>("Muzzle");

        // Kenney 手枪模型替换占位盒（加载失败则保留盒体）。
        // 缩小并贴向右下角，避免视图模型占屏过大。
        Node3D? model = AssetLibrary.AddModel(
            this, AssetLibrary.Sidearm, "SidearmModel",
            new Vector3(0.02f, -0.10f, 0.16f), Vector3.One * 0.38f, new Vector3(0, Mathf.Pi, 0));
        if (model != null && GetNodeOrNull<MeshInstance3D>("WeaponMesh") is MeshInstance3D box)
        {
            box.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        _cooldownSeconds = Math.Max(0.0, _cooldownSeconds - delta);
        _flashSeconds = Math.Max(0.0, _flashSeconds - delta);

        if (_muzzle != null)
        {
            _muzzle.Visible = _flashSeconds > 0.0;
        }
    }

    public void Configure(PlayerController owner, RayCast3D aimRay)
    {
        _owner = owner;
        _aimRay = aimRay;
        _aimRay.TargetPosition = new Vector3(0, 0, -Range);
        _aimRay.CollisionMask = GameLayers.World | GameLayers.Player;
    }

    public bool TryFire()
    {
        if (_owner == null || _aimRay == null || _cooldownSeconds > 0.0)
        {
            return false;
        }

        _cooldownSeconds = 1.0 / Math.Max(FireRate, 0.1f);
        _aimRay.ForceRaycastUpdate();

        int hitPeerId = 0;
        if (_aimRay.IsColliding() &&
            FindPlayerFromCollider(_aimRay.GetCollider()) is PlayerController target &&
            target.PeerId != _owner.PeerId)
        {
            hitPeerId = target.PeerId;
        }

        _owner.RequestFire(hitPeerId);
        return true;
    }

    public void ShowMuzzleFlash(bool hit)
    {
        _flashSeconds = hit ? 0.12 : 0.06;
    }

    private static PlayerController? FindPlayerFromCollider(GodotObject collider)
    {
        Node? node = collider as Node;
        while (node != null)
        {
            if (node is PlayerController player)
            {
                return player;
            }

            node = node.GetParent();
        }

        return null;
    }
}
