using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Map — zone overview, points of interest.
/// Floating window. Hotkey: M
/// </summary>
public partial class MapPanel : WindowPanel
{
	public MapPanel()
	{
		WindowTitle = "World Map";
		DefaultSize = new Vector2(400, 500);
		DefaultPosition = new Vector2(350, 80);
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
		mapRect.Color = UITheme.CardBg;
		content.AddChild(mapRect);

		var mapLabel = UITheme.CreateDim("Map display will render here.", 12);
		mapLabel.HorizontalAlignment = HorizontalAlignment.Center;
		content.AddChild(mapLabel);

		content.AddChild(UITheme.CreateSpacer(8));
		content.AddChild(ThinSeparator());

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
