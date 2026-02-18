using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Battle HUD — full tactical combat UI overlay.
///
///   • Command menu with keyboard/mouse nav (Move/Attack/Ability/Item/Defend/Flee/Wait)
///   • Ability sub-menu with EP costs + detail tooltip
///   • Item sub-menu with quantities
///   • Active unit info panel (HP/EP bars, stats)
///   • Target unit info panel
///   • Turn order bar (top)
///   • Unit inspect screen (Tab) — full stat sheet like Tactics Ogre
///   • Tile info tooltip
///   • Hint bar (bottom)
///
/// All built in code — no .tscn needed.
/// </summary>
public partial class BattleHUD : CanvasLayer
{
	// ─── SIGNALS ─────────────────────────────────────────
	[Signal] public delegate void CommandSelectedEventHandler(string command);
	[Signal] public delegate void CommandCancelledEventHandler();
	[Signal] public delegate void AbilitySelectedEventHandler(int index);
	[Signal] public delegate void ItemSelectedEventHandler(int index);

	// ─── COLORS (matching UITheme dark mode) ─────────────
	static readonly Color ColBgPanel    = new(0.03f, 0.03f, 0.07f, 0.92f);
	static readonly Color ColBgSub      = new(0.03f, 0.03f, 0.07f, 0.94f);
	static readonly Color ColBorder     = new(0.235f, 0.255f, 0.314f, 0.35f);
	static readonly Color ColBorderDim  = new(0.235f, 0.255f, 0.314f, 0.25f);
	static readonly Color ColViolet     = new("8B5CF6");
	static readonly Color ColVioletDim  = new(139/255f, 92/255f, 246/255f, 0.25f);
	static readonly Color ColVioletGlow = new(139/255f, 92/255f, 246/255f, 0.15f);
	static readonly Color ColTeamA      = new("5599ee");
	static readonly Color ColTeamB      = new("ee5555");
	static readonly Color ColText       = new("D4D2CC");
	static readonly Color ColTextDim    = new("9090A0");
	static readonly Color ColTextDark   = new("707080");
	static readonly Color ColTextDisabled = new("44445a");
	static readonly Color ColHpFull     = new("44cc55");
	static readonly Color ColHpMid      = new("aaaa33");
	static readonly Color ColHpLow      = new("cc3333");
	static readonly Color ColEther      = new("4488dd");
	static readonly Color ColGold       = new("D4A843");

	// ─── COMMAND DEFINITIONS ─────────────────────────────
	static readonly string[] CmdNames = { "Move", "Attack", "Ability", "Item", "Defend", "Flee", "Wait" };
	static readonly string[] CmdIcons = { "→", "⚔", "✦", "◆", "🛡", "↺", "◎" };

	// ─── STATE ───────────────────────────────────────────
	BattleUnit _activeUnit;
	int _cmdIndex;
	bool _menuOpen;
	bool _subMenuOpen;
	int _subIndex;
	string _activeSubType; // "Ability" or "Item"
	bool _inspectOpen;

	// ─── NODE REFS ───────────────────────────────────────
	Control _root;
	PanelContainer _cmdPanel;
	VBoxContainer _cmdList;
	readonly List<Button> _cmdBtns = new();
	PanelContainer _unitInfoPanel;
	PanelContainer _targetInfoPanel;
	HBoxContainer _turnOrderBar;
	PanelContainer _turnOrderBg;
	Label _tileInfoLabel;
	Label _phaseLabel;
	HBoxContainer _hintBar;

	// Sub-menus
	PanelContainer _abilityPanel;
	VBoxContainer _abilityList;
	readonly List<Button> _abilityBtns = new();
	PanelContainer _itemPanel;
	VBoxContainer _itemList;
	readonly List<Button> _itemBtns = new();
	PanelContainer _tooltipPanel;

	// Inspect
	Control _inspectOverlay;
	PanelContainer _inspectPanel;

	// Data
	List<AbilityInfo> _abilities = new();
	List<ItemInfo> _items = new();

	// ═══════════════════════════════════════════════════════
	//  SETUP
	// ═══════════════════════════════════════════════════════

	public override void _Ready()
	{
		Layer = 10;
		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_root);

		BuildPhaseLabel();
		BuildTurnOrderBar();
		BuildUnitInfoPanel();
		BuildTargetInfoPanel();
		BuildCommandMenu();
		BuildAbilitySubMenu();
		BuildItemSubMenu();
		BuildTooltip();
		BuildTileInfo();
		BuildHintBar();
		BuildInspectScreen();

