using Godot;

namespace PhaseHeist;

/// <summary>
/// 地图关键坐标。CityMapBuilder 与 Game 的传送/判定共用，
/// 保证逻辑与几何一致。坐标单位：米。
/// 约定：银行营业厅中心为原点，+Z 朝南（临街），-Z 朝北（后巷）。
/// </summary>
public static class MapLocations
{
    // 出生
    public static Vector3 RobberSpawn(int peerId)
    {
        int slot = System.Math.Abs(peerId) % 6;
        return new Vector3(-7.5f + slot * 3.0f, 0.1f, 15.5f);
    }

    public static Vector3 PoliceSpawn(int peerId)
    {
        int slot = System.Math.Abs(peerId) % 4;
        return new Vector3(-4.5f + slot * 3.0f, 0.1f, 24.5f);
    }

    // 银行门内外（传送点）
    public static readonly Vector3 LobbyInside = new(0, 0.1f, 1.5f);
    public static readonly Vector3 FrontDoorInside = new(0, 0.1f, 3.2f);
    public static readonly Vector3 FrontDoorOutside = new(0, 0.1f, 9.5f);
    public static readonly Vector3 SideDoorInside = new(15.5f, 0.1f, -1.0f);
    public static readonly Vector3 SideDoorOutside = new(20.5f, 0.1f, -1.0f);
    public static readonly Vector3 BackDoorInside = new(-6.0f, 0.1f, -18.0f);
    public static readonly Vector3 BackDoorOutside = new(-6.0f, 0.1f, -22.5f);

    // 警察堵点
    public static readonly Vector3 BlockSidePos = new(20.5f, 0.1f, 2.0f);
    public static readonly Vector3 BlockSewerPos = new(-11.0f, 0.1f, -22.5f);

    // 撤离终点
    public static readonly Vector3 SideEscapeExit = new(22.5f, 0.1f, 18.0f);
    public static readonly Vector3 SewerExit = new(-8.0f, 0.1f, -24.5f);
}
