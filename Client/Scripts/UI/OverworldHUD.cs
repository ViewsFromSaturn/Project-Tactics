using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.UI;

/// <summary>
/// Overworld HUD — identity bar, sidebar icons, floating window manager,
/// chat bubble, global UI focus tracking, theme change support.
/// </summary>
public partial class OverworldHUD : Control
{
	// ═══ GLOBAL UI FOCUS ═══
	public static bool IsAnyTextFieldFocused { get; private set; } = false;

	/// <summary>Singleton reference for external callers (ProfilePopup, etc.).</summary>
	public static OverworldHUD Instance { get; private set; }

	/// <summary>Open a panel by id from external code.</summary>
	public void OpenPanel(string id)
	{
		if (!_openWindows.ContainsKey(id))
			ToggleWindow(id);
	}

	/// <summary>Close a panel by id from external code.</summary>
	public void ClosePanel(string id)
	{
		if (_openWindows.ContainsKey(id))
			ToggleWindow(id);
	}

	private Label _nameLabel, _rankLabel, _tpLabel;
	private TextureRect _hpFill, _staFill, _ethFill;
	private Label _hpValue, _staValue, _ethValue;
	private const float BarTrackWidth = 160f;

	private PanelContainer _identityBarPanel;
	private VBoxContainer _sidebarContainer;
	private float _hudIdleTimer = 0f;
	private const float HudIdleTimeout = 8f;

	private readonly Dictionary<string, FloatingWindow> _openWindows = new();

	private readonly List<(string id, string iconPath, string tooltip, Key hotkey)> _panelDefs = new()
	{
		("charsheet", "res://Assets/Icons/icon_character.png",   "Character Sheet  [C]",   Key.C),
		("training",  "res://Assets/Icons/icon_training.png",    "Daily Training  [V]",    Key.V),
		("journal",   "res://Assets/Icons/icon_journal.png",     "Journal  [J]",            Key.J),
		("chronicle", "res://Assets/Icons/icon_chronicle.png",   "Chronicle Keeper  [K]",   Key.K),
		("map",       "res://Assets/Icons/icon_map.png",          "World Map  [M]",          Key.M),
		("mentor",    "res://Assets/Icons/icon_mentorship.png",  "Mentorship  [N]",        Key.N),
		("inventory", "res://Assets/Icons/icon_inventory.png",    "Inventory  [I]",          Key.I),
		("settings",  "res://Assets/Icons/icon_settings.png",    "Settings  [Esc]",        Key.Escape),
	};

	private Label _chatBubble;
	private PanelContainer _chatBubbleBg;

	public override void _Ready()
	{
		Instance = this;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildIdentityBar();
		BuildSidebar();
		HideDebugOverlay();
		SetupChatBubble();
		UITheme.ThemeChanged += OnThemeChanged;
	}

	public override void _ExitTree()
	{
		UITheme.ThemeChanged -= OnThemeChanged;
	}

	public override void _Process(double delta)
	{
		UpdateBars();
		UpdateGlobalFocusState();

		_hudIdleTimer += (float)delta;
		bool isIdle = _hudIdleTimer > HudIdleTimeout && !ChatPanel.IsUiFocused;
		float targetAlpha = isIdle ? 0.35f : 1.0f;

		if (_identityBarPanel != null)
			_identityBarPanel.Modulate = _identityBarPanel.Modulate.Lerp(new Color(1, 1, 1, targetAlpha), (float)delta * 3);
		if (_sidebarContainer != null)
			_sidebarContainer.Modulate = _sidebarContainer.Modulate.Lerp(new Color(1, 1, 1, isIdle ? 0.3f : 1f), (float)delta * 3);
	}

