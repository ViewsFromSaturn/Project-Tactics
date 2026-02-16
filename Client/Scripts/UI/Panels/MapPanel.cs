using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Map — zone overview, points of interest.
/// Hotkey: M | Slides: Left
/// </summary>
public partial class MapPanel : SlidePanel
{
    public MapPanel()
    {
        PanelTitle = "Map";
        Direction = SlideDirection.Left;
        PanelWidth = 400;
    }

    protected override void BuildContent(VBoxContainer content)
    {
        content.AddChild(SectionHeader("Current Zone"));

        var p = Core.GameManager.Instance?.ActiveCharacter;
        string city = p?.City ?? "Unknown";
        content.AddChild(UITheme.CreateTitle(city, 18));
        content.AddChild(UITheme.CreateDim("Overworld — Open Area", 12));

        content.AddChild(UITheme.CreateSpacer(12));

        // Map placeholder
        var mapRect = new ColorRect();
        mapRect.CustomMinimumSize = new Vector2(0, 200);
        mapRect.Color = new Color(0.05f, 0.05f, 0.08f, 0.8f);
        content.AddChild(mapRect);

        var mapLabel = UITheme.CreateDim("Map display will render here.", 12);
        mapLabel.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(mapLabel);

        content.AddChild(UITheme.CreateSpacer(8));
        content.AddChild(ThinSeparator());

        // Points of interest
        content.AddChild(SectionHeader("Points of Interest"));
        content.AddChild(PoiRow("Training Grounds", "Practice combat techniques"));
        content.AddChild(PoiRow("City Gate", "Entrance to the overworld"));
        content.AddChild(PoiRow("Market District", "Shops and traders"));
        content.AddChild(PoiRow("Barracks", "Sword faction headquarters"));
    }

    private static HBoxContainer PoiRow(string name, string desc)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var dot = new ColorRect();
        dot.CustomMinimumSize = new Vector2(4, 4);
        dot.Color = UITheme.Accent;
        dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(dot);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 1);
        row.AddChild(vbox);

        vbox.AddChild(UITheme.CreateBody(name, 13, UITheme.Text));
        vbox.AddChild(UITheme.CreateDim(desc, 10));

        return row;
    }
}
