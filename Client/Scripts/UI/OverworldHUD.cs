using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.UI;

/// <summary>
/// Overworld HUD — Identity bar (top-left), sidebar icon buttons (right edge),
/// floating window manager (multiple can be open), chat bubble on player.
///
/// KEY CHANGES from old version:
/// - WindowPanel (floating/draggable) replaces SlidePanel (slide-from-edge)
/// - Multiple windows can be open simultaneously
/// - Sidebar icons match Taskade mockup Image 2
/// - Dark/light mode support via UITheme
/// </summary>
public partial class OverworldHUD : Control
{
	// Identity bar elements
	private Label _nameLabel;
	private Label _rankLabel;
	private Label _tpLabel;
	private TextureRect _hpFill;
	private TextureRect _staFill;
	private TextureRect _ethFill;
	private Label _hpValue;
	private Label _staValue;
	private Label _ethValue;

	private const float BarTrackWidth = 160f;

	// Idle fade refs
	private PanelContainer _identityBarPanel;
	private VBoxContainer _sidebarContainer;
	private float _hudIdleTimer = 0f;
	private const float HudIdleTimeout = 8f;

	// ═══ WINDOW SYSTEM (replaces single-panel system) ═══
	private readonly Dictionary<string, Panels.WindowPanel> _windows = new();

	// Panel definitions — order matches Taskade mockup sidebar, top to bottom.
	// Icons loaded from res://Assets/Icons/ (Lucide PNGs).
	private readonly List<(string id, string iconPath, string tooltip, Key hotkey)> _panelDefs = new()
	{
		("journal",   "res://Assets/Icons/icon_chronicle.png",  "Chronicle Keeper  [J]",  Key.J),
		("charsheet", "res://Assets/Icons/icon_character.png",   "Character Sheet  [C]",   Key.C),
		("training",  "res://Assets/Icons/icon_training.png",    "Daily Training  [V]",    Key.V),
		("map",       "res://Assets/Icons/icon_map.png",          "World Map  [M]",          Key.M),
		("mentor",    "res://Assets/Icons/icon_mentorship.png",  "Mentorship  [N]",        Key.N),
		("inventory", "res://Assets/Icons/icon_inventory.png",    "Inventory  [I]",          Key.I),
		("settings",  "res://Assets/Icons/icon_settings.png",    "Settings  [Esc]",        Key.Escape),
	};

