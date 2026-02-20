using Godot;
using System;
using System.Collections.Generic;
using ProjectTactics.UI;

namespace ProjectTactics.Combat;

/// <summary>
/// Battle HUD — overlays combat UI on top of the overworld.
/// Positions all elements above the chat panel area (~200px from bottom).
/// Respects UITheme dark/light mode.
///
/// Layout:
///   Top-center: Turn order bar
///   Top-left: Phase label
///   Bottom-center (above chat): Unit info card with portrait
///   Bottom-right (above chat): Command menu + sub-menus
///   Fullscreen overlay: Inspect screen (Tab / right-click / Status cmd)
/// </summary>
public partial class BattleHUD : CanvasLayer
{
	[Signal] public delegate void CommandSelectedEventHandler(string command);
	[Signal] public delegate void CommandCancelledEventHandler();
	[Signal] public delegate void AbilitySelectedEventHandler(int index);
	[Signal] public delegate void ItemSelectedEventHandler(int index);
	[Signal] public delegate void MoveConfirmedEventHandler();

	// Chat area reserve — all battle UI sits above this
	const float ChatHeight = 200f;

	// Commands
	static readonly string[] CmdNames = { "Move", "Attack", "Ability", "Item", "Defend", "Flee", "End Turn", "Status" };
	static readonly string[] CmdIcons = { "→", "⚔", "✦", "◆", "🛡", "↺", "◎", "📋" };

	// State
	BattleUnit _activeUnit;
	int _cmdIndex;
	bool _menuOpen, _subMenuOpen, _inspectOpen;
	int _subIndex;
	string _activeSubType;
	List<AbilityInfo> _abilities = new();
	List<ItemInfo> _items = new();

	// Nodes
	Control _root;
	PanelContainer _cmdPanel;
	VBoxContainer _cmdList;
	readonly List<Button> _cmdBtns = new();
	PanelContainer _unitCard;
	PanelContainer _targetCard;
	HBoxContainer _turnOrderBar;
	PanelContainer _turnOrderBg;
	Label _phaseLabel;
	Label _tileInfoLabel;

	// Sub-menus
	PanelContainer _abilityPanel, _itemPanel, _tooltipPanel;
	VBoxContainer _abilityList, _itemList;
	readonly List<Button> _abilityBtns = new(), _itemBtns = new();

	// Move confirm
	PanelContainer _moveConfirmPanel;
	Button _confirmBtn, _cancelBtn;

	// Drag state for command menu
	bool _cmdDragging;
	Vector2 _cmdDragOffset;

	// Inspect
	Control _inspectOverlay;
	PanelContainer _inspectPanel;

	// ═══════════════════════════════════════════════════════
	//  THEME HELPERS — all colors go through these
	// ═══════════════════════════════════════════════════════

	static Color BgPanel   => UITheme.IsDarkMode ? new Color(0.03f, 0.03f, 0.07f, 0.92f) : new Color(1f, 1f, 1f, 0.95f);
	static Color BgSub     => UITheme.IsDarkMode ? new Color(0.03f, 0.03f, 0.07f, 0.94f) : new Color(0.98f, 0.98f, 1f, 0.96f);
	static Color BgDim     => UITheme.IsDarkMode ? new Color(0.016f, 0.016f, 0.04f, 0.85f) : new Color(0f, 0f, 0f, 0.4f);
	static Color BdPrimary => UITheme.Accent;
	static Color BdSubtle  => UITheme.BorderSubtle;
	static Color BdDim     => UITheme.IsDarkMode ? new Color(0.235f, 0.255f, 0.314f, 0.25f) : new Color(0f, 0f, 0f, 0.08f);
	static Color TxBright  => UITheme.TextBright;
	static Color TxPrimary => UITheme.Text;
	static Color TxDim     => UITheme.TextSecondary;
	static Color TxDark    => UITheme.TextDim;
	static Color TxDisabled => UITheme.IsDarkMode ? new Color("44445a") : new Color("b0b0b8");
	static Color ColTeamA  => new("5599ee");
	static Color ColTeamB  => new("ee5555");
	static Color ColHpFull => new("44cc55");
	static Color ColHpMid  => new("aaaa33");
	static Color ColHpLow  => new("cc3333");
	static Color ColEther  => new("4488dd");
	static Color ColGold   => UITheme.AccentGold;
	static Color BtnNormal => UITheme.IsDarkMode ? new Color(0.08f, 0.08f, 0.14f, 0.6f) : new Color(0f, 0f, 0f, 0.04f);
	static Color BtnHover  => UITheme.IsDarkMode ? UITheme.AccentVioletDim : new Color(0f, 0f, 0f, 0.08f);
	static Color BtnDisabled => UITheme.IsDarkMode ? new Color(0.05f, 0.05f, 0.08f, 0.4f) : new Color(0f, 0f, 0f, 0.02f);

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
		BuildUnitCard();
		BuildTargetCard();
		BuildCommandMenu();
		BuildAbilitySubMenu();
		BuildItemSubMenu();
		BuildTooltip();
		BuildTileInfo();
		BuildMoveConfirm();
		BuildInspectScreen();
		BuildCrownButton();

