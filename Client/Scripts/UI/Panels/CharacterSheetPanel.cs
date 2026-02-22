using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTactics.Core;
using ProjectTactics.Combat;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Character Sheet — Tactics Ogre: Reborn single-screen layout.
/// No tabs. Equipment | Portrait | Items/Spells/Skills | Stats all visible at once.
/// Reads from GameManager.ActiveLoadout (shared state).
/// Hotkey: C
/// </summary>
public partial class CharacterSheetPanel : WindowPanel
{
	VBoxContainer _root;
	bool _refreshPending;
	CharacterLoadout _loadout;

	// Skill equip state
	int _selectedSkillSlotIndex = -1;
	SkillSlotType _selectedSkillSlotType;
	int _selectedSpellSlot = -1;
	EquipSlot? _selectedEquipSlot = null;

	// Element icons/colors — matches AbilityShopPanel
	static readonly Dictionary<Element, string> ElIcons = new()
	{
		{Element.None, "◇"}, {Element.Fire, "🔥"}, {Element.Ice, "❄"},
		{Element.Lightning, "⚡"}, {Element.Earth, "🌍"}, {Element.Wind, "🌬"},
		{Element.Water, "🌊"}, {Element.Light, "✦"}, {Element.Dark, "🔮"}
	};
	static readonly Dictionary<Element, Color> ElColors = new()
	{
		{Element.None, new("64647A")}, {Element.Fire, new("CC4422")}, {Element.Ice, new("44AADD")},
		{Element.Lightning, new("DDAA22")}, {Element.Earth, new("887744")},
		{Element.Wind, new("55BB55")}, {Element.Water, new("3366AA")},
		{Element.Light, new("DDCC66")}, {Element.Dark, new("8844AA")}
	};
	static string TreeIcon(SkillTree t) => t switch
	{
		SkillTree.Vanguard => "⚔", SkillTree.Marksman => "🏹", SkillTree.Evoker => "✦",
		SkillTree.Mender => "✚", SkillTree.Runeblade => "◈", SkillTree.Bulwark => "🛡",
		SkillTree.Shadowstep => "🗡", SkillTree.Dreadnought => "💀", SkillTree.Warsinger => "🎵",
		SkillTree.Templar => "✦", SkillTree.Hexer => "🔮", SkillTree.Tactician => "⚙", _ => "◆"
	};

