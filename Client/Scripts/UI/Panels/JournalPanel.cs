using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Journal — private IC notes/log.
/// Hotkey: J | Slides: Right
/// </summary>
public partial class JournalPanel : SlidePanel
{
    public JournalPanel()
    {
        PanelTitle = "Journal";
        Direction = SlideDirection.Right;
        PanelWidth = 380;
    }

    protected override void BuildContent(VBoxContainer content)
    {
        content.AddChild(SectionHeader("Personal Log"));
        content.AddChild(PlaceholderText(
            "Your private journal entries will appear here. " +
            "Record thoughts, plans, and story notes that only you can see."));

        content.AddChild(UITheme.CreateSpacer(12));

        // New entry area (stub)
        content.AddChild(SectionHeader("New Entry"));
        var input = UITheme.CreateTextArea("Write a journal entry...");
        input.CustomMinimumSize = new Vector2(0, 100);
        content.AddChild(input);

        var addBtn = UITheme.CreatePrimaryButton("ADD ENTRY", 12);
        addBtn.Pressed += () => GD.Print("[Journal] Add entry — not implemented yet");
        content.AddChild(addBtn);

        content.AddChild(UITheme.CreateSpacer(8));
        content.AddChild(ThinSeparator());

        // Empty state
        content.AddChild(SectionHeader("Past Entries"));
        content.AddChild(PlaceholderText("No entries yet."));
    }
}
