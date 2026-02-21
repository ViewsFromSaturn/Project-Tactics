using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTactics.Combat;
using ProjectTactics.Core;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Full-screen ability database panel. Browse, filter, inspect, and purchase
/// all 180 skill tree abilities + 105 spells.
/// Two main tabs: SKILL TREES | SPELLS
/// Left sidebar: category filters. Center: scrollable list. Right: detail panel.
/// Purchases write to GameManager.ActiveLoadout so CharacterSheetPanel sees them.
/// </summary>
public partial class AbilityShopPanel : WindowPanel
{
	// ═══════════════════════════════════════════════════════════════
	//  THEME
	// ═══════════════════════════════════════════════════════════════

	static readonly Color BgDark       = new("080812");
	static readonly Color BgCard       = new(0.235f, 0.255f, 0.314f, 0.10f);
	static readonly Color BgCardHover  = new(0.235f, 0.255f, 0.314f, 0.20f);
	static readonly Color BgSelected   = new(0.545f, 0.361f, 0.965f, 0.15f);
	static readonly Color BdSubtle     = new(0.235f, 0.255f, 0.314f, 0.35f);
	static readonly Color BdAccent     = new("8B5CF6");
	static readonly Color TxBright     = new("EEEEE8");
	static readonly Color TxPrimary    = new("D4D2CC");
	static readonly Color TxSecondary  = new("9090A0");
	static readonly Color TxDim        = new("64647A");
	static readonly Color TxDisabled   = new("44445A");
	static readonly Color ColGold      = new("D4A843");
	static readonly Color ColStamina   = new("CC8833");
	static readonly Color ColAether    = new("5588DD");
	static readonly Color ColBoth      = new("AA66BB");
	static readonly Color ColGreen     = new("5CB85C");
	static readonly Color ColRed       = new("CC4444");

	static readonly Dictionary<Element, Color> ElColors = new() {
		{Element.None, TxDim}, {Element.Fire, new("CC4422")}, {Element.Ice, new("44AADD")},
		{Element.Lightning, new("DDAA22")}, {Element.Earth, new("887744")},
		{Element.Wind, new("55BB55")}, {Element.Water, new("3366AA")},
		{Element.Light, new("DDCC66")}, {Element.Dark, new("8844AA")}
	};

	static readonly Dictionary<Element, string> ElIcons = new() {
		{Element.None, "◇"}, {Element.Fire, "🔥"}, {Element.Ice, "❄"},
		{Element.Lightning, "⚡"}, {Element.Earth, "🌍"}, {Element.Wind, "🌬"},
		{Element.Water, "🌊"}, {Element.Light, "✦"}, {Element.Dark, "🔮"}
	};

	// ═══════════════════════════════════════════════════════════════
	//  STATE — reads from shared loadout
	// ═══════════════════════════════════════════════════════════════

	enum Tab { Skills, Spells }
	Tab _activeTab = Tab.Skills;

	// Skill filters
	SkillTree _selectedTree = SkillTree.Vanguard;
	SkillSlotType? _slotFilter = null;

	// Spell filters
	Element _selectedElement = Element.Fire;
	int _tierFilter = 0;

	// Selection
	SkillDefinition _selectedSkill;
	SpellDefinition _selectedSpell;

	// Shared loadout — single source of truth
	CharacterLoadout Loadout => GameManager.Instance?.ActiveLoadout;
	int PlayerRpp => Loadout?.Rpp ?? 0;
	HashSet<string> OwnedSkills => Loadout?.LearnedSkillIds ?? new();
	HashSet<string> OwnedSpells => Loadout?.LearnedSpellIds ?? new();

	// UI refs
	VBoxContainer _listContainer;
	VBoxContainer _detailContainer;
	HBoxContainer _tabBar;
	VBoxContainer _sidebarContainer;
	Label _rppLabel;

	public AbilityShopPanel()
	{
		PanelTitle = "◆ ABILITY COMPENDIUM";
		DefaultWidth = 960;
		DefaultHeight = 620;
		ManagesOwnScroll = true;
	}

	// ═══════════════════════════════════════════════════════════════
	//  BUILD
	// ═══════════════════════════════════════════════════════════════

