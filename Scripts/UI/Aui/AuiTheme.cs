using Godot;

namespace PhaseHeist.UI;

public enum AuiTone
{
    Neutral,
    Police,
    Robber,
    Warning,
    Danger,
    Success,
}

public static class AuiTheme
{
    public static readonly Color Ink = new(0.90f, 0.95f, 0.96f);
    public static readonly Color MutedInk = new(0.62f, 0.70f, 0.72f);
    public static readonly Color Panel = new(0.035f, 0.045f, 0.050f, 0.88f);
    public static readonly Color PanelStrong = new(0.060f, 0.075f, 0.080f, 0.96f);
    public static readonly Color Overlay = new(0.010f, 0.012f, 0.014f, 0.72f);
    public static readonly Color Stroke = new(0.18f, 0.23f, 0.24f, 0.92f);
    public static readonly Color Police = new(0.12f, 0.34f, 0.95f);
    public static readonly Color Robber = new(0.95f, 0.62f, 0.16f);
    public static readonly Color Warning = new(0.95f, 0.82f, 0.24f);
    public static readonly Color Danger = new(0.92f, 0.18f, 0.14f);
    public static readonly Color Success = new(0.18f, 0.78f, 0.36f);

    public const int Radius = 6;
    public const int Padding = 10;

    public static Color Accent(AuiTone tone)
    {
        return tone switch
        {
            AuiTone.Police => Police,
            AuiTone.Robber => Robber,
            AuiTone.Warning => Warning,
            AuiTone.Danger => Danger,
            AuiTone.Success => Success,
            _ => new Color(0.34f, 0.48f, 0.50f),
        };
    }

    public static StyleBoxFlat PanelBox(AuiTone tone = AuiTone.Neutral, bool strong = false)
    {
        Color accent = Accent(tone);
        var box = new StyleBoxFlat
        {
            BgColor = strong ? PanelStrong : Panel,
            BorderColor = accent.Darkened(0.18f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = Radius,
            CornerRadiusTopRight = Radius,
            CornerRadiusBottomLeft = Radius,
            CornerRadiusBottomRight = Radius,
            ShadowColor = new Color(0, 0, 0, 0.38f),
            ShadowSize = 8,
            ShadowOffset = new Vector2(0, 3),
        };
        box.SetContentMarginAll(Padding);
        return box;
    }

    public static StyleBoxFlat FieldBox(AuiTone tone = AuiTone.Neutral)
    {
        Color accent = Accent(tone);
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.105f, 0.92f),
            BorderColor = accent.Darkened(0.28f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        box.SetContentMarginAll(7);
        return box;
    }

    public static StyleBoxFlat BarFill(AuiTone tone)
    {
        Color accent = Accent(tone);
        return new StyleBoxFlat
        {
            BgColor = accent,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    public static StyleBoxFlat BarBack()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.14f, 0.15f, 0.96f),
            BorderColor = Stroke,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }
}
