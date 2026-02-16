using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Character Sheet — stats, bio, derived stats, race info.
/// Hotkey: C | Slides: Right
/// </summary>
public partial class CharacterSheetPanel : SlidePanel
{
    private VBoxContainer _statsContent;

    public CharacterSheetPanel()
    {
        PanelTitle = "Character Sheet";
        Direction = SlideDirection.Right;
        PanelWidth = 360;
    }

    protected override void BuildContent(VBoxContainer content)
    {
        _statsContent = content;
        // Populated on open
        content.AddChild(PlaceholderText("Open with a character loaded to view stats."));
    }

    protected override void OnOpen()
    {
        // Clear and rebuild with live data
        foreach (var child in _statsContent.GetChildren())
            child.QueueFree();

        var p = Core.GameManager.Instance?.ActiveCharacter;
        if (p == null)
        {
            _statsContent.AddChild(PlaceholderText("No character loaded."));
            return;
        }

        // Identity
        _statsContent.AddChild(UITheme.CreateTitle(p.CharacterName, 22));
        _statsContent.AddChild(UITheme.CreateDim($"{p.RpRank}  ·  {p.RaceName}  ·  {p.City}", 12));
        _statsContent.AddChild(UITheme.CreateSpacer(4));

        // Bio
        if (!string.IsNullOrEmpty(p.Bio))
        {
            _statsContent.AddChild(SectionHeader("Bio"));
            var bio = UITheme.CreateBody(p.Bio, 12, UITheme.Text);
            bio.AutowrapMode = TextServer.AutowrapMode.Word;
            _statsContent.AddChild(bio);
        }

        _statsContent.AddChild(ThinSeparator());

        // Training Stats
        _statsContent.AddChild(SectionHeader("Training Stats"));
        _statsContent.AddChild(StatRow("Strength", "STR", p.Strength));
        _statsContent.AddChild(StatRow("Speed", "SPD", p.Speed));
        _statsContent.AddChild(StatRow("Agility", "AGI", p.Agility));
        _statsContent.AddChild(StatRow("Endurance", "END", p.Endurance));
        _statsContent.AddChild(StatRow("Stamina", "STA", p.Stamina));
        _statsContent.AddChild(StatRow("Ether Control", "ETH", p.EtherControl));
        _statsContent.AddChild(UITheme.CreateDim($"Character Level: {p.CharacterLevel}", 11));

        _statsContent.AddChild(ThinSeparator());

        // Derived Stats
        _statsContent.AddChild(SectionHeader("Derived Stats"));
        _statsContent.AddChild(DerivedRow("HP", $"{p.CurrentHp} / {p.MaxHp}"));
        _statsContent.AddChild(DerivedRow("Ether", $"{p.CurrentEther} / {p.MaxEther}"));
        _statsContent.AddChild(DerivedRow("Ether Regen", $"{p.EtherRegen}/turn"));
        _statsContent.AddChild(DerivedRow("ATK", $"{p.Atk}"));
        _statsContent.AddChild(DerivedRow("DEF", $"{p.Def}"));
        _statsContent.AddChild(DerivedRow("EATK", $"{p.Eatk}"));
        _statsContent.AddChild(DerivedRow("EDEF", $"{p.Edef}"));
        _statsContent.AddChild(DerivedRow("AVD", $"{p.Avd}"));
        _statsContent.AddChild(DerivedRow("ACC", $"{p.Acc}"));
        _statsContent.AddChild(DerivedRow("CRIT", $"{p.CritPercent}%"));
        _statsContent.AddChild(DerivedRow("MOVE", $"{p.Move}"));
        _statsContent.AddChild(DerivedRow("JUMP", $"{p.Jump}"));
        _statsContent.AddChild(DerivedRow("RT", $"{p.BaseRt}"));
    }

    private static HBoxContainer StatRow(string name, string abbr, int value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = UITheme.CreateBody(name, 13, UITheme.Text);
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(nameLabel);

        var valLabel = UITheme.CreateNumbers($"{value}", 14, UITheme.TextBright);
        valLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valLabel.CustomMinimumSize = new Vector2(40, 0);
        row.AddChild(valLabel);

        return row;
    }

    private static HBoxContainer DerivedRow(string label, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = UITheme.CreateDim(label, 12);
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(nameLabel);

        var valLabel = UITheme.CreateNumbers(value, 12, UITheme.Text);
        valLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(valLabel);

        return row;
    }
}