	protected override void BuildContent(VBoxContainer content)
	{
		content.AddThemeConstantOverride("separation", 0);

		// ─── TOP BAR: Tabs + RPP ───
		var topBar = new HBoxContainer();
		topBar.AddThemeConstantOverride("separation", 0);
		content.AddChild(topBar);

		_tabBar = new HBoxContainer();
		_tabBar.AddThemeConstantOverride("separation", 2);
		_tabBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		topBar.AddChild(_tabBar);

		AddTab("⚔  SKILL TREES", Tab.Skills);
		AddTab("✦  SPELLS", Tab.Spells);

		var spacer = new Control(); spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		topBar.AddChild(spacer);

		// RPP display
		var rppBox = MakeCardPanel();
		rppBox.CustomMinimumSize = new Vector2(140, 0);
		var rppHb = new HBoxContainer(); rppHb.AddThemeConstantOverride("separation", 6);
		rppBox.AddChild(rppHb);
		Lbl(rppHb, "◈ RPP:", ColGold, 12);
		_rppLabel = Lbl(rppHb, PlayerRpp.ToString(), TxBright, 14, true);
		topBar.AddChild(rppBox);

		AddSep(content);

		// ─── MAIN BODY: Sidebar | List | Detail ───
		var body = new HBoxContainer();
		body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		body.AddThemeConstantOverride("separation", 0);
		content.AddChild(body);

		// ── LEFT SIDEBAR ──
		var sidebarScroll = new ScrollContainer();
		sidebarScroll.CustomMinimumSize = new Vector2(170, 0);
		sidebarScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		sidebarScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		body.AddChild(sidebarScroll);

		_sidebarContainer = new VBoxContainer();
		_sidebarContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_sidebarContainer.AddThemeConstantOverride("separation", 2);
		sidebarScroll.AddChild(_sidebarContainer);

		AddVSep(body);

		// ── CENTER LIST ──
		var listScroll = new ScrollContainer();
		listScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		listScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		body.AddChild(listScroll);

		_listContainer = new VBoxContainer();
		_listContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_listContainer.AddThemeConstantOverride("separation", 1);
		listScroll.AddChild(_listContainer);

		AddVSep(body);

		// ── RIGHT DETAIL ──
		var detailScroll = new ScrollContainer();
		detailScroll.CustomMinimumSize = new Vector2(280, 0);
		detailScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		detailScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		body.AddChild(detailScroll);

		var detailMargin = new MarginContainer();
		detailMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		detailMargin.AddThemeConstantOverride("margin_left", 12);
		detailMargin.AddThemeConstantOverride("margin_right", 12);
		detailMargin.AddThemeConstantOverride("margin_top", 8);
		detailScroll.AddChild(detailMargin);

		_detailContainer = new VBoxContainer();
		_detailContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_detailContainer.AddThemeConstantOverride("separation", 6);
		detailMargin.AddChild(_detailContainer);

		RebuildSidebar();
		RebuildList();
		ShowEmptyDetail();
	}

	// ═══════════════════════════════════════════════════════════════
	//  TABS
	// ═══════════════════════════════════════════════════════════════

	void AddTab(string text, Tab tab)
	{
		var btn = new Button();
		btn.Text = text;
		btn.ToggleMode = true;
		btn.ButtonPressed = (tab == _activeTab);
		btn.CustomMinimumSize = new Vector2(160, 36);
		btn.AddThemeFontSizeOverride("font_size", 12);

		var style = new StyleBoxFlat();
		style.BgColor = (tab == _activeTab) ? BgSelected : Colors.Transparent;
		style.SetContentMarginAll(8);
		style.BorderWidthBottom = (tab == _activeTab) ? 2 : 0;
		style.BorderColor = BdAccent;
		style.SetCornerRadiusAll(0);
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("pressed", style);

		var hover = style.Duplicate() as StyleBoxFlat;
		hover.BgColor = BgCardHover;
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.AddThemeColorOverride("font_color", (tab == _activeTab) ? TxBright : TxSecondary);
		btn.AddThemeColorOverride("font_pressed_color", TxBright);
		btn.AddThemeColorOverride("font_hover_color", TxBright);

		btn.Pressed += () => SwitchTab(tab);
		_tabBar.AddChild(btn);
	}

	void SwitchTab(Tab tab)
	{
		_activeTab = tab;
		_selectedSkill = null;
		_selectedSpell = null;
		CallDeferred(nameof(RebuildTabs));
	}

	void RebuildTabs()
	{
		foreach (var c in _tabBar.GetChildren()) (c as Node)?.QueueFree();
		AddTab("⚔  SKILL TREES", Tab.Skills);
		AddTab("✦  SPELLS", Tab.Spells);
		RebuildSidebar();
		RebuildList();
		ShowEmptyDetail();
	}