	public CharacterSheetPanel()
	{
		WindowTitle = "Character Sheet";
		DefaultSize = new Vector2(760, 540);
		DefaultPosition = new Vector2(40, 40);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_root = content;
		content.AddThemeConstantOverride("separation", 0);
		content.AddChild(PlaceholderText("Loading character..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		_loadout = GameManager.Instance?.ActiveLoadout;
		if (_loadout != null) _loadout.LoadoutChanged += OnLoadoutChanged;
		RebuildAll();
	}

	public override void OnClose()
	{
		base.OnClose();
		if (_loadout != null) _loadout.LoadoutChanged -= OnLoadoutChanged;
	}

	private void OnLoadoutChanged() => QueueRebuild();
	protected override void OnDataChanged() => QueueRebuild();

	private void QueueRebuild()
	{
		if (_refreshPending) return;
		_refreshPending = true;
		CallDeferred(nameof(DeferredRebuild));
	}
	private void DeferredRebuild() { _refreshPending = false; RebuildAll(); }

	// ═══════════════════════════════════════════════════════════════
	//  MASTER REBUILD — single screen, no tabs
	// ═══════════════════════════════════════════════════════════════

	private void RebuildAll()
	{
		if (_root == null) return;
		foreach (var c in _root.GetChildren()) c.QueueFree();

		var p = GameManager.Instance?.ActiveCharacter;
		_loadout ??= GameManager.Instance?.ActiveLoadout;
		if (p == null) { _root.AddChild(PlaceholderText("No character loaded.")); return; }
		if (_loadout == null) { _root.AddChild(PlaceholderText("No loadout.")); return; }

		// ── TOP: Identity + Resource Bars ──
		BuildHeader(p);
		_root.AddChild(Spacer(4));
		BuildResourceBars(p);
		_root.AddChild(Spacer(4));
		_root.AddChild(ThinSeparator());
		_root.AddChild(Spacer(4));

		// ── SECTION LABELS (TO:Reborn style) ──
		var labelRow = new HBoxContainer(); labelRow.AddThemeConstantOverride("separation", 0);
		_root.AddChild(labelRow);
		labelRow.AddChild(SectionTab("EQUIPMENT", 170));
		labelRow.AddChild(SectionTab("PORTRAIT", 0, true));  // expands to match portrait column
		labelRow.AddChild(SectionTab("LOADOUT", 220));
		labelRow.AddChild(SectionTab("STATS", 120));
		_root.AddChild(Spacer(4));

		// ── MAIN 4-COLUMN LAYOUT ──
		var main = new HBoxContainer(); main.AddThemeConstantOverride("separation", 6);
		main.SizeFlagsVertical = SizeFlags.ExpandFill;
		_root.AddChild(main);

		BuildEquipmentColumn(main);
		BuildPortraitColumn(main, p);
		BuildLoadoutColumn(main);
		BuildStatsColumn(main, p);
	}

	// ═══════════════════════════════════════════════════════════════
	//  HEADER — Name, Level, Race, MOVE/JUMP/RT
	// ═══════════════════════════════════════════════════════════════

	private void BuildHeader(PlayerData p)
	{
		var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 8);
		_root.AddChild(header);

		// Portrait thumbnail
		var thumb = MakeIconBox("⚔", 48, 56);
		header.AddChild(thumb);

		// Identity
		var id = new VBoxContainer(); id.AddThemeConstantOverride("separation", 0);
		id.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(id);
		var nameRow = new HBoxContainer(); nameRow.AddThemeConstantOverride("separation", 6);
		id.AddChild(nameRow);
		nameRow.AddChild(UITheme.CreateHeading2(p.CharacterName, 18));
		nameRow.AddChild(UITheme.CreateDim($"Lv.{p.CharacterLevel}", 12));
		var metaRow = new HBoxContainer(); metaRow.AddThemeConstantOverride("separation", 10);
		id.AddChild(metaRow);
		metaRow.AddChild(UITheme.CreateBody($"{p.RaceName}", 11, UITheme.TextSecondary));
		metaRow.AddChild(UITheme.CreateDim("·", 11));
		metaRow.AddChild(UITheme.CreateBody($"{p.City}", 11, UITheme.TextSecondary));
		metaRow.AddChild(UITheme.CreateDim("·", 11));
		metaRow.AddChild(UITheme.CreateBody($"{p.RpRank}", 11, UITheme.TextSecondary));

		// MOVE / JUMP / RT badges
		var badges = new VBoxContainer(); badges.AddThemeConstantOverride("separation", 1);
		header.AddChild(badges);
		badges.AddChild(BadgeRow("MOVE", $"{p.Move}"));
		badges.AddChild(BadgeRow("JUMP", $"{p.Jump}"));
		badges.AddChild(BadgeRow("RT", $"{p.BaseRt}"));
	}

	// ═══════════════════════════════════════════════════════════════
	//  RESOURCE BARS — HP / Stamina / Aether
	// ═══════════════════════════════════════════════════════════════

	private void BuildResourceBars(PlayerData p)
	{
		var bars = new VBoxContainer(); bars.AddThemeConstantOverride("separation", 2);
		_root.AddChild(bars);
		bars.AddChild(ResourceBar("HP", p.CurrentHp, p.MaxHp, UITheme.HpBar));
		bars.AddChild(ResourceBar("STA", p.CurrentStamina, p.MaxStamina, UITheme.StaBar));
		bars.AddChild(ResourceBar("AE", p.CurrentAether, p.MaxAether, UITheme.EthBar));
	}

	// ═══════════════════════════════════════════════════════════════
	//  COLUMN 1: EQUIPMENT (left, 170px)
	// ═══════════════════════════════════════════════════════════════

	private void BuildEquipmentColumn(HBoxContainer parent)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 3);
		col.CustomMinimumSize = new Vector2(170, 0);
		parent.AddChild(col);

		foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
			col.AddChild(BuildEquipSlotRow(slot));

