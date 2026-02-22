using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTactics.Core;
using ProjectTactics.Combat;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Floating panel for choosing skills/spells to equip in loadout slots.
/// Shows all learned abilities with search, tree/element filter, tooltips.
/// Opened by CharacterSheetPanel when a slot is clicked.
/// </summary>
public partial class LoadoutPickerPanel : WindowPanel
{
	// ── What we're picking for ──
	public enum PickMode { Skill, Spell }
	PickMode _mode;
	SkillSlotType _skillSlotType;
	int _slotIndex;
	CharacterLoadout _loadout;
	Action _onEquipped; // callback to refresh character sheet

	// ── UI ──
	LineEdit _searchBar;
	HBoxContainer _filterRow;
	VBoxContainer _listContainer;
	PanelContainer _tooltipPanel;
	VBoxContainer _tooltipContent;
	string _activeFilter = "All";

	// Element icons/colors
	static readonly Dictionary<Element, string> ElIcons = new()
	{
		{Element.None, "◇"}, {Element.Fire, "🔥"}, {Element.Ice, "❄"},
		{Element.Lightning, "⚡"}, {Element.Earth, "🌍"}, {Element.Wind, "🌬"},
		{Element.Water, "🌊"}, {Element.Light, "✦"}, {Element.Dark, "🔮"}
	};

	static readonly Dictionary<Element, Color> ElColors = new()
	{
		{Element.Fire, new("ff6633")}, {Element.Ice, new("66ccff")},
		{Element.Lightning, new("ffcc33")}, {Element.Earth, new("bb8844")},
		{Element.Wind, new("88ddaa")}, {Element.Water, new("4488ff")},
		{Element.Light, new("ffffaa")}, {Element.Dark, new("bb66ff")},
		{Element.None, new("cccccc")},
	};

	static string TreeIcon(SkillTree t) => t switch
	{
		SkillTree.Vanguard => "⚔", SkillTree.Marksman => "🏹", SkillTree.Evoker => "✦",
		SkillTree.Mender => "✚", SkillTree.Runeblade => "◈", SkillTree.Bulwark => "🛡",
		SkillTree.Shadowstep => "🗡", SkillTree.Dreadnought => "💀", SkillTree.Warsinger => "🎵",
		SkillTree.Templar => "✦", SkillTree.Hexer => "🔮", SkillTree.Tactician => "⚙", _ => "◆"
	};

	/// <summary>Create picker for a SKILL slot.</summary>
	public static LoadoutPickerPanel ForSkill(SkillSlotType slotType, int slotIndex, CharacterLoadout loadout, Action onEquipped)
	{
		var p = new LoadoutPickerPanel();
		p._mode = PickMode.Skill;
		p._skillSlotType = slotType;
		p._slotIndex = slotIndex;
		p._loadout = loadout;
		p._onEquipped = onEquipped;
		string slotLabel = slotType == SkillSlotType.Active ? $"Active {slotIndex + 1}"
			: slotType == SkillSlotType.Passive ? $"Passive {slotIndex + 1}" : "Auto";
		p.WindowTitle = $"Equip Skill → {slotLabel}";
		p.DefaultSize = new Vector2(360, 480);
		return p;
	}

	/// <summary>Create picker for a SPELL slot.</summary>
	public static LoadoutPickerPanel ForSpell(int slotIndex, CharacterLoadout loadout, Action onEquipped)
	{
		var p = new LoadoutPickerPanel();
		p._mode = PickMode.Spell;
		p._slotIndex = slotIndex;
		p._loadout = loadout;
		p._onEquipped = onEquipped;
		p.WindowTitle = $"Equip Spell → Slot {slotIndex + 1}";
		p.DefaultSize = new Vector2(360, 480);
		return p;
	}

