using Godot;

namespace PhaseHeist;

public partial class BankDoor : StaticBody3D
{
    [Export] public bool StartsClosed { get; set; }
    [Export] public float MoveSpeed { get; set; } = 7.5f;

    private MeshInstance3D? _mesh;
    private CollisionShape3D? _collision;
    private Label3D? _label;
    private bool _closed;
    private float _closedY;
    private float _openY;
    private float _targetY;

    public bool IsClosed => _closed;

    public override void _Ready()
    {
        AddToGroup("bank_main_doors");
        _mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
        _collision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _label = GetNodeOrNull<Label3D>("Label");
        CalculateTravel();
        SetClosed(StartsClosed, immediate: true);
        Loc.LanguageChanged += RefreshLabel;
    }

    public override void _ExitTree()
    {
        Loc.LanguageChanged -= RefreshLabel;
    }

    public override void _Process(double delta)
    {
        if (_mesh == null)
        {
            return;
        }

        Vector3 pos = _mesh.Position;
        pos.Y = (float)Mathf.MoveToward(pos.Y, _targetY, MoveSpeed * (float)delta);
        _mesh.Position = pos;
    }

    public void SetClosed(bool closed, bool immediate = false)
    {
        _closed = closed;
        CollisionLayer = closed ? GameLayers.World : 0;
        CollisionMask = 0;
        _collision?.SetDeferred(CollisionShape3D.PropertyName.Disabled, !closed);
        _targetY = closed ? _closedY : _openY;

        if (_mesh != null)
        {
            if (immediate)
            {
                _mesh.Position = new Vector3(_mesh.Position.X, _targetY, _mesh.Position.Z);
            }

            _mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = closed ? new Color(0.50f, 0.10f, 0.08f) : new Color(0.10f, 0.42f, 0.20f),
                Roughness = 0.55f,
                Metallic = 0.35f,
            };
        }

        RefreshLabel();
    }

    private void CalculateTravel()
    {
        float height = _mesh?.Mesh is BoxMesh box ? box.Size.Y : 4.0f;
        _closedY = height * 0.5f;
        _openY = height * 1.5f + 0.2f;
        _targetY = StartsClosed ? _closedY : _openY;
    }

    private void RefreshLabel()
    {
        if (_label != null)
        {
            _label.Text = Loc.T(_closed ? "door.closed" : "door.open");
        }
    }
}