	// ═══════════════════════════════════════════════════════════════
	//  SIDEBAR
	// ═══════════════════════════════════════════════════════════════

	void RebuildSidebar()
	{
		ClearChildren(_sidebarContainer);

		if (_activeTab == Tab.Skills)
		{
			Lbl(_sidebarContainer, "SKILL TREES", ColGold, 11, true);
			AddSep(_sidebarContainer);

			foreach (SkillTree tree in Enum.GetValues<SkillTree>())
			{
				var btn = MakeSidebarBtn(TreeLabel(tree), tree == _selectedTree);
				var t = tree;
				btn.Pressed += () => { _selectedTree = t; _slotFilter = null; RebuildSidebar(); RebuildList(); };
				_sidebarContainer.AddChild(btn);
			}

			AddSep(_sidebarContainer);
			Lbl(_sidebarContainer, "FILTER BY SLOT", TxDim, 10);

			var allBtn = MakeSidebarBtn("All Types", _slotFilter == null);
			allBtn.Pressed += () => { _slotFilter = null; RebuildSidebar(); RebuildList(); };
			_sidebarContainer.AddChild(allBtn);

			foreach (SkillSlotType slot in Enum.GetValues<SkillSlotType>())
			{
				string icon = slot == SkillSlotType.Active ? "⚔" : slot == SkillSlotType.Passive ? "◈" : "⟐";
				var btn = MakeSidebarBtn($"{icon} {slot}", _slotFilter == slot);
				var s = slot;
				btn.Pressed += () => { _slotFilter = s; RebuildSidebar(); RebuildList(); };
				_sidebarContainer.AddChild(btn);
			}
		}
		else
		{
			Lbl(_sidebarContainer, "ELEMENTS", ColGold, 11, true);
			AddSep(_sidebarContainer);

			foreach (Element el in new[] { Element.Fire, Element.Ice, Element.Lightning,
				Element.Earth, Element.Wind, Element.Water, Element.Light, Element.Dark })
			{
				string icon = ElIcons.GetValueOrDefault(el, "◇");
				var btn = MakeSidebarBtn($"{icon} {el}", el == _selectedElement);
				btn.AddThemeColorOverride("font_color", el == _selectedElement ?
					TxBright : ElColors.GetValueOrDefault(el, TxPrimary));
				var e = el;
				btn.Pressed += () => { _selectedElement = e; RebuildSidebar(); RebuildList(); };
				_sidebarContainer.AddChild(btn);
			}

			AddSep(_sidebarContainer);
			Lbl(_sidebarContainer, "FILTER BY TIER", TxDim, 10);

			var allBtn = MakeSidebarBtn("All Tiers", _tierFilter == 0);
			allBtn.Pressed += () => { _tierFilter = 0; RebuildSidebar(); RebuildList(); };
			_sidebarContainer.AddChild(allBtn);

			for (int t = 1; t <= 4; t++)
			{
				string roman = t switch { 1 => "I", 2 => "II", 3 => "III", _ => "IV" };
				int reqStat = SpellDatabase.GetStatReq(t);
				var btn = MakeSidebarBtn($"Tier {roman}  (req {reqStat})", _tierFilter == t);
				int tier = t;
				btn.Pressed += () => { _tierFilter = tier; RebuildSidebar(); RebuildList(); };
				_sidebarContainer.AddChild(btn);
			}
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  LIST
	// ═══════════════════════════════════════════════════════════════

	void RebuildList()
	{
		ClearChildren(_listContainer);

		if (_activeTab == Tab.Skills)
		{
			var skills = SkillDatabase.GetTree(_selectedTree);
			if (_slotFilter.HasValue)
				skills = skills.Where(s => s.Slot == _slotFilter.Value).ToList();

			var header = MakeListHeader();
			AddListHeaderCell(header, "Name", 200);
			AddListHeaderCell(header, "Type", 60);
			AddListHeaderCell(header, "Tier", 40);
			AddListHeaderCell(header, "Cost", 70);
			AddListHeaderCell(header, "Range", 45);
			AddListHeaderCell(header, "RT", 35);
			AddListHeaderCell(header, "Power", 50);
			AddListHeaderCell(header, "Status", 60);
			_listContainer.AddChild(header);

			foreach (var skill in skills)
				_listContainer.AddChild(BuildSkillRow(skill));
		}
		else
		{
			var spells = SpellDatabase.GetElement(_selectedElement);
			if (_tierFilter > 0)
				spells = spells.Where(s => s.Tier == _tierFilter).ToList();

			var header = MakeListHeader();
			AddListHeaderCell(header, "Name", 160);
			AddListHeaderCell(header, "Tier", 40);
			AddListHeaderCell(header, "AE", 40);
			AddListHeaderCell(header, "Range", 50);
			AddListHeaderCell(header, "Area", 50);
			AddListHeaderCell(header, "RT", 35);
			AddListHeaderCell(header, "Power", 50);
			AddListHeaderCell(header, "RPP", 45);
			AddListHeaderCell(header, "Type", 60);
			_listContainer.AddChild(header);

			foreach (var spell in spells)
				_listContainer.AddChild(BuildSpellRow(spell));
		}
	}

	// ─── Skill Row ──────────────────────────────────────────────

	Control BuildSkillRow(SkillDefinition sk)
	{
		bool owned = OwnedSkills.Contains(sk.Id);
		bool selected = _selectedSkill?.Id == sk.Id;
		Color rowBg = selected ? BgSelected : Colors.Transparent;

		var row = new PanelContainer();
		var rowStyle = new StyleBoxFlat();
		rowStyle.BgColor = rowBg;
		rowStyle.SetContentMarginAll(4);
		rowStyle.ContentMarginLeft = 8;
		row.AddThemeStyleboxOverride("panel", rowStyle);
		row.CustomMinimumSize = new Vector2(0, 28);

		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.GuiInput += (ev) => {
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				_selectedSkill = sk;
				_selectedSpell = null;
				RebuildList();
				ShowSkillDetail(sk);
			}
		};

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(hbox);

		string slotIcon = sk.Slot == SkillSlotType.Active ? "⚔" : sk.Slot == SkillSlotType.Passive ? "◈" : "⟐";
		Color slotColor = sk.Slot == SkillSlotType.Active ? ColStamina :
			sk.Slot == SkillSlotType.Passive ? ColAether : ColBoth;

		string nameStr = owned ? $"✓ {sk.Name}" : sk.Name;
		Color nameCol = owned ? ColGreen : TxPrimary;
		LblFixed(hbox, nameStr, nameCol, 11, 200);
		LblFixed(hbox, $"{slotIcon} {SlotShort(sk.Slot)}", slotColor, 10, 60);
		LblFixed(hbox, TierRoman(sk.Tier), ColGold, 10, 40);
		LblFixed(hbox, CostStr(sk), ResourceColor(sk.Resource), 10, 70);
		LblFixed(hbox, sk.Range > 0 ? sk.Range.ToString() : "—", TxDim, 10, 45);
		LblFixed(hbox, sk.RtCost > 0 ? $"+{sk.RtCost}" : "—", TxDim, 10, 35);
		LblFixed(hbox, sk.Power > 0 ? sk.Power.ToString() : "—", TxSecondary, 10, 50);
		LblFixed(hbox, owned ? "OWNED" : "—", owned ? ColGreen : TxDisabled, 9, 60);

		return row;
	}

	// ─── Spell Row ──────────────────────────────────────────────

	Control BuildSpellRow(SpellDefinition sp)
	{
		bool owned = OwnedSpells.Contains(sp.Id);
		bool selected = _selectedSpell?.Id == sp.Id;
		Color rowBg = selected ? BgSelected : Colors.Transparent;

		var row = new PanelContainer();
		var rowStyle = new StyleBoxFlat();
		rowStyle.BgColor = rowBg;
		rowStyle.SetContentMarginAll(4);
		rowStyle.ContentMarginLeft = 8;
		row.AddThemeStyleboxOverride("panel", rowStyle);
		row.CustomMinimumSize = new Vector2(0, 28);

		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.GuiInput += (ev) => {
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				_selectedSpell = sp;
				_selectedSkill = null;
				RebuildList();
				ShowSpellDetail(sp);
			}
		};

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(hbox);

		Color nameCol = owned ? ColGreen : ElColors.GetValueOrDefault(sp.Element, TxPrimary);
		string nameStr = owned ? $"✓ {sp.Name}" : sp.Name;

		LblFixed(hbox, nameStr, nameCol, 11, 160);
		LblFixed(hbox, TierRoman(sp.Tier), ColGold, 10, 40);
		LblFixed(hbox, sp.AetherCost.ToString(), ColAether, 10, 40);
		LblFixed(hbox, sp.RangeMin == sp.RangeMax ? sp.RangeMax.ToString() : $"{sp.RangeMin}-{sp.RangeMax}", TxDim, 10, 50);
		LblFixed(hbox, sp.AreaSize > 0 ? $"D({sp.AreaSize})" : "1", TxDim, 10, 50);
		LblFixed(hbox, $"+{sp.RtCost}", TxDim, 10, 35);
		LblFixed(hbox, sp.Power > 0 ? sp.Power.ToString() : "—", TxSecondary, 10, 50);
		LblFixed(hbox, sp.RppCost.ToString(), ColGold, 10, 45);
		LblFixed(hbox, CastTypeShort(sp.CastType), TxDim, 9, 60);

		return row;
	}