	public override void _Input(InputEvent ev)
	{
		if (ev is InputEventMouseMotion || ev is InputEventKey)
			_hudIdleTimer = 0f;
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (ChatPanel.IsUiFocused || IsAnyTextFieldFocused) return;

		// F5: Enter test battle (overlay on top of overworld)
		if (key.Keycode == Key.F5)
		{
			// Check if battle already running
			var existing = GetTree().Root.FindChild("Battle", true, false);
			if (existing != null)
			{
				GD.Print("[HUD] Battle already running, removing...");
				existing.QueueFree();
				return;
			}

			GD.Print("[HUD] Starting test battle overlay...");
			var battleScene = new Combat.BattleManager();
			battleScene.Name = "Battle";
			GetTree().CurrentScene.AddChild(battleScene);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode == Key.Escape)
		{
			if (_openWindows.Count > 0) CloseAllWindows();
			else ToggleWindow("settings");
			GetViewport().SetInputAsHandled();
			return;
		}

		foreach (var (id, _, _, hotkey) in _panelDefs)
		{
			if (key.Keycode == hotkey && hotkey != Key.Escape)
			{
				ToggleWindow(id);
				GetViewport().SetInputAsHandled();
				return;
			}
		}
	}

	// ═══ GLOBAL FOCUS ═══

	private void UpdateGlobalFocusState()
	{
		var focused = GetViewport()?.GuiGetFocusOwner();
		IsAnyTextFieldFocused = focused is LineEdit || focused is TextEdit;
	}

	// ═══ THEME CHANGE ═══

	private void OnThemeChanged(bool dark)
	{
		// Rebuild identity bar + sidebar with new colors
		if (_identityBarPanel != null) { _identityBarPanel.QueueFree(); _identityBarPanel = null; }
		BuildIdentityBar();

		if (_sidebarContainer != null) { _sidebarContainer.QueueFree(); _sidebarContainer = null; }
		BuildSidebar();

		// FloatingWindows handle their own repaint via UITheme.ThemeChanged
	}

	// ═══ IDENTITY BAR ═══

	private void BuildIdentityBar()
	{
		var panel = new PanelContainer();
		_identityBarPanel = panel;
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
		panel.CustomMinimumSize = new Vector2(280, 0);
		panel.MouseFilter = MouseFilterEnum.Stop;

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = UITheme.BgPanel;
		panelStyle.CornerRadiusBottomRight = 12;
		panelStyle.BorderWidthRight = 1;
		panelStyle.BorderWidthBottom = 1;
		panelStyle.BorderColor = UITheme.BorderSubtle;
		panelStyle.ShadowColor = UITheme.Shadow;
		panelStyle.ShadowSize = 8;
		panelStyle.ShadowOffset = new Vector2(2, 2);
		panelStyle.ContentMarginLeft = 12;
		panelStyle.ContentMarginRight = 18;
		panelStyle.ContentMarginTop = 8;
		panelStyle.ContentMarginBottom = 10;
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.MouseEntered += () => _hudIdleTimer = 0f;
		AddChild(panel);

		var outerVbox = new VBoxContainer();
		outerVbox.AddThemeConstantOverride("separation", 4);
		panel.AddChild(outerVbox);

		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 8);
		outerVbox.AddChild(topRow);

		var crownIcon = new TextureRect();
		crownIcon.CustomMinimumSize = new Vector2(24, 24);
		crownIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		crownIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		if (ResourceLoader.Exists("res://Assets/Icons/icon_gold_crown.png"))
			crownIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/icon_gold_crown.png");
		else if (ResourceLoader.Exists("res://Assets/Icons/icon_crown.png"))
			crownIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/icon_crown.png");
		topRow.AddChild(crownIcon);

		var nameStack = new VBoxContainer();
		nameStack.AddThemeConstantOverride("separation", 0);
		nameStack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		topRow.AddChild(nameStack);

