using Godot;

namespace ProjectTactics.UI.Panels;

public partial class CharacterSheetPanel : WindowPanel
{
	private VBoxContainer _statsContent;
	private bool _refreshPending = false;

	public CharacterSheetPanel()
	{
		WindowTitle = "Character Sheet";
		DefaultSize = new Vector2(380, 520);
		DefaultPosition = new Vector2(40, 60);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_statsContent = content;
		content.AddThemeConstantOverride("separation", 0);
		content.AddChild(PlaceholderText("Loading character..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		RebuildStats();
	}

	protected override void OnDataChanged()
	{
		if (_refreshPending) return;
		_refreshPending = true;
		CallDeferred(nameof(DeferredRebuild));
	}

	private void DeferredRebuild() { _refreshPending = false; RebuildStats(); }

	private void RebuildStats()
	{
		if (_statsContent == null) return;
		foreach (var child in _statsContent.GetChildren()) child.QueueFree();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) { _statsContent.AddChild(PlaceholderText("No character loaded.")); return; }

		// ═══ HEADER: Portrait + Identity ═══
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 10);
		_statsContent.AddChild(header);

		var portraitFrame = new PanelContainer();
		portraitFrame.CustomMinimumSize = new Vector2(72, 88);
		var ps = new StyleBoxFlat();
		ps.BgColor = UITheme.CardBg; ps.SetCornerRadiusAll(5);
		ps.SetBorderWidthAll(1); ps.BorderColor = UITheme.BorderSubtle;
		portraitFrame.AddThemeStyleboxOverride("panel", ps);
		header.AddChild(portraitFrame);

		var portraitIcon = new Label();
		portraitIcon.Text = "⚔"; portraitIcon.AddThemeFontSizeOverride("font_size", 26);
		portraitIcon.AddThemeColorOverride("font_color", UITheme.TextDim);
		portraitIcon.HorizontalAlignment = HorizontalAlignment.Center;
		portraitIcon.VerticalAlignment = VerticalAlignment.Center;
		portraitFrame.AddChild(portraitIcon);

		var identity = new VBoxContainer();
		identity.AddThemeConstantOverride("separation", 1);
		identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(identity);

		var nameLabel = new Label();
		nameLabel.Text = p.CharacterName;
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		identity.AddChild(nameLabel);

		identity.AddChild(IdentityField("Race:", p.RaceName ?? "Unknown"));
		identity.AddChild(IdentityField("City:", p.City ?? "Unknown"));
		identity.AddChild(IdentityField("Rank:", p.RpRank ?? "Aspirant"));
		identity.AddChild(IdentityField("Level:", p.CharacterLevel.ToString()));

		// ═══ BIOGRAPHY ═══
		if (!string.IsNullOrEmpty(p.Bio))
		{
			_statsContent.AddChild(Spacer(4));
			_statsContent.AddChild(ThinSeparator());
			_statsContent.AddChild(Spacer(3));

			_statsContent.AddChild(SecHeader("Biography"));
			var bio = UITheme.CreateBody(p.Bio, 12, UITheme.TextSecondary);
			bio.AutowrapMode = TextServer.AutowrapMode.Word;
			_statsContent.AddChild(bio);
		}

		// ═══ IC PROFILE BUTTON ═══
		_statsContent.AddChild(Spacer(5));
		var profileBtnRow = new HBoxContainer();
		profileBtnRow.AddThemeConstantOverride("separation", 6);
		_statsContent.AddChild(profileBtnRow);

		var editProfileBtn = UITheme.CreateSecondaryButton("✎  Edit IC Profile", 11);
		editProfileBtn.CustomMinimumSize = new Vector2(150, 26);
		editProfileBtn.Pressed += () => OverworldHUD.Instance?.OpenPanel("icprofile");
		profileBtnRow.AddChild(editProfileBtn);

		var viewProfileBtn = UITheme.CreateGhostButton("View as others see it →", 10, UITheme.Accent);
		viewProfileBtn.Pressed += () => OverworldHUD.Instance?.OpenPanel("icprofile_view");
		profileBtnRow.AddChild(viewProfileBtn);

		// ═══ TRAINING STATS ═══
		_statsContent.AddChild(Spacer(4));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(Spacer(3));
		_statsContent.AddChild(SecHeader("Training Stats"));
		_statsContent.AddChild(Spacer(2));

		_statsContent.AddChild(StatGridRow("Strength", p.Strength, "Vitality", p.Vitality));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(StatGridRow("Agility", p.Agility, "Dexterity", p.Dexterity));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(StatGridRow("Mind", p.Mind, "Aether", p.EtherControl));

		// ═══ DERIVED STATS ═══
		_statsContent.AddChild(Spacer(3));
		BuildDerivedStatsToggle(p);
	}

