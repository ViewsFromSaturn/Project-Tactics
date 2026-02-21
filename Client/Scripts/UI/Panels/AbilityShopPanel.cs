using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTactics.Combat;
using ProjectTactics.Core;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Ability Compendium — browse, filter, search, inspect, and purchase
/// all 180 skill-tree abilities + 105 spells.
/// Layout: Sidebar (category + filters) | Center (search + list) | Right (detail).
/// Prereq: 3×T1→unlock T2, 2×T2→unlock T3, 1×T3→unlock T4 (per tree/element).
/// </summary>
public partial class AbilityShopPanel : WindowPanel
{
	// ═══════════════════════════════════════════════════
	//  THEME COLORS
	// ═══════════════════════════════════════════════════

	static readonly Color BgSelected  = new(0.545f, 0.361f, 0.965f, 0.15f);
	static readonly Color BgHover     = new(0.235f, 0.255f, 0.314f, 0.20f);
	static readonly Color BgCard      = new(0.235f, 0.255f, 0.314f, 0.10f);
	static readonly Color BdSubtle    = new(0.235f, 0.255f, 0.314f, 0.35f);
	static readonly Color BdAccent    = new("8B5CF6");
	static readonly Color Tx          = new("D4D2CC");
	static readonly Color TxBright    = new("EEEEE8");
	static readonly Color TxSec       = new("9090A0");
	static readonly Color TxDim       = new("64647A");
	static readonly Color TxOff       = new("44445A");
	static readonly Color Gold        = new("D4A843");
	static readonly Color Sta         = new("CC8833");
	static readonly Color Ae          = new("5588DD");
	static readonly Color Both        = new("AA66BB");
	static readonly Color Green       = new("5CB85C");
	static readonly Color Red         = new("CC4444");

	static readonly Dictionary<Element, Color> EC = new() {
		{Element.None, TxDim}, {Element.Fire, new("CC4422")}, {Element.Ice, new("44AADD")},
		{Element.Lightning, new("DDAA22")}, {Element.Earth, new("887744")},
		{Element.Wind, new("55BB55")}, {Element.Water, new("3366AA")},
		{Element.Light, new("DDCC66")}, {Element.Dark, new("8844AA")}
	};

	static readonly Dictionary<Element, string> EI = new() {
		{Element.None, "◇"}, {Element.Fire, "🔥"}, {Element.Ice, "❄"},
		{Element.Lightning, "⚡"}, {Element.Earth, "🌍"}, {Element.Wind, "🌬"},
		{Element.Water, "🌊"}, {Element.Light, "✦"}, {Element.Dark, "🔮"}
	};

	// ═══════════════════════════════════════════════════
	//  STATE
	// ═══════════════════════════════════════════════════

	enum Mode { Skills, Spells }
	Mode _mode = Mode.Skills;

	SkillTree _tree = SkillTree.Vanguard;
	Element _element = Element.Fire;
	int _tierFilter;                // 0 = all
	SkillSlotType? _slotFilter;     // null = all
	string _search = "";

	SkillDefinition _selSkill;
	SpellDefinition _selSpell;

	CharacterLoadout Lo => GameManager.Instance?.ActiveLoadout;
	int Rpp => Lo?.Rpp ?? 0;
	HashSet<string> OwnSk => Lo?.LearnedSkillIds ?? new();
	HashSet<string> OwnSp => Lo?.LearnedSpellIds ?? new();

	// UI refs
	VBoxContainer _sidebar, _list, _detail;
	Label _rppVal;
	LineEdit _searchBox;

	public AbilityShopPanel()
	{
		PanelTitle = "◆ ABILITY COMPENDIUM";
		DefaultWidth = 940;
		DefaultHeight = 600;
		ManagesOwnScroll = true;
	}

	// ═══════════════════════════════════════════════════
	//  BUILD — anchored layout, no overflow
	// ═══════════════════════════════════════════════════

