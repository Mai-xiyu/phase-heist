using Godot;
using System;

namespace PhaseHeist.UI;

public static class Aui
{
    public static Control Root(string name = "AuiRoot")
    {
        return new Control
        {
            Name = name,
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    public static VBoxContainer VStack(int separation = 6, string name = "VStack")
    {
        var stack = new VBoxContainer
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", separation);
        return stack;
    }

    public static HBoxContainer HStack(int separation = 6, string name = "HStack")
    {
        var stack = new HBoxContainer
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", separation);
        return stack;
    }

    public static PanelContainer Panel(AuiTone tone = AuiTone.Neutral, bool strong = false, string name = "Panel")
    {
        var panel = new PanelContainer
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", AuiTheme.PanelBox(tone, strong));
        return panel;
    }

    public static PanelContainer Card(string title, AuiTone tone, out VBoxContainer body, bool strong = false)
    {
        var panel = Panel(tone, strong, $"{title}Card");
        body = VStack(7, $"{title}Body");
        body.AddChild(Text(title, 15, AuiTheme.Accent(tone), true));
        body.AddChild(Divider(tone));
        panel.AddChild(body);
        return panel;
    }

    public static Label Text(string text, int size = 14, Color? color = null, bool strong = false, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            // 默认不换行：容器压缩时中文会逐字竖排，长文本处显式传 wrap:true
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? (strong ? AuiTheme.Ink : AuiTheme.MutedInk));
        return label;
    }

    public static Label Stat(string text, AuiTone tone)
    {
        var label = Text(text, 13, AuiTheme.Accent(tone), true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(72, 22);
        return label;
    }

    public static PanelContainer Pill(string text, AuiTone tone)
    {
        var pill = Panel(tone, false, "Pill");
        var label = Text(text, 12, AuiTheme.Ink, true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        pill.AddChild(label);
        pill.CustomMinimumSize = new Vector2(76, 26);
        return pill;
    }

    public static PanelContainer Field(string caption, string value, AuiTone tone)
    {
        var field = new PanelContainer
        {
            Name = $"{caption}Field",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(104, 48),
        };
        field.AddThemeStyleboxOverride("panel", AuiTheme.FieldBox(tone));

        var stack = VStack(2, "FieldStack");
        stack.AddChild(Text(caption, 10, AuiTheme.MutedInk));
        Label valueLabel = Text(value, 15, AuiTheme.Accent(tone), true);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(valueLabel);
        field.AddChild(stack);
        return field;
    }

    public static Label KeyCap(string text)
    {
        var label = Text(text, 12, AuiTheme.Ink, true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(42, 22);
        label.AddThemeStyleboxOverride("normal", AuiTheme.FieldBox(AuiTone.Neutral));
        return label;
    }

    public static Button Button(string text, AuiTone tone, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(96, 30),
        };
        StyleBoxFlat normal = AuiTheme.PanelBox(tone, true);
        normal.BgColor = AuiTheme.Accent(tone).Darkened(0.45f);
        StyleBoxFlat hover = AuiTheme.PanelBox(tone, true);
        hover.BgColor = AuiTheme.Accent(tone).Darkened(0.30f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeColorOverride("font_color", AuiTheme.Ink);
        button.Pressed += pressed;
        return button;
    }

    public static Control Divider(AuiTone tone = AuiTone.Neutral)
    {
        return new ColorRect
        {
            Color = AuiTheme.Accent(tone).Darkened(0.20f),
            CustomMinimumSize = new Vector2(1, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    public static ColorRect Overlay(string name = "Overlay")
    {
        return new ColorRect
        {
            Name = name,
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = AuiTheme.Overlay,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
    }

    public static VBoxContainer BarRow(string title, AuiTone tone, out ProgressBar bar)
    {
        var box = VStack(3, $"{title}BarRow");
        box.AddChild(Text(title, 12, AuiTheme.MutedInk));

        bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(300, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("fill", AuiTheme.BarFill(tone));
        bar.AddThemeStyleboxOverride("background", AuiTheme.BarBack());
        box.AddChild(bar);
        return box;
    }

    public static void Dock(Control control, float left, float top, float right, float bottom)
    {
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }

    public static void BuildCrosshair(Control root)
    {
        var crosshair = new Control
        {
            Name = "AuiCrosshair",
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -16,
            OffsetTop = -16,
            OffsetRight = 16,
            OffsetBottom = 16,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(crosshair);

        // 细线 + 中心留空 + 暗色描边，弱化遮挡
        Color shadow = new(0.0f, 0.0f, 0.0f, 0.55f);
        Color line = new(0.92f, 0.97f, 0.98f, 0.92f);

        // 描边层（粗 1px 外扩）
        AddCrosshairRect(crosshair, new Rect2(14.5f, 3, 3, 8), shadow);
        AddCrosshairRect(crosshair, new Rect2(14.5f, 21, 3, 8), shadow);
        AddCrosshairRect(crosshair, new Rect2(3, 14.5f, 8, 3), shadow);
        AddCrosshairRect(crosshair, new Rect2(21, 14.5f, 8, 3), shadow);

        // 主线
        AddCrosshairRect(crosshair, new Rect2(15.25f, 4, 1.5f, 6.5f), line);
        AddCrosshairRect(crosshair, new Rect2(15.25f, 21.5f, 1.5f, 6.5f), line);
        AddCrosshairRect(crosshair, new Rect2(4, 15.25f, 6.5f, 1.5f), line);
        AddCrosshairRect(crosshair, new Rect2(21.5f, 15.25f, 6.5f, 1.5f), line);

        // 中心点
        AddCrosshairRect(crosshair, new Rect2(15, 15, 2, 2), AuiTheme.Robber);
    }

    private static void AddCrosshairRect(Control parent, Rect2 rect, Color color)
    {
        parent.AddChild(new ColorRect
        {
            Position = rect.Position,
            Size = rect.Size,
            Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }
}