	private static Label SecHeader(string text)
	{
		var l = new Label(); l.Text = text;
		l.AddThemeFontSizeOverride("font_size", 13);
		l.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontBodyMedium != null) l.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		return l;
	}

	private static HBoxContainer IdentityField(string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		var lbl = UITheme.CreateBody(label, 11, UITheme.TextSecondary);
		lbl.CustomMinimumSize = new Vector2(38, 0);
		row.AddChild(lbl);
		row.AddChild(UITheme.CreateBody(value, 11, UITheme.TextDim));
		return row;
	}

	private static HBoxContainer StatGridRow(string n1, int v1, string n2, int v2)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(StatCell(n1, v1));
		row.AddChild(StatCell(n2, v2));
		return row;
	}

	private static HBoxContainer StatCell(string name, int value)
	{
		var cell = new HBoxContainer();
		cell.AddThemeConstantOverride("separation", 4);
		cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		cell.CustomMinimumSize = new Vector2(0, 24);

		var n = UITheme.CreateBody(name, 12, UITheme.Text);
		n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		cell.AddChild(n);

		var v = new Label(); v.Text = value.ToString();
		v.AddThemeFontSizeOverride("font_size", 15);
		v.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontNumbersMedium != null) v.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		v.HorizontalAlignment = HorizontalAlignment.Right;
		cell.AddChild(v);
		return cell;
	}

	private VBoxContainer _derivedContainer;
	private Label _derivedToggleLabel;
	private bool _derivedExpanded = false;

	private void BuildDerivedStatsToggle(Core.PlayerData p)
	{
		var toggleBtn = new Button();
		toggleBtn.Alignment = HorizontalAlignment.Left;
		toggleBtn.CustomMinimumSize = new Vector2(0, 22);

		_derivedExpanded = false;
		_derivedToggleLabel = new Label();
		_derivedToggleLabel.Text = "▸ Derived Stats";
		_derivedToggleLabel.AddThemeFontSizeOverride("font_size", 12);
		_derivedToggleLabel.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		if (UITheme.FontBodyMedium != null)
			_derivedToggleLabel.AddThemeFontOverride("font", UITheme.FontBodyMedium);

		var btnNormal = new StyleBoxFlat();
		btnNormal.BgColor = Colors.Transparent; btnNormal.SetCornerRadiusAll(4);
		toggleBtn.AddThemeStyleboxOverride("normal", btnNormal);
		var btnHover = (StyleBoxFlat)btnNormal.Duplicate();
		btnHover.BgColor = UITheme.CardHoverBg;
		toggleBtn.AddThemeStyleboxOverride("hover", btnHover);

		toggleBtn.AddChild(_derivedToggleLabel);
		_statsContent.AddChild(toggleBtn);

		_derivedContainer = new VBoxContainer();
		_derivedContainer.AddThemeConstantOverride("separation", 0);
		_derivedContainer.Visible = false;
		_statsContent.AddChild(_derivedContainer);

		_derivedContainer.AddChild(DerivedRow("HP", $"{p.CurrentHp} / {p.MaxHp}"));
		_derivedContainer.AddChild(DerivedRow("Aether", $"{p.CurrentAether} / {p.MaxAether}"));
		_derivedContainer.AddChild(DerivedRow("Aether Regen", $"{p.AetherRegen}/turn"));
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
		};
	}

	private static HBoxContainer DerivedRow(string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.CustomMinimumSize = new Vector2(0, 18);

		var n = UITheme.CreateDim(label, 11);
		n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(n);

		var v = UITheme.CreateNumbers(value, 11, UITheme.Text);
		v.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(v);
		return row;
	}
}