		HideCommandMenu();
		UITheme.ThemeChanged += _ => Rebuild();
	}

	public override void _ExitTree() => UITheme.ThemeChanged -= _ => Rebuild();

	void Rebuild()
	{
		// Full rebuild on theme change
		foreach (var c in _root.GetChildren()) c.QueueFree();
		_cmdBtns.Clear(); _abilityBtns.Clear(); _itemBtns.Clear();
		BuildPhaseLabel();
		BuildTurnOrderBar();
		BuildUnitCard();
		BuildTargetCard();
		BuildCommandMenu();
		BuildAbilitySubMenu();
		BuildItemSubMenu();
		BuildTooltip();
		BuildTileInfo();
		BuildMoveConfirm();
		BuildCrownButton();
		// Re-populate data
		RebuildAbilityList();
		RebuildItemList();
		if (_activeUnit != null && _menuOpen) ShowCommandMenu(_activeUnit);
		else HideCommandMenu();
	}

	// ═══════════════════════════════════════════════════════
	//  COMMAND MENU — bottom-right, above chat
	// ═══════════════════════════════════════════════════════

	void BuildCommandMenu()
	{
		_cmdPanel = MakePanel(BgPanel, BdPrimary);
		_cmdPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_cmdPanel.Position = new Vector2(-196, -(ChatHeight + 310));
		_cmdPanel.CustomMinimumSize = new Vector2(170, 0);
		_cmdPanel.MouseFilter = Control.MouseFilterEnum.Stop;
		_root.AddChild(_cmdPanel);

		var outerVb = new VBoxContainer();
		outerVb.AddThemeConstantOverride("separation", 3);
		_cmdPanel.AddChild(outerVb);

		// Drag handle bar
		var dragBar = new Panel();
		dragBar.CustomMinimumSize = new Vector2(0, 20);
		dragBar.MouseFilter = Control.MouseFilterEnum.Stop;
		dragBar.MouseDefaultCursorShape = Control.CursorShape.Move;
		var dragStyle = new StyleBoxFlat();
		dragStyle.BgColor = Colors.Transparent;
		dragBar.AddThemeStyleboxOverride("panel", dragStyle);
		outerVb.AddChild(dragBar);

		// Title + drag hint
		var titleRow = new HBoxContainer();
		titleRow.MouseFilter = Control.MouseFilterEnum.Ignore;
		var titleLbl = new Label();
		titleLbl.Text = "COMMAND";
		titleLbl.AddThemeColorOverride("font_color", TxDim);
		titleLbl.AddThemeFontSizeOverride("font_size", 11);
		titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
		titleRow.AddChild(titleLbl);
		var dragHint = new Label();
		dragHint.Text = "⠿";
		dragHint.AddThemeColorOverride("font_color", TxDark);
		dragHint.AddThemeFontSizeOverride("font_size", 12);
		titleRow.AddChild(dragHint);
		dragBar.AddChild(titleRow);
		titleRow.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		// Wire drag events on the drag bar
		dragBar.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton mb)
			{
				if (mb.ButtonIndex == MouseButton.Left)
				{
					_cmdDragging = mb.Pressed;
					_cmdDragOffset = mb.GlobalPosition - _cmdPanel.GlobalPosition;
					GetViewport().SetInputAsHandled();
				}
			}
			else if (ev is InputEventMouseMotion mm && _cmdDragging)
			{
				_cmdPanel.GlobalPosition = mm.GlobalPosition - _cmdDragOffset;
				GetViewport().SetInputAsHandled();
			}
		};

		_cmdList = new VBoxContainer();
		_cmdList.AddThemeConstantOverride("separation", 3);
		outerVb.AddChild(_cmdList);

		MakeSep(outerVb);

		for (int i = 0; i < CmdNames.Length; i++)
		{
			if (i == 6) MakeSep(_cmdList);
			bool hasSub = CmdNames[i] is "Ability" or "Item";
			var btn = MakeCmdButton(CmdNames[i], CmdIcons[i], i, hasSub);
			_cmdList.AddChild(btn);
			_cmdBtns.Add(btn);
		}
	}

	Button MakeCmdButton(string label, string icon, int idx, bool hasSub)
	{
		var btn = new Button();
		btn.Text = hasSub ? $"  {icon}  {label}  ▸" : $"  {icon}  {label}";
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(158, 32);
		ApplyBtnTheme(btn);
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
		_abilityPanel = MakePanel(BgSub, new Color(UITheme.AccentViolet, 0.4f));
		_abilityPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_abilityPanel.Position = new Vector2(-446, -(ChatHeight + 360));
		_abilityPanel.CustomMinimumSize = new Vector2(240, 0);
		_abilityPanel.Visible = false;
		_root.AddChild(_abilityPanel);

		_abilityList = new VBoxContainer();
		_abilityList.AddThemeConstantOverride("separation", 2);
		_abilityPanel.AddChild(_abilityList);
	}

	public void SetAbilities(List<AbilityInfo> a) { _abilities = a; RebuildAbilityList(); }

	void RebuildAbilityList()
	{
		ClearChildren(_abilityList); _abilityBtns.Clear();
		MakeLabel(_abilityList, "✦ ABILITIES", UITheme.Accent, 11);
		MakeSep(_abilityList);
		for (int i = 0; i < _abilities.Count; i++)
		{
			var ab = _abilities[i];
			var btn = MakeSubBtn(ab.Icon, ab.Name, $"{ab.EtherCost} EP", ab.IsUsable);
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
		_itemPanel = MakePanel(BgSub, new Color(UITheme.AccentViolet, 0.4f));
		_itemPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_itemPanel.Position = new Vector2(-446, -(ChatHeight + 280));
		_itemPanel.CustomMinimumSize = new Vector2(240, 0);
		_itemPanel.Visible = false;
		_root.AddChild(_itemPanel);

		_itemList = new VBoxContainer();
		_itemList.AddThemeConstantOverride("separation", 2);
		_itemPanel.AddChild(_itemList);
	}

	public void SetItems(List<ItemInfo> items) { _items = items; RebuildItemList(); }

	void RebuildItemList()
	{
		ClearChildren(_itemList); _itemBtns.Clear();
		MakeLabel(_itemList, "◆ ITEMS", UITheme.Accent, 11);
		MakeSep(_itemList);
		for (int i = 0; i < _items.Count; i++)
		{
			var it = _items[i];
			var btn = MakeSubBtn(it.Icon, it.Name, $"x{it.Quantity}", it.IsUsable);
			int ci = i;
			btn.Pressed += () => OnItemClicked(ci);
			btn.MouseEntered += () => SelectSubItem(ci);
			_itemList.AddChild(btn);
			_itemBtns.Add(btn);
		}
	}

	Button MakeSubBtn(string icon, string name, string cost, bool usable)
	{
		var btn = new Button();
		btn.Text = $"  {icon}  {name}          {cost}";
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(228, 30);
		btn.Disabled = !usable;
		ApplyBtnTheme(btn);
		return btn;
	}

	// ═══════════════════════════════════════════════════════
	//  ABILITY TOOLTIP
	// ═══════════════════════════════════════════════════════

	void BuildTooltip()
	{
		_tooltipPanel = MakePanel(BgSub, BdSubtle);
		_tooltipPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_tooltipPanel.Position = new Vector2(-700, -(ChatHeight + 310));
		_tooltipPanel.CustomMinimumSize = new Vector2(200, 0);
		_tooltipPanel.Visible = false;
		_root.AddChild(_tooltipPanel);
	}

	void UpdateTooltip(int i)
	{
		if (i < 0 || i >= _abilities.Count) return;
		var ab = _abilities[i];
		ClearChildren(_tooltipPanel);
		var vb = new VBoxContainer(); vb.AddThemeConstantOverride("separation", 2);
		_tooltipPanel.AddChild(vb);
		MakeLabel(vb, ab.Name, TxBright, 14);
		MakeLabel(vb, $"{Cap(ab.Category)} · {ab.TargetType}", UITheme.Accent, 11);
		var desc = new Label();
		desc.Text = ab.Description; desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.CustomMinimumSize = new Vector2(180, 0);
		desc.AddThemeColorOverride("font_color", TxDim); desc.AddThemeFontSizeOverride("font_size", 11);
		vb.AddChild(desc);
		MakeSep(vb);
		MakeLabel(vb, $"Power: {ab.Power}", TxDark, 11);
		MakeLabel(vb, $"Range: {ab.Range} tiles", TxDark, 11);
		MakeLabel(vb, $"RT Cost: {ab.RtCost}", TxDark, 11);
		MakeLabel(vb, $"Cost: {ab.EtherCost} EP", ColEther, 11);
		_tooltipPanel.Visible = true;
	}

	// ═══════════════════════════════════════════════════════
	//  UNIT INFO CARD — Sword of Convallaria style
	//  Active unit: bottom-right (always visible during turn)
	//  Target unit: bottom-left (appears on hover)
	//  Layout: [info panel | large portrait]
	// ═══════════════════════════════════════════════════════

	void BuildUnitCard()
	{
		_unitCard = MakePanel(BgPanel, BdSubtle);
		// Bottom center-right, inward from edge
		_unitCard.AnchorLeft = 0.5f; _unitCard.AnchorRight = 0.5f;
		_unitCard.AnchorBottom = 1; _unitCard.AnchorTop = 1;
		_unitCard.OffsetLeft = 60; _unitCard.OffsetRight = 320;
		_unitCard.OffsetTop = -(ChatHeight + 120); _unitCard.OffsetBottom = -(ChatHeight + 4);
		_unitCard.Visible = false;
		_root.AddChild(_unitCard);
	}

	void BuildTargetCard()
	{
		_targetCard = MakePanel(BgPanel, BdSubtle);
		// Bottom center-left, inward from edge
		_targetCard.AnchorLeft = 0.5f; _targetCard.AnchorRight = 0.5f;
		_targetCard.AnchorBottom = 1; _targetCard.AnchorTop = 1;
		_targetCard.OffsetLeft = -320; _targetCard.OffsetRight = -60;
		_targetCard.OffsetTop = -(ChatHeight + 120); _targetCard.OffsetBottom = -(ChatHeight + 4);
		_targetCard.Visible = false;
		_root.AddChild(_targetCard);
	}

	void PopulateUnitCard(PanelContainer card, BattleUnit unit, bool isTarget)
	{
		ClearChildren(card);
		bool isA = unit.Team == UnitTeam.TeamA;
		var teamCol = isA ? ColTeamA : ColTeamB;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		card.AddChild(hbox);

		// ─── Info column (left side) ───
		var infoVb = new VBoxContainer();
		infoVb.AddThemeConstantOverride("separation", 3);
		infoVb.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// HP number — large and prominent
		var hpNum = new Label();
		float hpPct = unit.MaxHp > 0 ? (float)unit.CurrentHp / unit.MaxHp : 0;
		hpNum.Text = $"{unit.CurrentHp}/{unit.MaxHp}";
		hpNum.AddThemeColorOverride("font_color", TxBright);
		hpNum.AddThemeFontSizeOverride("font_size", 22);
		infoVb.AddChild(hpNum);

		// HP bar — full width, chunky
		AddTorBar(infoVb, hpPct, true, 10);

		// EP bar — thinner
		float epPct = unit.MaxAether > 0 ? (float)unit.CurrentAether / unit.MaxAether : 0;
		AddTorBar(infoVb, epPct, false, 5);

		// Stars row with class icon
		var starsRow = new HBoxContainer(); starsRow.AddThemeConstantOverride("separation", 4);
		var classIcon = new Label();
		classIcon.Text = "◆"; classIcon.AddThemeColorOverride("font_color", ColGold);
		classIcon.AddThemeFontSizeOverride("font_size", 12);
		starsRow.AddChild(classIcon);
		int lvl = (unit.Strength + unit.Speed + unit.Agility + unit.Endurance + unit.Stamina + unit.EtherControl) / 6;
		int stars = Mathf.Clamp(lvl / 10 + 1, 1, 5);
		var starLabel = new Label();
		starLabel.Text = new string('★', stars) + new string('☆', 5 - stars);
		starLabel.AddThemeColorOverride("font_color", ColGold);
		starLabel.AddThemeFontSizeOverride("font_size", 11);
		starsRow.AddChild(starLabel);
		infoVb.AddChild(starsRow);

		// Level circle + Name
		var nameRow = new HBoxContainer(); nameRow.AddThemeConstantOverride("separation", 6);
		var lvlPanel = new PanelContainer();
		var lvlStyle = new StyleBoxFlat();
		lvlStyle.BgColor = new Color(teamCol, 0.2f);
		lvlStyle.BorderColor = teamCol; lvlStyle.SetBorderWidthAll(1);
		lvlStyle.SetCornerRadiusAll(12);
		lvlStyle.ContentMarginLeft = 5; lvlStyle.ContentMarginRight = 5;
		lvlStyle.ContentMarginTop = 1; lvlStyle.ContentMarginBottom = 1;
		lvlPanel.AddThemeStyleboxOverride("panel", lvlStyle);
		var lvlLabel = new Label();
		lvlLabel.Text = lvl.ToString();
		lvlLabel.AddThemeColorOverride("font_color", teamCol);
		lvlLabel.AddThemeFontSizeOverride("font_size", 13);
		lvlLabel.HorizontalAlignment = HorizontalAlignment.Center;
		lvlPanel.AddChild(lvlLabel);
		nameRow.AddChild(lvlPanel);
		var nameLbl = new Label();
		nameLbl.Text = unit.Name;
		nameLbl.AddThemeColorOverride("font_color", TxBright);
		nameLbl.AddThemeFontSizeOverride("font_size", 14);
		nameRow.AddChild(nameLbl);
		infoVb.AddChild(nameRow);

		// ─── Portrait (right side, large) ───
		var portrait = new PanelContainer();
		var pStyle = new StyleBoxFlat();
		pStyle.BgColor = UITheme.IsDarkMode ? new Color(0.05f, 0.05f, 0.09f, 0.9f) : new Color(0.93f, 0.93f, 0.96f, 0.95f);
		pStyle.BorderWidthLeft = 2; pStyle.BorderColor = teamCol;
		pStyle.SetCornerRadiusAll(0);
		pStyle.CornerRadiusTopRight = 4; pStyle.CornerRadiusBottomRight = 4;
		pStyle.SetContentMarginAll(0);
		portrait.AddThemeStyleboxOverride("panel", pStyle);
		portrait.CustomMinimumSize = new Vector2(85, 100);
		var pIcon = new Label();
		pIcon.Text = "⚔"; pIcon.HorizontalAlignment = HorizontalAlignment.Center;
		pIcon.VerticalAlignment = VerticalAlignment.Center;
		pIcon.AddThemeFontSizeOverride("font_size", 40);
		pIcon.AddThemeColorOverride("font_color", new Color(teamCol, 0.4f));
		portrait.AddChild(pIcon);

		// Assemble: [info] [portrait]
		hbox.AddChild(infoVb);
		var pad = new Control(); pad.CustomMinimumSize = new Vector2(6, 0); hbox.AddChild(pad);
		hbox.AddChild(portrait);
	}

	void AddTorBar(VBoxContainer par, float pct, bool hp, int barHeight)
	{
		var bg = new Panel();
		bg.CustomMinimumSize = new Vector2(0, barHeight);
		var bgs = new StyleBoxFlat();
		bgs.BgColor = UITheme.IsDarkMode ? new Color(0.1f, 0.1f, 0.15f, 0.8f) : new Color(0, 0, 0, 0.06f);
		bgs.SetCornerRadiusAll(2);
		bg.AddThemeStyleboxOverride("panel", bgs);
		par.AddChild(bg);

		var fill = new Panel();
		fill.AnchorLeft = 0; fill.AnchorRight = Mathf.Clamp(pct, 0, 1);
		fill.AnchorTop = 0; fill.AnchorBottom = 1;
		fill.OffsetLeft = 1; fill.OffsetRight = -1; fill.OffsetTop = 1; fill.OffsetBottom = -1;
		var fs = new StyleBoxFlat();
		fs.BgColor = hp ? (pct > 0.5f ? ColHpFull : pct > 0.25f ? ColHpMid : ColHpLow) : ColEther;
		fs.SetCornerRadiusAll(2);
		fill.AddThemeStyleboxOverride("panel", fs);
		fill.MouseFilter = Control.MouseFilterEnum.Ignore;
		bg.AddChild(fill);
	}

	public void UpdateUnitInfo(BattleUnit unit)
	{
		if (unit == null) { _unitCard.Visible = false; return; }
		_activeUnit = unit; _unitCard.Visible = true;
		PopulateUnitCard(_unitCard, unit, false);
	}

	public void UpdateTargetInfo(BattleUnit unit)
	{
		if (unit == null) { _targetCard.Visible = false; return; }
		_targetCard.Visible = true;
		PopulateUnitCard(_targetCard, unit, true);
	}

	// ═══════════════════════════════════════════════════════
	//  TURN ORDER BAR — top-center
	// ═══════════════════════════════════════════════════════

	void BuildTurnOrderBar()
	{
		// TO:R-style portrait strip — top center, horizontal cards
		_turnOrderBg = new PanelContainer();
		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = UITheme.IsDarkMode ? new Color(0.02f, 0.02f, 0.05f, 0.85f) : new Color(0f, 0f, 0f, 0.3f);
		bgStyle.SetCornerRadiusAll(4);
		bgStyle.SetContentMarginAll(4);
		bgStyle.ContentMarginLeft = 8; bgStyle.ContentMarginRight = 8;
		_turnOrderBg.AddThemeStyleboxOverride("panel", bgStyle);

		_turnOrderBg.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_turnOrderBg.OffsetLeft = 64; _turnOrderBg.OffsetRight = -64;
		_turnOrderBg.OffsetTop = 6; _turnOrderBg.OffsetBottom = 72;
		_root.AddChild(_turnOrderBg);

		_turnOrderBar = new HBoxContainer();
		_turnOrderBar.AddThemeConstantOverride("separation", 3);
		_turnOrderBar.Alignment = BoxContainer.AlignmentMode.Center;
		_turnOrderBg.AddChild(_turnOrderBar);

		// L/R scroll indicators (visual only for now)
		var lArrow = new Label();
		lArrow.Text = "◀";
		lArrow.AddThemeColorOverride("font_color", TxDark);
		lArrow.AddThemeFontSizeOverride("font_size", 14);
		lArrow.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
		lArrow.Position = new Vector2(66, 26);
		_root.AddChild(lArrow);

		var rArrow = new Label();
		rArrow.Text = "▶";
		rArrow.AddThemeColorOverride("font_color", TxDark);
		rArrow.AddThemeFontSizeOverride("font_size", 14);
		rArrow.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
		// We'll position this in the top area
		rArrow.AnchorLeft = 1f; rArrow.AnchorRight = 1f;
		rArrow.AnchorTop = 0f; rArrow.AnchorBottom = 0f;
		rArrow.OffsetLeft = -78; rArrow.OffsetTop = 26;
		_root.AddChild(rArrow);
	}

	public void UpdateTurnOrder(List<BattleUnit> ordered, BattleUnit active)
	{
		ClearChildren(_turnOrderBar);
		int ct = Math.Min(ordered.Count, 12);
		for (int i = 0; i < ct; i++)
			_turnOrderBar.AddChild(MakeTurnPortrait(ordered[i], ordered[i] == active, i));
	}

	/// <summary>TO:R-style portrait card — square with blue border for active unit.</summary>
	PanelContainer MakeTurnPortrait(BattleUnit u, bool active, int index)
	{
		var card = new PanelContainer();
		var tc = u.Team == UnitTeam.TeamA ? ColTeamA : ColTeamB;

		float cardSize = active ? 56 : 48;
		card.CustomMinimumSize = new Vector2(cardSize, 56);

		var style = new StyleBoxFlat();
		// Active unit: bright border, slightly raised. Others: dim
		if (active)
		{
			style.BgColor = new Color(tc, 0.25f);
			style.BorderColor = tc;
			style.SetBorderWidthAll(2);
		}
		else
		{
			style.BgColor = UITheme.IsDarkMode
				? new Color(0.05f, 0.05f, 0.10f, 0.85f)
				: new Color(0.12f, 0.12f, 0.18f, 0.85f);
			style.BorderColor = new Color(tc, 0.35f);
			style.SetBorderWidthAll(1);
		}
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(2);
		card.AddThemeStyleboxOverride("panel", style);

		var vb = new VBoxContainer();
		vb.AddThemeConstantOverride("separation", 1);
		vb.Alignment = BoxContainer.AlignmentMode.Center;
		card.AddChild(vb);

		// Portrait placeholder — colored square representing the unit
		var portrait = new Panel();
		portrait.CustomMinimumSize = new Vector2(active ? 40 : 34, active ? 34 : 28);
		portrait.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		var pStyle = new StyleBoxFlat();
		pStyle.BgColor = new Color(tc, active ? 0.5f : 0.3f);
		pStyle.SetCornerRadiusAll(3);
		portrait.AddThemeStyleboxOverride("panel", pStyle);
		vb.AddChild(portrait);

		// Unit initial inside portrait
		var initial = new Label();
		initial.Text = u.Name.Length > 0 ? u.Name[..1].ToUpper() : "?";
		initial.HorizontalAlignment = HorizontalAlignment.Center;
		initial.VerticalAlignment = VerticalAlignment.Center;
		initial.AddThemeColorOverride("font_color", active ? TxBright : TxPrimary);
		initial.AddThemeFontSizeOverride("font_size", active ? 16 : 13);
		initial.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		initial.MouseFilter = Control.MouseFilterEnum.Ignore;
		portrait.AddChild(initial);

		// Name label below portrait
		var nameLabel = new Label();
		nameLabel.Text = u.Name.Length > 6 ? u.Name[..5] + "." : u.Name;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AddThemeColorOverride("font_color", active ? TxBright : TxDim);
		nameLabel.AddThemeFontSizeOverride("font_size", active ? 10 : 9);
		nameLabel.CustomMinimumSize = new Vector2(0, 12);
		vb.AddChild(nameLabel);

		// Active unit gets a small marker triangle below
		if (active)
		{
			var marker = new Label();
			marker.Text = "▼";
			marker.HorizontalAlignment = HorizontalAlignment.Center;
			marker.AddThemeColorOverride("font_color", ColGold);
			marker.AddThemeFontSizeOverride("font_size", 8);
			marker.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
			marker.OffsetTop = -2;
			card.AddChild(marker);
		}

		return card;
	}

	// ═══════════════════════════════════════════════════════
	//  MOVE CONFIRM PANEL — appears when tile selected
	// ═══════════════════════════════════════════════════════

	void BuildMoveConfirm()
	{
		_moveConfirmPanel = MakePanel(BgPanel, BdPrimary);
		_moveConfirmPanel.AnchorLeft = 0.5f; _moveConfirmPanel.AnchorRight = 0.5f;
		_moveConfirmPanel.AnchorBottom = 1f; _moveConfirmPanel.AnchorTop = 1f;
		_moveConfirmPanel.OffsetLeft = -90; _moveConfirmPanel.OffsetRight = 90;
		_moveConfirmPanel.OffsetTop = -(ChatHeight + 50); _moveConfirmPanel.OffsetBottom = -(ChatHeight + 4);
		_moveConfirmPanel.Visible = false;
		_root.AddChild(_moveConfirmPanel);

		var hb = new HBoxContainer();
		hb.AddThemeConstantOverride("separation", 8);
		hb.Alignment = BoxContainer.AlignmentMode.Center;
		_moveConfirmPanel.AddChild(hb);

		_confirmBtn = new Button();
		_confirmBtn.Text = "✓ Confirm";
		_confirmBtn.CustomMinimumSize = new Vector2(80, 30);
		ApplyConfirmBtnTheme(_confirmBtn, true);
		_confirmBtn.Pressed += () => EmitSignal(SignalName.MoveConfirmed);
		hb.AddChild(_confirmBtn);

		_cancelBtn = new Button();
		_cancelBtn.Text = "✕ Cancel";
		_cancelBtn.CustomMinimumSize = new Vector2(80, 30);
		ApplyConfirmBtnTheme(_cancelBtn, false);
		_cancelBtn.Pressed += () => EmitSignal(SignalName.CommandCancelled);
		hb.AddChild(_cancelBtn);
	}

	void ApplyConfirmBtnTheme(Button btn, bool primary)
	{
		var col = primary ? new Color("44aa66") : new Color("aa4444");
		btn.AddThemeStyleboxOverride("normal", MakeBtnStyle(new Color(col, 0.2f), col));
		btn.AddThemeStyleboxOverride("hover", MakeBtnStyle(new Color(col, 0.35f), col));
		btn.AddThemeStyleboxOverride("focus", MakeBtnStyle(new Color(col, 0.35f), col));
		btn.AddThemeStyleboxOverride("pressed", MakeBtnStyle(new Color(col, 0.5f), col));
		btn.AddThemeColorOverride("font_color", TxBright);
		btn.AddThemeColorOverride("font_hover_color", TxBright);
		btn.AddThemeFontSizeOverride("font_size", 13);
	}

	public void ShowMoveConfirm(int tileX, int tileY)
	{
		_moveConfirmPanel.Visible = true;
	}

	public void HideMoveConfirm()
	{
		_moveConfirmPanel.Visible = false;
	}

	// ═══════════════════════════════════════════════════════
	//  PHASE LABEL + TILE INFO
	// ═══════════════════════════════════════════════════════

	void BuildPhaseLabel()
	{
		// Phase label — centered, below the turn order strip
		_phaseLabel = new Label();
		_phaseLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_phaseLabel.OffsetTop = 78;  // Below the 72px turn order strip
		_phaseLabel.OffsetBottom = 100;
		_phaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_phaseLabel.AddThemeColorOverride("font_color", UITheme.Accent);
		_phaseLabel.AddThemeFontSizeOverride("font_size", 16);
		_root.AddChild(_phaseLabel);
	}

	void BuildTileInfo()
	{
		_tileInfoLabel = new Label();
		_tileInfoLabel.AnchorLeft = 0.5f; _tileInfoLabel.AnchorRight = 0.5f;
		_tileInfoLabel.AnchorBottom = 1f; _tileInfoLabel.AnchorTop = 1f;
		_tileInfoLabel.OffsetTop = -(ChatHeight + 2); _tileInfoLabel.OffsetBottom = -(ChatHeight - 14);
		_tileInfoLabel.OffsetLeft = -100; _tileInfoLabel.OffsetRight = 100;
		_tileInfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_tileInfoLabel.AddThemeColorOverride("font_color", TxDim);
		_tileInfoLabel.AddThemeFontSizeOverride("font_size", 11);
		_tileInfoLabel.Visible = false;
		_root.AddChild(_tileInfoLabel);
	}

	public void ShowTileInfo(GridTile t)
	{
		if (t == null) { _tileInfoLabel.Visible = false; return; }
		_tileInfoLabel.Visible = true;
		_tileInfoLabel.Text = $"({t.X},{t.Y}) · H:{t.Height} · {t.Terrain}";
	}

	public void SetPhaseText(string t) => _phaseLabel.Text = t;

	// ═══════════════════════════════════════════════════════
	//  INSPECT SCREEN (Tab / right-click / Status)
	// ═══════════════════════════════════════════════════════

	void BuildInspectScreen()
	{
		_inspectOverlay = new Control();
		_inspectOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_inspectOverlay.Visible = false;
		AddChild(_inspectOverlay);

		var dim = new ColorRect(); dim.Color = BgDim;
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_inspectOverlay.AddChild(dim);

		_inspectPanel = new PanelContainer();
		var s = new StyleBoxFlat();
		s.BgColor = UITheme.IsDarkMode ? new Color(0.04f, 0.047f, 0.078f, 0.96f) : new Color(1f, 1f, 1f, 0.97f);
		s.BorderColor = BdSubtle; s.SetBorderWidthAll(1); s.SetCornerRadiusAll(8);
		s.SetContentMarginAll(0);
		_inspectPanel.AddThemeStyleboxOverride("panel", s);
		_inspectPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_inspectPanel.CustomMinimumSize = new Vector2(900, 520);
		_inspectPanel.Position = new Vector2(-450, -260);
		_inspectOverlay.AddChild(_inspectPanel);
	}

	void PopulateInspect(BattleUnit u)
	{
		ClearChildren(_inspectPanel);
		var hbox = new HBoxContainer(); hbox.AddThemeConstantOverride("separation", 0);
		_inspectPanel.AddChild(hbox);

		// ─── PORTRAIT ───
		var pCol = new PanelContainer();
		var pSt = new StyleBoxFlat();
		pSt.BgColor = UITheme.IsDarkMode ? new Color(0.04f, 0.04f, 0.06f, 0.5f) : new Color(0.96f, 0.96f, 0.98f);
		pSt.BorderWidthRight = 1; pSt.BorderColor = BdDim; pSt.SetContentMarginAll(20);
		pCol.AddThemeStyleboxOverride("panel", pSt); pCol.CustomMinimumSize = new Vector2(260, 520);
		hbox.AddChild(pCol);

		var pvb = new VBoxContainer(); pvb.AddThemeConstantOverride("separation", 4); pCol.AddChild(pvb);

		// Frame
		var frame = MakePanel(BtnNormal, new Color(UITheme.Accent, 0.2f));
		frame.CustomMinimumSize = new Vector2(220, 200); pvb.AddChild(frame);
		var ph = new Label(); ph.Text = "⚔"; ph.HorizontalAlignment = HorizontalAlignment.Center;
		ph.VerticalAlignment = VerticalAlignment.Center;
		ph.AddThemeFontSizeOverride("font_size", 64); ph.AddThemeColorOverride("font_color", new Color(UITheme.Accent, 0.3f));
		frame.AddChild(ph);

		int lvl = (u.Strength + u.Speed + u.Agility + u.Endurance + u.Stamina + u.EtherControl) / 6;
		MakeLabel(pvb, u.Name, TxBright, 20, HorizontalAlignment.Center);
		MakeLabel(pvb, "▸ Combatant", UITheme.Accent, 13, HorizontalAlignment.Center);
		MakeLabel(pvb, $"Lv. {lvl} · {u.Team}", TxDark, 12, HorizontalAlignment.Center);

		var bars = new VBoxContainer(); bars.AddThemeConstantOverride("separation", 2); pvb.AddChild(bars);
		AddInspectBarRow(bars, "HP", u.CurrentHp, u.MaxHp, true);
		AddInspectBarRow(bars, "AE", u.CurrentAether, u.MaxAether, false);

		// ─── STATS ───
		var sCol = new PanelContainer();
		var sSt = new StyleBoxFlat(); sSt.BgColor = Colors.Transparent;
		sSt.BorderWidthRight = 1; sSt.BorderColor = BdDim; sSt.SetContentMarginAll(20);
		sCol.AddThemeStyleboxOverride("panel", sSt); sCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(sCol);

		var svb = new VBoxContainer(); svb.AddThemeConstantOverride("separation", 2); sCol.AddChild(svb);

		AddSection(svb, "◆ EQUIPMENT");
		AddEquip(svb, "Weapon", "Ether-Forged Blade", "★★");
		AddEquip(svb, "Accessory", "Speed Amulet", "★");
		AddEquip(svb, "Accessory", "Warden's Ring", "★★★");

		AddSection(svb, "◆ TRAINING STATS");
		var sg = new GridContainer(); sg.Columns = 2;
		sg.AddThemeConstantOverride("h_separation", 16); sg.AddThemeConstantOverride("v_separation", 1);
		svb.AddChild(sg);
		AddStat(sg, "Strength", u.Strength); AddStat(sg, "Vitality", u.Vitality);
		AddStat(sg, "Agility", u.Agility); AddStat(sg, "Dexterity", u.Dexterity);
		AddStat(sg, "Stamina", u.Stamina); AddStat(sg, "Ether Ctrl", u.EtherControl);

		AddSection(svb, "◆ DERIVED");
		var dg = new GridContainer(); dg.Columns = 2;
		dg.AddThemeConstantOverride("h_separation", 16); dg.AddThemeConstantOverride("v_separation", 1);
		svb.AddChild(dg);
		AddStat(dg, "ATK", u.Atk); AddStat(dg, "DEF", u.Def);
		AddStat(dg, "EATK", u.Eatk); AddStat(dg, "EDEF", u.Edef);
		AddStat(dg, "AVD", u.Avd); AddStat(dg, "ACC", u.Acc);
		AddStat(dg, "MOV", u.Move); AddStat(dg, "JMP", u.Jump);
		AddStat(dg, "Crit%", (int)u.CritPercent); AddStat(dg, "Base WT", u.BaseWt);

		AddSection(svb, "◆ RESISTANCES");
		var rr = new HBoxContainer(); rr.AddThemeConstantOverride("separation", 16); svb.AddChild(rr);
		foreach (var r in new[] { "🔥 0%", "❄ 0%", "⚡ 0%", "🌿 0%", "💀 0%" })
			MakeLabel(rr, r, TxDark, 11);

		// ─── ABILITIES ───
		var aP = new PanelContainer();
		var aSt = new StyleBoxFlat(); aSt.BgColor = Colors.Transparent; aSt.SetContentMarginAll(20);
		aP.AddThemeStyleboxOverride("panel", aSt); aP.CustomMinimumSize = new Vector2(260, 0);
		hbox.AddChild(aP);
		var avb = new VBoxContainer(); avb.AddThemeConstantOverride("separation", 2); aP.AddChild(avb);

		AddSection(avb, "◆ ABILITIES");
		foreach (var ab in _abilities)
		{
			var ar = new HBoxContainer(); ar.AddThemeConstantOverride("separation", 8);
			MakeLabel(ar, ab.Icon, CatColor(ab.Category), 12);
			var n = new Label(); n.Text = ab.Name; n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			n.AddThemeColorOverride("font_color", TxPrimary); n.AddThemeFontSizeOverride("font_size", 12);
			ar.AddChild(n);
			MakeLabel(ar, new string('◆', Math.Clamp(ab.EtherCost / 15, 1, 3)), ColGold, 10);
			avb.AddChild(ar);
		}
		AddSection(avb, "◆ WEAPON SKILL");
		MakeLabel(avb, "⊘ Locked", TxDisabled, 12);

		// Close button
		var cls = UITheme.CreateCloseButton();
		cls.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		cls.Position = new Vector2(-40, 12);
		cls.Pressed += () => ToggleInspect(false);
		_inspectPanel.AddChild(cls);
	}

	// ═══════════════════════════════════════════════════════
	//  CROWN BUTTON — access main menus during battle
	// ═══════════════════════════════════════════════════════

	Button _crownBtn;

	void BuildCrownButton()
	{
		// Crown button — top-left corner, standalone
		_crownBtn = new Button();
		_crownBtn.TooltipText = "Main Menu";
		_crownBtn.CustomMinimumSize = new Vector2(44, 44);
		_crownBtn.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_crownBtn.Position = new Vector2(12, 8);

		var crownIcon = new TextureRect();
		crownIcon.CustomMinimumSize = new Vector2(28, 28);
		crownIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		crownIcon.SetAnchorsPreset(Control.LayoutPreset.Center);
		crownIcon.GrowHorizontal = Control.GrowDirection.Both;
		crownIcon.GrowVertical = Control.GrowDirection.Both;
		crownIcon.MouseFilter = Control.MouseFilterEnum.Ignore;
		if (ResourceLoader.Exists("res://Assets/Icons/icon_gold_crown.png"))
			crownIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/icon_gold_crown.png");
		else if (ResourceLoader.Exists("res://Assets/Icons/icon_crown.png"))
			crownIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/icon_crown.png");
		_crownBtn.AddChild(crownIcon);

		var normal = new StyleBoxFlat();
		normal.BgColor = BgPanel;
		normal.SetCornerRadiusAll(6);
		normal.SetContentMarginAll(6);
		normal.BorderColor = BdSubtle;
		normal.SetBorderWidthAll(1);
		_crownBtn.AddThemeStyleboxOverride("normal", normal);

		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = UITheme.IsDarkMode ? new Color(0.1f, 0.1f, 0.18f, 0.95f) : new Color(0.95f, 0.95f, 1f, 0.98f);
		hover.BorderColor = UITheme.Accent;
		_crownBtn.AddThemeStyleboxOverride("hover", hover);
		_crownBtn.AddThemeStyleboxOverride("focus", hover);

		_crownBtn.Pressed += OnCrownPressed;
		_root.AddChild(_crownBtn);
	}

	void OnCrownPressed()
	{
		// Find and toggle the overworld sidebar visibility
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		var hud = scene.FindChild("OverworldHUD", true, false) as Control;
		if (hud == null) return;
		foreach (var child in hud.GetChildren())
		{
			if (child is VBoxContainer vb)
			{
				vb.Visible = !vb.Visible;
				break;
			}
		}
	}

	// ═══════════════════════════════════════════════════════
	//  PUBLIC API
	// ═══════════════════════════════════════════════════════

	public void ShowCommandMenu(BattleUnit unit)
	{
		_activeUnit = unit; _menuOpen = true; _cmdIndex = 0;
		_cmdPanel.Visible = true;
		SetEnabled("Move", !unit.HasMoved);
		SetEnabled("Attack", !unit.HasActed);
		SetEnabled("Ability", !unit.HasActed && unit.CurrentAether > 0);
		SetEnabled("Item", !unit.HasActed);
		SetEnabled("Defend", !unit.HasActed);
		SetEnabled("Flee", true);
		SetEnabled("End Turn", true);
		SetEnabled("Status", true);
		CloseSubMenus(); SelectCmd(0); UpdateUnitInfo(unit);
	}

	public void HideCommandMenu() { _menuOpen = false; _cmdPanel.Visible = false; CloseSubMenus(); HideMoveConfirm(); }

	public void InspectUnit(BattleUnit unit)
	{
		if (unit == null) return;
		_inspectOpen = true; _inspectOverlay.Visible = true;
		PopulateInspect(unit);
	}

	// ═══════════════════════════════════════════════════════
	//  INPUT
	// ═══════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed) return;

		if (key.Keycode == Key.Tab) { if (_activeUnit != null) { ToggleInspect(!_inspectOpen); } GetViewport().SetInputAsHandled(); return; }

		if (_inspectOpen) { if (key.Keycode == Key.Escape) { ToggleInspect(false); GetViewport().SetInputAsHandled(); } return; }
		if (!_menuOpen) return;

		if (_subMenuOpen)
		{
			var btns = _activeSubType == "Ability" ? _abilityBtns : _itemBtns;
			switch (key.Keycode)
			{
				case Key.Up: case Key.W: NavSub(-1, btns); GetViewport().SetInputAsHandled(); break;
				case Key.Down: case Key.S: NavSub(1, btns); GetViewport().SetInputAsHandled(); break;
				case Key.Enter: case Key.Space:
					if (_activeSubType == "Ability") OnAbilityClicked(_subIndex); else OnItemClicked(_subIndex);
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

		int n = key.Keycode switch { Key.Key1=>0, Key.Key2=>1, Key.Key3=>2, Key.Key4=>3, Key.Key5=>4, Key.Key6=>5, Key.Key7=>6, Key.Key8=>7, _=>-1 };
		if (n >= 0 && n < CmdNames.Length) { OnCmdPressed(n); GetViewport().SetInputAsHandled(); }
	}

	// ═══════════════════════════════════════════════════════
	//  NAV LOGIC
	// ═══════════════════════════════════════════════════════

	void SelectCmd(int i)
	{
		_cmdIndex = i;
		for (int j = 0; j < _cmdBtns.Count; j++)
			if (j == i && !_cmdBtns[j].Disabled) _cmdBtns[j].GrabFocus();
		string cmd = CmdNames[i];
		_abilityPanel.Visible = cmd == "Ability" && !_cmdBtns[i].Disabled;
		_itemPanel.Visible = cmd == "Item" && !_cmdBtns[i].Disabled;
		_tooltipPanel.Visible = cmd == "Ability" && _abilities.Count > 0 && !_cmdBtns[i].Disabled;
		if (cmd == "Ability" && _abilities.Count > 0) UpdateTooltip(0);
	}

	void NavCmd(int d) { int n = _cmdIndex; int a = CmdNames.Length; do { n = (n+d+a)%a; a--; } while (_cmdBtns[n].Disabled && a > 0); SelectCmd(n); }
	void SelectSubItem(int i) { _subIndex = i; var b = _activeSubType == "Ability" ? _abilityBtns : _itemBtns; for (int j = 0; j < b.Count; j++) if (j == i && !b[j].Disabled) b[j].GrabFocus(); }
	void NavSub(int d, List<Button> b) { int n = _subIndex; int a = b.Count; do { n = (n+d+a)%a; a--; } while (b[n].Disabled && a > 0); _subIndex = n; SelectSubItem(n); if (_activeSubType == "Ability") UpdateTooltip(n); }

	void OnCmdPressed(int i)
	{
		if (_cmdBtns[i].Disabled) return;
		string cmd = CmdNames[i];
		if (cmd == "Status") { ToggleInspect(true); return; }
		if (cmd == "Ability") { _subMenuOpen = true; _subIndex = 0; _activeSubType = "Ability"; _abilityPanel.Visible = true; _itemPanel.Visible = false; if (_abilityBtns.Count > 0) SelectSubItem(0); if (_abilities.Count > 0) UpdateTooltip(0); return; }
		if (cmd == "Item") { _subMenuOpen = true; _subIndex = 0; _activeSubType = "Item"; _itemPanel.Visible = true; _abilityPanel.Visible = false; _tooltipPanel.Visible = false; if (_itemBtns.Count > 0) SelectSubItem(0); return; }
		CloseSubMenus(); EmitSignal(SignalName.CommandSelected, cmd);
	}

	void OnAbilityClicked(int i) { if (i >= 0 && i < _abilities.Count && _abilities[i].IsUsable) { CloseSubMenus(); EmitSignal(SignalName.AbilitySelected, i); } }
	void OnItemClicked(int i) { if (i >= 0 && i < _items.Count && _items[i].IsUsable) { CloseSubMenus(); EmitSignal(SignalName.ItemSelected, i); } }
	void CloseSubMenus() { _subMenuOpen = false; _abilityPanel.Visible = false; _itemPanel.Visible = false; _tooltipPanel.Visible = false; }
	void ToggleInspect(bool o) { _inspectOpen = o; _inspectOverlay.Visible = o; if (o && _activeUnit != null) PopulateInspect(_activeUnit); }
	void SetEnabled(string nm, bool en) { int i = Array.IndexOf(CmdNames, nm); if (i >= 0 && i < _cmdBtns.Count) _cmdBtns[i].Disabled = !en; }

	// ═══════════════════════════════════════════════════════
	//  UI HELPERS
	// ═══════════════════════════════════════════════════════

	PanelContainer MakePanel(Color bg, Color bd)
	{
		var p = new PanelContainer(); var s = new StyleBoxFlat();
		s.BgColor = bg; s.BorderColor = bd; s.SetBorderWidthAll(1); s.SetCornerRadiusAll(6); s.SetContentMarginAll(8);
		p.AddThemeStyleboxOverride("panel", s); return p;
	}

	void ApplyBtnTheme(Button btn)
	{
		btn.AddThemeStyleboxOverride("normal", MakeBtnStyle(BtnNormal, Colors.Transparent));
		btn.AddThemeStyleboxOverride("hover", MakeBtnStyle(BtnHover, UITheme.Accent));
		btn.AddThemeStyleboxOverride("focus", MakeBtnStyle(BtnHover, UITheme.Accent));
		btn.AddThemeStyleboxOverride("pressed", MakeBtnStyle(new Color(UITheme.Accent, 0.45f), Colors.Transparent));
		btn.AddThemeStyleboxOverride("disabled", MakeBtnStyle(BtnDisabled, Colors.Transparent));
		btn.AddThemeColorOverride("font_color", TxPrimary);
		btn.AddThemeColorOverride("font_hover_color", TxBright);
		btn.AddThemeColorOverride("font_focus_color", TxBright);
		btn.AddThemeColorOverride("font_disabled_color", TxDisabled);
		btn.AddThemeFontSizeOverride("font_size", 14);
	}

	static StyleBoxFlat MakeBtnStyle(Color bg, Color bd)
	{
		var s = new StyleBoxFlat(); s.BgColor = bg; s.SetCornerRadiusAll(4); s.SetContentMarginAll(4);
		if (bd != Colors.Transparent) { s.BorderColor = bd; s.SetBorderWidthAll(1); }
		return s;
	}

	static Label MakeLabel(Control p, string text, Color col, int size, HorizontalAlignment a = HorizontalAlignment.Left)
	{
		var l = new Label(); l.Text = text; l.HorizontalAlignment = a;
		l.AddThemeColorOverride("font_color", col); l.AddThemeFontSizeOverride("font_size", size);
		p.AddChild(l); return l;
	}

	static void MakeSep(Control p)
	{
		var s = new HSeparator(); var st = new StyleBoxLine(); st.Color = BdDim; st.Thickness = 1;
		s.AddThemeStyleboxOverride("separator", st); p.AddChild(s);
	}

	void AddCompactBar(VBoxContainer par, string label, int cur, int max, bool hp)
	{
		var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4); par.AddChild(h);
		MakeLabel(h, label, TxDark, 10);

		// Track background
		var bg = new Panel(); bg.CustomMinimumSize = new Vector2(120, 10); bg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var bgs = new StyleBoxFlat(); bgs.BgColor = UITheme.IsDarkMode ? new Color(0.1f, 0.1f, 0.15f, 0.8f) : new Color(0, 0, 0, 0.06f);
		bgs.SetCornerRadiusAll(2); bg.AddThemeStyleboxOverride("panel", bgs); h.AddChild(bg);

		// Fill bar using anchors
		float pct = max > 0 ? Mathf.Clamp((float)cur / max, 0f, 1f) : 0;
		var fill = new Panel();
		fill.AnchorLeft = 0; fill.AnchorRight = pct;
		fill.AnchorTop = 0; fill.AnchorBottom = 1;
		fill.OffsetLeft = 1; fill.OffsetRight = -1; fill.OffsetTop = 1; fill.OffsetBottom = -1;
		var fs = new StyleBoxFlat(); fs.BgColor = hp ? (pct > 0.5f ? ColHpFull : pct > 0.25f ? ColHpMid : ColHpLow) : ColEther;
		fs.SetCornerRadiusAll(2); fill.AddThemeStyleboxOverride("panel", fs); fill.MouseFilter = Control.MouseFilterEnum.Ignore;
		bg.AddChild(fill);

		MakeLabel(h, $"{cur}/{max}", TxDim, 10);
	}

	void AddInspectBarRow(VBoxContainer par, string label, int cur, int max, bool hp)
	{
		var r = new HBoxContainer(); MakeLabel(r, label, TxDark, 11);
		var sp = new Control(); sp.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; r.AddChild(sp);
		MakeLabel(r, $"{cur} / {max}", TxDim, 11); par.AddChild(r);
		float pct = max > 0 ? (float)cur / max : 0;
		var bg = new Panel(); bg.CustomMinimumSize = new Vector2(220, 8);
		var bgs = new StyleBoxFlat(); bgs.BgColor = UITheme.IsDarkMode ? new Color(0.1f,0.1f,0.15f,0.8f) : new Color(0,0,0,0.06f);
		bgs.SetCornerRadiusAll(2); bg.AddThemeStyleboxOverride("panel", bgs); par.AddChild(bg);
		var f = new Panel(); f.SetAnchorsPreset(Control.LayoutPreset.LeftWide); f.Size = new Vector2(220*pct, 8);
		var fs = new StyleBoxFlat(); fs.BgColor = hp ? (pct > 0.5f ? ColHpFull : pct > 0.25f ? ColHpMid : ColHpLow) : ColEther;
		fs.SetCornerRadiusAll(2); f.AddThemeStyleboxOverride("panel", fs); bg.AddChild(f);
	}

	void AddSection(VBoxContainer p, string t) { var sp = new Control(); sp.CustomMinimumSize = new Vector2(0,6); p.AddChild(sp); MakeLabel(p, t, UITheme.Accent, 11); var s = new HSeparator(); var st = new StyleBoxLine(); st.Color = new Color(UITheme.Accent, 0.15f); st.Thickness = 1; s.AddThemeStyleboxOverride("separator", st); p.AddChild(s); }
	void AddEquip(VBoxContainer p, string slot, string name, string stars) { var h = new HBoxContainer(); p.AddChild(h); var sl = new Label(); sl.Text = slot; sl.CustomMinimumSize = new Vector2(80,0); sl.AddThemeColorOverride("font_color", TxDark); sl.AddThemeFontSizeOverride("font_size", 12); h.AddChild(sl); var nl = new Label(); nl.Text = name; nl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; nl.AddThemeColorOverride("font_color", TxPrimary); nl.AddThemeFontSizeOverride("font_size", 12); h.AddChild(nl); MakeLabel(h, stars, ColGold, 10); }
	void AddStat(GridContainer g, string nm, int v) { var r = new HBoxContainer(); r.CustomMinimumSize = new Vector2(140,20); var n = new Label(); n.Text = nm; n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; n.AddThemeColorOverride("font_color", TxDim); n.AddThemeFontSizeOverride("font_size", 12); r.AddChild(n); var vl = new Label(); vl.Text = v.ToString(); vl.AddThemeColorOverride("font_color", TxPrimary); vl.AddThemeFontSizeOverride("font_size", 12); r.AddChild(vl); g.AddChild(r); }
	static void ClearChildren(Control n) { foreach (var c in n.GetChildren()) c.QueueFree(); }
	static Color CatColor(string c) => c switch { "ether" => new("aa88ff"), "phys" => new("ff8855"), "heal" => new("55cc77"), "buff" => new("ddcc55"), _ => new("66bbdd") };
	static string Cap(string s) => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s;
}