	// Chat bubble (positioned above player sprite)
	private Label _chatBubble;
	private PanelContainer _chatBubbleBg;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildIdentityBar();
		BuildSidebar();
		BuildWindows();
		HideDebugOverlay();
		SetupChatBubble();
	}

	public override void _Process(double delta)
	{
		UpdateBars();

		// Idle fade for identity bar and sidebar
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
		if (ChatPanel.IsUiFocused) return;

		// Esc: close all windows, or open settings if none are open
		if (key.Keycode == Key.Escape)
		{
			bool anyOpen = false;
			foreach (var (_, win) in _windows)
			{
				if (win.IsOpen) { win.Close(); anyOpen = true; }
			}
			if (!anyOpen)
				ToggleWindow("settings");
			GetViewport().SetInputAsHandled();
			return;
		}

		// Other hotkeys — toggle individual windows
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

	// ═════════════════════════════════════════════════════════
	//  IDENTITY BAR (top-left) — same structure, themed
	// ═════════════════════════════════════════════════════════

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

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		panel.AddChild(vbox);

		// Top row: icon, name, rank, spacer, TP
		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(topRow);

		// Crown icon (from Lucide)
		var crownIcon = new TextureRect();
		crownIcon.CustomMinimumSize = new Vector2(18, 18);
		crownIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		if (ResourceLoader.Exists("res://Assets/Icons/icon_crown.png"))
			crownIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/icon_crown.png");
		topRow.AddChild(crownIcon);

		_nameLabel = new Label();
		_nameLabel.Text = "...";
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) _nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		topRow.AddChild(_nameLabel);

		_rankLabel = new Label();
		_rankLabel.Text = "";
		_rankLabel.AddThemeFontSizeOverride("font_size", 10);
		_rankLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontBody != null) _rankLabel.AddThemeFontOverride("font", UITheme.FontBody);
		topRow.AddChild(_rankLabel);

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		topRow.AddChild(spacer);

		// TP indicator
		var tpBox = new HBoxContainer();
		tpBox.AddThemeConstantOverride("separation", 4);
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

		// Resource bars
		var barsVbox = new VBoxContainer();
		barsVbox.AddThemeConstantOverride("separation", 3);
		vbox.AddChild(barsVbox);

		(_hpFill, _hpValue) = CreateBar(barsVbox, "HP",
			new Color("B03030"), new Color("E04040"), new Color("F06060"));

		(_staFill, _staValue) = CreateBar(barsVbox, "STA",
			new Color("C08020"), new Color("E8A030"), new Color("F0B848"));

		(_ethFill, _ethValue) = CreateBar(barsVbox, "ETH",
			new Color("3060A0"), new Color("4080D0"), new Color("5898E0"));
	}

	private (TextureRect fill, Label value) CreateBar(VBoxContainer parent, string label,
		Color fillColorStart, Color fillColorMid, Color fillColorEnd)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.CustomMinimumSize = new Vector2(0, 14);
		parent.AddChild(row);

		// Bar label color matches the fill
		Color textColor = fillColorMid;

		var lbl = new Label();
		lbl.Text = label;
		lbl.CustomMinimumSize = new Vector2(26, 0);
		lbl.HorizontalAlignment = HorizontalAlignment.Right;
		lbl.AddThemeFontSizeOverride("font_size", 9);
		lbl.AddThemeColorOverride("font_color", textColor);
		if (UITheme.FontNumbersMedium != null) lbl.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		row.AddChild(lbl);

		var track = new Panel();
		track.CustomMinimumSize = new Vector2(BarTrackWidth, 8);
		track.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		track.SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var trackStyle = new StyleBoxFlat();
		trackStyle.BgColor = UITheme.CardBg;
		trackStyle.SetCornerRadiusAll(2);
		trackStyle.BorderColor = UITheme.BorderSubtle;
		trackStyle.SetBorderWidthAll(1);
		track.AddThemeStyleboxOverride("panel", trackStyle);
		row.AddChild(track);

		// Gradient fill
		var fill = new TextureRect();
		fill.CustomMinimumSize = new Vector2(0, 6);
		fill.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		fill.StretchMode = TextureRect.StretchModeEnum.Scale;
		fill.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
		fill.OffsetTop = 1; fill.OffsetBottom = -1; fill.OffsetLeft = 1;

		var gradient = new Gradient();
		gradient.SetColor(0, fillColorStart);
		gradient.AddPoint(0.6f, fillColorMid);
		gradient.SetColor(gradient.GetPointCount() - 1, fillColorEnd);

		var gradTex = new GradientTexture1D();
		gradTex.Gradient = gradient;
		gradTex.Width = 128;
		fill.Texture = gradTex;
		track.AddChild(fill);

		// Shine overlay
		var shine = new ColorRect();
		shine.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		shine.OffsetBottom = 3;
		shine.OffsetLeft = 1;
		shine.Color = new Color(1f, 1f, 1f, 0.12f);
		shine.MouseFilter = MouseFilterEnum.Ignore;
		fill.AddChild(shine);

		var val = new Label();
		val.Text = "0 / 0";
		val.CustomMinimumSize = new Vector2(60, 0);
		val.HorizontalAlignment = HorizontalAlignment.Right;
		val.AddThemeFontSizeOverride("font_size", 10);
		val.AddThemeColorOverride("font_color", textColor);
		if (UITheme.FontNumbersMedium != null) val.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		row.AddChild(val);

		return (fill, val);
	}

	// ═════════════════════════════════════════════════════════
	//  SIDEBAR (right edge — vertical icon buttons)
	// ═════════════════════════════════════════════════════════

	private void BuildSidebar()
	{
		// Container strip along right edge
		var strip = new PanelContainer();
		strip.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		strip.CustomMinimumSize = new Vector2(56, 0);
		strip.OffsetLeft = -56;
		strip.MouseFilter = MouseFilterEnum.Stop;

		// Sidebar background
		var stripStyle = new StyleBoxFlat();
		stripStyle.BgColor = UITheme.BgSidebar;
		stripStyle.BorderWidthLeft = 1;
		stripStyle.BorderColor = UITheme.BorderSubtle;
		stripStyle.ContentMarginTop = 12;
		stripStyle.ContentMarginBottom = 12;
		strip.AddThemeStyleboxOverride("panel", stripStyle);
		strip.MouseEntered += () => _hudIdleTimer = 0f;
		AddChild(strip);

		var vbox = new VBoxContainer();
		_sidebarContainer = vbox;
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		strip.AddChild(vbox);

		foreach (var (id, iconPath, tooltip, _) in _panelDefs)
		{
			var btn = CreateIconButton(iconPath, tooltip);
			btn.Pressed += () => ToggleWindow(id);
			vbox.AddChild(btn);
		}
	}

	/// <summary>
	/// Creates a 44x44 icon button with a Lucide PNG texture inside.
	/// Matches Taskade mockup: rounded square, light/dark bg, centered icon.
	/// </summary>
	private Button CreateIconButton(string iconPath, string tooltip)
	{
		var btn = new Button();
		btn.TooltipText = tooltip;
		btn.CustomMinimumSize = new Vector2(44, 44);
		btn.MouseFilter = MouseFilterEnum.Stop;

		// Clear any text
		btn.Text = "";

		// Style: rounded square matching sidebar mockup
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgIconBtn;
		style.SetCornerRadiusAll(10);
		style.BorderColor = UITheme.BorderSubtle;
		style.SetBorderWidthAll(1);
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = UITheme.BgIconBtnActive;
		hover.BorderColor = UITheme.BorderMedium;
		btn.AddThemeStyleboxOverride("hover", hover);

		var press = (StyleBoxFlat)style.Duplicate();
		press.BgColor = UITheme.IsDarkMode ? UITheme.BorderSubtle : new Color("D8D8DC");
		btn.AddThemeStyleboxOverride("pressed", press);

		// Load icon texture
		if (ResourceLoader.Exists(iconPath))
		{
			var tex = GD.Load<Texture2D>(iconPath);
			if (tex != null)
			{
				btn.Icon = tex;
				btn.IconAlignment = HorizontalAlignment.Center;
				btn.ExpandIcon = true;
				// Fit icon inside button with padding
				btn.AddThemeConstantOverride("icon_max_width", 22);
			}
		}
		else
		{
			GD.PrintErr($"[OverworldHUD] Icon not found: {iconPath}");
			btn.Text = "?";
			btn.AddThemeFontSizeOverride("font_size", 16);
			btn.AddThemeColorOverride("font_color", UITheme.TextSecondary);
		}

		return btn;
	}

	// ═════════════════════════════════════════════════════════
	//  WINDOW MANAGEMENT (multiple can be open)
	// ═════════════════════════════════════════════════════════

	private void BuildWindows()
	{
		RegisterWindow("charsheet", new Panels.CharacterSheetPanel());
		RegisterWindow("training",  new Panels.TrainingPanel());
		RegisterWindow("journal",   new Panels.JournalPanel());
		RegisterWindow("map",       new Panels.MapPanel());
		RegisterWindow("inventory", new Panels.InventoryPanel());
		RegisterWindow("mentor",    new Panels.MentorPanel());
		RegisterWindow("settings",  new Panels.SettingsPanel());
	}

	private void RegisterWindow(string id, Panels.WindowPanel window)
	{
		_windows[id] = window;
		AddChild(window);
	}

	/// <summary>
	/// Toggle a floating window. Multiple can be open at once.
	/// If already open, close it. If closed, open it.
	/// </summary>
	private void ToggleWindow(string id)
	{
		if (!_windows.ContainsKey(id)) return;

		var win = _windows[id];
		if (win.IsOpen)
			win.Close();
		else
			win.Open();
	}

	// ═════════════════════════════════════════════════════════
	//  CHAT BUBBLE (on player sprite)
	// ═════════════════════════════════════════════════════════

	private void SetupChatBubble()
	{
		CallDeferred(nameof(LinkChatBubble));
	}

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
			_chatBubble.Position = Vector2.Zero;

			player.AddChild(_chatBubbleBg);

			chatPanel.ChatBubbleLabel = _chatBubble;
			chatPanel.ChatBubbleBg = _chatBubbleBg;
		}
		else
		{
			GD.Print("[OverworldHUD] Player node not found for chat bubble.");
		}
	}

	// ═════════════════════════════════════════════════════════
	//  HIDE DEBUG OVERLAY
	// ═════════════════════════════════════════════════════════

	private void HideDebugOverlay()
	{
		var debugLayer = GetTree().Root.FindChild("DebugOverlay", true, false) as Control;
		if (debugLayer != null)
		{
			debugLayer.Visible = false;
			GD.Print("[OverworldHUD] Debug overlay hidden. Press F1 to re-toggle.");
		}
	}

	// ═════════════════════════════════════════════════════════
	//  UPDATE LOOP
	// ═════════════════════════════════════════════════════════

	private void UpdateBars()
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return;

		_nameLabel.Text = p.CharacterName;
		_rankLabel.Text = p.RpRank?.ToUpper() ?? "";
		_tpLabel.Text = $"{p.DailyPointsRemaining} TP";

		float hpPct = p.MaxHp > 0 ? (float)p.CurrentHp / p.MaxHp : 0;
		_hpFill.AnchorRight = Math.Clamp(hpPct, 0f, 1f);
		_hpValue.Text = $"{p.CurrentHp} / {p.MaxHp}";

		int maxSta = (int)(p.Stamina * 10 + 50);
		_staFill.AnchorRight = 1.0f;
		_staValue.Text = $"{maxSta} / {maxSta}";

		float ethPct = p.MaxEther > 0 ? (float)p.CurrentEther / p.MaxEther : 0;
		_ethFill.AnchorRight = Math.Clamp(ethPct, 0f, 1f);
		_ethValue.Text = $"{p.CurrentEther} / {p.MaxEther}";
	}
}