	protected override void BuildContent(VBoxContainer root)
	{
		root.AddThemeConstantOverride("separation", 0);

		// ── Top bar: tabs + RPP ──
		var top = H(root, 0); top.CustomMinimumSize = new Vector2(0, 34);
		MakeTab(top, "⚔ SKILL TREES", Mode.Skills);
		MakeTab(top, "✦ SPELLS", Mode.Spells);
		var sp = new Control(); sp.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(sp);
		var rppH = H(null, 6);
		L(rppH, "◈ RPP:", Gold, 10);
		_rppVal = L(rppH, Rpp.ToString(), TxBright, 11, true);
		var rppCard = Card(); rppCard.AddChild(rppH); top.AddChild(rppCard);

		Sep(root);

		// ── Body: sidebar | center | detail ──
		var body = H(root, 0);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;

		// Sidebar (fixed 160px)
		var sbScroll = Scroll(160);
		body.AddChild(sbScroll);
		_sidebar = V(null, 2); _sidebar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		sbScroll.AddChild(_sidebar);

		VSep(body);

		// Center (flex)
		var centerV = new VBoxContainer();
		centerV.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		centerV.SizeFlagsVertical = SizeFlags.ExpandFill;
		centerV.AddThemeConstantOverride("separation", 0);
		body.AddChild(centerV);

		// Search bar
		var searchRow = H(centerV, 4);
		searchRow.CustomMinimumSize = new Vector2(0, 28);
		var searchMargin = new MarginContainer();
		searchMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		searchMargin.AddThemeConstantOverride("margin_left", 6);
		searchMargin.AddThemeConstantOverride("margin_right", 6);
		searchMargin.AddThemeConstantOverride("margin_top", 2);
		searchMargin.AddThemeConstantOverride("margin_bottom", 2);
		searchRow.AddChild(searchMargin);

		_searchBox = new LineEdit();
		_searchBox.PlaceholderText = "🔍 Search abilities...";
		_searchBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_searchBox.AddThemeFontSizeOverride("font_size", 10);
		_searchBox.AddThemeColorOverride("font_color", Tx);
		_searchBox.AddThemeColorOverride("font_placeholder_color", TxDim);
		var searchStyle = new StyleBoxFlat();
		searchStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.6f);
		searchStyle.SetContentMarginAll(4); searchStyle.ContentMarginLeft = 8;
		searchStyle.SetCornerRadiusAll(4);
		searchStyle.BorderColor = BdSubtle; searchStyle.SetBorderWidthAll(1);
		_searchBox.AddThemeStyleboxOverride("normal", searchStyle);
		_searchBox.TextChanged += s => { _search = s; RebuildList(); };
		searchMargin.AddChild(_searchBox);

		Sep(centerV);

		var listScroll = new ScrollContainer();
		listScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		listScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		centerV.AddChild(listScroll);

		_list = V(null, 1); _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		listScroll.AddChild(_list);

		VSep(body);

		// Detail (fixed 240px)
		var detScroll = Scroll(240);
		body.AddChild(detScroll);
		var detMargin = new MarginContainer();
		detMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detMargin.AddThemeConstantOverride("margin_left", 10);
		detMargin.AddThemeConstantOverride("margin_right", 10);
		detMargin.AddThemeConstantOverride("margin_top", 8);
		detScroll.AddChild(detMargin);
		_detail = V(null, 6); _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detMargin.AddChild(_detail);

