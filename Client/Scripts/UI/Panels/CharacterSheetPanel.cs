using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Character Sheet — stats, bio, derived stats, race info.
/// Floating window. Hotkey: C
/// </summary>
public partial class CharacterSheetPanel : WindowPanel
{
    private VBoxContainer _statsContent;

    public CharacterSheetPanel()
    {
        WindowTitle = "Character Sheet";
        DefaultSize = new Vector2(360, 520);
        DefaultPosition = new Vector2(40, 80);
    }

    protected override void BuildContent(VBoxContainer content)
    {
        _statsContent = content;
        content.AddChild(PlaceholderText("Loading character..."));
    }

    protected override void OnOpen()
    {
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

        // Derived Stats — collapsible toggle
        BuildDerivedStatsToggle(p);
    }

    // ─── Collapsible derived stats ───
    private VBoxContainer _derivedContainer;
    private Label _derivedToggleLabel;
    private bool _derivedExpanded = false;

    private void BuildDerivedStatsToggle(Core.PlayerData p)
    {
        var toggleBtn = new Button();
        toggleBtn.Alignment = HorizontalAlignment.Left;
        toggleBtn.CustomMinimumSize = new Vector2(0, 30);

        _derivedExpanded = false;
        _derivedToggleLabel = new Label();
        _derivedToggleLabel.Text = "▸ Derived Stats";
        _derivedToggleLabel.AddThemeFontSizeOverride("font_size", 13);
        _derivedToggleLabel.AddThemeColorOverride("font_color", UITheme.TextSecondary);
        if (UITheme.FontBodyMedium != null)
            _derivedToggleLabel.AddThemeFontOverride("font", UITheme.FontBodyMedium);

        var btnNormal = new StyleBoxFlat();
        btnNormal.BgColor = Colors.Transparent;
        btnNormal.SetCornerRadiusAll(6);
        btnNormal.ContentMarginLeft = 0;
        btnNormal.ContentMarginRight = 8;
        btnNormal.ContentMarginTop = 2;
        btnNormal.ContentMarginBottom = 2;
        toggleBtn.AddThemeStyleboxOverride("normal", btnNormal);

        var btnHover = (StyleBoxFlat)btnNormal.Duplicate();
        btnHover.BgColor = UITheme.CardHoverBg;
        toggleBtn.AddThemeStyleboxOverride("hover", btnHover);

        toggleBtn.AddChild(_derivedToggleLabel);
        _statsContent.AddChild(toggleBtn);

        _derivedContainer = new VBoxContainer();
        _derivedContainer.AddThemeConstantOverride("separation", 2);
        _derivedContainer.Visible = false;
        _statsContent.AddChild(_derivedContainer);

        // Summary row (visible when collapsed)
        var summaryRow = new HBoxContainer();
        summaryRow.AddThemeConstantOverride("separation", 12);
        summaryRow.Name = "DerivedSummary";

        summaryRow.AddChild(MiniStatChip("HP", $"{p.CurrentHp}/{p.MaxHp}", UITheme.AccentRed));
        summaryRow.AddChild(MiniStatChip("Ether", $"{p.CurrentEther}/{p.MaxEther}", UITheme.AccentBlue));
        summaryRow.AddChild(MiniStatChip("ATK", $"{p.Atk}", UITheme.TextSecondary));

        _statsContent.AddChild(summaryRow);

        // Full derived stats
        _derivedContainer.AddChild(DerivedRow("HP", $"{p.CurrentHp} / {p.MaxHp}"));
        _derivedContainer.AddChild(DerivedRow("Ether", $"{p.CurrentEther} / {p.MaxEther}"));
        _derivedContainer.AddChild(DerivedRow("Ether Regen", $"{p.EtherRegen}/turn"));
        _derivedContainer.AddChild(ThinSeparator());
        _derivedContainer.AddChild(DerivedRow("ATK", $"{p.Atk}"));
        _derivedContainer.AddChild(DerivedRow("DEF", $"{p.Def}"));
        _derivedContainer.AddChild(DerivedRow("EATK", $"{p.Eatk}"));
        _derivedContainer.AddChild(DerivedRow("EDEF", $"{p.Edef}"));
        _derivedContainer.AddChild(ThinSeparator());
        _derivedContainer.AddChild(DerivedRow("AVD", $"{p.Avd}"));
        _derivedContainer.AddChild(DerivedRow("ACC", $"{p.Acc}"));
        _derivedContainer.AddChild(DerivedRow("CRIT", $"{p.CritPercent}%"));
        _derivedContainer.AddChild(ThinSeparator());
        _derivedContainer.AddChild(DerivedRow("MOVE", $"{p.Move}"));
        _derivedContainer.AddChild(DerivedRow("JUMP", $"{p.Jump}"));
        _derivedContainer.AddChild(DerivedRow("RT", $"{p.BaseRt}"));

        toggleBtn.Pressed += () =>
        {
            _derivedExpanded = !_derivedExpanded;
            _derivedContainer.Visible = _derivedExpanded;
            _derivedToggleLabel.Text = _derivedExpanded ? "▾ Derived Stats" : "▸ Derived Stats";
            summaryRow.Visible = !_derivedExpanded;
        };
    }

    private static HBoxContainer MiniStatChip(string label, string value, Color color)
    {
        var chip = new HBoxContainer();
        chip.AddThemeConstantOverride("separation", 4);
        chip.AddChild(UITheme.CreateDim(label, 11));
        var val = UITheme.CreateNumbers(value, 12, color);
        chip.AddChild(val);
        return chip;
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