	// ═══════════════════════════════════════════════════════════════
	//  DETAIL PANEL
	// ═══════════════════════════════════════════════════════════════

	void ShowEmptyDetail()
	{
		ClearChildren(_detailContainer);
		Lbl(_detailContainer, "Select an ability", TxDim, 12);
		Lbl(_detailContainer, "to view details", TxDisabled, 10);
	}

	void ShowSkillDetail(SkillDefinition sk)
	{
		ClearChildren(_detailContainer);
		bool owned = OwnedSkills.Contains(sk.Id);

		Lbl(_detailContainer, sk.Name, TxBright, 16, true);

		// Tags
		var tags = new HBoxContainer();
		tags.AddThemeConstantOverride("separation", 6);
		_detailContainer.AddChild(tags);

		AddTag(tags, $"{SlotShort(sk.Slot)}", sk.Slot == SkillSlotType.Active ? ColStamina :
			sk.Slot == SkillSlotType.Passive ? ColAether : ColBoth);
		AddTag(tags, $"Tier {TierRoman(sk.Tier)}", ColGold);
		AddTag(tags, sk.Tree.ToString(), BdAccent);
		if (sk.Element != Element.None)
			AddTag(tags, sk.Element.ToString(), ElColors.GetValueOrDefault(sk.Element, TxDim));

		AddSep(_detailContainer);

		// Description
		var descLabel = new Label();
		descLabel.Text = sk.Description;
		descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descLabel.AddThemeColorOverride("font_color", TxPrimary);
		descLabel.AddThemeFontSizeOverride("font_size", 11);
		_detailContainer.AddChild(descLabel);

		if (!string.IsNullOrEmpty(sk.ScalingNote))
		{
			Lbl(_detailContainer, $"Ranks: {sk.ScalingNote}", ColGold, 10);
			Lbl(_detailContainer, $"Max Rank: {sk.MaxRank}", TxDim, 10);
		}

		AddSep(_detailContainer);

		// Stats grid
		Lbl(_detailContainer, "◆ COMBAT DATA", TxSecondary, 10, true);
		var grid = new GridContainer();
		grid.Columns = 2;
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 2);
		_detailContainer.AddChild(grid);

