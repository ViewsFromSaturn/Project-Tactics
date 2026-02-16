using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Inventory / Equipment — future system, stubbed now.
/// Floating window. Hotkey: I
/// </summary>
public partial class InventoryPanel : WindowPanel
{
    public InventoryPanel()
    {
        WindowTitle = "Inventory";
        DefaultSize = new Vector2(360, 480);
        DefaultPosition = new Vector2(200, 100);
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

        var notice = UITheme.CreateBody("This system is planned for a future update.", 12, UITheme.TextDim);
        notice.HorizontalAlignment = HorizontalAlignment.Center;
        notice.AutowrapMode = TextServer.AutowrapMode.Word;
        content.AddChild(notice);
    }
}
