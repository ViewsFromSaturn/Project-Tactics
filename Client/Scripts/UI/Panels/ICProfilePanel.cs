using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// IC Profile — the public-facing RP identity card.
/// View mode: what others see. Edit mode: opened from Character Sheet.
/// Closely matches the ic-profile-mockup.html design.
/// </summary>
public partial class ICProfilePanel : WindowPanel
{
	private readonly bool _editMode;
	private VBoxContainer _body;

	// Edit mode field refs
	private LineEdit _taglineInput;
	private OptionButton _statusDropdown;
	private TextEdit _bioInput;
	private Label _bioCharCount;
	private HFlowContainer _traitsContainer;
	private VBoxContainer _rumorsContainer;
	private LineEdit _traitAddInput;

	// Data
	private List<string> _traits = new();
	private List<string> _rumors = new();

	private static readonly string[] StatusOptions = {
		"Open to RP", "In Scene", "Training", "AFK / Busy", "Do Not Disturb"
	};

	public ICProfilePanel(bool editMode = false)
	{
		_editMode = editMode;
		WindowTitle = editMode ? "Edit IC Profile" : "IC Profile";
		DefaultSize = new Vector2(420, editMode ? 640 : 560);
		DefaultPosition = Vector2.Zero;
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_body = content;
		content.AddThemeConstantOverride("separation", 0);
		content.AddChild(PlaceholderText("Loading..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		Rebuild();
	}

	private void Rebuild()
	{
		if (_body == null) return;
		foreach (var c in _body.GetChildren()) c.QueueFree();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null)
		{
			_body.AddChild(PlaceholderText("No character loaded."));
			return;
		}

		_traits = p.GetTraitsList().ToList();
		_rumors = p.GetRumorsList().ToList();

		if (_editMode) BuildEditMode(p);
		else BuildViewMode(p);
	}

	// ═════════════════════════════════════════════════════════
	//  VIEW MODE
	// ═════════════════════════════════════════════════════════

	private void BuildViewMode(Core.PlayerData p)
	{
		// ─── Header: Portrait + Identity ───
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 16);
		_body.AddChild(header);

		header.AddChild(BuildPortrait(false));

		var identity = new VBoxContainer();
		identity.AddThemeConstantOverride("separation", 1);
		identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(identity);

		// Name — large serif (Georgia Bold = EB Garamond equivalent)
		var nameLabel = new Label();
		nameLabel.Text = p.CharacterName;
		nameLabel.AddThemeFontSizeOverride("font_size", 24);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		identity.AddChild(nameLabel);

		// Tagline — italic serif, gold
		if (!string.IsNullOrEmpty(p.Tagline))
		{
			var tagline = new Label();
			tagline.Text = $"\"{p.Tagline}\"";
			tagline.AddThemeFontSizeOverride("font_size", 13);
			tagline.AddThemeColorOverride("font_color", UITheme.AccentGold);
			tagline.AutowrapMode = TextServer.AutowrapMode.Word;
			if (UITheme.FontTitle != null) tagline.AddThemeFontOverride("font", UITheme.FontTitle);
			identity.AddChild(tagline);
		}

		// Meta rows
		identity.AddChild(VSpacer(6));
		identity.AddChild(ViewMetaRow("Race", p.RaceName ?? "Unknown", null));
		identity.AddChild(ViewMetaRow("City", p.City ?? "Unknown", UITheme.Accent));
		identity.AddChild(ViewMetaRow("Rank", p.RpRank ?? "Aspirant", UITheme.AccentGold));

		// ─── RP Status Badge ───
		_body.AddChild(VSpacer(10));
		_body.AddChild(BuildStatusBadge(p.RpStatus ?? "Open to RP"));

		// ─── Biography ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(12));
		_body.AddChild(SectionLabel("BIOGRAPHY"));
		_body.AddChild(VSpacer(8));

		if (!string.IsNullOrEmpty(p.Bio))
		{
			var bio = new Label();
			bio.Text = p.Bio;
			bio.AddThemeFontSizeOverride("font_size", 13);
			bio.AddThemeColorOverride("font_color", UITheme.Text);
			bio.AutowrapMode = TextServer.AutowrapMode.Word;
			if (UITheme.FontTitle != null) bio.AddThemeFontOverride("font", UITheme.FontTitle); // Serif for bio
			_body.AddChild(bio);
		}
		else
		{
			_body.AddChild(DimText("No biography written."));
		}

		// ─── Personality Traits ───
		var traits = p.GetTraitsList();
		if (traits.Length > 0)
		{
			_body.AddChild(VSpacer(12));
			_body.AddChild(BuildSep());
			_body.AddChild(VSpacer(12));
			_body.AddChild(SectionLabel("PERSONALITY"));
			_body.AddChild(VSpacer(8));

			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", 6);
			flow.AddThemeConstantOverride("v_separation", 6);
			_body.AddChild(flow);

			foreach (var t in traits)
				flow.AddChild(BuildTraitPill(t));
		}

		// ─── Rumors & Known For ───
		var rumors = p.GetRumorsList();
		if (rumors.Length > 0)
		{
			_body.AddChild(VSpacer(12));
			_body.AddChild(BuildSep());
			_body.AddChild(VSpacer(12));
			_body.AddChild(SectionLabel("RUMORS & KNOWN FOR"));
			_body.AddChild(VSpacer(8));

			foreach (var r in rumors)
				_body.AddChild(BuildRumorRow(r));
		}
	}