		HideCommandMenu();
	}

	// ═══════════════════════════════════════════════════════
	//  COMMAND MENU
	// ═══════════════════════════════════════════════════════

	void BuildCommandMenu()
	{
		_cmdPanel = MakePanel(ColBgPanel, ColViolet);
		_cmdPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_cmdPanel.Position = new Vector2(-196, -310);
		_cmdPanel.CustomMinimumSize = new Vector2(170, 0);
		_root.AddChild(_cmdPanel);

		_cmdList = new VBoxContainer();
		_cmdList.AddThemeConstantOverride("separation", 3);
		_cmdPanel.AddChild(_cmdList);

		AddLabel(_cmdList, "COMMAND", ColTextDim, 11, HorizontalAlignment.Center);
		AddSep(_cmdList);

		for (int i = 0; i < CmdNames.Length; i++)
		{
			bool hasSub = CmdNames[i] is "Ability" or "Item";
			var btn = MakeCmdButton(CmdNames[i], CmdIcons[i], i, hasSub);
			_cmdList.AddChild(btn);
			_cmdBtns.Add(btn);
		}
	}

	Button MakeCmdButton(string label, string icon, int idx, bool hasSubmenu)
	{
		var btn = new Button();
		string arrow = hasSubmenu ? "  ▸" : "";
		btn.Text = $"  {icon}  {label}{arrow}";
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(158, 32);

		ApplyButtonTheme(btn);

		int ci = idx;
		btn.Pressed += () => OnCmdPressed(ci);
		btn.MouseEntered += () => { if (!btn.Disabled) SelectCmd(ci); };

		return btn;
	}

	// ═══════════════════════════════════════════════════════
	//  ABILITY SUB-MENU
	// ═══════════════════════════════════════════════════════

	void BuildAbilitySubMenu()
	{
		_abilityPanel = MakePanel(ColBgSub, new Color(139/255f, 92/255f, 246/255f, 0.4f));
		_abilityPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_abilityPanel.Position = new Vector2(-446, -360);
		_abilityPanel.CustomMinimumSize = new Vector2(240, 0);
		_abilityPanel.Visible = false;
		_root.AddChild(_abilityPanel);

		_abilityList = new VBoxContainer();
		_abilityList.AddThemeConstantOverride("separation", 2);
		_abilityPanel.AddChild(_abilityList);
	}

	public void SetAbilities(List<AbilityInfo> abilities)
	{
		_abilities = abilities;
		RebuildAbilityList();
	}

	void RebuildAbilityList()
	{
		foreach (var c in _abilityList.GetChildren()) c.QueueFree();
		_abilityBtns.Clear();

		AddLabel(_abilityList, "✦ ABILITIES", ColViolet, 11, HorizontalAlignment.Left);
		AddSep(_abilityList);

		for (int i = 0; i < _abilities.Count; i++)
		{
			var ab = _abilities[i];
			var btn = MakeSubItemButton(ab.Icon, ab.Name, $"{ab.EtherCost} EP", ab.Category, ab.IsUsable);
			int ci = i;
			btn.Pressed += () => OnAbilityClicked(ci);
			btn.MouseEntered += () => { SelectSubItem(ci); UpdateTooltip(ci); };
			_abilityList.AddChild(btn);
			_abilityBtns.Add(btn);
		}
	}

	// ═══════════════════════════════════════════════════════
	//  ITEM SUB-MENU
	// ═══════════════════════════════════════════════════════

	void BuildItemSubMenu()
	{
		_itemPanel = MakePanel(ColBgSub, new Color(139/255f, 92/255f, 246/255f, 0.4f));
		_itemPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_itemPanel.Position = new Vector2(-446, -280);
		_itemPanel.CustomMinimumSize = new Vector2(240, 0);
		_itemPanel.Visible = false;
		_root.AddChild(_itemPanel);

		_itemList = new VBoxContainer();
		_itemList.AddThemeConstantOverride("separation", 2);
		_itemPanel.AddChild(_itemList);
	}

	public void SetItems(List<ItemInfo> items)
	{
		_items = items;
		RebuildItemList();
	}

	void RebuildItemList()
	{
		foreach (var c in _itemList.GetChildren()) c.QueueFree();
		_itemBtns.Clear();

		AddLabel(_itemList, "◆ ITEMS", ColViolet, 11, HorizontalAlignment.Left);
		AddSep(_itemList);

		for (int i = 0; i < _items.Count; i++)
		{
			var it = _items[i];
			var btn = MakeSubItemButton(it.Icon, it.Name, $"x{it.Quantity}", "item", it.IsUsable);
			int ci = i;
			btn.Pressed += () => OnItemClicked(ci);
			btn.MouseEntered += () => SelectSubItem(ci);
			_itemList.AddChild(btn);
			_itemBtns.Add(btn);
		}
	}

	Button MakeSubItemButton(string icon, string name, string cost, string category, bool usable)
	{
		var btn = new Button();
		btn.Text = $"  {icon}  {name}          {cost}";
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(228, 30);
		btn.Disabled = !usable;
		ApplyButtonTheme(btn);
		return btn;
	}

	// ═══════════════════════════════════════════════════════
	//  ABILITY TOOLTIP
	// ═══════════════════════════════════════════════════════

	void BuildTooltip()
	{
		_tooltipPanel = MakePanel(ColBgSub, ColBorder);
		_tooltipPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_tooltipPanel.Position = new Vector2(-700, -310);
		_tooltipPanel.CustomMinimumSize = new Vector2(200, 0);
		_tooltipPanel.Visible = false;
		_root.AddChild(_tooltipPanel);
	}

	void UpdateTooltip(int abilityIndex)
	{
		if (abilityIndex < 0 || abilityIndex >= _abilities.Count) return;
		var ab = _abilities[abilityIndex];

		ClearChildren(_tooltipPanel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		_tooltipPanel.AddChild(vbox);

		AddLabel(vbox, ab.Name, ColText, 14, HorizontalAlignment.Left, true);
		AddLabel(vbox, $"{Capitalize(ab.Category)} · {ab.TargetType} · Range {ab.Range}", ColViolet, 11);

		var desc = new Label();
		desc.Text = ab.Description;
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.CustomMinimumSize = new Vector2(180, 0);
		desc.AddThemeColorOverride("font_color", ColTextDim);
		desc.AddThemeFontSizeOverride("font_size", 11);
		vbox.AddChild(desc);

		AddSep(vbox);
		AddLabel(vbox, $"Power: {ab.Power}", ColTextDark, 11);
		AddLabel(vbox, $"Range: {ab.Range} tiles", ColTextDark, 11);
		AddLabel(vbox, $"RT Cost: {ab.RtCost}", ColTextDark, 11);
		AddLabel(vbox, $"Cost: {ab.EtherCost} EP", ColEther, 11);

		_tooltipPanel.Visible = true;
	}

	// ═══════════════════════════════════════════════════════
	//  UNIT INFO PANEL (bottom-left)
	// ═══════════════════════════════════════════════════════

	void BuildUnitInfoPanel()
	{
		_unitInfoPanel = MakePanel(ColBgPanel, ColBorder);
		_unitInfoPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		_unitInfoPanel.Position = new Vector2(16, -176);
		_unitInfoPanel.CustomMinimumSize = new Vector2(230, 140);
		_unitInfoPanel.Visible = false;
		_root.AddChild(_unitInfoPanel);
	}

	void BuildTargetInfoPanel()
	{
		_targetInfoPanel = MakePanel(ColBgPanel, ColBorder);
		_targetInfoPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		_targetInfoPanel.Position = new Vector2(260, -146);
		_targetInfoPanel.CustomMinimumSize = new Vector2(200, 100);
		_targetInfoPanel.Visible = false;
		_root.AddChild(_targetInfoPanel);
	}

	public void UpdateUnitInfo(BattleUnit unit)
	{
		if (unit == null) { _unitInfoPanel.Visible = false; return; }
		_activeUnit = unit;
		_unitInfoPanel.Visible = true;

		ClearChildren(_unitInfoPanel);
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		_unitInfoPanel.AddChild(vbox);

		AddLabel(vbox, unit.Name, unit.Team == UnitTeam.TeamA ? ColTeamA : ColTeamB, 15, HorizontalAlignment.Left, true);
		AddStatBar(vbox, "HP", unit.CurrentHp, unit.MaxHp, true);
		AddStatBar(vbox, "EP", unit.CurrentEther, unit.MaxEther, false);
		AddLabel(vbox, $"ATK {unit.Atk} · DEF {unit.Def} · AVD {unit.Avd}", ColTextDim, 11);
		AddLabel(vbox, $"MOV {unit.Move} · JMP {unit.Jump} · RT {unit.CurrentRt}", ColTextDim, 11);
	}

	public void UpdateTargetInfo(BattleUnit unit)
	{
		if (unit == null) { _targetInfoPanel.Visible = false; return; }
		_targetInfoPanel.Visible = true;

		ClearChildren(_targetInfoPanel);
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		_targetInfoPanel.AddChild(vbox);

		AddLabel(vbox, unit.Name, unit.Team == UnitTeam.TeamA ? ColTeamA : ColTeamB, 15, HorizontalAlignment.Left, true);
		AddStatBar(vbox, "HP", unit.CurrentHp, unit.MaxHp, true);
		AddLabel(vbox, $"DEF {unit.Def} · EDEF {unit.Edef} · AVD {unit.Avd}", ColTextDim, 11);
	}

	// ═══════════════════════════════════════════════════════
	//  TURN ORDER BAR (top-center)
	// ═══════════════════════════════════════════════════════

	void BuildTurnOrderBar()
	{
		_turnOrderBg = MakePanel(new Color(0.03f, 0.03f, 0.07f, 0.80f), ColBorderDim);
		_turnOrderBg.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_turnOrderBg.OffsetLeft = 100;
		_turnOrderBg.OffsetRight = -100;
		_turnOrderBg.OffsetTop = 8;
		_turnOrderBg.OffsetBottom = 52;
		_root.AddChild(_turnOrderBg);

		_turnOrderBar = new HBoxContainer();
		_turnOrderBar.AddThemeConstantOverride("separation", 4);
		_turnOrderBar.Alignment = BoxContainer.AlignmentMode.Center;
		_turnOrderBg.AddChild(_turnOrderBar);
	}

	public void UpdateTurnOrder(List<BattleUnit> ordered, BattleUnit active)
	{
		ClearChildren(_turnOrderBar);
		int count = Math.Min(ordered.Count, 10);
		for (int i = 0; i < count; i++)
		{
			var u = ordered[i];
			_turnOrderBar.AddChild(MakeTurnSlot(u, u == active));
		}
	}

	PanelContainer MakeTurnSlot(BattleUnit unit, bool active)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		var tc = unit.Team == UnitTeam.TeamA ? ColTeamA : ColTeamB;

		if (active)
		{
			style.BgColor = tc * new Color(1, 1, 1, 0.15f);
			style.BorderColor = tc;
			style.SetBorderWidthAll(2);
		}
		else
		{
			style.BgColor = new Color(0.06f, 0.06f, 0.10f, 0.8f);
			style.BorderColor = tc * new Color(1, 1, 1, 0.3f);
			style.SetBorderWidthAll(1);
		}
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(4);
		panel.AddThemeStyleboxOverride("panel", style);
		panel.CustomMinimumSize = new Vector2(60, 32);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		panel.AddChild(vbox);
		AddLabel(vbox, unit.Name, active ? Colors.White : new Color("B0B0C0"), 10, HorizontalAlignment.Center);
		AddLabel(vbox, $"RT:{unit.CurrentRt}", ColTextDark, 9, HorizontalAlignment.Center);
		return panel;
	}

	// ═══════════════════════════════════════════════════════
	//  TILE INFO + PHASE LABEL + HINT BAR
	// ═══════════════════════════════════════════════════════

	void BuildTileInfo()
	{
		_tileInfoLabel = new Label();
		_tileInfoLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		_tileInfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_tileInfoLabel.Position = new Vector2(0, -26);
		_tileInfoLabel.AddThemeColorOverride("font_color", ColTextDim);
		_tileInfoLabel.AddThemeFontSizeOverride("font_size", 12);
		_tileInfoLabel.Visible = false;
		_root.AddChild(_tileInfoLabel);
	}

	void BuildPhaseLabel()
	{
		_phaseLabel = new Label();
		_phaseLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_phaseLabel.Position = new Vector2(16, 60);
		_phaseLabel.AddThemeColorOverride("font_color", ColViolet);
		_phaseLabel.AddThemeFontSizeOverride("font_size", 14);
		_root.AddChild(_phaseLabel);
	}

	void BuildHintBar()
	{
		var bg = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.03f, 0.03f, 0.07f, 0.7f);
		style.BorderColor = ColBorderDim;
		style.BorderWidthTop = 1;
		style.SetContentMarginAll(4);
		bg.AddThemeStyleboxOverride("panel", style);
		bg.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		bg.OffsetTop = -22;
		_root.AddChild(bg);

		_hintBar = new HBoxContainer();
		_hintBar.AddThemeConstantOverride("separation", 20);
		bg.AddChild(_hintBar);

		string[] hints = { "W/S Navigate", "Enter Confirm", "Esc Back", "Tab Inspect", "1-7 Hotkeys" };
		foreach (var h in hints)
			AddLabel(_hintBar, h, ColTextDisabled, 10);
	}

	public void ShowTileInfo(GridTile tile)
	{
		if (tile == null) { _tileInfoLabel.Visible = false; return; }
		_tileInfoLabel.Visible = true;
		_tileInfoLabel.Text = $"({tile.X},{tile.Y}) · H:{tile.Height} · {tile.Terrain}";
	}

	public void SetPhaseText(string text) => _phaseLabel.Text = text;

	// ═══════════════════════════════════════════════════════
	//  INSPECT SCREEN (Tab)
	// ═══════════════════════════════════════════════════════

	void BuildInspectScreen()
	{
		_inspectOverlay = new Control();
		_inspectOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_inspectOverlay.Visible = false;
		AddChild(_inspectOverlay);

		var dimBg = new ColorRect();
		dimBg.Color = new Color(0.016f, 0.016f, 0.04f, 0.85f);
		dimBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_inspectOverlay.AddChild(dimBg);

		_inspectPanel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.04f, 0.047f, 0.078f, 0.96f);
		style.BorderColor = ColBorder;
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(0);
		_inspectPanel.AddThemeStyleboxOverride("panel", style);
		_inspectPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_inspectPanel.CustomMinimumSize = new Vector2(900, 520);
		_inspectPanel.Position = new Vector2(-450, -260);
		_inspectOverlay.AddChild(_inspectPanel);
	}

	void PopulateInspect(BattleUnit unit)
	{
		ClearChildren(_inspectPanel);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		_inspectPanel.AddChild(hbox);

		// ─── PORTRAIT COLUMN ────────────────────
		var portrait = new PanelContainer();
		var pStyle = new StyleBoxFlat();
		pStyle.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.5f);
		pStyle.BorderWidthRight = 1;
		pStyle.BorderColor = ColBorderDim;
		pStyle.SetContentMarginAll(20);
		portrait.AddThemeStyleboxOverride("panel", pStyle);
		portrait.CustomMinimumSize = new Vector2(260, 520);
		hbox.AddChild(portrait);

		var pVbox = new VBoxContainer();
		pVbox.AddThemeConstantOverride("separation", 4);
		portrait.AddChild(pVbox);

		var frame = new PanelContainer();
		var fStyle = new StyleBoxFlat();
		fStyle.BgColor = new Color(0.08f, 0.08f, 0.14f, 0.6f);
		fStyle.BorderColor = new Color(139/255f, 92/255f, 246/255f, 0.2f);
		fStyle.SetBorderWidthAll(1);
		fStyle.SetCornerRadiusAll(6);
		fStyle.SetContentMarginAll(0);
		frame.AddThemeStyleboxOverride("panel", fStyle);
		frame.CustomMinimumSize = new Vector2(220, 200);
		pVbox.AddChild(frame);

		var placeholder = new Label();
		placeholder.Text = "⚔";
		placeholder.HorizontalAlignment = HorizontalAlignment.Center;
		placeholder.VerticalAlignment = VerticalAlignment.Center;
		placeholder.AddThemeFontSizeOverride("font_size", 64);
		placeholder.AddThemeColorOverride("font_color", new Color(139/255f, 92/255f, 246/255f, 0.3f));
		frame.AddChild(placeholder);

		int avgLevel = (unit.Strength + unit.Speed + unit.Agility + unit.Endurance + unit.Stamina + unit.EtherControl) / 6;
		AddLabel(pVbox, unit.Name, Colors.White, 20, HorizontalAlignment.Center, true);
		AddLabel(pVbox, "▸ Combatant", ColViolet, 13, HorizontalAlignment.Center);
		AddLabel(pVbox, $"Lv. {avgLevel} · {unit.Team}", ColTextDark, 12, HorizontalAlignment.Center);

		var barSection = new VBoxContainer();
		barSection.AddThemeConstantOverride("separation", 2);
		pVbox.AddChild(barSection);

		AddInspectLabelRow(barSection, "HP", $"{unit.CurrentHp} / {unit.MaxHp}");
		AddInspectBar(barSection, unit.MaxHp > 0 ? (float)unit.CurrentHp / unit.MaxHp : 0, true);
		AddInspectLabelRow(barSection, "EP", $"{unit.CurrentEther} / {unit.MaxEther}");
		AddInspectBar(barSection, unit.MaxEther > 0 ? (float)unit.CurrentEther / unit.MaxEther : 0, false);

		// ─── STATS COLUMN ───────────────────────
		var statsCol = new PanelContainer();
		var sStyle = new StyleBoxFlat();
		sStyle.BgColor = Colors.Transparent;
		sStyle.BorderWidthRight = 1;
		sStyle.BorderColor = ColBorderDim;
		sStyle.SetContentMarginAll(20);
		statsCol.AddThemeStyleboxOverride("panel", sStyle);
		statsCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(statsCol);

		var sVbox = new VBoxContainer();
		sVbox.AddThemeConstantOverride("separation", 2);
		statsCol.AddChild(sVbox);

		AddSectionHeader(sVbox, "◆ EQUIPMENT");
		AddEquipRow(sVbox, "Weapon", "Ether-Forged Blade", "★★");
		AddEquipRow(sVbox, "Accessory", "Speed Amulet", "★");
		AddEquipRow(sVbox, "Accessory", "Warden's Ring", "★★★");

		AddSectionHeader(sVbox, "◆ TRAINING STATS");
		var statsGrid = new GridContainer();
		statsGrid.Columns = 2;
		statsGrid.AddThemeConstantOverride("h_separation", 16);
		statsGrid.AddThemeConstantOverride("v_separation", 1);
		sVbox.AddChild(statsGrid);
		AddStatPair(statsGrid, "Strength", unit.Strength);
		AddStatPair(statsGrid, "Speed", unit.Speed);
		AddStatPair(statsGrid, "Agility", unit.Agility);
		AddStatPair(statsGrid, "Endurance", unit.Endurance);
		AddStatPair(statsGrid, "Stamina", unit.Stamina);
		AddStatPair(statsGrid, "Ether Ctrl", unit.EtherControl);

		AddSectionHeader(sVbox, "◆ DERIVED");
		var derivedGrid = new GridContainer();
		derivedGrid.Columns = 2;
		derivedGrid.AddThemeConstantOverride("h_separation", 16);
		derivedGrid.AddThemeConstantOverride("v_separation", 1);
		sVbox.AddChild(derivedGrid);
		AddStatPair(derivedGrid, "ATK", unit.Atk);
		AddStatPair(derivedGrid, "DEF", unit.Def);
		AddStatPair(derivedGrid, "EATK", unit.Eatk);
		AddStatPair(derivedGrid, "EDEF", unit.Edef);
		AddStatPair(derivedGrid, "AVD", unit.Avd);
		AddStatPair(derivedGrid, "ACC", unit.Acc);
		AddStatPair(derivedGrid, "MOV", unit.Move);
		AddStatPair(derivedGrid, "JMP", unit.Jump);
		AddStatPair(derivedGrid, "Crit%", (int)unit.CritPercent);
		AddStatPair(derivedGrid, "Base RT", unit.BaseWt);

		AddSectionHeader(sVbox, "◆ RESISTANCES");
		var resRow = new HBoxContainer();
		resRow.AddThemeConstantOverride("separation", 16);
		sVbox.AddChild(resRow);
		foreach (var r in new[] { "🔥 0%", "❄ 0%", "⚡ 0%", "🌿 0%", "💀 0%" })
			AddLabel(resRow, r, ColTextDark, 11);

		// ─── ABILITIES COLUMN ───────────────────
		var abPanel = new PanelContainer();
		var abStyle = new StyleBoxFlat();
		abStyle.BgColor = Colors.Transparent;
		abStyle.SetContentMarginAll(20);
		abPanel.AddThemeStyleboxOverride("panel", abStyle);
		abPanel.CustomMinimumSize = new Vector2(260, 0);
		hbox.AddChild(abPanel);

		var abCol = new VBoxContainer();
		abCol.AddThemeConstantOverride("separation", 2);
		abPanel.AddChild(abCol);

		AddSectionHeader(abCol, "◆ ABILITIES");
		foreach (var ab in _abilities)
		{
			var abRow = new HBoxContainer();
			abRow.AddThemeConstantOverride("separation", 8);
			AddLabel(abRow, ab.Icon, CategoryColor(ab.Category), 12);
			var nameLbl = new Label();
			nameLbl.Text = ab.Name;
			nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			nameLbl.AddThemeColorOverride("font_color", ColText);
			nameLbl.AddThemeFontSizeOverride("font_size", 12);
			abRow.AddChild(nameLbl);
			string pips = new string('◆', Math.Clamp(ab.EtherCost / 15, 1, 3));
			AddLabel(abRow, pips, ColGold, 10);
			abCol.AddChild(abRow);
		}

		AddSectionHeader(abCol, "◆ WEAPON SKILL");
		AddLabel(abCol, "⊘ Locked", ColTextDisabled, 12);

		// Close button
		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.CustomMinimumSize = new Vector2(28, 28);
		var closeSty = new StyleBoxFlat();
		closeSty.BgColor = new Color(200/255f, 80/255f, 80/255f, 0.2f);
		closeSty.SetCornerRadiusAll(4);
		closeBtn.AddThemeStyleboxOverride("normal", closeSty);
		closeBtn.AddThemeColorOverride("font_color", new Color("cc6666"));
		closeBtn.AddThemeFontSizeOverride("font_size", 14);
		closeBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		closeBtn.Position = new Vector2(-40, 12);
		closeBtn.Pressed += () => ToggleInspect(false);
		_inspectPanel.AddChild(closeBtn);
	}

	// ═══════════════════════════════════════════════════════
	//  PUBLIC API
	// ═══════════════════════════════════════════════════════

	public void ShowCommandMenu(BattleUnit unit)
	{
		_activeUnit = unit;
		_menuOpen = true;
		_cmdIndex = 0;
		_cmdPanel.Visible = true;

		SetCmdEnabled("Move", !unit.HasMoved);
		SetCmdEnabled("Attack", !unit.HasActed);
		SetCmdEnabled("Ability", !unit.HasActed && unit.CurrentEther > 0);
		SetCmdEnabled("Item", !unit.HasActed);
		SetCmdEnabled("Defend", !unit.HasActed);
		SetCmdEnabled("Flee", true);
		SetCmdEnabled("Wait", true);

		CloseSubMenus();
		SelectCmd(0);
		UpdateUnitInfo(unit);
	}

	public void HideCommandMenu()
	{
		_menuOpen = false;
		_cmdPanel.Visible = false;
		CloseSubMenus();
	}

	// ═══════════════════════════════════════════════════════
	//  INPUT
	// ═══════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed) return;

		if (key.Keycode == Key.Tab)
		{
			ToggleInspect(!_inspectOpen);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_inspectOpen)
		{
			if (key.Keycode == Key.Escape) { ToggleInspect(false); GetViewport().SetInputAsHandled(); }
			return;
		}

		if (!_menuOpen) return;

		if (_subMenuOpen)
		{
			var btns = _activeSubType == "Ability" ? _abilityBtns : _itemBtns;
			switch (key.Keycode)
			{
				case Key.Up: case Key.W: NavSubMenu(-1, btns); GetViewport().SetInputAsHandled(); break;
				case Key.Down: case Key.S: NavSubMenu(1, btns); GetViewport().SetInputAsHandled(); break;
				case Key.Enter: case Key.Space:
					if (_activeSubType == "Ability") OnAbilityClicked(_subIndex);
					else OnItemClicked(_subIndex);
					GetViewport().SetInputAsHandled(); break;
				case Key.Escape: CloseSubMenus(); GetViewport().SetInputAsHandled(); break;
			}
			return;
		}

		switch (key.Keycode)
		{
			case Key.Up: case Key.W: NavCmd(-1); GetViewport().SetInputAsHandled(); break;
			case Key.Down: case Key.S: NavCmd(1); GetViewport().SetInputAsHandled(); break;
			case Key.Enter: case Key.Space: OnCmdPressed(_cmdIndex); GetViewport().SetInputAsHandled(); break;
			case Key.Escape: EmitSignal(SignalName.CommandCancelled); GetViewport().SetInputAsHandled(); break;
		}

		int num = key.Keycode switch
		{
			Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3,
			Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, _ => -1
		};
		if (num >= 0 && num < CmdNames.Length)
		{
			OnCmdPressed(num);
			GetViewport().SetInputAsHandled();
		}
	}

	// ═══════════════════════════════════════════════════════
	//  NAVIGATION LOGIC
	// ═══════════════════════════════════════════════════════

	void SelectCmd(int idx)
	{
		_cmdIndex = idx;
		for (int i = 0; i < _cmdBtns.Count; i++)
			if (i == idx && !_cmdBtns[i].Disabled) _cmdBtns[i].GrabFocus();

		string cmd = CmdNames[idx];
		_abilityPanel.Visible = cmd == "Ability" && !_cmdBtns[idx].Disabled;
		_itemPanel.Visible = cmd == "Item" && !_cmdBtns[idx].Disabled;
		_tooltipPanel.Visible = cmd == "Ability" && _abilities.Count > 0 && !_cmdBtns[idx].Disabled;
		if (cmd == "Ability" && _abilities.Count > 0) UpdateTooltip(0);
	}

	void NavCmd(int dir)
	{
		int next = _cmdIndex;
		int attempts = CmdNames.Length;
		do { next = (next + dir + CmdNames.Length) % CmdNames.Length; attempts--; }
		while (_cmdBtns[next].Disabled && attempts > 0);
		SelectCmd(next);
	}

	void SelectSubItem(int idx)
	{
		_subIndex = idx;
		var btns = _activeSubType == "Ability" ? _abilityBtns : _itemBtns;
		for (int i = 0; i < btns.Count; i++)
			if (i == idx && !btns[i].Disabled) btns[i].GrabFocus();
	}

	void NavSubMenu(int dir, List<Button> btns)
	{
		int next = _subIndex;
		int attempts = btns.Count;
		do { next = (next + dir + btns.Count) % btns.Count; attempts--; }
		while (btns[next].Disabled && attempts > 0);
		_subIndex = next;
		SelectSubItem(next);
		if (_activeSubType == "Ability") UpdateTooltip(next);
	}

	void OnCmdPressed(int idx)
	{
		if (_cmdBtns[idx].Disabled) return;
		string cmd = CmdNames[idx];

		if (cmd == "Ability")
		{
			_subMenuOpen = true;
			_subIndex = 0;
			_activeSubType = "Ability";
			_abilityPanel.Visible = true;
			_itemPanel.Visible = false;
			if (_abilityBtns.Count > 0) SelectSubItem(0);
			if (_abilities.Count > 0) UpdateTooltip(0);
			return;
		}
		if (cmd == "Item")
		{
			_subMenuOpen = true;
			_subIndex = 0;
			_activeSubType = "Item";
			_itemPanel.Visible = true;
			_abilityPanel.Visible = false;
			_tooltipPanel.Visible = false;
			if (_itemBtns.Count > 0) SelectSubItem(0);
			return;
		}

		CloseSubMenus();
		EmitSignal(SignalName.CommandSelected, cmd);
	}

	void OnAbilityClicked(int idx)
	{
		if (idx >= 0 && idx < _abilities.Count && _abilities[idx].IsUsable)
		{
			CloseSubMenus();
			EmitSignal(SignalName.AbilitySelected, idx);
		}
	}

	void OnItemClicked(int idx)
	{
		if (idx >= 0 && idx < _items.Count && _items[idx].IsUsable)
		{
			CloseSubMenus();
			EmitSignal(SignalName.ItemSelected, idx);
		}
	}

	void CloseSubMenus()
	{
		_subMenuOpen = false;
		_abilityPanel.Visible = false;
		_itemPanel.Visible = false;
		_tooltipPanel.Visible = false;
	}

	void ToggleInspect(bool open)
	{
		_inspectOpen = open;
		_inspectOverlay.Visible = open;
		if (open && _activeUnit != null) PopulateInspect(_activeUnit);
	}

	// ═══════════════════════════════════════════════════════
	//  UI BUILDING HELPERS
	// ═══════════════════════════════════════════════════════

	PanelContainer MakePanel(Color bg, Color border)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = bg;
		style.BorderColor = border;
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(8);
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	void ApplyButtonTheme(Button btn)
	{
		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.08f, 0.08f, 0.14f, 0.6f);
		normal.SetCornerRadiusAll(4);
		normal.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = ColVioletDim;
		hover.BorderColor = ColViolet;
		hover.SetBorderWidthAll(1);
		hover.SetCornerRadiusAll(4);
		hover.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("hover", hover);

		var pressed = new StyleBoxFlat();
		pressed.BgColor = new Color(139/255f, 92/255f, 246/255f, 0.45f);
		pressed.SetCornerRadiusAll(4);
		pressed.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("pressed", pressed);

		var focus = new StyleBoxFlat();
		focus.BgColor = ColVioletDim;
		focus.BorderColor = ColViolet;
		focus.SetBorderWidthAll(1);
		focus.SetCornerRadiusAll(4);
		focus.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("focus", focus);

		var disabled = new StyleBoxFlat();
		disabled.BgColor = new Color(0.05f, 0.05f, 0.08f, 0.4f);
		disabled.SetCornerRadiusAll(4);
		disabled.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("disabled", disabled);

		btn.AddThemeColorOverride("font_color", ColText);
		btn.AddThemeColorOverride("font_hover_color", Colors.White);
		btn.AddThemeColorOverride("font_focus_color", Colors.White);
		btn.AddThemeColorOverride("font_disabled_color", ColTextDisabled);
		btn.AddThemeFontSizeOverride("font_size", 14);
	}

	static Label AddLabel(Control parent, string text, Color color, int size,
		HorizontalAlignment align = HorizontalAlignment.Left, bool bold = false)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.HorizontalAlignment = align;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", size);
		parent.AddChild(lbl);
		return lbl;
	}

	static void AddSep(Control parent)
	{
		var sep = new HSeparator();
		var style = new StyleBoxLine();
		style.Color = ColBorderDim;
		style.Thickness = 1;
		sep.AddThemeStyleboxOverride("separator", style);
		parent.AddChild(sep);
	}

	void AddStatBar(VBoxContainer parent, string label, int current, int max, bool isHp)
	{
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 6);
		parent.AddChild(hbox);

		AddLabel(hbox, label, ColTextDark, 11);

		var barBg = new Panel();
		barBg.CustomMinimumSize = new Vector2(120, 12);
		barBg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
		bgStyle.SetCornerRadiusAll(2);
		barBg.AddThemeStyleboxOverride("panel", bgStyle);
		hbox.AddChild(barBg);

		float pct = max > 0 ? (float)current / max : 0;
		var fill = new Panel();
		fill.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
		fill.Size = new Vector2(120 * pct, 12);
		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = isHp ? (pct > 0.5f ? ColHpFull : pct > 0.25f ? ColHpMid : ColHpLow) : ColEther;
		fillStyle.SetCornerRadiusAll(2);
		fill.AddThemeStyleboxOverride("panel", fillStyle);
		barBg.AddChild(fill);

		AddLabel(hbox, $"{current}/{max}", new Color("B0B0C0"), 11);
	}

	void AddInspectLabelRow(VBoxContainer parent, string label, string value)
	{
		var hbox = new HBoxContainer();
		AddLabel(hbox, label, ColTextDark, 11);
		var spacer = new Control();
		spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(spacer);
		AddLabel(hbox, value, new Color("B0B0C0"), 11);
		parent.AddChild(hbox);
	}

	void AddInspectBar(VBoxContainer parent, float pct, bool isHp)
	{
		var barBg = new Panel();
		barBg.CustomMinimumSize = new Vector2(220, 8);
		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
		bgStyle.SetCornerRadiusAll(2);
		barBg.AddThemeStyleboxOverride("panel", bgStyle);
		parent.AddChild(barBg);

		var fill = new Panel();
		fill.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
		fill.Size = new Vector2(220 * pct, 8);
		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = isHp ? (pct > 0.5f ? ColHpFull : pct > 0.25f ? ColHpMid : ColHpLow) : ColEther;
		fillStyle.SetCornerRadiusAll(2);
		fill.AddThemeStyleboxOverride("panel", fillStyle);
		barBg.AddChild(fill);
	}

	void AddSectionHeader(VBoxContainer parent, string text)
	{
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 6);
		parent.AddChild(spacer);

		AddLabel(parent, text, ColViolet, 11);

		var sep = new HSeparator();
		var style = new StyleBoxLine();
		style.Color = new Color(139/255f, 92/255f, 246/255f, 0.15f);
		style.Thickness = 1;
		sep.AddThemeStyleboxOverride("separator", style);
		parent.AddChild(sep);
	}

	void AddEquipRow(VBoxContainer parent, string slot, string name, string stars)
	{
		var hbox = new HBoxContainer();
		parent.AddChild(hbox);
		var slotLbl = new Label();
		slotLbl.Text = slot;
		slotLbl.CustomMinimumSize = new Vector2(80, 0);
		slotLbl.AddThemeColorOverride("font_color", ColTextDark);
		slotLbl.AddThemeFontSizeOverride("font_size", 12);
		hbox.AddChild(slotLbl);
		var nameLbl = new Label();
		nameLbl.Text = name;
		nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLbl.AddThemeColorOverride("font_color", ColText);
		nameLbl.AddThemeFontSizeOverride("font_size", 12);
		hbox.AddChild(nameLbl);
		AddLabel(hbox, stars, ColGold, 10);
	}

	void AddStatPair(GridContainer grid, string name, int val)
	{
		var row = new HBoxContainer();
		row.CustomMinimumSize = new Vector2(140, 20);
		var nameLbl = new Label();
		nameLbl.Text = name;
		nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLbl.AddThemeColorOverride("font_color", ColTextDim);
		nameLbl.AddThemeFontSizeOverride("font_size", 12);
		row.AddChild(nameLbl);
		var valLbl = new Label();
		valLbl.Text = val.ToString();
		valLbl.AddThemeColorOverride("font_color", ColText);
		valLbl.AddThemeFontSizeOverride("font_size", 12);
		row.AddChild(valLbl);
		grid.AddChild(row);
	}

	void SetCmdEnabled(string name, bool enabled)
	{
		int idx = Array.IndexOf(CmdNames, name);
		if (idx >= 0 && idx < _cmdBtns.Count) _cmdBtns[idx].Disabled = !enabled;
	}

	static void ClearChildren(Control node)
	{
		foreach (var c in node.GetChildren()) c.QueueFree();
	}

	static Color CategoryColor(string cat) => cat switch
	{
		"ether" => new Color("aa88ff"),
		"phys"  => new Color("ff8855"),
		"heal"  => new Color("55cc77"),
		"buff"  => new Color("ddcc55"),
		"item"  => new Color("66bbdd"),
		_ => ColText
	};

	static string Capitalize(string s) => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s;
}