		// Item picker (when a slot is selected)
		if (_selectedEquipSlot.HasValue)
		{
			col.AddChild(Spacer(4));
			col.AddChild(ThinSeparator());
			col.AddChild(Spacer(2));

			var selSlot = _selectedEquipSlot.Value;
			col.AddChild(UITheme.CreateDim($"▸ Equip {EquipmentItem.GetSlotName(selSlot)}:", 8));

			// Filter inventory for items matching this slot
			var candidates = _loadout.Inventory
				.Where(i => i.IsEquipment && i.EquipData.Slot == selSlot)
				.ToList();

			if (candidates.Count == 0)
			{
				col.AddChild(UITheme.CreateDim("No items for this slot.", 8));
			}
			else
			{
				foreach (var inv in candidates)
				{
					var eq = inv.EquipData;
					var row = new Button();
					row.Text = $" {eq.Icon} {eq.Name}";
					row.Alignment = HorizontalAlignment.Left;
					row.AddThemeFontSizeOverride("font_size", 9);
					row.AddThemeColorOverride("font_color", EquipmentItem.GetRarityColor(eq.Rarity));
					row.CustomMinimumSize = new Vector2(0, 22);
					row.TooltipText = eq.BonusSummary;
					StyleFlatButton(row);
					var item = eq;
					row.Pressed += () =>
					{
						_loadout.SetEquipment(selSlot, item);
						_selectedEquipSlot = null;
					};
					col.AddChild(row);
				}
			}

			// Unequip option if something is already equipped
			if (_loadout.Equipment[selSlot] != null)
			{
				var unequipBtn = new Button();
				unequipBtn.Text = "  ✕  Unequip";
				unequipBtn.Alignment = HorizontalAlignment.Left;
				unequipBtn.AddThemeFontSizeOverride("font_size", 9);
				unequipBtn.AddThemeColorOverride("font_color", UITheme.AccentRed);
				unequipBtn.CustomMinimumSize = new Vector2(0, 22);
				StyleFlatButton(unequipBtn);
				unequipBtn.Pressed += () =>
				{
					_loadout.SetEquipment(selSlot, null);
					_selectedEquipSlot = null;
				};
				col.AddChild(unequipBtn);
			}
		}

		col.AddChild(Spacer(4));
		col.AddChild(ThinSeparator());

		// Weight
		var wRow = new HBoxContainer(); wRow.AddThemeConstantOverride("separation", 4);
		wRow.AddChild(UITheme.CreateDim("Weight:", 9));
		wRow.AddChild(UITheme.CreateNumbers($"{_loadout.TotalEquipWeight}", 10, UITheme.TextSecondary));
		col.AddChild(wRow);

