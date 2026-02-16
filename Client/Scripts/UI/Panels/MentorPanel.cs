using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Mentor / Teaching — active teaching arcs, student/mentor status.
/// Floating window. Hotkey: N
/// </summary>
public partial class MentorPanel : WindowPanel
{
    public MentorPanel()
    {
        WindowTitle = "Mentorship";
        DefaultSize = new Vector2(380, 460);
        DefaultPosition = new Vector2(300, 100);
    }

    protected override void BuildContent(VBoxContainer content)
    {
        var p = Core.GameManager.Instance?.ActiveCharacter;
        string rank = p?.RpRank ?? "Aspirant";

        content.AddChild(SectionHeader("Your Status"));
        content.AddChild(UITheme.CreateBody($"Rank: {rank}", 13, UITheme.Text));

        bool canMentor = rank is "Genin" or "Chunin" or "Warden" or "Jonin" or "Justicar" or "Banneret";
        bool canLearn = rank is not "Jonin" and not "Justicar";

        var mentorStatus = UITheme.CreateBody(
            canMentor ? "You can mentor lower-ranked students." : "Reach a higher rank to become a mentor.",
            12, canMentor ? UITheme.Accent : UITheme.TextDim);
        content.AddChild(mentorStatus);

        content.AddChild(UITheme.CreateSpacer(8));
        content.AddChild(ThinSeparator());

        content.AddChild(SectionHeader("Active Teaching Arcs"));
        content.AddChild(PlaceholderText("No active teaching arcs."));

        content.AddChild(UITheme.CreateSpacer(4));
        content.AddChild(ThinSeparator());

        content.AddChild(SectionHeader("Currently Learning"));
        if (canLearn)
            content.AddChild(PlaceholderText("You are not learning any technique. Find a mentor to begin."));
        else
            content.AddChild(PlaceholderText(
                "At your rank, you learn through solo training (5 days), " +
                "technique scrolls, or staff-run events."));

        content.AddChild(UITheme.CreateSpacer(4));
        content.AddChild(ThinSeparator());

        content.AddChild(SectionHeader("Teaching Chain"));
        content.AddChild(ChainRow("Sworn / Levy", "→ teaches →", "Aspirants"));
        content.AddChild(ChainRow("Warden", "→ teaches →", "Sworn"));
        content.AddChild(ChainRow("Banneret / Justicar", "→ teaches →", "Wardens"));
    }

    private static HBoxContainer ChainRow(string mentor, string arrow, string student)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(UITheme.CreateBody(mentor, 11, UITheme.TextBright));
        row.AddChild(UITheme.CreateDim(arrow, 10));
        row.AddChild(UITheme.CreateBody(student, 11, UITheme.Text));
        return row;
    }
}