	public LoadoutPickerPanel()
	{
		DefaultSize = new Vector2(360, 480);
		DefaultPosition = new Vector2(500, 100);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		content.AddThemeConstantOverride("separation", 4);

		// ── SEARCH BAR ──
		_searchBar = new LineEdit();
		_searchBar.PlaceholderText = "Search...";
		_searchBar.ClearButtonEnabled = true;
		_searchBar.AddThemeFontSizeOverride("font_size", 11);
		_searchBar.AddThemeColorOverride("font_color", UITheme.Text);
		var searchBg = new StyleBoxFlat();
		searchBg.BgColor = new Color(UITheme.BgDark, 0.6f);
		searchBg.CornerRadiusBottomLeft = searchBg.CornerRadiusBottomRight =
		searchBg.CornerRadiusTopLeft = searchBg.CornerRadiusTopRight = 4;
		searchBg.ContentMarginLeft = searchBg.ContentMarginRight = 8;
		searchBg.ContentMarginTop = searchBg.ContentMarginBottom = 4;
		_searchBar.AddThemeStyleboxOverride("normal", searchBg);
		_searchBar.TextChanged += _ => RebuildList();
		content.AddChild(_searchBar);

		// ── FILTER ROW ──
		_filterRow = new HBoxContainer();
		_filterRow.AddThemeConstantOverride("separation", 2);
		content.AddChild(_filterRow);

		// ── SCROLLABLE LIST ──
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		content.AddChild(scroll);

		_listContainer = new VBoxContainer();
		_listContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_listContainer.AddThemeConstantOverride("separation", 1);
		scroll.AddChild(_listContainer);

		// ── TOOLTIP (bottom) ──
		_tooltipPanel = new PanelContainer();
		var tooltipStyle = new StyleBoxFlat();
		tooltipStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
		tooltipStyle.BorderColor = new Color(UITheme.AccentViolet, 0.3f);
		tooltipStyle.BorderWidthBottom = tooltipStyle.BorderWidthLeft =
		tooltipStyle.BorderWidthRight = tooltipStyle.BorderWidthTop = 1;
		tooltipStyle.CornerRadiusBottomLeft = tooltipStyle.CornerRadiusBottomRight =
		tooltipStyle.CornerRadiusTopLeft = tooltipStyle.CornerRadiusTopRight = 4;
		tooltipStyle.ContentMarginLeft = tooltipStyle.ContentMarginRight = 8;
		tooltipStyle.ContentMarginTop = tooltipStyle.ContentMarginBottom = 6;
		_tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);
		_tooltipPanel.CustomMinimumSize = new Vector2(0, 80);
		_tooltipPanel.Visible = false;
		content.AddChild(_tooltipPanel);

		_tooltipContent = new VBoxContainer();
		_tooltipContent.AddThemeConstantOverride("separation", 1);
		_tooltipPanel.AddChild(_tooltipContent);

