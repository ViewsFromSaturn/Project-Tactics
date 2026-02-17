using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Character Sheet — portrait + identity, bio, 2-column stat grid,
/// collapsible derived stats. Auto-refreshes when PlayerData changes.
/// Hotkey: C
/// </summary>
public partial class CharacterSheetPanel : WindowPanel
{
	private VBoxContainer _statsContent;
	private bool _refreshPending = false;

	public CharacterSheetPanel()
	{
		WindowTitle = "Character Sheet";
		DefaultSize = new Vector2(420, 580);
		DefaultPosition = new Vector2(40, 60);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_statsContent = content;
		content.AddChild(PlaceholderText("Loading character..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		RebuildStats();
	}

	/// <summary>Throttle: only rebuild once per frame even if multiple stats change.</summary>
	protected override void OnDataChanged()
	{
		if (_refreshPending) return;
		_refreshPending = true;
		CallDeferred(nameof(DeferredRebuild));
	}

	private void DeferredRebuild()
	{
		_refreshPending = false;
		RebuildStats();
	}

	private void RebuildStats()
	{
		if (_statsContent == null) return;

		foreach (var child in _statsContent.GetChildren())
			child.QueueFree();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null)
		{
			_statsContent.AddChild(PlaceholderText("No character loaded."));
			return;
		}

		// ═══ HEADER: Portrait + Identity side by side ═══
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		_statsContent.AddChild(header);

		var portraitFrame = new PanelContainer();
		portraitFrame.CustomMinimumSize = new Vector2(100, 120);
		var portraitStyle = new StyleBoxFlat();
		portraitStyle.BgColor = UITheme.CardBg;
		portraitStyle.SetCornerRadiusAll(6);
		portraitStyle.SetBorderWidthAll(1);
		portraitStyle.BorderColor = UITheme.BorderSubtle;
		portraitFrame.AddThemeStyleboxOverride("panel", portraitStyle);
		header.AddChild(portraitFrame);

		var portraitIcon = new Label();
		portraitIcon.Text = "⚔";
		portraitIcon.AddThemeFontSizeOverride("font_size", 32);
		portraitIcon.AddThemeColorOverride("font_color", UITheme.TextDim);
		portraitIcon.HorizontalAlignment = HorizontalAlignment.Center;
		portraitIcon.VerticalAlignment = VerticalAlignment.Center;
		portraitFrame.AddChild(portraitIcon);

		var identity = new VBoxContainer();
		identity.AddThemeConstantOverride("separation", 2);
		identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(identity);

		var nameLabel = new Label();
		nameLabel.Text = p.CharacterName;
		nameLabel.AddThemeFontSizeOverride("font_size", 22);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		identity.AddChild(nameLabel);

		identity.AddChild(Spacer(2));
		identity.AddChild(IdentityField("Race:", p.RaceName ?? "Unknown"));
		identity.AddChild(IdentityField("City:", p.City ?? "Unknown"));
		identity.AddChild(IdentityField("Rank:", p.RpRank ?? "Aspirant"));
		identity.AddChild(IdentityField("Level:", p.CharacterLevel.ToString()));

		// ═══ BIOGRAPHY ═══
		_statsContent.AddChild(Spacer(6));

		if (!string.IsNullOrEmpty(p.Bio))
		{
			var bioHeader = new Label();
			bioHeader.Text = "Biography";
			bioHeader.AddThemeFontSizeOverride("font_size", 15);
			bioHeader.AddThemeColorOverride("font_color", UITheme.TextBright);
			if (UITheme.FontTitleMedium != null) bioHeader.AddThemeFontOverride("font", UITheme.FontTitleMedium);
			_statsContent.AddChild(bioHeader);

			var bio = UITheme.CreateBody(p.Bio, 12, UITheme.TextSecondary);
			bio.AutowrapMode = TextServer.AutowrapMode.Word;
			_statsContent.AddChild(bio);
		}

		// ═══ IC PROFILE BUTTON ═══
		_statsContent.AddChild(Spacer(8));
		var profileBtnRow = new HBoxContainer();
		profileBtnRow.Alignment = BoxContainer.AlignmentMode.Center;
		_statsContent.AddChild(profileBtnRow);

		var editProfileBtn = UITheme.CreateSecondaryButton("✎  Edit IC Profile", 12);
		editProfileBtn.CustomMinimumSize = new Vector2(180, 32);
		editProfileBtn.Pressed += () =>
		{
			OverworldHUD.Instance?.OpenPanel("icprofile");
		};
		profileBtnRow.AddChild(editProfileBtn);

		var viewProfileBtn = UITheme.CreateGhostButton("View as others see it →", 11, UITheme.Accent);
		viewProfileBtn.Pressed += () =>
		{
			OverworldHUD.Instance?.OpenPanel("icprofile_view");
		};
		profileBtnRow.AddChild(viewProfileBtn);

		// ═══ TRAINING STATS — 2 column grid ═══
		_statsContent.AddChild(Spacer(6));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(Spacer(4));

		var statsHeader = new Label();
		statsHeader.Text = "Training Stats";
		statsHeader.AddThemeFontSizeOverride("font_size", 15);
		statsHeader.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) statsHeader.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		_statsContent.AddChild(statsHeader);

		_statsContent.AddChild(Spacer(4));

		_statsContent.AddChild(StatGridRow("Strength", p.Strength, "Speed", p.Speed));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(StatGridRow("Agility", p.Agility, "Endurance", p.Endurance));
		_statsContent.AddChild(ThinSeparator());
		_statsContent.AddChild(StatGridRow("Stamina", p.Stamina, "Ether", p.EtherControl));

		// ═══ DERIVED STATS — collapsible ═══
		_statsContent.AddChild(Spacer(6));
		BuildDerivedStatsToggle(p);
	}

	private static HBoxContainer IdentityField(string label, string value)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);

		var lbl = UITheme.CreateBody(label, 12, UITheme.TextSecondary);
		lbl.CustomMinimumSize = new Vector2(44, 0);
		row.AddChild(lbl);

		row.AddChild(UITheme.CreateBody(value, 12, UITheme.TextDim));

		return row;
	}

	private static HBoxContainer StatGridRow(string name1, int val1, string name2, int val2)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(StatCell(name1, val1));
		row.AddChild(StatCell(name2, val2));
		return row;
	}

	private static HBoxContainer StatCell(string name, int value)
	{
		var cell = new HBoxContainer();
		cell.AddThemeConstantOverride("separation", 8);
		cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		cell.CustomMinimumSize = new Vector2(0, 32);

		var nameLabel = UITheme.CreateBody(name, 13, UITheme.Text);
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		cell.AddChild(nameLabel);

		var valLabel = new Label();
		valLabel.Text = value.ToString();
		valLabel.AddThemeFontSizeOverride("font_size", 18);
		valLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontNumbersMedium != null) valLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		valLabel.HorizontalAlignment = HorizontalAlignment.Right;
		cell.AddChild(valLabel);

		return cell;
	}

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
		_derivedToggleLabel.AddThemeFontSizeOverride("font_size", 14);
		_derivedToggleLabel.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		if (UITheme.FontBodyMedium != null)
			_derivedToggleLabel.AddThemeFontOverride("font", UITheme.FontBodyMedium);

		var btnNormal = new StyleBoxFlat();
		btnNormal.BgColor = Colors.Transparent;
		btnNormal.SetCornerRadiusAll(6);
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
		};
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
