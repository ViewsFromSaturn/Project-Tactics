using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Inventory / Equipment — future system, stubbed now.
/// Hotkey: I | Slides: Right
/// </summary>
public partial class InventoryPanel : SlidePanel
{
    public InventoryPanel()
    {
        PanelTitle = "Inventory";
        Direction = SlideDirection.Right;
        PanelWidth = 340;
    }

    protected override void BuildContent(VBoxContainer content)
    {
        content.AddChild(SectionHeader("Equipment"));
        content.AddChild(PlaceholderText(
            "Equipment slots will appear here when the inventory system is implemented."));

        content.AddChild(UITheme.CreateSpacer(12));
        content.AddChild(ThinSeparator());

        content.AddChild(SectionHeader("Items"));
        content.AddChild(PlaceholderText("No items in inventory."));

        content.AddChild(UITheme.CreateSpacer(20));

        // Coming soon notice
        var notice = UITheme.CreateBody("This system is planned for a future update.", 12, UITheme.TextDim);
        notice.HorizontalAlignment = HorizontalAlignment.Center;
        notice.AutowrapMode = TextServer.AutowrapMode.Word;
        content.AddChild(notice);
    }
}