		if (sk.Resource != ResourceType.None)
			AddStatRow(grid, "Resource", sk.Resource.ToString(), ResourceColor(sk.Resource));
		if (sk.StaminaCost > 0) AddStatRow(grid, "Stamina", sk.StaminaCost.ToString(), ColStamina);
		if (sk.AetherCost > 0) AddStatRow(grid, "Aether", sk.AetherCost.ToString(), ColAether);
		if (sk.RtCost > 0) AddStatRow(grid, "RT Cost", $"+{sk.RtCost}", TxPrimary);
		if (sk.Power > 0) AddStatRow(grid, "Power", sk.Power.ToString(), TxBright);
		if (sk.Range > 0) AddStatRow(grid, "Range", sk.Range.ToString(), TxPrimary);
		AddStatRow(grid, "Target", sk.Target.ToString(), TxPrimary);
		if (sk.AreaSize > 0) AddStatRow(grid, "Area", $"Diamond({sk.AreaSize})", TxPrimary);
		if (sk.Weapon != WeaponReq.None)
			AddStatRow(grid, "Weapon", WeaponLabel(sk.Weapon), TxSecondary);

		AddSep(_detailContainer);

		// Purchase / Status
		if (owned)
		{
			var ownedPanel = MakeCardPanel();
			Lbl(ownedPanel, "✓  LEARNED", ColGreen, 12, true);
			_detailContainer.AddChild(ownedPanel);
		}
		else
		{
			int rppCost = sk.Tier * 5;
			bool canAfford = PlayerRpp >= rppCost;

			var costRow = new HBoxContainer();
			costRow.AddThemeConstantOverride("separation", 8);
			_detailContainer.AddChild(costRow);
			Lbl(costRow, "Cost:", TxSecondary, 11);
			Lbl(costRow, $"{rppCost} RPP", canAfford ? ColGold : ColRed, 11, true);

			var buyBtn = new Button();
			buyBtn.Text = canAfford ? $"LEARN  ({rppCost} RPP)" : "INSUFFICIENT RPP";
			buyBtn.Disabled = !canAfford;
			buyBtn.CustomMinimumSize = new Vector2(250, 36);
			buyBtn.AddThemeFontSizeOverride("font_size", 12);

			var btnStyle = new StyleBoxFlat();
			btnStyle.BgColor = canAfford ? new Color(BdAccent, 0.3f) : new Color(TxDisabled, 0.1f);
			btnStyle.SetContentMarginAll(8);
			btnStyle.SetCornerRadiusAll(4);
			btnStyle.BorderWidthBottom = 2;
			btnStyle.BorderColor = canAfford ? BdAccent : TxDisabled;
			buyBtn.AddThemeStyleboxOverride("normal", btnStyle);
			buyBtn.AddThemeColorOverride("font_color", canAfford ? TxBright : TxDisabled);

			var hoverStyle = btnStyle.Duplicate() as StyleBoxFlat;
			hoverStyle.BgColor = canAfford ? new Color(BdAccent, 0.5f) : new Color(TxDisabled, 0.1f);
			buyBtn.AddThemeStyleboxOverride("hover", hoverStyle);

			buyBtn.Pressed += () => PurchaseSkill(sk, rppCost);
			_detailContainer.AddChild(buyBtn);
		}
	}

	void ShowSpellDetail(SpellDefinition sp)
	{
		ClearChildren(_detailContainer);
		bool owned = OwnedSpells.Contains(sp.Id);

		string icon = ElIcons.GetValueOrDefault(sp.Element, "◇");
		Lbl(_detailContainer, $"{icon}  {sp.Name}", ElColors.GetValueOrDefault(sp.Element, TxBright), 16, true);

		// Tags
		var tags = new HBoxContainer();
		tags.AddThemeConstantOverride("separation", 6);
		_detailContainer.AddChild(tags);

		AddTag(tags, $"Tier {TierRoman(sp.Tier)}", ColGold);
		AddTag(tags, sp.Element.ToString(), ElColors.GetValueOrDefault(sp.Element, TxDim));
		AddTag(tags, CastTypeShort(sp.CastType), TxSecondary);

		AddSep(_detailContainer);

		var descLabel = new Label();
		descLabel.Text = sp.Description;
		descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descLabel.AddThemeColorOverride("font_color", TxPrimary);
		descLabel.AddThemeFontSizeOverride("font_size", 11);
		_detailContainer.AddChild(descLabel);

		if (!string.IsNullOrEmpty(sp.StatusEffect))
			Lbl(_detailContainer, $"Status: {sp.StatusEffect}", ColRed, 10);

		AddSep(_detailContainer);

		Lbl(_detailContainer, "◆ SPELL DATA", TxSecondary, 10, true);
		var grid = new GridContainer();
		grid.Columns = 2;
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 2);
		_detailContainer.AddChild(grid);

		AddStatRow(grid, "Aether Cost", sp.AetherCost.ToString(), ColAether);
		AddStatRow(grid, "RT Cost", $"+{sp.RtCost}", TxPrimary);
		if (sp.Power > 0) AddStatRow(grid, "Power", sp.Power.ToString(), TxBright);
		string rangeStr = sp.RangeMin == sp.RangeMax ? sp.RangeMax.ToString() : $"{sp.RangeMin}-{sp.RangeMax}";
		AddStatRow(grid, "Range", rangeStr, TxPrimary);
		AddStatRow(grid, "Target", sp.Target.ToString(), TxPrimary);
		if (sp.AreaSize > 0) AddStatRow(grid, "Area", $"Diamond({sp.AreaSize})", TxPrimary);
		AddStatRow(grid, "Cast Type", CastTypeShort(sp.CastType), TxSecondary);

		AddSep(_detailContainer);

		if (owned)
		{
			var ownedPanel = MakeCardPanel();
			Lbl(ownedPanel, "✓  LEARNED", ColGreen, 12, true);
			_detailContainer.AddChild(ownedPanel);
		}
		else
		{
			int rppCost = sp.RppCost;
			bool canAfford = PlayerRpp >= rppCost;

			var costRow = new HBoxContainer();
			costRow.AddThemeConstantOverride("separation", 8);
			_detailContainer.AddChild(costRow);
			Lbl(costRow, "Cost:", TxSecondary, 11);
			Lbl(costRow, $"{rppCost} RPP", canAfford ? ColGold : ColRed, 11, true);

			var buyBtn = new Button();
			buyBtn.Text = canAfford ? $"LEARN  ({rppCost} RPP)" : "INSUFFICIENT RPP";
			buyBtn.Disabled = !canAfford;
			buyBtn.CustomMinimumSize = new Vector2(250, 36);
			buyBtn.AddThemeFontSizeOverride("font_size", 12);

			var btnStyle = new StyleBoxFlat();
			btnStyle.BgColor = canAfford ? new Color(BdAccent, 0.3f) : new Color(TxDisabled, 0.1f);
			btnStyle.SetContentMarginAll(8);
			btnStyle.SetCornerRadiusAll(4);
			btnStyle.BorderWidthBottom = 2;
			btnStyle.BorderColor = canAfford ? BdAccent : TxDisabled;
			buyBtn.AddThemeStyleboxOverride("normal", btnStyle);
			buyBtn.AddThemeColorOverride("font_color", canAfford ? TxBright : TxDisabled);

			var hoverStyle = btnStyle.Duplicate() as StyleBoxFlat;
			hoverStyle.BgColor = canAfford ? new Color(BdAccent, 0.5f) : new Color(TxDisabled, 0.1f);
			buyBtn.AddThemeStyleboxOverride("hover", hoverStyle);

			buyBtn.Pressed += () => PurchaseSpell(sp);
			_detailContainer.AddChild(buyBtn);
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  PURCHASE — writes to shared CharacterLoadout
	// ═══════════════════════════════════════════════════════════════

	void PurchaseSkill(SkillDefinition sk, int cost)
	{
		if (Loadout == null || !Loadout.LearnSkill(sk.Id, cost)) return;
		_rppLabel.Text = Loadout.Rpp.ToString();
		GD.Print($"[AbilityShop] Learned skill: {sk.Name} ({sk.Id}) for {cost} RPP");
		RebuildList();
		ShowSkillDetail(sk);
	}

	void PurchaseSpell(SpellDefinition sp)
	{
		if (Loadout == null || !Loadout.LearnSpell(sp.Id, sp.RppCost)) return;
		_rppLabel.Text = Loadout.Rpp.ToString();
		GD.Print($"[AbilityShop] Learned spell: {sp.Name} ({sp.Id}) for {sp.RppCost} RPP");
		RebuildList();
		ShowSpellDetail(sp);
	}

	// ═══════════════════════════════════════════════════════════════
	//  UI HELPERS
	// ═══════════════════════════════════════════════════════════════

	Label Lbl(Control parent, string text, Color color, int size, bool bold = false)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", size);
		if (bold) lbl.LabelSettings = new LabelSettings { FontColor = color, FontSize = size };
		parent.AddChild(lbl);
		return lbl;
	}

	void LblFixed(Control parent, string text, Color color, int size, float width)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", size);
		lbl.CustomMinimumSize = new Vector2(width, 0);
		lbl.ClipText = true;
		lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
		parent.AddChild(lbl);
	}

	void AddStatRow(GridContainer grid, string label, string value, Color valColor)
	{
		var lbl = new Label(); lbl.Text = label;
		lbl.AddThemeColorOverride("font_color", TxDim);
		lbl.AddThemeFontSizeOverride("font_size", 10);
		grid.AddChild(lbl);

		var val = new Label(); val.Text = value;
		val.AddThemeColorOverride("font_color", valColor);
		val.AddThemeFontSizeOverride("font_size", 10);
		grid.AddChild(val);
	}

	void AddTag(HBoxContainer parent, string text, Color color)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = new Color(color, 0.15f);
		style.SetContentMarginAll(2);
		style.ContentMarginLeft = 6; style.ContentMarginRight = 6;
		style.SetCornerRadiusAll(3);
		style.BorderWidthLeft = 1; style.BorderColor = new Color(color, 0.4f);
		panel.AddThemeStyleboxOverride("panel", style);

		var lbl = new Label(); lbl.Text = text;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 9);
		panel.AddChild(lbl);
		parent.AddChild(panel);
	}

	Button MakeSidebarBtn(string text, bool active)
	{
		var btn = new Button();
		btn.Text = text;
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(160, 28);
		btn.AddThemeFontSizeOverride("font_size", 11);

		var style = new StyleBoxFlat();
		style.BgColor = active ? BgSelected : Colors.Transparent;
		style.SetContentMarginAll(4);
		style.ContentMarginLeft = 10;
		style.SetCornerRadiusAll(3);
		if (active) { style.BorderWidthLeft = 2; style.BorderColor = BdAccent; }
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeColorOverride("font_color", active ? TxBright : TxSecondary);

		var hover = style.Duplicate() as StyleBoxFlat;
		hover.BgColor = BgCardHover;
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeColorOverride("font_hover_color", TxBright);

		return btn;
	}

	HBoxContainer MakeListHeader()
	{
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		hbox.CustomMinimumSize = new Vector2(0, 24);
		return hbox;
	}

	void AddListHeaderCell(HBoxContainer header, string text, float width)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.CustomMinimumSize = new Vector2(width, 0);
		lbl.ClipText = true;
		lbl.AddThemeColorOverride("font_color", TxDim);
		lbl.AddThemeFontSizeOverride("font_size", 9);
		header.AddChild(lbl);
	}

	PanelContainer MakeCardPanel()
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = BgCard;
		style.SetContentMarginAll(8);
		style.SetCornerRadiusAll(4);
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	void AddSep(Control parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("separator", BdSubtle);
		sep.AddThemeConstantOverride("separation", 6);
		parent.AddChild(sep);
	}

	void AddVSep(Control parent)
	{
		var sep = new VSeparator();
		sep.AddThemeColorOverride("separator", BdSubtle);
		sep.AddThemeConstantOverride("separation", 0);
		sep.CustomMinimumSize = new Vector2(1, 0);
		parent.AddChild(sep);
	}

	void ClearChildren(Control c)
	{
		foreach (var child in c.GetChildren())
			if (child is Node n) n.QueueFree();
	}

	// ═══════════════════════════════════════════════════════════════
	//  FORMAT HELPERS
	// ═══════════════════════════════════════════════════════════════

	static string TierRoman(int t) => t switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "—" };

	static string SlotShort(SkillSlotType s) => s switch {
		SkillSlotType.Active => "Active", SkillSlotType.Passive => "Passive",
		SkillSlotType.Auto => "Auto", _ => "?" };

	static string CastTypeShort(SpellCastType t) => t switch {
		SpellCastType.Missile => "Missile", SpellCastType.Indirect => "Indirect",
		SpellCastType.Healing => "Heal", SpellCastType.Transfer => "Drain",
		SpellCastType.Status => "Status", SpellCastType.Utility => "Utility", _ => "?" };

	static string CostStr(SkillDefinition sk)
	{
		if (sk.StaminaCost > 0 && sk.AetherCost > 0) return $"{sk.StaminaCost}/{sk.AetherCost}";
		if (sk.StaminaCost > 0) return $"{sk.StaminaCost} STA";
		if (sk.AetherCost > 0) return $"{sk.AetherCost} AE";
		return "—";
	}

	static Color ResourceColor(ResourceType r) => r switch {
		ResourceType.Stamina => ColStamina, ResourceType.Aether => ColAether,
		ResourceType.Both => ColBoth, _ => TxDim };

	static string WeaponLabel(WeaponReq w) => w switch {
		WeaponReq.AnyMelee => "Any Melee", WeaponReq.AnyRanged => "Any Ranged",
		WeaponReq.Any => "Any Weapon", WeaponReq.AnyPlusShield => "Any + Shield",
		WeaponReq.AnyOnehanded => "One-Handed", _ => w.ToString() };

	static string TreeLabel(SkillTree t) => t switch {
		SkillTree.Vanguard => "⚔ Vanguard", SkillTree.Marksman => "🏹 Marksman",
		SkillTree.Evoker => "✦ Evoker", SkillTree.Mender => "✚ Mender",
		SkillTree.Runeblade => "◈ Runeblade", SkillTree.Bulwark => "🛡 Bulwark",
		SkillTree.Shadowstep => "🗡 Shadowstep", SkillTree.Dreadnought => "💀 Dreadnought",
		SkillTree.Warsinger => "🎵 Warsinger", SkillTree.Templar => "✦ Templar",
		SkillTree.Hexer => "🔮 Hexer", SkillTree.Tactician => "⚙ Tactician", _ => t.ToString() };
}
