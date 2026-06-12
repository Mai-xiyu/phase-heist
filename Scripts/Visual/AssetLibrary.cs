using Godot;

namespace PhaseHeist;

/// <summary>Kenney CC0 模型库（见 Assets/Kenney/ATTRIBUTION.md）。</summary>
public static class AssetLibrary
{
    // BlasterKit
    public const string Sidearm = "res://Assets/Kenney/BlasterKit/phase-sidearm.glb";
    public const string CrateMedium = "res://Assets/Kenney/BlasterKit/crate-medium.glb";
    public const string CrateSmall = "res://Assets/Kenney/BlasterKit/crate-small.glb";

    // PrototypeKit
    public const string DoorSlidingDouble = "res://Assets/Kenney/PrototypeKit/door-sliding-double.glb";
    public const string DoorGarage = "res://Assets/Kenney/PrototypeKit/door-garage.glb";
    public const string Column = "res://Assets/Kenney/PrototypeKit/column.glb";
    public const string Pipe = "res://Assets/Kenney/PrototypeKit/pipe.glb";
    public const string IndicatorArea = "res://Assets/Kenney/PrototypeKit/indicator-area.glb";

    // CityKit（Starter Kit City Builder）
    public const string BuildingSmallA = "res://Assets/Kenney/CityKit/building-small-a.glb";
    public const string BuildingSmallB = "res://Assets/Kenney/CityKit/building-small-b.glb";
    public const string BuildingSmallC = "res://Assets/Kenney/CityKit/building-small-c.glb";
    public const string BuildingSmallD = "res://Assets/Kenney/CityKit/building-small-d.glb";
    public const string BuildingGarage = "res://Assets/Kenney/CityKit/building-garage.glb";
    public const string PavementFountain = "res://Assets/Kenney/CityKit/pavement-fountain.glb";
    public const string Trees = "res://Assets/Kenney/CityKit/grass-trees.glb";
    public const string TreesTall = "res://Assets/Kenney/CityKit/grass-trees-tall.glb";

    // RacingKit（Starter Kit Racing）
    public const string TruckGreen = "res://Assets/Kenney/RacingKit/vehicle-truck-green.glb";
    public const string TruckPurple = "res://Assets/Kenney/RacingKit/vehicle-truck-purple.glb";
    public const string TruckRed = "res://Assets/Kenney/RacingKit/vehicle-truck-red.glb";
    public const string TruckYellow = "res://Assets/Kenney/RacingKit/vehicle-truck-yellow.glb";

    public static Node3D? Instantiate(string resourcePath)
    {
        var scene = ResourceLoader.Load<PackedScene>(resourcePath);
        return scene?.Instantiate<Node3D>();
    }

    /// <summary>实例化模型并挂到 parent；失败返回 null（资产缺失时静默降级）。</summary>
    public static Node3D? AddModel(Node3D parent, string resourcePath, string name, Vector3 position, Vector3 scale, Vector3 rotation)
    {
        Node3D? instance = Instantiate(resourcePath);
        if (instance == null)
        {
            return null;
        }

        instance.Name = name;
        instance.Position = position;
        instance.Scale = scale;
        instance.Rotation = rotation;
        parent.AddChild(instance);
        return instance;
    }
}