		_nameLabel = new Label();
		_nameLabel.Text = "...";
		_nameLabel.AddThemeFontSizeOverride("font_size", 16);
		_nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) _nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		nameStack.AddChild(_nameLabel);

		_rankLabel = new Label();
		_rankLabel.Text = "";
		_rankLabel.AddThemeFontSizeOverride("font_size", 11);
		_rankLabel.AddThemeColorOverride("font_color", UITheme.AccentGold);
		if (UITheme.FontBody != null) _rankLabel.AddThemeFontOverride("font", UITheme.FontBody);
		nameStack.AddChild(_rankLabel);

		var tpBox = new HBoxContainer();
		tpBox.AddThemeConstantOverride("separation", 4);
		tpBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		topRow.AddChild(tpBox);

		var tpDot = new ColorRect();
		tpDot.CustomMinimumSize = new Vector2(5, 5);
		tpDot.Color = UITheme.Accent;
		tpDot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		tpBox.AddChild(tpDot);

		_tpLabel = new Label();
		_tpLabel.Text = "0 TP";
		_tpLabel.AddThemeFontSizeOverride("font_size", 11);
		_tpLabel.AddThemeColorOverride("font_color", UITheme.Accent);
		if (UITheme.FontNumbersMedium != null) _tpLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		tpBox.AddChild(_tpLabel);

		var barsVbox = new VBoxContainer();
		barsVbox.AddThemeConstantOverride("separation", 3);
		outerVbox.AddChild(barsVbox);

		(_hpFill, _hpValue) = CreateBar(barsVbox, "HP",
			new Color("B03030"), new Color("E04040"), new Color("F06060"));
		(_staFill, _staValue) = CreateBar(barsVbox, "STA",
			new Color("C08020"), new Color("E8A030"), new Color("F0B848"));
		(_ethFill, _ethValue) = CreateBar(barsVbox, "ETH",
			new Color("3060A0"), new Color("4080D0"), new Color("5898E0"));
	}

	private (TextureRect fill, Label value) CreateBar(VBoxContainer parent, string label,
		Color fillStart, Color fillMid, Color fillEnd)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.CustomMinimumSize = new Vector2(0, 14);
		parent.AddChild(row);

		var lbl = new Label();
		lbl.Text = label;
		lbl.CustomMinimumSize = new Vector2(26, 0);
		lbl.HorizontalAlignment = HorizontalAlignment.Right;
		lbl.AddThemeFontSizeOverride("font_size", 9);
		lbl.AddThemeColorOverride("font_color", fillMid);
		if (UITheme.FontNumbersMedium != null) lbl.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		row.AddChild(lbl);

		var track = new Panel();
		track.CustomMinimumSize = new Vector2(BarTrackWidth, 8);
		track.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		track.SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var trackStyle = new StyleBoxFlat();
		trackStyle.BgColor = UITheme.IsDarkMode
			? new Color(0.12f, 0.12f, 0.18f, 0.5f)
			: new Color(0, 0, 0, 0.08f);
		trackStyle.SetCornerRadiusAll(3);
		trackStyle.BorderColor = UITheme.IsDarkMode
			? new Color(1f, 1f, 1f, 0.04f)
			: new Color(0, 0, 0, 0.06f);
		trackStyle.SetBorderWidthAll(1);
		track.AddThemeStyleboxOverride("panel", trackStyle);
		row.AddChild(track);

		var fill = new TextureRect();
		fill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		fill.AnchorRight = 1.0f;
		fill.OffsetLeft = 1; fill.OffsetTop = 1;
		fill.OffsetRight = -1; fill.OffsetBottom = -1;
		fill.MouseFilter = MouseFilterEnum.Ignore;

		var gradTex = new GradientTexture2D();
		var grad = new Gradient();
		grad.SetColor(0, fillStart);
		grad.AddPoint(0.5f, fillMid);
		grad.SetColor(2, fillEnd);
		gradTex.Gradient = grad;
		gradTex.Width = 128; gradTex.Height = 4;
		fill.Texture = gradTex;
		fill.StretchMode = TextureRect.StretchModeEnum.Scale;
		track.AddChild(fill);

		var shine = new ColorRect();
		shine.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		shine.AnchorBottom = 0.4f;
		shine.OffsetLeft = 1;
		shine.Color = new Color(1f, 1f, 1f, 0.12f);
		shine.MouseFilter = MouseFilterEnum.Ignore;
		fill.AddChild(shine);

		var val = new Label();
		val.Text = "0/0";
		val.CustomMinimumSize = new Vector2(60, 0);
		val.HorizontalAlignment = HorizontalAlignment.Right;
		val.AddThemeFontSizeOverride("font_size", 10);
		val.AddThemeColorOverride("font_color", fillMid);
		if (UITheme.FontNumbersMedium != null) val.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		row.AddChild(val);

		return (fill, val);
	}

	// ═══ SIDEBAR ═══

	private void BuildSidebar()
	{
		var vbox = new VBoxContainer();
		_sidebarContainer = vbox;
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight);
		vbox.GrowHorizontal = GrowDirection.Begin;
		vbox.OffsetLeft = -52;
		vbox.OffsetRight = -8;
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.MouseEntered += () => _hudIdleTimer = 0f;
		AddChild(vbox);

		foreach (var (id, iconPath, tooltip, _) in _panelDefs)
		{
			var btn = CreateGlassIconButton(iconPath, tooltip);
			btn.Pressed += () => ToggleWindow(id);
			vbox.AddChild(btn);
		}
	}

	private Button CreateGlassIconButton(string iconPath, string tooltip)
	{
		var btn = new Button();
		btn.TooltipText = tooltip;
		btn.CustomMinimumSize = new Vector2(44, 44);
		btn.MouseFilter = MouseFilterEnum.Stop;
		btn.Text = "";

		var normal = new StyleBoxFlat();
		normal.BgColor = UITheme.SidebarBtnNormal;
		normal.SetCornerRadiusAll(10);
		normal.SetBorderWidthAll(1);
		normal.BorderColor = UITheme.SidebarBtnBorder;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = UITheme.SidebarBtnHover;
		hover.SetCornerRadiusAll(10);
		hover.SetBorderWidthAll(1);
		hover.BorderColor = UITheme.SidebarBtnBorder;
		btn.AddThemeStyleboxOverride("hover", hover);

		var pressed = new StyleBoxFlat();
		pressed.BgColor = UITheme.SidebarBtnPressed;
		pressed.SetCornerRadiusAll(10);
		pressed.SetBorderWidthAll(1);
		pressed.BorderColor = UITheme.SidebarBtnBorder;
		btn.AddThemeStyleboxOverride("pressed", pressed);

		if (ResourceLoader.Exists(iconPath))
		{
			var tex = GD.Load<Texture2D>(iconPath);
			if (tex != null)
			{
				btn.Icon = tex;
				btn.IconAlignment = HorizontalAlignment.Center;
				btn.ExpandIcon = true;
				btn.AddThemeConstantOverride("icon_max_width", 22);
			}
		}
		else
		{
			btn.Text = "?";
			btn.AddThemeFontSizeOverride("font_size", 16);
			btn.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		}

		return btn;
	}

	// ═══ WINDOW MANAGEMENT ═══

	private void ToggleWindow(string id)
	{
		if (_openWindows.TryGetValue(id, out var existing))
		{
			existing.CloseWindow();
			return;
		}

		Panels.WindowPanel panel = id switch
		{
			"charsheet" => new Panels.CharacterSheetPanel(),
			"training"  => new Panels.TrainingPanel(),
			"journal"   => new Panels.JournalPanel(),
			"chronicle" => new Panels.ChronicleKeeperPanel(),
			"map"       => new Panels.MapPanel(),
			"inventory" => new Panels.InventoryPanel(),
			"mentor"    => new Panels.MentorPanel(),
			"settings"       => new Panels.SettingsPanel(),
			"icprofile"      => new Panels.ICProfilePanel(editMode: true),
			"icprofile_view" => new Panels.ICProfilePanel(editMode: false),
			_ => null
		};

		if (panel == null) return;

		int offsetIndex = _openWindows.Count;
		Vector2 offset = new Vector2(30 * offsetIndex, 30 * offsetIndex);

		var window = FloatingWindow.Open(
			this, panel.PanelTitle, panel,
			panel.DefaultWidth, panel.DefaultHeight
		);

		panel.CallDeferred(nameof(Panels.WindowPanel.DeferredOpen));

		if (panel.DefaultPosition != Vector2.Zero)
			window.CallDeferred(nameof(FloatingWindow.SetWindowPosition), panel.DefaultPosition);
		else if (offsetIndex > 0)
		{
			var viewport = GetViewportRect().Size;
			var centered = (viewport - new Vector2(panel.DefaultWidth, panel.DefaultHeight)) / 2f;
			window.CallDeferred(nameof(FloatingWindow.SetWindowPosition), centered + offset);
		}

		_openWindows[id] = window;
		window.WindowClosed += () => _openWindows.Remove(id);
	}

	private void CloseAllWindows()
	{
		foreach (var (_, window) in new Dictionary<string, FloatingWindow>(_openWindows))
			window.CloseWindow();
	}

	// ═══ CHAT BUBBLE ═══

	private void SetupChatBubble() => CallDeferred(nameof(LinkChatBubble));

	private void LinkChatBubble()
	{
		var chatPanel = GetParent()?.GetNodeOrNull<ChatPanel>("ChatPanel");
		if (chatPanel == null) return;

		var player = GetTree().Root.GetNodeOrNull<Node2D>("Overworld/Player");
		if (player == null)
			player = GetTree().Root.FindChild("Player", true, false) as Node2D;

		if (player != null)
		{
			_chatBubble = new Label();
			_chatBubble.Text = "";
			_chatBubble.Visible = false;
			_chatBubble.HorizontalAlignment = HorizontalAlignment.Center;
			_chatBubble.AddThemeFontSizeOverride("font_size", 12);
			_chatBubble.AddThemeColorOverride("font_color", UITheme.Text);
			if (UITheme.FontBody != null) _chatBubble.AddThemeFontOverride("font", UITheme.FontBody);

			_chatBubbleBg = new PanelContainer();
			var bgStyle = new StyleBoxFlat();
			bgStyle.BgColor = UITheme.BgPanel;
			bgStyle.SetCornerRadiusAll(8);
			bgStyle.BorderColor = UITheme.BorderSubtle;
			bgStyle.SetBorderWidthAll(1);
			bgStyle.ShadowColor = UITheme.Shadow;
			bgStyle.ShadowSize = 4;
			bgStyle.ShadowOffset = new Vector2(0, 2);
			bgStyle.ContentMarginLeft = 10; bgStyle.ContentMarginRight = 10;
			bgStyle.ContentMarginTop = 4; bgStyle.ContentMarginBottom = 4;
			_chatBubbleBg.AddThemeStyleboxOverride("panel", bgStyle);
			_chatBubbleBg.Visible = false;
			_chatBubbleBg.Name = "BubbleBg";
			_chatBubbleBg.AddChild(_chatBubble);

			player.AddChild(_chatBubbleBg);
			chatPanel.ChatBubbleLabel = _chatBubble;
			chatPanel.ChatBubbleBg = _chatBubbleBg;
		}
	}

	private void HideDebugOverlay()
	{
		var debugLayer = GetTree().Root.FindChild("DebugOverlay", true, false) as Control;
		if (debugLayer != null)
		{
			debugLayer.Visible = false;
			GD.Print("[OverworldHUD] Debug overlay hidden. Press F1 to re-toggle.");
		}
	}

	// ═══ UPDATE ═══

	private void UpdateBars()
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return;

		_nameLabel.Text = p.CharacterName;
		_rankLabel.Text = p.RpRank ?? "";
		_tpLabel.Text = $"{p.TrainingPointsBank} TP";

		float hpPct = p.MaxHp > 0 ? (float)p.CurrentHp / p.MaxHp : 0;
		_hpFill.AnchorRight = Math.Clamp(hpPct, 0f, 1f);
		_hpValue.Text = $"{p.CurrentHp}/{p.MaxHp}";

		int maxSta = p.MaxStamina;
		_staFill.AnchorRight = 1.0f;
		_staValue.Text = $"{maxSta}/{maxSta}";

		float ethPct = p.MaxAether > 0 ? (float)p.CurrentAether / p.MaxAether : 0;
		_ethFill.AnchorRight = Math.Clamp(ethPct, 0f, 1f);
		_ethValue.Text = $"{p.CurrentAether}/{p.MaxAether}";
	}
}