		BuildFilters();
		RebuildList();
	}

	void BuildFilters()
	{
		foreach (var c in _filterRow.GetChildren()) c.QueueFree();

		var filters = new List<string> { "All" };
		if (_mode == PickMode.Skill)
		{
			foreach (SkillTree t in Enum.GetValues(typeof(SkillTree)))
			{
				// Only show trees that have learned skills of the right slot type
				if (_loadout.GetLearnedSkills().Any(s => s.Tree == t && s.Slot == _skillSlotType))
					filters.Add(t.ToString());
			}
		}
		else
		{
			foreach (Element el in Enum.GetValues(typeof(Element)))
			{
				if (el == Element.None) continue;
				if (_loadout.GetLearnedSpells().Any(s => s.Element == el))
					filters.Add(el.ToString());
			}
		}

		foreach (var f in filters)
		{
			var btn = new Button();
			btn.Text = f;
			btn.AddThemeFontSizeOverride("font_size", 9);
			btn.CustomMinimumSize = new Vector2(0, 22);
			btn.ToggleMode = true;
			btn.ButtonPressed = f == _activeFilter;

			var style = new StyleBoxFlat();
			style.BgColor = f == _activeFilter ? new Color(UITheme.AccentViolet, 0.3f) : new Color(0.15f, 0.15f, 0.2f, 0.8f);
			style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight =
			style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 3;
			style.ContentMarginLeft = style.ContentMarginRight = 6;
			style.ContentMarginTop = style.ContentMarginBottom = 2;
			btn.AddThemeStyleboxOverride("normal", style);

			var activeStyle = (StyleBoxFlat)style.Duplicate();
			activeStyle.BgColor = new Color(UITheme.AccentViolet, 0.5f);
			btn.AddThemeStyleboxOverride("pressed", activeStyle);
			btn.AddThemeStyleboxOverride("hover", activeStyle);

			btn.AddThemeColorOverride("font_color", f == _activeFilter ? UITheme.TextBright : UITheme.TextDim);
			btn.AddThemeColorOverride("font_pressed_color", UITheme.TextBright);

			string captured = f;
			btn.Pressed += () => { _activeFilter = captured; BuildFilters(); RebuildList(); };
			_filterRow.AddChild(btn);
		}
	}

	void RebuildList()
	{
		foreach (var c in _listContainer.GetChildren()) c.QueueFree();
		_tooltipPanel.Visible = false;

		string search = _searchBar?.Text?.Trim().ToLower() ?? "";

		// Get equipped IDs to filter duplicates
		var equippedIds = new HashSet<string>();

		if (_mode == PickMode.Skill)
		{
			foreach (var s in _loadout.ActiveSkills) if (s != null) equippedIds.Add(s.Id);
			foreach (var s in _loadout.PassiveSkills) if (s != null) equippedIds.Add(s.Id);
			if (_loadout.AutoSkill != null) equippedIds.Add(_loadout.AutoSkill.Id);

			var skills = _loadout.GetLearnedSkills()
				.Where(s => s.Slot == _skillSlotType && !equippedIds.Contains(s.Id))
				.Where(s => _activeFilter == "All" || s.Tree.ToString() == _activeFilter)
				.Where(s => search == "" || s.Name.ToLower().Contains(search) || s.Tree.ToString().ToLower().Contains(search))
				.OrderBy(s => s.Tree.ToString()).ThenBy(s => s.Tier).ThenBy(s => s.Name)
				.ToList();

			if (skills.Count == 0)
			{
				_listContainer.AddChild(UITheme.CreateDim(
					equippedIds.Count > 0
						? "All matching skills already equipped."
						: "No learned skills of this type.\nVisit the Ability Compendium [B].", 10));
				return;
			}

			SkillTree? lastTree = null;
			foreach (var sk in skills)
			{
				if (lastTree != sk.Tree)
				{
					if (lastTree != null) _listContainer.AddChild(Spacer(2));
					var header = UITheme.CreateDim($"{TreeIcon(sk.Tree)} {sk.Tree}", 9);
					header.AddThemeColorOverride("font_color", UITheme.Accent);
					_listContainer.AddChild(header);
					lastTree = sk.Tree;
				}
				AddSkillRow(sk);
			}
		}
		else // Spell
		{
			foreach (var s in _loadout.EquippedSpells) if (s != null) equippedIds.Add(s.Id);

			var spells = _loadout.GetLearnedSpells()
				.Where(s => !equippedIds.Contains(s.Id))
				.Where(s => _activeFilter == "All" || s.Element.ToString() == _activeFilter)
				.Where(s => search == "" || s.Name.ToLower().Contains(search) || s.Element.ToString().ToLower().Contains(search))
				.OrderBy(s => s.Element.ToString()).ThenBy(s => s.Tier).ThenBy(s => s.Name)
				.ToList();

			if (spells.Count == 0)
			{
				_listContainer.AddChild(UITheme.CreateDim(
					equippedIds.Count > 0
						? "All matching spells already equipped."
						: "No learned spells.\nVisit the Ability Compendium [B].", 10));
				return;
			}

			Element? lastEl = null;
			foreach (var sp in spells)
			{
				if (lastEl != sp.Element)
				{
					if (lastEl != null) _listContainer.AddChild(Spacer(2));
					string eIcon = ElIcons.GetValueOrDefault(sp.Element, "◇");
					var header = UITheme.CreateDim($"{eIcon} {sp.Element}", 9);
					header.AddThemeColorOverride("font_color", ElColors.GetValueOrDefault(sp.Element, UITheme.Accent));
					_listContainer.AddChild(header);
					lastEl = sp.Element;
				}
				AddSpellRow(sp);
			}
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  SKILL ROW
	// ═══════════════════════════════════════════════════════════════

	void AddSkillRow(SkillDefinition sk)
	{
		var btn = new Button();
		btn.Alignment = HorizontalAlignment.Left;
		btn.AddThemeFontSizeOverride("font_size", 10);
		btn.CustomMinimumSize = new Vector2(0, 26);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		string icon = TreeIcon(sk.Tree);
		string tier = sk.Tier > 0 ? $"T{sk.Tier}" : "";
		string cost = sk.Resource switch
		{
			ResourceType.Stamina => $"{sk.StaminaCost} STA",
			ResourceType.Aether => $"{sk.AetherCost} AE",
			ResourceType.Both => $"{sk.StaminaCost}S+{sk.AetherCost}A",
			_ => "Free",
		};
		btn.Text = $" {icon} {sk.Name}   {tier}   {cost}";
		btn.AddThemeColorOverride("font_color", UITheme.Text);

		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.12f, 0.12f, 0.16f, 0.6f);
		normal.CornerRadiusBottomLeft = normal.CornerRadiusBottomRight =
		normal.CornerRadiusTopLeft = normal.CornerRadiusTopRight = 3;
		normal.ContentMarginLeft = 6; normal.ContentMarginRight = 6;
		normal.ContentMarginTop = 2; normal.ContentMarginBottom = 2;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = new Color(UITheme.AccentViolet, 0.2f);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("focus", hover);

		btn.MouseEntered += () => ShowSkillTooltip(sk);
		string capturedId = sk.Id;
		btn.Pressed += () => EquipSkill(capturedId);

		_listContainer.AddChild(btn);
	}

	void ShowSkillTooltip(SkillDefinition sk)
	{
		foreach (var c in _tooltipContent.GetChildren()) c.QueueFree();
		_tooltipPanel.Visible = true;

		var nameLabel = UITheme.CreateBody(sk.Name, 12, UITheme.TextBright);
		_tooltipContent.AddChild(nameLabel);

		string slotStr = sk.Slot.ToString();
		string treeStr = $"{TreeIcon(sk.Tree)} {sk.Tree}";
		string tierStr = $"Tier {sk.Tier}";
		_tooltipContent.AddChild(UITheme.CreateDim($"{slotStr} · {treeStr} · {tierStr}", 9));

		if (!string.IsNullOrEmpty(sk.Description))
		{
			var desc = new Label();
			desc.Text = sk.Description;
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.AddThemeFontSizeOverride("font_size", 9);
			desc.AddThemeColorOverride("font_color", UITheme.TextSecondary);
			_tooltipContent.AddChild(desc);
		}

		var statsRow = new HBoxContainer(); statsRow.AddThemeConstantOverride("separation", 12);
		if (sk.Power > 0) statsRow.AddChild(UITheme.CreateDim($"POW {sk.Power}", 9));
		if (sk.Range > 0) statsRow.AddChild(UITheme.CreateDim($"RNG {sk.Range}", 9));
		statsRow.AddChild(UITheme.CreateDim($"RT +{sk.RtCost}", 9));
		_tooltipContent.AddChild(statsRow);

		if (sk.Weapon != WeaponReq.None)
			_tooltipContent.AddChild(UITheme.CreateDim($"Requires: {sk.Weapon}", 9));
	}

	void EquipSkill(string skillId)
	{
		var skill = SkillDatabase.Get(skillId);
		if (skill == null) { GD.PrintErr($"[LoadoutPicker] Skill not found: {skillId}"); return; }
		GD.Print($"[LoadoutPicker] Equipping {skill.Name} ({skill.Slot}) to index {_slotIndex}");
		_loadout.EquipSkill(_skillSlotType, _slotIndex, skill);
		GD.Print($"[LoadoutPicker] Auto slot after equip: {_loadout.AutoSkill?.Name ?? "null"}");
		_onEquipped?.Invoke();
		// Close the floating window that contains us
		CallDeferred(nameof(DeferredCloseFloatingWindow));
	}

	void DeferredCloseFloatingWindow()
	{
		// Walk up to find the FloatingWindow control
		Node n = GetParent();
		while (n != null)
		{
			if (n is FloatingWindow fw)
			{
				fw.QueueFree();
				return;
			}
			n = n.GetParent();
		}
		// Fallback: just hide self
		Close();
	}

	// ═══════════════════════════════════════════════════════════════
	//  SPELL ROW
	// ═══════════════════════════════════════════════════════════════

	void AddSpellRow(SpellDefinition sp)
	{
		var btn = new Button();
		btn.Alignment = HorizontalAlignment.Left;
		btn.AddThemeFontSizeOverride("font_size", 10);
		btn.CustomMinimumSize = new Vector2(0, 26);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		string icon = ElIcons.GetValueOrDefault(sp.Element, "◇");
		string tier = sp.Tier > 0 ? $"T{sp.Tier}" : "";
		btn.Text = $" {icon} {sp.Name}   {tier}   {sp.AetherCost} AE";
		btn.AddThemeColorOverride("font_color", ElColors.GetValueOrDefault(sp.Element, UITheme.Text));

		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.12f, 0.12f, 0.16f, 0.6f);
		normal.CornerRadiusBottomLeft = normal.CornerRadiusBottomRight =
		normal.CornerRadiusTopLeft = normal.CornerRadiusTopRight = 3;
		normal.ContentMarginLeft = 6; normal.ContentMarginRight = 6;
		normal.ContentMarginTop = 2; normal.ContentMarginBottom = 2;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = new Color(UITheme.AccentViolet, 0.2f);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("focus", hover);

		btn.MouseEntered += () => ShowSpellTooltip(sp);
		string capturedId = sp.Id;
		btn.Pressed += () => EquipSpell(capturedId);

		_listContainer.AddChild(btn);
	}

	void ShowSpellTooltip(SpellDefinition sp)
	{
		foreach (var c in _tooltipContent.GetChildren()) c.QueueFree();
		_tooltipPanel.Visible = true;

		string icon = ElIcons.GetValueOrDefault(sp.Element, "◇");
		var nameLabel = UITheme.CreateBody($"{icon} {sp.Name}", 12,
			ElColors.GetValueOrDefault(sp.Element, UITheme.TextBright));
		_tooltipContent.AddChild(nameLabel);

		string tierStr = $"Tier {sp.Tier}";
		string castStr = sp.CastType.ToString();
		_tooltipContent.AddChild(UITheme.CreateDim($"{sp.Element} · {castStr} · {tierStr}", 9));

		if (!string.IsNullOrEmpty(sp.Description))
		{
			var desc = new Label();
			desc.Text = sp.Description;
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.AddThemeFontSizeOverride("font_size", 9);
			desc.AddThemeColorOverride("font_color", UITheme.TextSecondary);
			_tooltipContent.AddChild(desc);
		}

		var statsRow = new HBoxContainer(); statsRow.AddThemeConstantOverride("separation", 12);
		if (sp.Power > 0) statsRow.AddChild(UITheme.CreateDim($"POW {sp.Power}", 9));
		statsRow.AddChild(UITheme.CreateDim($"RNG {sp.RangeMin}-{sp.RangeMax}", 9));
		statsRow.AddChild(UITheme.CreateDim($"RT +{sp.RtCost}", 9));
		statsRow.AddChild(UITheme.CreateDim($"{sp.AetherCost} AE", 9));
		_tooltipContent.AddChild(statsRow);

		if (!string.IsNullOrEmpty(sp.StatusEffect))
			_tooltipContent.AddChild(UITheme.CreateDim($"Effect: {sp.StatusEffect}", 9));
	}

	void EquipSpell(string spellId)
	{
		var spell = SpellDatabase.Get(spellId);
		if (spell == null) { GD.PrintErr($"[LoadoutPicker] Spell not found: {spellId}"); return; }
		GD.Print($"[LoadoutPicker] Equipping spell {spell.Name} to slot {_slotIndex}");
		_loadout.EquipSpell(_slotIndex, spell);
		_onEquipped?.Invoke();
		CallDeferred(nameof(DeferredCloseFloatingWindow));
	}
}