		// Rank / Allegiance at bottom
		col.AddChild(Spacer(4));
		col.AddChild(ThinSeparator());
		col.AddChild(Spacer(2));
		var p = GameManager.Instance.ActiveCharacter;
		col.AddChild(FieldRow("Rank", p.RpRank));
		col.AddChild(FieldRow("Allegiance", p.Allegiance));
	}

	private HBoxContainer BuildEquipSlotRow(EquipSlot slot)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 3);
		row.CustomMinimumSize = new Vector2(0, 28);

		bool selected = _selectedEquipSlot == slot;
		var item = _loadout.Equipment[slot];

		// Clickable slot icon
		var iconBtn = new Button { Text = EquipmentItem.GetSlotIcon(slot) };
		iconBtn.CustomMinimumSize = new Vector2(26, 26);
		iconBtn.AddThemeFontSizeOverride("font_size", 12);
		iconBtn.AddThemeColorOverride("font_color", item != null ? UITheme.TextBright : UITheme.TextDim);
		var iconStyle = new StyleBoxFlat();
		iconStyle.BgColor = selected ? new Color(UITheme.Accent, 0.15f) : UITheme.CardBg;
		iconStyle.SetCornerRadiusAll(4);
		iconStyle.SetBorderWidthAll(selected ? 2 : 1);
		iconStyle.BorderColor = selected ? UITheme.Accent : UITheme.BorderSubtle;
		iconBtn.AddThemeStyleboxOverride("normal", iconStyle);
		var iconHover = (StyleBoxFlat)iconStyle.Duplicate();
		iconHover.BgColor = UITheme.CardHoverBg; iconHover.BorderColor = UITheme.Accent;
		iconBtn.AddThemeStyleboxOverride("hover", iconHover);
		var s = slot;
		iconBtn.Pressed += () => OnEquipSlotPressed(s);
		row.AddChild(iconBtn);

		if (item != null)
		{
			var name = UITheme.CreateBody(item.Name, 10, EquipmentItem.GetRarityColor(item.Rarity));
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			name.ClipText = true;
			row.AddChild(name);
		}
		else
		{
			var empty = UITheme.CreateDim("Empty", 10);
			empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(empty);
		}
		return row;
	}

	private void OnEquipSlotPressed(EquipSlot slot)
	{
		if (ProjectTactics.UI.OverworldHUD.Instance?.InCombat == true) return;
		_selectedEquipSlot = _selectedEquipSlot == slot ? null : slot;
		_selectedSkillSlotIndex = -1;
		_selectedSpellSlot = -1;
		RebuildAll();
	}

	// ═══════════════════════════════════════════════════════════════
	//  COLUMN 2: PORTRAIT (center, expands)
	// ═══════════════════════════════════════════════════════════════

	private void BuildPortraitColumn(HBoxContainer parent, PlayerData p)
	{
		var col = new VBoxContainer();
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		col.AddThemeConstantOverride("separation", 4);
		parent.AddChild(col);

		var portraitPanel = new PanelContainer();
		portraitPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
		portraitPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var ps = new StyleBoxFlat();
		ps.BgColor = UITheme.CardBg; ps.SetCornerRadiusAll(6);
		ps.SetBorderWidthAll(1); ps.BorderColor = UITheme.BorderSubtle;
		portraitPanel.AddThemeStyleboxOverride("panel", ps);
		col.AddChild(portraitPanel);

		// Center label using anchors so it stays put regardless of panel size
		var ppLabel = new Label { Text = "PORTRAIT" };
		ppLabel.AddThemeFontSizeOverride("font_size", 14);
		ppLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
		ppLabel.HorizontalAlignment = HorizontalAlignment.Center;
		ppLabel.VerticalAlignment = VerticalAlignment.Center;
		ppLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		portraitPanel.AddChild(ppLabel);

		// ── IC Profile link + Edit button ──
		var btnRow = new HBoxContainer(); btnRow.AddThemeConstantOverride("separation", 6);
		col.AddChild(btnRow);

		var profileBtn = new Button { Text = "View IC Profile →" };
		profileBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		profileBtn.AddThemeFontSizeOverride("font_size", 9);
		profileBtn.AddThemeColorOverride("font_color", UITheme.AccentGreen);
		profileBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
		var pbStyle = new StyleBoxFlat(); pbStyle.BgColor = Colors.Transparent; pbStyle.SetCornerRadiusAll(3);
		profileBtn.AddThemeStyleboxOverride("normal", pbStyle);
		var pbHover = (StyleBoxFlat)pbStyle.Duplicate(); pbHover.BgColor = UITheme.CardHoverBg;
		profileBtn.AddThemeStyleboxOverride("hover", pbHover);
		profileBtn.Pressed += () =>
		{
			OverworldHUD.Instance?.OpenPanel("icprofile_view");
		};
		btnRow.AddChild(profileBtn);

		var editBtn = new Button { Text = "✎ Edit" };
		editBtn.AddThemeFontSizeOverride("font_size", 9);
		editBtn.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		editBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
		var ebStyle = new StyleBoxFlat(); ebStyle.BgColor = Colors.Transparent; ebStyle.SetCornerRadiusAll(3);
		editBtn.AddThemeStyleboxOverride("normal", ebStyle);
		var ebHover = (StyleBoxFlat)ebStyle.Duplicate(); ebHover.BgColor = UITheme.CardHoverBg;
		editBtn.AddThemeStyleboxOverride("hover", ebHover);
		editBtn.Pressed += () =>
		{
			OverworldHUD.Instance?.OpenPanel("icprofile");
		};
		btnRow.AddChild(editBtn);
	}

	// ═══════════════════════════════════════════════════════════════
	//  COLUMN 3: LOADOUT — Items, Spells, Skills (200px)
	// ═══════════════════════════════════════════════════════════════

	private void BuildLoadoutColumn(HBoxContainer parent)
	{
		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(220, 0);
		// Fixed width — don't let it expand and steal space from portrait
		scroll.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		parent.AddChild(scroll);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 3);
		col.CustomMinimumSize = new Vector2(220, 0);
		scroll.AddChild(col);

		// ── ITEMS ──
		col.AddChild(SmallHeader("ITEMS"));
		BuildItemsGrid(col);

		col.AddChild(Spacer(4));

		// ── SPELLS (4 slots) ──
		col.AddChild(SmallHeader("SPELLS"));
		BuildSpellSlots(col);

		col.AddChild(Spacer(4));

		// ── SKILLS (5 Active + 2 Passive + 1 Auto) ──
		col.AddChild(SmallHeader("SKILLS"));
		BuildSkillSlots(col);
	}

	// ── Items Grid ──

	private void BuildItemsGrid(VBoxContainer col)
	{
		if (_loadout.Inventory.Count == 0)
		{
			col.AddChild(UITheme.CreateDim("No items", 9));
			return;
		}
		var grid = new GridContainer { Columns = 5 };
		grid.AddThemeConstantOverride("h_separation", 3);
		grid.AddThemeConstantOverride("v_separation", 3);
		col.AddChild(grid);

		foreach (var item in _loadout.Inventory)
		{
			var btn = MakeSlotIcon(item.Icon, item.Quantity > 1 ? $"{item.Quantity}" : null);
			btn.TooltipText = $"{item.Name}\n{item.Description}";
			grid.AddChild(btn);
		}
	}

	// ── Spell Slots ──

	private void BuildSpellSlots(VBoxContainer col)
	{
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 3);
		grid.AddThemeConstantOverride("v_separation", 3);
		col.AddChild(grid);

		for (int i = 0; i < 4; i++)
		{
			var sp = _loadout.EquippedSpells[i];
			string icon = sp != null ? ElIcons.GetValueOrDefault(sp.Element, "◇") : "·";
			string tip = sp?.Name ?? "Empty spell slot";
			var btn = MakeSlotIcon(icon, null, sp != null);
			btn.TooltipText = sp != null ? $"{sp.Name}\n{sp.AetherCost} AE · R:{sp.RangeMax}" : tip;
			int idx = i;
			btn.Pressed += () => OnSpellSlotPressed(idx);
			if (_selectedSpellSlot == i) HighlightSlot(btn);
			grid.AddChild(btn);
		}
	}

	private void OnSpellSlotPressed(int idx)
	{
		if (ProjectTactics.UI.OverworldHUD.Instance?.InCombat == true) return;

		// If clicking same slot, unequip
		if (_selectedSpellSlot == idx)
		{
			_loadout.EquipSpell(idx, null);
			_selectedSpellSlot = -1;
			return;
		}

		_selectedSkillSlotIndex = -1;
		_selectedSpellSlot = -1;
		_selectedEquipSlot = null;

		// Open the loadout picker panel
		OpenSpellPicker(idx);
	}

	private void OpenSpellPicker(int idx)
	{
		var hud = OverworldHUD.Instance;
		if (hud == null) return;

		var picker = LoadoutPickerPanel.ForSpell(idx, _loadout, () => RebuildAll());
		var window = FloatingWindow.Open(hud, picker.PanelTitle, picker, 360, 480);
		picker.CallDeferred(nameof(WindowPanel.DeferredOpen));

		var viewport = hud.GetViewportRect().Size;
		window.CallDeferred(nameof(FloatingWindow.SetWindowPosition),
			new Vector2(viewport.X / 2f - 180f, viewport.Y / 2f - 240f));
	}

	// ── Skill Slots ──

	private void BuildSkillSlots(VBoxContainer col)
	{
		// Active (5)
		col.AddChild(UITheme.CreateDim("Active", 8));
		var activeGrid = new GridContainer { Columns = 5 };
		activeGrid.AddThemeConstantOverride("h_separation", 3);
		activeGrid.AddThemeConstantOverride("v_separation", 3);
		col.AddChild(activeGrid);
		for (int i = 0; i < 5; i++)
		{
			var sk = _loadout.ActiveSkills[i];
			string icon = sk != null ? TreeIcon(sk.Tree) : "·";
			var btn = MakeSlotIcon(icon, null, sk != null);
			btn.TooltipText = sk?.Name ?? "Empty active slot (click to equip)";
			int idx = i;
			btn.Pressed += () => OnSkillSlotPressed(SkillSlotType.Active, idx);
			if (_selectedSkillSlotType == SkillSlotType.Active && _selectedSkillSlotIndex == i) HighlightSlot(btn);
			activeGrid.AddChild(btn);
		}

		// Passive (2) + Auto (1)
		col.AddChild(UITheme.CreateDim("Passive", 8));
		var passGrid = new GridContainer { Columns = 2 };
		passGrid.AddThemeConstantOverride("h_separation", 3);
		passGrid.AddThemeConstantOverride("v_separation", 3);
		col.AddChild(passGrid);

		for (int i = 0; i < 2; i++)
		{
			var sk = _loadout.PassiveSkills[i];
			string icon = sk != null ? TreeIcon(sk.Tree) : "·";
			var btn = MakeSlotIcon(icon, "P", sk != null);
			btn.TooltipText = sk != null ? $"[Passive] {sk.Name}" : "Empty passive slot";
			int idx = i;
			btn.Pressed += () => OnSkillSlotPressed(SkillSlotType.Passive, idx);
			if (_selectedSkillSlotType == SkillSlotType.Passive && _selectedSkillSlotIndex == i) HighlightSlot(btn);
			passGrid.AddChild(btn);
		}

		col.AddChild(Spacer(2));
		col.AddChild(UITheme.CreateDim("Auto", 8));
		var autoRow = new HBoxContainer(); autoRow.AddThemeConstantOverride("separation", 3);
		col.AddChild(autoRow);
		var autoSk = _loadout.AutoSkill;
		string autoIcon = autoSk != null ? TreeIcon(autoSk.Tree) : "·";
		var autoBtn = MakeSlotIcon(autoIcon, "A", autoSk != null);
		autoBtn.TooltipText = autoSk != null ? $"[Auto] {autoSk.Name}" : "Empty auto slot";
		autoBtn.Pressed += () => OnSkillSlotPressed(SkillSlotType.Auto, 0);
		if (_selectedSkillSlotType == SkillSlotType.Auto && _selectedSkillSlotIndex == 0) HighlightSlot(autoBtn);
		autoRow.AddChild(autoBtn);
		if (autoSk != null)
			autoRow.AddChild(UITheme.CreateDim(autoSk.Name, 8));
	}

	private void OnSkillSlotPressed(SkillSlotType type, int idx)
	{
		if (ProjectTactics.UI.OverworldHUD.Instance?.InCombat == true) return;

		// If clicking same slot, unequip
		if (_selectedSkillSlotType == type && _selectedSkillSlotIndex == idx)
		{
			_loadout.EquipSkill(type, idx, null);
			_selectedSkillSlotIndex = -1;
			return;
		}

		_selectedSkillSlotIndex = -1;
		_selectedSpellSlot = -1;
		_selectedEquipSlot = null;

		// Open the loadout picker panel
		OpenSkillPicker(type, idx);
	}

	private void OpenSkillPicker(SkillSlotType type, int idx)
	{
		var hud = OverworldHUD.Instance;
		if (hud == null) return;

		var picker = LoadoutPickerPanel.ForSkill(type, idx, _loadout, () => RebuildAll());
		var window = FloatingWindow.Open(hud, picker.PanelTitle, picker, 360, 480);
		picker.CallDeferred(nameof(WindowPanel.DeferredOpen));

		var viewport = hud.GetViewportRect().Size;
		window.CallDeferred(nameof(FloatingWindow.SetWindowPosition),
			new Vector2(viewport.X / 2f - 180f, viewport.Y / 2f - 240f));
	}

	// ═══════════════════════════════════════════════════════════════
	//  COLUMN 4: STATS (far right, 120px)
	// ═══════════════════════════════════════════════════════════════

	private void BuildStatsColumn(HBoxContainer parent, PlayerData p)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 1);
		col.CustomMinimumSize = new Vector2(120, 0);
		parent.AddChild(col);

		var bonuses = _loadout.TotalEquipBonuses();

		// Derived combat stats
		col.AddChild(StatRow("ATK", p.Atk, bonuses.GetValueOrDefault("ATK")));
		col.AddChild(StatRow("DEF", p.Def, bonuses.GetValueOrDefault("DEF")));
		col.AddChild(StatRow("EATK", p.Eatk, bonuses.GetValueOrDefault("EATK")));
		col.AddChild(StatRow("EDEF", p.Edef, bonuses.GetValueOrDefault("EDEF")));
		col.AddChild(ThinSeparator());
		col.AddChild(StatRow("AVD", p.Avd, bonuses.GetValueOrDefault("AVD")));
		col.AddChild(StatRow("ACC", p.Acc, bonuses.GetValueOrDefault("ACC")));
		col.AddChild(StatRow("CRIT", (int)p.CritPercent, 0, "%"));
		col.AddChild(Spacer(4));
		col.AddChild(ThinSeparator());
		col.AddChild(Spacer(2));

		// Training stats
		col.AddChild(TrainRow("STR", p.Strength));
		col.AddChild(TrainRow("VIT", p.Vitality));
		col.AddChild(TrainRow("DEX", p.Dexterity));
		col.AddChild(TrainRow("AGI", p.Agility));
		col.AddChild(TrainRow("ETC", p.EtherControl));
		col.AddChild(TrainRow("MND", p.Mind));

		col.AddChild(Spacer(4));
		col.AddChild(ThinSeparator());
		col.AddChild(Spacer(2));

		// RPP display
		var rppRow = new HBoxContainer(); rppRow.AddThemeConstantOverride("separation", 4);
		rppRow.AddChild(UITheme.CreateDim("RPP", 9));
		rppRow.AddChild(UITheme.CreateNumbers($"{_loadout.Rpp}", 11, new Color("D4A843")));
		col.AddChild(rppRow);
	}

	// ═══════════════════════════════════════════════════════════════
	//  UI HELPERS
	// ═══════════════════════════════════════════════════════════════

	static HBoxContainer BadgeRow(string label, string value)
	{
		var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 3);
		var l = UITheme.CreateDim(label, 9); l.CustomMinimumSize = new Vector2(32, 0);
		l.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(l);
		row.AddChild(UITheme.CreateNumbers(value, 12, UITheme.TextBright));
		return row;
	}

	static HBoxContainer ResourceBar(string label, int current, int max, Color barColor)
	{
		var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
		var lbl = UITheme.CreateDim(label, 10);
		lbl.CustomMinimumSize = new Vector2(24, 0);
		lbl.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(lbl);

		var barBg = new PanelContainer();
		barBg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		barBg.CustomMinimumSize = new Vector2(0, 14);
		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = UITheme.IsDarkMode ? new Color(0.1f, 0.1f, 0.15f) : new Color(0.85f, 0.85f, 0.88f);
		bgStyle.SetCornerRadiusAll(3);
		barBg.AddThemeStyleboxOverride("panel", bgStyle);
		row.AddChild(barBg);

		float pct = max > 0 ? Mathf.Clamp((float)current / max, 0f, 1f) : 0f;
		var fill = new ColorRect { Color = barColor };
		fill.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
		fill.AnchorRight = pct;
		fill.OffsetLeft = 1; fill.OffsetTop = 1; fill.OffsetRight = -1; fill.OffsetBottom = -1;
		barBg.AddChild(fill);

		var val = UITheme.CreateNumbers($"{current}/{max}", 9, UITheme.TextBright);
		val.HorizontalAlignment = HorizontalAlignment.Center;
		val.VerticalAlignment = VerticalAlignment.Center;
		val.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		barBg.AddChild(val);
		return row;
	}

	static HBoxContainer StatRow(string label, int value, int bonus, string suffix = "")
	{
		var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 3);
		row.CustomMinimumSize = new Vector2(0, 16);
		var l = UITheme.CreateDim(label, 10); l.CustomMinimumSize = new Vector2(34, 0); row.AddChild(l);
		var v = UITheme.CreateNumbers($"{value}{suffix}", 11, UITheme.TextBright);
		v.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(v);
		if (bonus != 0)
		{
			var c = bonus > 0 ? UITheme.AccentGreen : UITheme.AccentRed;
			row.AddChild(UITheme.CreateNumbers($"{(bonus > 0 ? "+" : "")}{bonus}", 9, c));
		}
		return row;
	}

	static HBoxContainer TrainRow(string label, int value)
	{
		var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 3);
		row.CustomMinimumSize = new Vector2(0, 15);
		var l = UITheme.CreateDim(label, 9); l.CustomMinimumSize = new Vector2(28, 0); row.AddChild(l);
		var v = UITheme.CreateNumbers($"{value}", 11, UITheme.Text);
		v.HorizontalAlignment = HorizontalAlignment.Right;
		v.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(v);
		return row;
	}

	static HBoxContainer FieldRow(string label, string value)
	{
		var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4);
		row.AddChild(UITheme.CreateDim(label + ":", 9));
		row.AddChild(UITheme.CreateBody(value ?? "None", 9, UITheme.TextSecondary));
		return row;
	}

	static PanelContainer SectionTab(string text, float minW, bool expand = false)
	{
		var panel = new PanelContainer();
		if (minW > 0) panel.CustomMinimumSize = new Vector2(minW, 20);
		if (expand) panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.CardBg;
		style.SetBorderWidthAll(1); style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 6; style.ContentMarginRight = 6;
		style.ContentMarginTop = 2; style.ContentMarginBottom = 2;
		panel.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 9);
		lbl.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		if (UITheme.FontBodySemiBold != null) lbl.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		panel.AddChild(lbl);
		return panel;
	}

	static Label SmallHeader(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", 10);
		l.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		if (UITheme.FontBodySemiBold != null) l.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		return l;
	}

	static PanelContainer MakeIconBox(string icon, float w, float h)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(w, h);
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.CardBg; style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1); style.BorderColor = UITheme.BorderSubtle;
		panel.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label { Text = icon };
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.AddThemeColorOverride("font_color", UITheme.TextDim);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		panel.AddChild(lbl);
		return panel;
	}

	Button MakeSlotIcon(string icon, string badge, bool filled = false)
	{
		var btn = new Button { Text = icon };
		btn.CustomMinimumSize = new Vector2(34, 34);
		btn.AddThemeFontSizeOverride("font_size", 14);
		btn.AddThemeColorOverride("font_color", filled ? UITheme.TextBright : UITheme.TextDim);
		var style = new StyleBoxFlat();
		style.BgColor = filled ? new Color(UITheme.Accent, 0.08f) : UITheme.CardBg;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = filled ? new Color(UITheme.Accent, 0.3f) : UITheme.BorderSubtle;
		btn.AddThemeStyleboxOverride("normal", style);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = UITheme.CardHoverBg; hover.BorderColor = UITheme.Accent;
		btn.AddThemeStyleboxOverride("hover", hover);
		// TODO: badge overlay for P/A labels
		return btn;
	}

	static void HighlightSlot(Button btn)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(UITheme.Accent, 0.15f);
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(2); style.BorderColor = UITheme.Accent;
		btn.AddThemeStyleboxOverride("normal", style);
	}

	static Button MakeSmallButton(string text)
	{
		var btn = new Button { Text = text };
		btn.CustomMinimumSize = new Vector2(20, 20);
		btn.AddThemeFontSizeOverride("font_size", 9);
		btn.AddThemeColorOverride("font_color", UITheme.TextDim);
		btn.AddThemeColorOverride("font_hover_color", UITheme.AccentRed);
		var s = new StyleBoxFlat(); s.BgColor = Colors.Transparent; s.SetCornerRadiusAll(3);
		btn.AddThemeStyleboxOverride("normal", s);
		var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = UITheme.AccentRedDim;
		btn.AddThemeStyleboxOverride("hover", h);
		return btn;
	}

	static void StyleFlatButton(Button btn)
	{
		var s = new StyleBoxFlat(); s.BgColor = Colors.Transparent; s.SetCornerRadiusAll(3);
		s.ContentMarginLeft = 4; s.ContentMarginRight = 4;
		btn.AddThemeStyleboxOverride("normal", s);
		var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = UITheme.CardHoverBg;
		btn.AddThemeStyleboxOverride("hover", h);
	}
}