		RebuildSidebar(); RebuildList(); ShowEmpty();
	}

	// ═══════════════════════════════════════════════════
	//  TABS
	// ═══════════════════════════════════════════════════

	void MakeTab(HBoxContainer parent, string text, Mode mode)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(140, 34);
		btn.AddThemeFontSizeOverride("font_size", 11);

		bool active = mode == _mode;
		var s = new StyleBoxFlat();
		s.BgColor = active ? BgSelected : Colors.Transparent;
		s.SetContentMarginAll(6);
		s.BorderWidthBottom = active ? 2 : 0;
		s.BorderColor = BdAccent;
		btn.AddThemeStyleboxOverride("normal", s);
		btn.AddThemeStyleboxOverride("pressed", s);
		var hv = s.Duplicate() as StyleBoxFlat; hv.BgColor = BgHover;
		btn.AddThemeStyleboxOverride("hover", hv);
		btn.AddThemeColorOverride("font_color", active ? TxBright : TxSec);
		btn.AddThemeColorOverride("font_hover_color", TxBright);

		btn.Pressed += () => { _mode = mode; _selSkill = null; _selSpell = null;
			_tierFilter = 0; _slotFilter = null; _search = ""; _searchBox.Text = "";
			CallDeferred(nameof(FullRebuild)); };
		parent.AddChild(btn);
	}

	void FullRebuild() { RebuildTabs(); RebuildSidebar(); RebuildList(); ShowEmpty(); }

	void RebuildTabs()
	{
		var root = GetContentRoot();
		if (root == null) return;
		var oldTop = root.GetChild(0) as Control;
		if (oldTop == null) return;

		var top = H(null, 0); top.CustomMinimumSize = new Vector2(0, 34);
		MakeTab(top, "⚔ SKILL TREES", Mode.Skills);
		MakeTab(top, "✦ SPELLS", Mode.Spells);
		var sp = new Control(); sp.SizeFlagsHorizontal = SizeFlags.ExpandFill; top.AddChild(sp);
		var rppH = H(null, 6);
		L(rppH, "◈ RPP:", Gold, 10);
		_rppVal = L(rppH, Rpp.ToString(), TxBright, 11, true);
		var rppCard = Card(); rppCard.AddChild(rppH); top.AddChild(rppCard);

		root.AddChild(top);
		root.MoveChild(top, 0);
		oldTop.QueueFree();
	}

	/// <summary>Walk up from sidebar to find the VBoxContainer root.</summary>
	VBoxContainer GetContentRoot()
	{
		// _sidebar → sbScroll → body(HBox) → root(VBox)
		var sbScroll = _sidebar?.GetParent();
		var body = sbScroll?.GetParent();
		return body?.GetParent() as VBoxContainer;
	}

	// ═══════════════════════════════════════════════════
	//  SIDEBAR
	// ═══════════════════════════════════════════════════

	void RebuildSidebar()
	{
		Clear(_sidebar);

		if (_mode == Mode.Skills)
		{
			SideLabel("  SKILL TREES");
			Sep(_sidebar);
			foreach (SkillTree t in Enum.GetValues<SkillTree>())
			{
				var bt = SideBtn(TreeLbl(t), t == _tree);
				var tt = t;
				bt.Pressed += () => { _tree = tt; _slotFilter = null; _tierFilter = 0;
					RebuildSidebar(); RebuildList(); };
				_sidebar.AddChild(bt);
			}
			Sep(_sidebar);
			SideLabel("  FILTER BY TYPE");
			AddSlotBtn("All Types", null);
			AddSlotBtn("⚔ Active", SkillSlotType.Active);
			AddSlotBtn("◈ Passive", SkillSlotType.Passive);
			AddSlotBtn("⟐ Auto", SkillSlotType.Auto);
			Sep(_sidebar);
			SideLabel("  FILTER BY TIER");
			AddTierBtn("All Tiers", 0);
			AddTierBtn("Tier I", 1);
			AddTierBtn("Tier II", 2);
			AddTierBtn("Tier III", 3);
			AddTierBtn("Tier IV", 4);
		}
		else
		{
			SideLabel("  ELEMENTS");
			Sep(_sidebar);
			foreach (Element el in new[] { Element.Fire, Element.Ice, Element.Lightning,
				Element.Earth, Element.Wind, Element.Water, Element.Light, Element.Dark })
			{
				string icon = EI.GetValueOrDefault(el, "◇");
				var bt = SideBtn($"{icon} {el}", el == _element);
				bt.AddThemeColorOverride("font_color", el == _element ? TxBright : EC.GetValueOrDefault(el, Tx));
				var e = el;
				bt.Pressed += () => { _element = e; _tierFilter = 0;
					RebuildSidebar(); RebuildList(); };
				_sidebar.AddChild(bt);
			}
			Sep(_sidebar);
			SideLabel("  FILTER BY TIER");
			string stat = GetElStat(_element);
			AddTierBtn("All Tiers", 0);
			for (int t = 1; t <= 4; t++)
			{
				int req = SpellDatabase.GetStatReq(t);
				AddTierBtn($"Tier {TR(t)}  ({stat} {req}+)", t);
			}
		}
	}

	void AddSlotBtn(string text, SkillSlotType? slot)
	{
		var bt = SideBtn(text, _slotFilter == slot);
		bt.Pressed += () => { _slotFilter = slot; RebuildSidebar(); RebuildList(); };
		_sidebar.AddChild(bt);
	}

	void AddTierBtn(string text, int tier)
	{
		var bt = SideBtn(text, _tierFilter == tier);
		bt.Pressed += () => { _tierFilter = tier; RebuildSidebar(); RebuildList(); };
		_sidebar.AddChild(bt);
	}

	// ═══════════════════════════════════════════════════
	//  LIST — header + rows
	// ═══════════════════════════════════════════════════

	void RebuildList()
	{
		Clear(_list);

		if (_mode == Mode.Skills)
		{
			var skills = SkillDatabase.GetTree(_tree).AsEnumerable();
			if (_slotFilter.HasValue) skills = skills.Where(s => s.Slot == _slotFilter.Value);
			if (_tierFilter > 0) skills = skills.Where(s => s.Tier == _tierFilter);
			if (!string.IsNullOrEmpty(_search))
				skills = skills.Where(s => s.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));

			var hdr = HRow();
			HCell(hdr, "Name", 180); HCell(hdr, "Type", 55); HCell(hdr, "Tier", 35);
			HCell(hdr, "Cost", 60); HCell(hdr, "Range", 40); HCell(hdr, "RT", 30);
			HCell(hdr, "Pwr", 40); HCell(hdr, "Status", 55);
			_list.AddChild(hdr);

			foreach (var sk in skills) _list.AddChild(SkillRow(sk));
		}
		else
		{
			var spells = SpellDatabase.GetElement(_element).AsEnumerable();
			if (_tierFilter > 0) spells = spells.Where(s => s.Tier == _tierFilter);
			if (!string.IsNullOrEmpty(_search))
				spells = spells.Where(s => s.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));

			var hdr = HRow();
			HCell(hdr, "Name", 140); HCell(hdr, "Tier", 35); HCell(hdr, "AE", 35);
			HCell(hdr, "Range", 45); HCell(hdr, "Area", 40); HCell(hdr, "RT", 30);
			HCell(hdr, "Pwr", 40); HCell(hdr, "RPP", 35); HCell(hdr, "Status", 55);
			_list.AddChild(hdr);

			foreach (var sp in spells) _list.AddChild(SpellRow(sp));
		}
	}

	// ─── Skill row ──────────────────────────────────

	Control SkillRow(SkillDefinition sk)
	{
		bool owned = OwnSk.Contains(sk.Id);
		bool unlocked = SkillTierUnlocked(sk);
		bool locked = !owned && !unlocked;
		bool sel = _selSkill?.Id == sk.Id;

		var row = Row(sel);
		row.GuiInput += e => { if (Click(e)) { _selSkill = sk; _selSpell = null; RebuildList(); ShowSkill(sk); } };

		var h = H(null, 0); h.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(h);

		string ico = sk.Slot == SkillSlotType.Active ? "⚔" : sk.Slot == SkillSlotType.Passive ? "◈" : "⟐";
		Color sc = sk.Slot == SkillSlotType.Active ? Sta : sk.Slot == SkillSlotType.Passive ? Ae : Both;

		string nm = owned ? $"✓ {sk.Name}" : locked ? $"🔒 {sk.Name}" : sk.Name;
		Color nc = owned ? Green : locked ? TxOff : Tx;

		F(h, nm, nc, 10, 180);
		F(h, $"{ico} {SlotS(sk.Slot)}", locked ? TxOff : sc, 9, 55);
		F(h, TR(sk.Tier), locked ? TxOff : Gold, 9, 35);
		F(h, CostS(sk), locked ? TxOff : ResC(sk.Resource), 9, 60);
		F(h, sk.Range > 0 ? sk.Range.ToString() : "—", TxDim, 9, 40);
		F(h, sk.RtCost > 0 ? $"+{sk.RtCost}" : "—", TxDim, 9, 30);
		F(h, sk.Power > 0 ? sk.Power.ToString() : "—", TxSec, 9, 40);

		string st = owned ? "OWNED" : locked ? "LOCKED" : "—";
		Color stc = owned ? Green : locked ? Red : TxOff;
		F(h, st, stc, 8, 55);

		return row;
	}

	// ─── Spell row ──────────────────────────────────

	Control SpellRow(SpellDefinition sp)
	{
		bool owned = OwnSp.Contains(sp.Id);
		bool unlocked = SpellTierUnlocked(sp);
		bool locked = !owned && !unlocked;
		bool sel = _selSpell?.Id == sp.Id;

		var row = Row(sel);
		row.GuiInput += e => { if (Click(e)) { _selSpell = sp; _selSkill = null; RebuildList(); ShowSpell(sp); } };

		var h = H(null, 0); h.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(h);

		string nm = owned ? $"✓ {sp.Name}" : locked ? $"🔒 {sp.Name}" : sp.Name;
		Color nc = owned ? Green : locked ? TxOff : EC.GetValueOrDefault(sp.Element, Tx);

		F(h, nm, nc, 10, 140);
		F(h, TR(sp.Tier), locked ? TxOff : Gold, 9, 35);
		F(h, sp.AetherCost.ToString(), locked ? TxOff : Ae, 9, 35);
		string rng = sp.RangeMin == sp.RangeMax ? sp.RangeMax.ToString() : $"{sp.RangeMin}-{sp.RangeMax}";
		F(h, rng, TxDim, 9, 45);
		F(h, sp.AreaSize > 0 ? $"D({sp.AreaSize})" : "1", TxDim, 9, 40);
		F(h, $"+{sp.RtCost}", TxDim, 9, 30);
		F(h, sp.Power > 0 ? sp.Power.ToString() : "—", TxSec, 9, 40);
		F(h, sp.RppCost.ToString(), locked ? TxOff : Gold, 9, 35);

		string st = owned ? "OWNED" : locked ? "LOCKED" : "—";
		Color stc = owned ? Green : locked ? Red : TxOff;
		F(h, st, stc, 8, 55);

		return row;
	}

	// ═══════════════════════════════════════════════════
	//  DETAIL PANEL
	// ═══════════════════════════════════════════════════

	void ShowEmpty()
	{
		Clear(_detail);
		L(_detail, "Select an ability", TxSec, 12);
		L(_detail, "to view details", TxDim, 10);
	}

	void ShowSkill(SkillDefinition sk)
	{
		Clear(_detail);
		bool owned = OwnSk.Contains(sk.Id);
		bool unlocked = SkillTierUnlocked(sk);

		L(_detail, sk.Name, TxBright, 14, true);

		var tags = H(_detail, 4);
		Tag(tags, SlotS(sk.Slot), sk.Slot == SkillSlotType.Active ? Sta : sk.Slot == SkillSlotType.Passive ? Ae : Both);
		Tag(tags, $"Tier {TR(sk.Tier)}", Gold);
		Tag(tags, sk.Tree.ToString(), BdAccent);
		if (sk.Element != Element.None) Tag(tags, sk.Element.ToString(), EC.GetValueOrDefault(sk.Element, TxDim));

		Sep(_detail);

		var desc = new Label();
		desc.Text = sk.Description;
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.AddThemeColorOverride("font_color", Tx);
		desc.AddThemeFontSizeOverride("font_size", 10);
		_detail.AddChild(desc);

		if (!string.IsNullOrEmpty(sk.ScalingNote))
		{
			L(_detail, $"Ranks: {sk.ScalingNote}", Gold, 9);
			L(_detail, $"Max Rank: {sk.MaxRank}", TxDim, 9);
		}

		Sep(_detail);
		L(_detail, "◆ COMBAT DATA", TxSec, 9, true);
		var g = Grid();
		if (sk.StaminaCost > 0) GRow(g, "Stamina", sk.StaminaCost.ToString(), Sta);
		if (sk.AetherCost > 0) GRow(g, "Aether", sk.AetherCost.ToString(), Ae);
		if (sk.RtCost > 0) GRow(g, "RT Cost", $"+{sk.RtCost}", Tx);
		if (sk.Power > 0) GRow(g, "Power", sk.Power.ToString(), TxBright);
		if (sk.Range > 0) GRow(g, "Range", sk.Range.ToString(), Tx);
		GRow(g, "Target", sk.Target.ToString(), Tx);
		if (sk.AreaSize > 0) GRow(g, "Area", $"Diamond({sk.AreaSize})", Tx);
		if (sk.Weapon != WeaponReq.None) GRow(g, "Weapon", WpnS(sk.Weapon), TxSec);
		_detail.AddChild(g);

		Sep(_detail);
		BuySection_Skill(sk, owned, unlocked);
	}

	void ShowSpell(SpellDefinition sp)
	{
		Clear(_detail);
		bool owned = OwnSp.Contains(sp.Id);
		bool unlocked = SpellTierUnlocked(sp);

		string icon = EI.GetValueOrDefault(sp.Element, "◇");
		L(_detail, $"{icon}  {sp.Name}", EC.GetValueOrDefault(sp.Element, TxBright), 14, true);

		var tags = H(_detail, 4);
		Tag(tags, $"Tier {TR(sp.Tier)}", Gold);
		Tag(tags, sp.Element.ToString(), EC.GetValueOrDefault(sp.Element, TxDim));
		Tag(tags, CastS(sp.CastType), TxSec);

		Sep(_detail);

		var desc = new Label();
		desc.Text = sp.Description;
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.AddThemeColorOverride("font_color", Tx);
		desc.AddThemeFontSizeOverride("font_size", 10);
		_detail.AddChild(desc);

		if (!string.IsNullOrEmpty(sp.StatusEffect))
			L(_detail, $"Status: {sp.StatusEffect}", Red, 9);

		Sep(_detail);
		L(_detail, "◆ SPELL DATA", TxSec, 9, true);
		var g = Grid();
		GRow(g, "Aether Cost", sp.AetherCost.ToString(), Ae);
		GRow(g, "RT Cost", $"+{sp.RtCost}", Tx);
		if (sp.Power > 0) GRow(g, "Power", sp.Power.ToString(), TxBright);
		string rng = sp.RangeMin == sp.RangeMax ? sp.RangeMax.ToString() : $"{sp.RangeMin}-{sp.RangeMax}";
		GRow(g, "Range", rng, Tx);
		GRow(g, "Target", sp.Target.ToString(), Tx);
		if (sp.AreaSize > 0) GRow(g, "Area", $"Diamond({sp.AreaSize})", Tx);
		GRow(g, "Cast Type", CastS(sp.CastType), TxSec);
		if (!string.IsNullOrEmpty(sp.StatReq))
			GRow(g, "Requires", $"{StatFull(sp.StatReq)} {sp.StatReqValue}+", Gold);
		_detail.AddChild(g);

		Sep(_detail);
		BuySection_Spell(sp, owned, unlocked);
	}

	// ─── Buy / Lock UI ──────────────────────────────

	void BuySection_Skill(SkillDefinition sk, bool owned, bool unlocked)
	{
		if (owned)
		{
			var p = Card(); L(p, "✓  LEARNED", Green, 11, true); _detail.AddChild(p);
			return;
		}
		if (!unlocked)
		{
			L(_detail, "🔒  LOCKED", Red, 11, true);
			int need = TierGateReq(sk.Tier);
			int have = CountOwnedInTreeTier(sk.Tree, sk.Tier - 1);
			L(_detail, $"Requires {need}× Tier {TR(sk.Tier - 1)} skills (have {have})", TxSec, 9);
			return;
		}
		int cost = sk.Tier * 5;
		bool afford = Rpp >= cost;
		var cr = H(_detail, 6); L(cr, "Cost:", TxSec, 10); L(cr, $"{cost} RPP", afford ? Gold : Red, 10, true);
		BuyBtn(afford ? $"LEARN  ({cost} RPP)" : "INSUFFICIENT RPP", afford,
			() => { if (Lo != null && Lo.LearnSkill(sk.Id, cost)) { _rppVal.Text = Lo.Rpp.ToString(); RebuildList(); ShowSkill(sk); } });
	}

	void BuySection_Spell(SpellDefinition sp, bool owned, bool unlocked)
	{
		if (owned)
		{
			var p = Card(); L(p, "✓  LEARNED", Green, 11, true); _detail.AddChild(p);
			return;
		}
		if (!unlocked)
		{
			L(_detail, "🔒  LOCKED", Red, 11, true);
			int need = TierGateReq(sp.Tier);
			int have = CountOwnedInElTier(sp.Element, sp.Tier - 1);
			L(_detail, $"Requires {need}× Tier {TR(sp.Tier - 1)} spells (have {have})", TxSec, 9);
			return;
		}
		int cost = sp.RppCost;
		bool afford = Rpp >= cost;
		var cr = H(_detail, 6); L(cr, "Cost:", TxSec, 10); L(cr, $"{cost} RPP", afford ? Gold : Red, 10, true);
		BuyBtn(afford ? $"LEARN  ({cost} RPP)" : "INSUFFICIENT RPP", afford,
			() => { if (Lo != null && Lo.LearnSpell(sp.Id, cost)) { _rppVal.Text = Lo.Rpp.ToString(); RebuildList(); ShowSpell(sp); } });
	}

	void BuyBtn(string text, bool enabled, Action onPress)
	{
		var btn = new Button();
		btn.Text = text;
		btn.Disabled = !enabled;
		btn.CustomMinimumSize = new Vector2(0, 32);
		btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		btn.AddThemeFontSizeOverride("font_size", 10);

		var s = new StyleBoxFlat();
		s.BgColor = enabled ? new Color(BdAccent, 0.3f) : new Color(TxOff, 0.1f);
		s.SetContentMarginAll(6); s.SetCornerRadiusAll(4);
		s.BorderWidthBottom = 2; s.BorderColor = enabled ? BdAccent : TxOff;
		btn.AddThemeStyleboxOverride("normal", s);
		btn.AddThemeColorOverride("font_color", enabled ? TxBright : TxOff);

		var hv = s.Duplicate() as StyleBoxFlat; hv.BgColor = enabled ? new Color(BdAccent, 0.5f) : s.BgColor;
		btn.AddThemeStyleboxOverride("hover", hv);

		if (enabled) btn.Pressed += onPress;
		_detail.AddChild(btn);
	}

	// ═══════════════════════════════════════════════════
	//  PREREQUISITE SYSTEM
	//  T1 = free, T2 = need 3×T1, T3 = need 2×T2, T4 = need 1×T3
	// ═══════════════════════════════════════════════════

	static int TierGateReq(int tier) => tier switch { 2 => 3, 3 => 2, 4 => 1, _ => 0 };

	int CountOwnedInTreeTier(SkillTree tree, int tier)
		=> SkillDatabase.GetTree(tree).Count(s => s.Tier == tier && OwnSk.Contains(s.Id));

	int CountOwnedInElTier(Element el, int tier)
		=> SpellDatabase.GetElement(el).Count(s => s.Tier == tier && OwnSp.Contains(s.Id));

	bool SkillTierUnlocked(SkillDefinition sk)
	{
		if (sk.Tier <= 1) return true;
		return CountOwnedInTreeTier(sk.Tree, sk.Tier - 1) >= TierGateReq(sk.Tier);
	}

	bool SpellTierUnlocked(SpellDefinition sp)
	{
		if (sp.Tier <= 1) return true;
		return CountOwnedInElTier(sp.Element, sp.Tier - 1) >= TierGateReq(sp.Tier);
	}

	// ═══════════════════════════════════════════════════
	//  UI PRIMITIVES
	// ═══════════════════════════════════════════════════

	Label L(Control p, string t, Color c, int sz, bool bold = false)
	{
		var l = new Label(); l.Text = t;
		l.AddThemeColorOverride("font_color", c);
		l.AddThemeFontSizeOverride("font_size", sz);
		if (bold) l.LabelSettings = new LabelSettings { FontColor = c, FontSize = sz };
		p?.AddChild(l); return l;
	}

	void F(Control p, string t, Color c, int sz, float w)
	{
		var l = new Label(); l.Text = t;
		l.AddThemeColorOverride("font_color", c);
		l.AddThemeFontSizeOverride("font_size", sz);
		l.CustomMinimumSize = new Vector2(w, 0);
		l.ClipText = true; l.MouseFilter = MouseFilterEnum.Ignore;
		p.AddChild(l);
	}

	HBoxContainer H(Control p, int sep)
	{
		var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", sep);
		p?.AddChild(h); return h;
	}

	VBoxContainer V(Control p, int sep)
	{
		var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", sep);
		p?.AddChild(v); return v;
	}

	ScrollContainer Scroll(float minW)
	{
		var sc = new ScrollContainer();
		sc.CustomMinimumSize = new Vector2(minW, 0);
		sc.SizeFlagsVertical = SizeFlags.ExpandFill;
		sc.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		return sc;
	}

	PanelContainer Row(bool sel)
	{
		var r = new PanelContainer();
		var s = new StyleBoxFlat();
		s.BgColor = sel ? BgSelected : Colors.Transparent;
		s.SetContentMarginAll(3); s.ContentMarginLeft = 6;
		r.AddThemeStyleboxOverride("panel", s);
		r.CustomMinimumSize = new Vector2(0, 24);
		r.MouseFilter = MouseFilterEnum.Stop;
		return r;
	}

	HBoxContainer HRow()
	{
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 0);
		h.CustomMinimumSize = new Vector2(0, 22);
		return h;
	}

	void HCell(HBoxContainer h, string t, float w)
	{
		var l = new Label(); l.Text = t;
		l.CustomMinimumSize = new Vector2(w, 0); l.ClipText = true;
		l.AddThemeColorOverride("font_color", TxDim);
		l.AddThemeFontSizeOverride("font_size", 8);
		h.AddChild(l);
	}

	PanelContainer Card()
	{
		var p = new PanelContainer();
		var s = new StyleBoxFlat();
		s.BgColor = BgCard; s.SetContentMarginAll(6); s.SetCornerRadiusAll(4);
		p.AddThemeStyleboxOverride("panel", s); return p;
	}

	void Tag(HBoxContainer p, string t, Color c)
	{
		var pc = new PanelContainer();
		var s = new StyleBoxFlat();
		s.BgColor = new Color(c, 0.15f);
		s.SetContentMarginAll(1); s.ContentMarginLeft = 5; s.ContentMarginRight = 5;
		s.SetCornerRadiusAll(3); s.BorderWidthLeft = 1; s.BorderColor = new Color(c, 0.4f);
		pc.AddThemeStyleboxOverride("panel", s);
		var l = new Label(); l.Text = t;
		l.AddThemeColorOverride("font_color", c);
		l.AddThemeFontSizeOverride("font_size", 8);
		pc.AddChild(l); p.AddChild(pc);
	}

	GridContainer Grid()
	{
		var g = new GridContainer(); g.Columns = 2;
		g.AddThemeConstantOverride("h_separation", 12);
		g.AddThemeConstantOverride("v_separation", 1);
		return g;
	}

	void GRow(GridContainer g, string lbl, string val, Color vc)
	{
		var a = new Label(); a.Text = lbl;
		a.AddThemeColorOverride("font_color", TxDim);
		a.AddThemeFontSizeOverride("font_size", 9); g.AddChild(a);
		var b = new Label(); b.Text = val;
		b.AddThemeColorOverride("font_color", vc);
		b.AddThemeFontSizeOverride("font_size", 9); g.AddChild(b);
	}

	Button SideBtn(string t, bool active)
	{
		var b = new Button(); b.Text = t;
		b.Alignment = HorizontalAlignment.Left;
		b.CustomMinimumSize = new Vector2(0, 26);
		b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		b.AddThemeFontSizeOverride("font_size", 10);
		var s = new StyleBoxFlat();
		s.BgColor = active ? BgSelected : Colors.Transparent;
		s.SetContentMarginAll(3); s.ContentMarginLeft = 8; s.SetCornerRadiusAll(3);
		if (active) { s.BorderWidthLeft = 2; s.BorderColor = BdAccent; }
		b.AddThemeStyleboxOverride("normal", s);
		b.AddThemeColorOverride("font_color", active ? TxBright : TxSec);
		var hv = s.Duplicate() as StyleBoxFlat; hv.BgColor = BgHover;
		b.AddThemeStyleboxOverride("hover", hv);
		b.AddThemeColorOverride("font_hover_color", TxBright);
		return b;
	}

	void SideLabel(string t) => L(_sidebar, t, Gold, 10, true);

	void Sep(Control p)
	{
		var s = new HSeparator();
		s.AddThemeColorOverride("separator", BdSubtle);
		s.AddThemeConstantOverride("separation", 4);
		p.AddChild(s);
	}

	void VSep(Control p)
	{
		var s = new VSeparator();
		s.AddThemeColorOverride("separator", BdSubtle);
		s.AddThemeConstantOverride("separation", 0);
		s.CustomMinimumSize = new Vector2(1, 0);
		p.AddChild(s);
	}

	void Clear(Control c) { foreach (var ch in c.GetChildren()) if (ch is Node n) n.QueueFree(); }
	bool Click(InputEvent e) => e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;

	// ═══════════════════════════════════════════════════
	//  FORMAT HELPERS
	// ═══════════════════════════════════════════════════

	static string TR(int t) => t switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "—" };

	static string SlotS(SkillSlotType s) => s switch {
		SkillSlotType.Active => "Active", SkillSlotType.Passive => "Passive",
		SkillSlotType.Auto => "Auto", _ => "?" };

	static string CastS(SpellCastType t) => t switch {
		SpellCastType.Missile => "Missile", SpellCastType.Indirect => "Indirect",
		SpellCastType.Healing => "Heal", SpellCastType.Transfer => "Drain",
		SpellCastType.Status => "Status", SpellCastType.Utility => "Utility", _ => "?" };

	static string CostS(SkillDefinition sk)
	{
		if (sk.StaminaCost > 0 && sk.AetherCost > 0) return $"{sk.StaminaCost}/{sk.AetherCost}";
		if (sk.StaminaCost > 0) return $"{sk.StaminaCost} STA";
		if (sk.AetherCost > 0) return $"{sk.AetherCost} AE";
		return "—";
	}

	static Color ResC(ResourceType r) => r switch {
		ResourceType.Stamina => Sta, ResourceType.Aether => Ae, ResourceType.Both => Both, _ => TxDim };

	static string WpnS(WeaponReq w) => w switch {
		WeaponReq.AnyMelee => "Any Melee", WeaponReq.AnyRanged => "Any Ranged",
		WeaponReq.Any => "Any Weapon", _ => w.ToString() };

	static string TreeLbl(SkillTree t) => t switch {
		SkillTree.Vanguard => "⚔ Vanguard", SkillTree.Marksman => "🏹 Marksman",
		SkillTree.Evoker => "✦ Evoker", SkillTree.Mender => "✚ Mender",
		SkillTree.Runeblade => "◈ Runeblade", SkillTree.Bulwark => "🛡 Bulwark",
		SkillTree.Shadowstep => "🗡 Shadowstep", SkillTree.Dreadnought => "💀 Dreadnought",
		SkillTree.Warsinger => "🎵 Warsinger", SkillTree.Templar => "✦ Templar",
		SkillTree.Hexer => "🔮 Hexer", SkillTree.Tactician => "⚙ Tactician", _ => t.ToString() };

	static string GetElStat(Element el) => el switch { Element.Light => "MND", Element.Dark => "ETC", _ => "ETC" };

	static string StatFull(string s) => s switch {
		"ETC" => "Ether Control", "MND" => "Mind", "STR" => "Strength",
		"AGI" => "Agility", "VIT" => "Vitality", "DEX" => "Dexterity", _ => s };
}