	// ═════════════════════════════════════════════════════════
	//  EDIT MODE
	// ═════════════════════════════════════════════════════════

	private void BuildEditMode(Core.PlayerData p)
	{
		// ─── Header: Portrait + Identity + Tagline ───
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 16);
		_body.AddChild(header);

		header.AddChild(BuildPortrait(true));

		var identityCol = new VBoxContainer();
		identityCol.AddThemeConstantOverride("separation", 2);
		identityCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(identityCol);

		// Name — serif bold
		var nameLabel = new Label();
		nameLabel.Text = p.CharacterName;
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		identityCol.AddChild(nameLabel);

		// Meta line
		var metaLine = new Label();
		metaLine.Text = $"{p.RaceName}  ·  {p.City}  ·  {p.RpRank}";
		metaLine.AddThemeFontSizeOverride("font_size", 11);
		metaLine.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontBody != null) metaLine.AddThemeFontOverride("font", UITheme.FontBody);
		identityCol.AddChild(metaLine);

		identityCol.AddChild(VSpacer(8));

		// Tagline label
		var tagLabel = new Label();
		tagLabel.Text = "Tagline";
		tagLabel.AddThemeFontSizeOverride("font_size", 11);
		tagLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontBody != null) tagLabel.AddThemeFontOverride("font", UITheme.FontBody);
		identityCol.AddChild(tagLabel);

		// Tagline input
		_taglineInput = BuildInput("A short IC title or quote...");
		_taglineInput.Text = p.Tagline ?? "";
		_taglineInput.MaxLength = 80;
		identityCol.AddChild(_taglineInput);

		var taglineCount = BuildCharCount($"{(p.Tagline ?? "").Length} / 80");
		identityCol.AddChild(taglineCount);
		_taglineInput.TextChanged += (text) => taglineCount.Text = $"{text.Length} / 80";

		// ─── RP Status ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(12));
		_body.AddChild(SectionLabel("RP STATUS"));
		_body.AddChild(VSpacer(8));

		_statusDropdown = new OptionButton();
		_statusDropdown.CustomMinimumSize = new Vector2(160, 32);
		StyleOptionButton(_statusDropdown);

		int selectedIdx = 0;
		for (int i = 0; i < StatusOptions.Length; i++)
		{
			_statusDropdown.AddItem(StatusOptions[i], i);
			if (StatusOptions[i] == (p.RpStatus ?? "Open to RP"))
				selectedIdx = i;
		}
		_statusDropdown.Selected = selectedIdx;
		_body.AddChild(_statusDropdown);

		// ─── Biography ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(12));
		_body.AddChild(SectionLabel("BIOGRAPHY"));
		_body.AddChild(VSpacer(8));

		_bioInput = BuildTextArea("Write your character's backstory, description, or public history...");
		_bioInput.Text = p.Bio ?? "";
		_bioInput.CustomMinimumSize = new Vector2(0, 100);
		_body.AddChild(_bioInput);

		_bioCharCount = BuildCharCount($"{(p.Bio ?? "").Length} / 500");
		_body.AddChild(_bioCharCount);

		_bioInput.TextChanged += () =>
		{
			if (_bioInput.Text.Length > 500)
				_bioInput.Text = _bioInput.Text.Substring(0, 500);
			_bioCharCount.Text = $"{_bioInput.Text.Length} / 500";
		};

		// ─── Personality Traits ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(12));

		var traitsHeader = new HBoxContainer();
		_body.AddChild(traitsHeader);
		traitsHeader.AddChild(SectionLabel("PERSONALITY TRAITS"));
		var traitsSpacer = new Control();
		traitsSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		traitsHeader.AddChild(traitsSpacer);
		var traitsMax = new Label();
		traitsMax.Text = "(max 5)";
		traitsMax.AddThemeFontSizeOverride("font_size", 10);
		traitsMax.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontBody != null) traitsMax.AddThemeFontOverride("font", UITheme.FontBody);
		traitsHeader.AddChild(traitsMax);

		_body.AddChild(VSpacer(8));

		_traitsContainer = new HFlowContainer();
		_traitsContainer.AddThemeConstantOverride("h_separation", 6);
		_traitsContainer.AddThemeConstantOverride("v_separation", 6);
		_body.AddChild(_traitsContainer);
		RebuildTraitTags();

		_body.AddChild(VSpacer(6));

		// Trait add row
		var traitAddRow = new HBoxContainer();
		traitAddRow.AddThemeConstantOverride("separation", 6);
		_body.AddChild(traitAddRow);

		_traitAddInput = BuildInput("New trait...");
		_traitAddInput.MaxLength = 20;
		_traitAddInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_traitAddInput.CustomMinimumSize = new Vector2(0, 30);
		traitAddRow.AddChild(_traitAddInput);

		var addTraitBtn = BuildAccentButton("+ Add");
		addTraitBtn.Pressed += OnAddTrait;
		traitAddRow.AddChild(addTraitBtn);

		_traitAddInput.TextSubmitted += (_) => OnAddTrait();

		// ─── Rumors & Known For ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(12));

		var rumorsHeader = new HBoxContainer();
		_body.AddChild(rumorsHeader);
		rumorsHeader.AddChild(SectionLabel("RUMORS & KNOWN FOR"));
		var rumorsSpacer = new Control();
		rumorsSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		rumorsHeader.AddChild(rumorsSpacer);
		var rumorsMax = new Label();
		rumorsMax.Text = "(max 5)";
		rumorsMax.AddThemeFontSizeOverride("font_size", 10);
		rumorsMax.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontBody != null) rumorsMax.AddThemeFontOverride("font", UITheme.FontBody);
		rumorsHeader.AddChild(rumorsMax);

		_body.AddChild(VSpacer(8));

		_rumorsContainer = new VBoxContainer();
		_rumorsContainer.AddThemeConstantOverride("separation", 6);
		_body.AddChild(_rumorsContainer);
		RebuildRumorRows();

		_body.AddChild(VSpacer(6));

		var addRumorBtn = BuildAccentButton("+ Add Rumor");
		addRumorBtn.Pressed += OnAddRumor;
		_body.AddChild(addRumorBtn);

		// ─── Save / Cancel ───
		_body.AddChild(VSpacer(12));
		_body.AddChild(BuildSep());
		_body.AddChild(VSpacer(16));

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 8);
		btnRow.Alignment = BoxContainer.AlignmentMode.End;
		_body.AddChild(btnRow);

		var cancelBtn = BuildGhostButton("Cancel");
		cancelBtn.CustomMinimumSize = new Vector2(80, 34);
		cancelBtn.Pressed += () => OverworldHUD.Instance?.ClosePanel("icprofile");
		btnRow.AddChild(cancelBtn);

		var saveBtn = BuildPrimaryButton("Save Profile");
		saveBtn.CustomMinimumSize = new Vector2(120, 34);
		saveBtn.Pressed += () => OnSave(p);
		btnRow.AddChild(saveBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  TRAIT MANAGEMENT
	// ═════════════════════════════════════════════════════════

	private void RebuildTraitTags()
	{
		if (_traitsContainer == null) return;
		foreach (var c in _traitsContainer.GetChildren()) c.QueueFree();

		for (int i = 0; i < _traits.Count; i++)
		{
			int idx = i;
			var bg = new PanelContainer();
			var bgStyle = new StyleBoxFlat();
			bgStyle.BgColor = UITheme.CardBg;
			bgStyle.SetCornerRadiusAll(12);
			bgStyle.SetBorderWidthAll(1);
			bgStyle.BorderColor = UITheme.BorderSubtle;
			bgStyle.ContentMarginLeft = 10;
			bgStyle.ContentMarginRight = 8;
			bgStyle.ContentMarginTop = 4;
			bgStyle.ContentMarginBottom = 4;
			bg.AddThemeStyleboxOverride("panel", bgStyle);

			var inner = new HBoxContainer();
			inner.AddThemeConstantOverride("separation", 4);
			bg.AddChild(inner);

			var text = new Label();
			text.Text = _traits[idx];
			text.AddThemeFontSizeOverride("font_size", 11);
			text.AddThemeColorOverride("font_color", UITheme.TextSecondary);
			if (UITheme.FontBodyMedium != null) text.AddThemeFontOverride("font", UITheme.FontBodyMedium);
			inner.AddChild(text);

			var removeBtn = new Button();
			removeBtn.Text = "×";
			removeBtn.FocusMode = FocusModeEnum.None;
			removeBtn.AddThemeFontSizeOverride("font_size", 14);
			removeBtn.AddThemeColorOverride("font_color", UITheme.AccentRed);
			removeBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
			ApplyInvisibleBtnStyle(removeBtn);
			removeBtn.Pressed += () => { _traits.RemoveAt(idx); RebuildTraitTags(); };
			inner.AddChild(removeBtn);

			_traitsContainer.AddChild(bg);
		}
	}

	private void OnAddTrait()
	{
		if (_traitAddInput == null) return;
		var text = _traitAddInput.Text.Trim();
		if (string.IsNullOrEmpty(text) || _traits.Count >= 5 || _traits.Contains(text)) return;
		_traits.Add(text);
		_traitAddInput.Text = "";
		RebuildTraitTags();
	}

	// ═════════════════════════════════════════════════════════
	//  RUMOR MANAGEMENT
	// ═════════════════════════════════════════════════════════

	private void RebuildRumorRows()
	{
		if (_rumorsContainer == null) return;
		foreach (var c in _rumorsContainer.GetChildren()) c.QueueFree();

		for (int i = 0; i < _rumors.Count; i++)
		{
			int idx = i;
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var input = BuildInput("");
			input.Text = _rumors[idx];
			input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			input.CustomMinimumSize = new Vector2(0, 30);
			input.TextChanged += (newText) => { if (idx < _rumors.Count) _rumors[idx] = newText; };
			row.AddChild(input);

			var removeBtn = new Button();
			removeBtn.Text = "×";
			removeBtn.FocusMode = FocusModeEnum.None;
			removeBtn.AddThemeFontSizeOverride("font_size", 14);
			removeBtn.AddThemeColorOverride("font_color", UITheme.AccentRed);
			removeBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
			ApplyInvisibleBtnStyle(removeBtn);
			removeBtn.CustomMinimumSize = new Vector2(24, 0);
			removeBtn.Pressed += () => { _rumors.RemoveAt(idx); RebuildRumorRows(); };
			row.AddChild(removeBtn);

			_rumorsContainer.AddChild(row);
		}
	}

	private void OnAddRumor()
	{
		if (_rumors.Count >= 5) return;
		_rumors.Add("");
		RebuildRumorRows();
	}

	// ═════════════════════════════════════════════════════════
	//  SAVE
	// ═════════════════════════════════════════════════════════

	private void OnSave(Core.PlayerData p)
	{
		p.Tagline = _taglineInput?.Text?.Trim() ?? "";
		p.RpStatus = StatusOptions[_statusDropdown?.Selected ?? 0];
		p.Bio = _bioInput?.Text?.Trim() ?? "";
		p.PersonalityTraits = string.Join(",", _traits.Where(t => !string.IsNullOrWhiteSpace(t)));
		p.Rumors = string.Join("|", _rumors.Where(r => !string.IsNullOrWhiteSpace(r)));

		GD.Print($"[ICProfile] Saved: tagline=\"{p.Tagline}\", status={p.RpStatus}, traits={p.PersonalityTraits}, rumors={p.Rumors}");

		// TODO: Send to server via API
		OverworldHUD.Instance?.ClosePanel("icprofile");
	}

	// ═════════════════════════════════════════════════════════
	//  SHARED UI BUILDERS (matching mockup exactly)
	// ═════════════════════════════════════════════════════════

	/// <summary>Portrait frame — 90×110, rounded, with icon + optional CHANGE overlay.</summary>
	private static PanelContainer BuildPortrait(bool showChange)
	{
		var frame = new PanelContainer();
		frame.CustomMinimumSize = new Vector2(90, 110);
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.CardBg;
		style.SetCornerRadiusAll(6);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		frame.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		frame.AddChild(vbox);

		var icon = new Label();
		icon.Text = "⚔";
		icon.AddThemeFontSizeOverride("font_size", 32);
		icon.AddThemeColorOverride("font_color", UITheme.TextDim);
		icon.HorizontalAlignment = HorizontalAlignment.Center;
		icon.VerticalAlignment = VerticalAlignment.Center;
		vbox.AddChild(icon);

		if (showChange)
		{
			var overlay = new Label();
			overlay.Text = "CHANGE";
			overlay.AddThemeFontSizeOverride("font_size", 9);
			overlay.AddThemeColorOverride("font_color", UITheme.TextSecondary);
			overlay.HorizontalAlignment = HorizontalAlignment.Center;
			if (UITheme.FontBodyMedium != null) overlay.AddThemeFontOverride("font", UITheme.FontBodyMedium);
			vbox.AddChild(overlay);
		}

		return frame;
	}

	/// <summary>Status badge — rounded pill with color dot + text.</summary>
	private static PanelContainer BuildStatusBadge(string status)
	{
		var badge = new PanelContainer();
		var badgeStyle = new StyleBoxFlat();
		badgeStyle.BgColor = UITheme.AccentVioletDim;
		badgeStyle.SetCornerRadiusAll(14);
		badgeStyle.ContentMarginLeft = 12;
		badgeStyle.ContentMarginRight = 12;
		badgeStyle.ContentMarginTop = 5;
		badgeStyle.ContentMarginBottom = 5;
		badge.AddThemeStyleboxOverride("panel", badgeStyle);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; // Don't stretch full width

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		badge.AddChild(row);

		// Color dot
		Color dotColor = status switch
		{
			"Open to RP" => UITheme.AccentEmerald,
			"In Scene" or "Training" => UITheme.AccentGold,
			"AFK / Busy" or "Do Not Disturb" => UITheme.AccentRed,
			_ => UITheme.AccentEmerald
		};

		var dot = new PanelContainer();
		dot.CustomMinimumSize = new Vector2(7, 7);
		var dotStyle = new StyleBoxFlat();
		dotStyle.BgColor = dotColor;
		dotStyle.SetCornerRadiusAll(4); // Circle
		dot.AddThemeStyleboxOverride("panel", dotStyle);
		dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(dot);

		var text = new Label();
		text.Text = status;
		text.AddThemeFontSizeOverride("font_size", 11);
		text.AddThemeColorOverride("font_color", UITheme.Accent);
		if (UITheme.FontBody != null) text.AddThemeFontOverride("font", UITheme.FontBody);
		row.AddChild(text);

		return badge;
	}

	/// <summary>Trait pill — rounded tag with text.</summary>
	private static PanelContainer BuildTraitPill(string trait)
	{
		var pill = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.CardBg;
		style.SetCornerRadiusAll(12);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 4;
		pill.AddThemeStyleboxOverride("panel", style);

		var label = new Label();
		label.Text = trait;
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		if (UITheme.FontBodyMedium != null) label.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		pill.AddChild(label);
		return pill;
	}

	/// <summary>Rumor row — gold arrow + serif text.</summary>
	private static HBoxContainer BuildRumorRow(string rumor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var arrow = new Label();
		arrow.Text = "▸";
		arrow.AddThemeFontSizeOverride("font_size", 11);
		arrow.AddThemeColorOverride("font_color", UITheme.AccentGold);
		arrow.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		row.AddChild(arrow);

		var text = new Label();
		text.Text = rumor;
		text.AddThemeFontSizeOverride("font_size", 12);
		text.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		text.AutowrapMode = TextServer.AutowrapMode.Word;
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (UITheme.FontTitle != null) text.AddThemeFontOverride("font", UITheme.FontTitle); // Serif
		row.AddChild(text);

		return row;
	}

	/// <summary>View-mode meta row: dim label + colored value.</summary>
	private static HBoxContainer ViewMetaRow(string label, string value, Color? valueColor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);

		var lbl = new Label();
		lbl.Text = label;
		lbl.AddThemeFontSizeOverride("font_size", 11);
		lbl.AddThemeColorOverride("font_color", UITheme.TextDim);
		lbl.CustomMinimumSize = new Vector2(36, 0);
		if (UITheme.FontBody != null) lbl.AddThemeFontOverride("font", UITheme.FontBody);
		row.AddChild(lbl);

		var val = new Label();
		val.Text = value;
		val.AddThemeFontSizeOverride("font_size", 11);
		val.AddThemeColorOverride("font_color", valueColor ?? UITheme.TextSecondary);
		if (UITheme.FontBody != null) val.AddThemeFontOverride("font", UITheme.FontBody);
		row.AddChild(val);

		return row;
	}

	/// <summary>Section header — small caps monospace style.</summary>
	private static Label SectionLabel(string text)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontNumbersMedium != null) label.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		return label;
	}

	/// <summary>Dim text for empty states.</summary>
	private static Label DimText(string text)
	{
		var l = new Label();
		l.Text = text;
		l.AddThemeFontSizeOverride("font_size", 12);
		l.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontTitle != null) l.AddThemeFontOverride("font", UITheme.FontTitle);
		return l;
	}

	/// <summary>Char count label — right-aligned monospace.</summary>
	private static Label BuildCharCount(string text)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", UITheme.TextDim);
		label.HorizontalAlignment = HorizontalAlignment.Right;
		if (UITheme.FontNumbers != null) label.AddThemeFontOverride("font", UITheme.FontNumbers);
		return label;
	}

	/// <summary>Thin separator line.</summary>
	private static Control BuildSep()
	{
		var sep = new PanelContainer();
		sep.CustomMinimumSize = new Vector2(0, 1);
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BorderSubtle;
		sep.AddThemeStyleboxOverride("panel", style);
		return sep;
	}

	/// <summary>Vertical spacer.</summary>
	private static Control VSpacer(float h)
	{
		var s = new Control();
		s.CustomMinimumSize = new Vector2(0, h);
		return s;
	}

	// ═════════════════════════════════════════════════════════
	//  INPUT BUILDERS (matching mockup styling)
	// ═════════════════════════════════════════════════════════

	private static LineEdit BuildInput(string placeholder)
	{
		var input = new LineEdit();
		input.PlaceholderText = placeholder;
		input.AddThemeFontSizeOverride("font_size", 13);
		input.AddThemeColorOverride("font_color", UITheme.TextBright);
		input.AddThemeColorOverride("font_placeholder_color", UITheme.TextDim);
		input.AddThemeColorOverride("caret_color", UITheme.Accent);
		if (UITheme.FontBody != null) input.AddThemeFontOverride("font", UITheme.FontBody);

		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgInput;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		input.AddThemeStyleboxOverride("normal", style);

		var focus = (StyleBoxFlat)style.Duplicate();
		focus.BorderColor = UITheme.Accent;
		input.AddThemeStyleboxOverride("focus", focus);

		return input;
	}

	private static TextEdit BuildTextArea(string placeholder)
	{
		var input = new TextEdit();
		input.PlaceholderText = placeholder;
		input.AddThemeFontSizeOverride("font_size", 13);
		input.AddThemeColorOverride("font_color", UITheme.TextBright);
		input.AddThemeColorOverride("font_placeholder_color", UITheme.TextDim);
		input.AddThemeColorOverride("caret_color", UITheme.Accent);
		if (UITheme.FontTitle != null) input.AddThemeFontOverride("font", UITheme.FontTitle); // Serif for bio

		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgInput;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 12;
		style.ContentMarginRight = 12;
		style.ContentMarginTop = 10;
		style.ContentMarginBottom = 10;
		input.AddThemeStyleboxOverride("normal", style);

		var focus = (StyleBoxFlat)style.Duplicate();
		focus.BorderColor = UITheme.Accent;
		input.AddThemeStyleboxOverride("focus", focus);

		return input;
	}

	// ═════════════════════════════════════════════════════════
	//  BUTTON BUILDERS (matching mockup)
	// ═════════════════════════════════════════════════════════

	/// <summary>Primary button — violet bg, white text.</summary>
	private static Button BuildPrimaryButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.FocusMode = FocusModeEnum.None;
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", Colors.White);
		if (UITheme.FontBodyMedium != null) btn.AddThemeFontOverride("font", UITheme.FontBodyMedium);

		var style = new StyleBoxFlat();
		style.BgColor = UITheme.AccentViolet;
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 20;
		style.ContentMarginRight = 20;
		style.ContentMarginTop = 8;
		style.ContentMarginBottom = 8;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = UITheme.AccentViolet.Lightened(0.1f);
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.AddThemeStyleboxOverride("pressed", style);
		return btn;
	}

	/// <summary>Ghost button — transparent bg, subtle border.</summary>
	private static Button BuildGhostButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.FocusMode = FocusModeEnum.None;
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		if (UITheme.FontBody != null) btn.AddThemeFontOverride("font", UITheme.FontBody);

		var style = new StyleBoxFlat();
		style.BgColor = Colors.Transparent;
		style.SetCornerRadiusAll(6);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 16;
		style.ContentMarginRight = 16;
		style.ContentMarginTop = 8;
		style.ContentMarginBottom = 8;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = UITheme.CardBg;
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.AddThemeStyleboxOverride("pressed", style);
		return btn;
	}

	/// <summary>Accent text button — no bg, accent color text.</summary>
	private static Button BuildAccentButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.FocusMode = FocusModeEnum.None;
		btn.AddThemeFontSizeOverride("font_size", 11);
		btn.AddThemeColorOverride("font_color", UITheme.Accent);
		btn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
		if (UITheme.FontBody != null) btn.AddThemeFontOverride("font", UITheme.FontBody);
		ApplyInvisibleBtnStyle(btn);
		return btn;
	}

	private static void StyleOptionButton(OptionButton btn)
	{
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontBody != null) btn.AddThemeFontOverride("font", UITheme.FontBody);

		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgInput;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 5;
		style.ContentMarginBottom = 5;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BorderColor = UITheme.BorderMedium;
		btn.AddThemeStyleboxOverride("hover", hover);
	}

	private static void ApplyInvisibleBtnStyle(Button btn)
	{
		var s = new StyleBoxFlat();
		s.BgColor = Colors.Transparent;
		s.SetCornerRadiusAll(3);
		btn.AddThemeStyleboxOverride("normal", s);
		btn.AddThemeStyleboxOverride("hover", s);
		btn.AddThemeStyleboxOverride("pressed", s);
	}
}
