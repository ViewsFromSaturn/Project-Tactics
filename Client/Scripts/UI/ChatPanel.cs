using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.UI;

/// <summary>
/// Chat Panel — full implementation matching HUD v4 mockup.
/// Features: tabs, verb cycling, emote composition panel, IC color settings,
/// text size, scroll lock, export, idle fade, chat bubble, formatted messages.
/// </summary>
public partial class ChatPanel : Control
{
	// ═══ STATIC STATE (checked by PlayerController) ═══
	/// <summary>Set true when any UI text field is focused. PlayerController checks this.</summary>
	public static bool IsUiFocused { get; private set; } = false;

	// ═══ MESSAGE TYPES ═══
	public enum MsgType { Say, Whisper, Yell, Emote, Ooc, Faction, Story, System }

	public record ChatMessage(string Sender, string Text, MsgType Type, string Time, Color SenderColor);

	// ═══ CHAT COLORS (from HUD v4) ═══
	static readonly Color SayColor     = new("d4d2cc");
	static readonly Color WhisperColor = new("b080d0");
	static readonly Color YellColor    = new("e8a040");
	static readonly Color EmoteColor   = new("70c870");
	static readonly Color OocColor     = new("78a8d0");
	static readonly Color SystemColor  = new("b0a060");
	static readonly Color FactionColor = new("d0a050");
	static readonly Color StoryColor   = new("c0a0d8");
	static readonly Color SpeechWhite  = new("f0ede6");

	// ═══ STATE ═══
	readonly List<ChatMessage> _messages = new();
	readonly string[] _baseVerbs    = { "Say", "Whisper", "Yell", "Emote", "OOC" };
	readonly string[] _leaderVerbs  = { "Say", "Whisper", "Yell", "Emote", "OOC", "Faction", "Story" };
	int _currentVerb = 0;
	string _currentTab = "all";
	bool _collapsed = false;
	bool _emoteOpen = false;
	bool _settingsOpen = false;
	bool _scrollLocked = false;
	int _newMsgCount = 0;
	Color _playerIcColor = new("d4d2cc");
	int _chatFontSize = 13;
	float _idleTimer = 0f;
	const float IdleTimeout = 8f;
	float _panelHeight = 290f;

	// ═══ UI REFS ═══
	PanelContainer _panel;
	VBoxContainer _panelContent;
	RichTextLabel _messageLog;
	ScrollContainer _scrollWrap;
	LineEdit _chatInput;
	Button _verbBtn;
	Button _collapseBtn;
	Button _emoteExpandBtn;
	Button _exportBtn;
	Button _settingsBtn;
	HBoxContainer _tabRow;
	Label _newMsgIndicator;
	readonly Dictionary<string, Button> _tabButtons = new();
	readonly Dictionary<string, ColorRect> _tabUnreadDots = new();

	// Emote panel refs
	PanelContainer _emotePanel;
	TextEdit _emoteTextarea;

	// Settings panel refs
	PanelContainer _settingsPanel;
	ColorWheelPicker _colorWheel;
	HSlider _fontSizeSlider;
	Label _textSizeLabel;

	// Export toast
	PanelContainer _exportToast;
	Label _exportToastLabel;

	// Resize state
	bool _isResizingHeight = false;
	bool _isResizingWidth = false;
	float _resizeStartY = 0f;
	float _resizeStartX = 0f;
	float _resizeStartHeight = 0f;
	float _resizeStartWidth = 0f;
	const float ChatMinHeight = 160f;
	const float ChatMaxHeight = 520f;
	const float ChatMinWidth = 300f;
	const float ChatMaxWidthPct = 0.7f;
	float _panelWidth = 0f; // 0 = use default percentage

	// Emote panel drag + resize state
	bool _emoteDragging = false;
	bool _emoteResizing = false;
	Vector2 _emoteDragOffset;
	Vector2 _emoteResizeStart;
	Vector2 _emoteStartSize;

	// Profile popup
	private ProfilePopup _profilePopup;

	// Chat bubble ref (set by OverworldHUD)
	public Label ChatBubbleLabel { get; set; }
	private float _bubbleTimer = 0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		BuildPanel();
		BuildEmotePanel();
		BuildSettingsPanel();
		BuildProfilePopup();
		BuildExportToast();
		AddWelcomeMessages();
	}

	public override void _Process(double delta)
	{
		// Track focus state for input blocking
		bool chatFocused = _chatInput?.HasFocus() == true;
		bool emoteFocused = _emoteTextarea?.HasFocus() == true;
		IsUiFocused = chatFocused || emoteFocused;

		// Release all drag states if mouse not held
		if (!Input.IsMouseButtonPressed(MouseButton.Left))
		{
			_isResizingHeight = false;
			_isResizingWidth = false;
			_emoteDragging = false;
			_emoteResizing = false;
		}

		// Idle fade
		_idleTimer += (float)delta;
		if (_idleTimer > IdleTimeout && !IsUiFocused)
		{
			_panel.Modulate = _panel.Modulate.Lerp(new Color(1, 1, 1, 0.35f), (float)delta * 2);
		}

		// Chat bubble countdown
		if (_bubbleTimer > 0)
		{
			_bubbleTimer -= (float)delta;
			if (_bubbleTimer <= 0 && ChatBubbleLabel != null)
			{
				ChatBubbleLabel.Visible = false;
				var parent = ChatBubbleLabel.GetParent<Control>();
				if (parent != null) parent.Visible = false;
			}
			else if (ChatBubbleLabel != null && _bubbleTimer < 1f)
			{
				float alpha = _bubbleTimer;
				ChatBubbleLabel.Modulate = new Color(1, 1, 1, alpha);
				var parent = ChatBubbleLabel.GetParent<Control>();
				if (parent != null) parent.Modulate = new Color(1, 1, 1, alpha);
			}
		}
	}

	private void ResetIdle()
	{
		_idleTimer = 0f;
		_panel.Modulate = Colors.White;
	}

	// ═════════════════════════════════════════════════════════
	//  BUILD MAIN PANEL
	// ═════════════════════════════════════════════════════════

	private void BuildPanel()
	{
		_panel = new PanelContainer();
		_panel.AnchorTop = 1f; _panel.AnchorBottom = 1f;
		_panel.AnchorLeft = 0f; _panel.AnchorRight = 0f;
		_panel.OffsetLeft = 0; _panel.OffsetRight = 560;
		_panel.OffsetTop = -_panelHeight; _panel.OffsetBottom = 0;
		_panel.MouseFilter = MouseFilterEnum.Stop;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.031f, 0.031f, 0.063f, 0.62f);
		style.CornerRadiusTopRight = 8;
		style.BorderWidthTop = 1; style.BorderWidthRight = 1;
		style.BorderColor = UITheme.BorderLight;
		_panel.AddThemeStyleboxOverride("panel", style);

		_panel.GuiInput += OnPanelInput;
		AddChild(_panel);

		_panelContent = new VBoxContainer();
		_panelContent.AddThemeConstantOverride("separation", 0);
		_panel.AddChild(_panelContent);

		BuildTabs();
		BuildMessageArea();
		BuildNewMsgIndicator();
		BuildInputRow();
		BuildResizeHandle();
	}

	private void OnPanelInput(InputEvent ev)
	{
		if (ev is InputEventMouseMotion) ResetIdle();
	}

	/// <summary>Build the export toast notification.</summary>
	private void BuildExportToast()
	{
		_exportToast = new PanelContainer();
		_exportToast.Visible = false;
		_exportToast.MouseFilter = MouseFilterEnum.Ignore;
		_exportToast.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
		_exportToast.GrowHorizontal = GrowDirection.Both;
		_exportToast.GrowVertical = GrowDirection.Begin;
		_exportToast.OffsetBottom = -60;
		_exportToast.OffsetLeft = -100;
		_exportToast.OffsetRight = 100;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.039f, 0.039f, 0.071f, 0.92f);
		style.SetCornerRadiusAll(6);
		style.BorderColor = new Color(0.392f, 0.627f, 0.314f, 0.3f);
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 18;
		style.ContentMarginRight = 18;
		style.ContentMarginTop = 8;
		style.ContentMarginBottom = 8;
		_exportToast.AddThemeStyleboxOverride("panel", style);

		_exportToastLabel = new Label();
		_exportToastLabel.Text = "Chat log copied to clipboard";
		_exportToastLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_exportToastLabel.AddThemeFontSizeOverride("font_size", 12);
		_exportToastLabel.AddThemeColorOverride("font_color", UITheme.Accent);
		if (UITheme.FontBody != null) _exportToastLabel.AddThemeFontOverride("font", UITheme.FontBody);
		_exportToast.AddChild(_exportToastLabel);

		AddChild(_exportToast);
	}

	private void ShowExportToast(int count)
	{
		_exportToastLabel.Text = $"✓ {count} messages copied to clipboard";
		_exportToast.Visible = true;
		_exportToast.Modulate = Colors.White;

		GetTree().CreateTimer(2.5).Timeout += () =>
		{
			_exportToast.Visible = false;
		};
	}

	// ─── RESIZE HANDLE ──────────────────────────────────────

	private void BuildResizeHandle()
	{
		// Top edge — height resize
		var topHandle = new Control();
		topHandle.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		topHandle.OffsetTop = -4;
		topHandle.OffsetBottom = 4;
		topHandle.MouseFilter = MouseFilterEnum.Stop;
		topHandle.MouseDefaultCursorShape = CursorShape.Vsize;
		topHandle.Name = "ResizeHandleTop";
		topHandle.GuiInput += OnResizeInput;
		_panel.AddChild(topHandle);

		// Right edge — width resize
		var rightHandle = new Control();
		rightHandle.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		rightHandle.OffsetLeft = -4;
		rightHandle.OffsetRight = 4;
		rightHandle.MouseFilter = MouseFilterEnum.Stop;
		rightHandle.MouseDefaultCursorShape = CursorShape.Hsize;
		rightHandle.Name = "ResizeHandleRight";
		rightHandle.GuiInput += OnWidthResizeInput;
		_panel.AddChild(rightHandle);
	}

	private void OnResizeInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_isResizingHeight = true;
					_resizeStartY = mb.GlobalPosition.Y;
					_resizeStartHeight = _panelHeight;
				}
				else _isResizingHeight = false;
			}
		}
		else if (ev is InputEventMouseMotion mm && _isResizingHeight)
		{
			float delta = _resizeStartY - mm.GlobalPosition.Y;
			_panelHeight = Math.Clamp(_resizeStartHeight + delta, ChatMinHeight, ChatMaxHeight);
			_panel.OffsetTop = -_panelHeight;
			if (_settingsOpen)
				_settingsPanel.OffsetBottom = -(_panelHeight + 6);
		}
	}

	private void OnWidthResizeInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_isResizingWidth = true;
				_resizeStartX = mb.GlobalPosition.X;
				_resizeStartWidth = _panel.Size.X > 0 ? _panel.Size.X : GetViewportRect().Size.X * 0.44f;
			}
			else _isResizingWidth = false;
		}
		else if (ev is InputEventMouseMotion mm && _isResizingWidth)
		{
			float viewport = GetViewportRect().Size.X;
			float delta = mm.GlobalPosition.X - _resizeStartX;
			float newWidth = Math.Clamp(_resizeStartWidth + delta, ChatMinWidth, viewport * ChatMaxWidthPct);
			_panelWidth = newWidth;
			_panel.OffsetRight = newWidth;
			_settingsPanel.OffsetRight = 280; // Keep settings fixed width
		}
	}

	// ─── TABS ────────────────────────────────────────────────

	private void BuildTabs()
	{
		var tabContainer = new PanelContainer();
		var tabBg = new StyleBoxFlat();
		tabBg.BgColor = Colors.Transparent;
		tabBg.ContentMarginLeft = 8; tabBg.ContentMarginRight = 8;
		tabBg.ContentMarginTop = 3; tabBg.ContentMarginBottom = 3;
		tabBg.BorderWidthBottom = 1;
		tabBg.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.18f);
		tabContainer.AddThemeStyleboxOverride("panel", tabBg);
		_panelContent.AddChild(tabContainer);

		_tabRow = new HBoxContainer();
		_tabRow.AddThemeConstantOverride("separation", 2);
		tabContainer.AddChild(_tabRow);

		AddTab("all", "All");
		AddTab("ic", "IC");
		AddTab("ooc", "OOC");
		AddTab("fac", "Fac");
		AddTab("sys", "Sys");

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_tabRow.AddChild(spacer);

		// Export button
		_exportBtn = MakeUtilButton("📋", "Export chat log");
		_exportBtn.Pressed += OnExportPressed;
		_tabRow.AddChild(_exportBtn);

		// Settings button
		_settingsBtn = MakeUtilButton("⚙", "Chat Settings");
		_settingsBtn.Pressed += ToggleSettings;
		_tabRow.AddChild(_settingsBtn);

		// Collapse button
		_collapseBtn = MakeUtilButton("▼", "Collapse chat");
		_collapseBtn.Pressed += ToggleCollapse;
		_tabRow.AddChild(_collapseBtn);

		SetActiveTab("all");
	}

	private void AddTab(string id, string label)
	{
		var container = new HBoxContainer();
		container.AddThemeConstantOverride("separation", 0);

		var btn = new Button();
		btn.Text = label;
		btn.AddThemeFontSizeOverride("font_size", 11);
		btn.AddThemeColorOverride("font_color", UITheme.TextDim);
		btn.AddThemeColorOverride("font_hover_color", UITheme.Text);
		if (UITheme.FontBodyMedium != null) btn.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		ApplyGhostStyle(btn);
		btn.Pressed += () => SetActiveTab(id);
		container.AddChild(btn);

		// Unread dot (hidden by default)
		var dot = new ColorRect();
		dot.CustomMinimumSize = new Vector2(5, 5);
		dot.Color = FactionColor;
		dot.Visible = false;
		dot.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		container.AddChild(dot);

		_tabButtons[id] = btn;
		_tabUnreadDots[id] = dot;
		_tabRow.AddChild(container);
	}

	private void SetActiveTab(string id)
	{
		_currentTab = id;
		if (_tabUnreadDots.ContainsKey(id)) _tabUnreadDots[id].Visible = false;

		foreach (var (tabId, btn) in _tabButtons)
		{
			bool active = tabId == id;
			btn.AddThemeColorOverride("font_color", active ? UITheme.TextBright : UITheme.TextDim);
			var s = new StyleBoxFlat();
			s.BgColor = active ? new Color(1, 1, 1, 0.06f) : Colors.Transparent;
			s.SetCornerRadiusAll(3);
			s.ContentMarginLeft = 8; s.ContentMarginRight = 8;
			s.ContentMarginTop = 2; s.ContentMarginBottom = 2;
			btn.AddThemeStyleboxOverride("normal", s);
		}
		RefreshMessages();
	}

	// ─── MESSAGE AREA ────────────────────────────────────────

	private void BuildMessageArea()
	{
		var margin = new MarginContainer();
		margin.SizeFlagsVertical = SizeFlags.ExpandFill;
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 4);
		margin.AddThemeConstantOverride("margin_bottom", 4);
		margin.Name = "MessageArea";
		_panelContent.AddChild(margin);

		_messageLog = new RichTextLabel();
		_messageLog.BbcodeEnabled = true;
		_messageLog.FitContent = false;
		_messageLog.ScrollFollowing = true;
		_messageLog.SelectionEnabled = true;
		_messageLog.SizeFlagsVertical = SizeFlags.ExpandFill;
		_messageLog.AddThemeFontSizeOverride("normal_font_size", _chatFontSize);
		_messageLog.AddThemeColorOverride("default_color", SayColor);
		if (UITheme.FontBody != null) _messageLog.AddThemeFontOverride("normal_font", UITheme.FontBody);
		if (UITheme.FontBodySemiBold != null) _messageLog.AddThemeFontOverride("bold_font", UITheme.FontBodySemiBold);

		// Meta clicked for clickable names (future)
		_messageLog.MetaClicked += OnNameClicked;

		margin.AddChild(_messageLog);
	}

	private void BuildNewMsgIndicator()
	{
		_newMsgIndicator = new Label();
		_newMsgIndicator.Text = "▼ New messages";
		_newMsgIndicator.HorizontalAlignment = HorizontalAlignment.Center;
		_newMsgIndicator.Visible = false;
		_newMsgIndicator.AddThemeFontSizeOverride("font_size", 11);
		_newMsgIndicator.AddThemeColorOverride("font_color", OocColor);
		if (UITheme.FontBody != null) _newMsgIndicator.AddThemeFontOverride("font", UITheme.FontBody);

		var indicatorPanel = new PanelContainer();
		var indicatorStyle = new StyleBoxFlat();
		indicatorStyle.BgColor = new Color(0.047f, 0.047f, 0.086f, 0.92f);
		indicatorStyle.SetCornerRadiusAll(12);
		indicatorStyle.BorderColor = new Color(0.314f, 0.431f, 0.588f, 0.35f);
		indicatorStyle.SetBorderWidthAll(1);
		indicatorStyle.ContentMarginLeft = 14; indicatorStyle.ContentMarginRight = 14;
		indicatorStyle.ContentMarginTop = 3; indicatorStyle.ContentMarginBottom = 3;
		indicatorPanel.AddThemeStyleboxOverride("panel", indicatorStyle);
		indicatorPanel.AddChild(_newMsgIndicator);
		indicatorPanel.Name = "NewMsgIndicator";

		// Position centered above input row
		indicatorPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
		indicatorPanel.GrowVertical = GrowDirection.Begin;
		indicatorPanel.OffsetBottom = -40;
		indicatorPanel.MouseFilter = MouseFilterEnum.Stop;

		indicatorPanel.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				_messageLog.ScrollFollowing = true;
				_scrollLocked = false;
				_newMsgCount = 0;
				_newMsgIndicator.Visible = false;
				_newMsgIndicator.GetParent<PanelContainer>().Visible = false;
			}
		};

		_panel.AddChild(indicatorPanel);
		indicatorPanel.Visible = false;
	}

	// ─── INPUT ROW ───────────────────────────────────────────

	private void BuildInputRow()
	{
		var inputMargin = new MarginContainer();
		inputMargin.AddThemeConstantOverride("margin_left", 8);
		inputMargin.AddThemeConstantOverride("margin_right", 8);
		inputMargin.AddThemeConstantOverride("margin_top", 4);
		inputMargin.AddThemeConstantOverride("margin_bottom", 6);
		inputMargin.Name = "InputArea";
		_panelContent.AddChild(inputMargin);

		var inputRow = new HBoxContainer();
		inputRow.AddThemeConstantOverride("separation", 0);
		inputMargin.AddChild(inputRow);

		// Verb button
		_verbBtn = new Button();
		_verbBtn.Text = "Say ▸";
		_verbBtn.CustomMinimumSize = new Vector2(80, 0);
		_verbBtn.AddThemeFontSizeOverride("font_size", 12);
		_verbBtn.AddThemeColorOverride("font_color", SayColor);
		if (UITheme.FontBodyMedium != null) _verbBtn.AddThemeFontOverride("font", UITheme.FontBodyMedium);

		var verbStyle = new StyleBoxFlat();
		verbStyle.BgColor = new Color(0.07f, 0.07f, 0.11f, 0.9f);
		verbStyle.CornerRadiusTopLeft = 4; verbStyle.CornerRadiusBottomLeft = 4;
		verbStyle.ContentMarginLeft = 10; verbStyle.ContentMarginRight = 10;
		verbStyle.ContentMarginTop = 6; verbStyle.ContentMarginBottom = 6;
		verbStyle.BorderWidthRight = 1;
		verbStyle.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.2f);
		_verbBtn.AddThemeStyleboxOverride("normal", verbStyle);
		var verbHover = (StyleBoxFlat)verbStyle.Duplicate();
		verbHover.BgColor = new Color(0.11f, 0.11f, 0.165f, 0.95f);
		_verbBtn.AddThemeStyleboxOverride("hover", verbHover);
		_verbBtn.AddThemeStyleboxOverride("pressed", verbStyle);
		_verbBtn.Pressed += CycleVerb;
		inputRow.AddChild(_verbBtn);

		// Chat input
		_chatInput = new LineEdit();
		_chatInput.PlaceholderText = "Press Enter to chat, Tab to change verb...";
		_chatInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_chatInput.AddThemeFontSizeOverride("font_size", _chatFontSize);
		_chatInput.AddThemeColorOverride("font_color", new Color("e0e0e0"));
		_chatInput.AddThemeColorOverride("font_placeholder_color", new Color(0.392f, 0.392f, 0.431f, 0.45f));
		_chatInput.AddThemeColorOverride("caret_color", UITheme.Accent);
		if (UITheme.FontBody != null) _chatInput.AddThemeFontOverride("font", UITheme.FontBody);

		var inputStyle = new StyleBoxFlat();
		inputStyle.BgColor = new Color(0.047f, 0.047f, 0.078f, 0.85f);
		inputStyle.ContentMarginLeft = 10; inputStyle.ContentMarginRight = 10;
		inputStyle.ContentMarginTop = 6; inputStyle.ContentMarginBottom = 6;
		inputStyle.BorderColor = Colors.Transparent; inputStyle.SetBorderWidthAll(1);
		_chatInput.AddThemeStyleboxOverride("normal", inputStyle);

		var inputFocus = (StyleBoxFlat)inputStyle.Duplicate();
		inputFocus.BgColor = new Color(0.063f, 0.063f, 0.102f, 0.95f);
		inputFocus.BorderColor = new Color(0.314f, 0.431f, 0.588f, 0.35f);
		_chatInput.AddThemeStyleboxOverride("focus", inputFocus);

		_chatInput.TextSubmitted += OnTextSubmitted;
		_chatInput.FocusEntered += ResetIdle;
		inputRow.AddChild(_chatInput);

		// Emote expand button ✎
		_emoteExpandBtn = new Button();
		_emoteExpandBtn.Text = "✎";
		_emoteExpandBtn.TooltipText = "Open long-form emote box";
		_emoteExpandBtn.CustomMinimumSize = new Vector2(32, 0);
		_emoteExpandBtn.AddThemeFontSizeOverride("font_size", 11);
		_emoteExpandBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
		_emoteExpandBtn.AddThemeColorOverride("font_hover_color", UITheme.Text);

		var emoteStyle = new StyleBoxFlat();
		emoteStyle.BgColor = new Color(0.07f, 0.07f, 0.11f, 0.9f);
		emoteStyle.CornerRadiusTopRight = 4; emoteStyle.CornerRadiusBottomRight = 4;
		emoteStyle.ContentMarginLeft = 8; emoteStyle.ContentMarginRight = 8;
		emoteStyle.ContentMarginTop = 6; emoteStyle.ContentMarginBottom = 6;
		emoteStyle.BorderWidthLeft = 1;
		emoteStyle.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.2f);
		_emoteExpandBtn.AddThemeStyleboxOverride("normal", emoteStyle);
		var emoteHover = (StyleBoxFlat)emoteStyle.Duplicate();
		emoteHover.BgColor = new Color(0.11f, 0.11f, 0.165f, 0.95f);
		_emoteExpandBtn.AddThemeStyleboxOverride("hover", emoteHover);
		_emoteExpandBtn.AddThemeStyleboxOverride("pressed", emoteStyle);

		_emoteExpandBtn.Pressed += ToggleEmotePanel;
		inputRow.AddChild(_emoteExpandBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  FLOATING EMOTE PANEL
	// ═════════════════════════════════════════════════════════

	private void BuildEmotePanel()
	{
		_emotePanel = new PanelContainer();
		_emotePanel.Visible = false;
		_emotePanel.MouseFilter = MouseFilterEnum.Stop;
		_emotePanel.CustomMinimumSize = new Vector2(420, 260);

		// Position: floating above chat, slightly right of center
		_emotePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
		_emotePanel.GrowHorizontal = GrowDirection.Both;
		_emotePanel.GrowVertical = GrowDirection.Begin;
		_emotePanel.OffsetBottom = -60;
		_emotePanel.OffsetLeft = -210; _emotePanel.OffsetRight = 210;
		_emotePanel.OffsetTop = -320;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.039f, 0.039f, 0.071f, 0.95f);
		style.SetCornerRadiusAll(8);
		style.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.4f);
		style.SetBorderWidthAll(1);
		_emotePanel.AddThemeStyleboxOverride("panel", style);
		AddChild(_emotePanel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		_emotePanel.AddChild(vbox);

		// ─── Draggable Header ──────────────────────────────
		var headerControl = new Control();
		headerControl.CustomMinimumSize = new Vector2(0, 36);
		headerControl.MouseFilter = MouseFilterEnum.Stop;
		headerControl.MouseDefaultCursorShape = CursorShape.Move;
		headerControl.GuiInput += OnEmoteHeaderInput;
		vbox.AddChild(headerControl);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		header.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		header.OffsetLeft = 12; header.OffsetRight = -12;
		header.OffsetTop = 8; header.OffsetBottom = -8;
		headerControl.AddChild(header);

		header.AddChild(UITheme.CreateTitle("✎ Emote Composition", 12));
		var hSpacer = new Control(); hSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(hSpacer);
		header.AddChild(UITheme.CreateDim("drag to move", 9));

		var closeEmote = new Button();
		closeEmote.Text = "✕";
		closeEmote.AddThemeFontSizeOverride("font_size", 13);
		closeEmote.AddThemeColorOverride("font_color", UITheme.TextDim);
		closeEmote.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
		ApplyGhostStyle(closeEmote);
		closeEmote.Pressed += ToggleEmotePanel;
		header.AddChild(closeEmote);

		// Separator
		vbox.AddChild(UITheme.CreateSeparator());

		// Toolbar
		var toolbar = new HBoxContainer();
		toolbar.AddThemeConstantOverride("separation", 2);
		var toolMargin = new MarginContainer();
		toolMargin.AddThemeConstantOverride("margin_left", 8); toolMargin.AddThemeConstantOverride("margin_right", 8);
		toolMargin.AddThemeConstantOverride("margin_top", 4); toolMargin.AddThemeConstantOverride("margin_bottom", 4);
		vbox.AddChild(toolMargin);
		toolMargin.AddChild(toolbar);

		var boldBtn = MakeToolButton("B", "Bold (**text**)");
		boldBtn.Pressed += () => InsertFormat("**");
		toolbar.AddChild(boldBtn);

		var italicBtn = MakeToolButton("I", "Italic (*text*)");
		italicBtn.Pressed += () => InsertFormat("*");
		toolbar.AddChild(italicBtn);

		toolbar.AddChild(MakeToolSep());

		var speechBtn = MakeToolButton("\" \"", "Speech (\"text\")");
		speechBtn.Pressed += () => InsertQuote();
		toolbar.AddChild(speechBtn);

		toolbar.AddChild(MakeToolSep());

		var toolHint = UITheme.CreateDim("*italic* · **bold** · \"speech\"", 10);
		toolHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		toolHint.HorizontalAlignment = HorizontalAlignment.Right;
		toolbar.AddChild(toolHint);

		// Textarea
		_emoteTextarea = new TextEdit();
		_emoteTextarea.PlaceholderText = "Write your emote here...\n\nUse \"quotes\" for dialogue — renders in white.\n*italic* and **bold** for emphasis.";
		_emoteTextarea.SizeFlagsVertical = SizeFlags.ExpandFill;
		_emoteTextarea.CustomMinimumSize = new Vector2(0, 80);
		_emoteTextarea.AddThemeFontSizeOverride("font_size", 13);
		_emoteTextarea.AddThemeColorOverride("font_color", EmoteColor);
		_emoteTextarea.AddThemeColorOverride("font_placeholder_color", new Color(0.392f, 0.392f, 0.431f, 0.4f));
		if (UITheme.FontBody != null) _emoteTextarea.AddThemeFontOverride("font", UITheme.FontBody);

		var textareaStyle = new StyleBoxFlat();
		textareaStyle.BgColor = new Color(0.039f, 0.039f, 0.071f, 0.5f);
		textareaStyle.ContentMarginLeft = 12; textareaStyle.ContentMarginRight = 12;
		textareaStyle.ContentMarginTop = 10; textareaStyle.ContentMarginBottom = 10;
		textareaStyle.BorderWidthBottom = 1;
		textareaStyle.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.15f);
		_emoteTextarea.AddThemeStyleboxOverride("normal", textareaStyle);
		var textareaFocus = (StyleBoxFlat)textareaStyle.Duplicate();
		textareaFocus.BorderColor = new Color(0.314f, 0.549f, 0.314f, 0.4f);
		_emoteTextarea.AddThemeStyleboxOverride("focus", textareaFocus);

		_emoteTextarea.FocusEntered += ResetIdle;
		vbox.AddChild(_emoteTextarea);

		// Send row
		var sendRow = new HBoxContainer();
		sendRow.AddThemeConstantOverride("separation", 8);
		var sendMargin = new MarginContainer();
		sendMargin.AddThemeConstantOverride("margin_left", 12); sendMargin.AddThemeConstantOverride("margin_right", 12);
		sendMargin.AddThemeConstantOverride("margin_top", 8); sendMargin.AddThemeConstantOverride("margin_bottom", 8);
		vbox.AddChild(sendMargin);
		sendMargin.AddChild(sendRow);

		var sendSpacer = new Control(); sendSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		sendRow.AddChild(sendSpacer);
		sendRow.AddChild(UITheme.CreateDim("Ctrl+Enter to send", 10));

		var sendBtn = new Button();
		sendBtn.Text = "Send Emote";
		sendBtn.AddThemeFontSizeOverride("font_size", 11);
		sendBtn.AddThemeColorOverride("font_color", EmoteColor);
		if (UITheme.FontBody != null) sendBtn.AddThemeFontOverride("font", UITheme.FontBody);
		var sendStyle = new StyleBoxFlat();
		sendStyle.BgColor = new Color(0.314f, 0.549f, 0.314f, 0.2f);
		sendStyle.SetCornerRadiusAll(4);
		sendStyle.BorderColor = new Color(0.314f, 0.549f, 0.314f, 0.3f);
		sendStyle.SetBorderWidthAll(1);
		sendStyle.ContentMarginLeft = 16; sendStyle.ContentMarginRight = 16;
		sendStyle.ContentMarginTop = 4; sendStyle.ContentMarginBottom = 4;
		sendBtn.AddThemeStyleboxOverride("normal", sendStyle);
		var sendHover = (StyleBoxFlat)sendStyle.Duplicate();
		sendHover.BgColor = new Color(0.314f, 0.549f, 0.314f, 0.3f);
		sendBtn.AddThemeStyleboxOverride("hover", sendHover);
		sendBtn.Pressed += SendEmote;
		sendRow.AddChild(sendBtn);

		// ─── Corner Resize Handle ──────────────────────────
		var resizeCorner = new Control();
		resizeCorner.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		resizeCorner.GrowHorizontal = GrowDirection.Begin;
		resizeCorner.GrowVertical = GrowDirection.Begin;
		resizeCorner.OffsetLeft = -16; resizeCorner.OffsetTop = -16;
		resizeCorner.CustomMinimumSize = new Vector2(16, 16);
		resizeCorner.MouseFilter = MouseFilterEnum.Stop;
		resizeCorner.MouseDefaultCursorShape = CursorShape.Fdiagsize;
		resizeCorner.GuiInput += OnEmoteResizeInput;
		_emotePanel.AddChild(resizeCorner);
	}

	// ─── Emote Panel Drag ──────────────────────────────
	private void OnEmoteHeaderInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_emoteDragging = true;
				_emoteDragOffset = mb.GlobalPosition - _emotePanel.GlobalPosition;
			}
			else _emoteDragging = false;
		}
		else if (ev is InputEventMouseMotion mm && _emoteDragging)
		{
			var viewport = GetViewportRect().Size;
			float x = Math.Clamp(mm.GlobalPosition.X - _emoteDragOffset.X, 0, viewport.X - 100);
			float y = Math.Clamp(mm.GlobalPosition.Y - _emoteDragOffset.Y, 0, viewport.Y - 60);

			// Switch to absolute positioning
			_emotePanel.AnchorLeft = 0; _emotePanel.AnchorRight = 0;
			_emotePanel.AnchorTop = 0; _emotePanel.AnchorBottom = 0;
			_emotePanel.Position = new Vector2(x, y);
		}
	}

	// ─── Emote Panel Resize ────────────────────────────
	private void OnEmoteResizeInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_emoteResizing = true;
				_emoteResizeStart = mb.GlobalPosition;
				_emoteStartSize = _emotePanel.Size;
			}
			else _emoteResizing = false;
		}
		else if (ev is InputEventMouseMotion mm && _emoteResizing)
		{
			float newW = Math.Clamp(_emoteStartSize.X + (mm.GlobalPosition.X - _emoteResizeStart.X), 320, 700);
			float newH = Math.Clamp(_emoteStartSize.Y + (mm.GlobalPosition.Y - _emoteResizeStart.Y), 200, 600);
			_emotePanel.Size = new Vector2(newW, newH);
		}
	}

	// ═════════════════════════════════════════════════════════
	//  SETTINGS PANEL (floats above chat)
	// ═════════════════════════════════════════════════════════

	private void BuildSettingsPanel()
	{
		_settingsPanel = new PanelContainer();
		_settingsPanel.Visible = false;
		_settingsPanel.MouseFilter = MouseFilterEnum.Stop;
		_settingsPanel.CustomMinimumSize = new Vector2(280, 0);

		// Position above chat panel
		_settingsPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
		_settingsPanel.GrowVertical = GrowDirection.Begin;
		_settingsPanel.OffsetBottom = -(_panelHeight + 6);
		_settingsPanel.OffsetLeft = 0; _settingsPanel.OffsetRight = 280;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.047f, 0.047f, 0.078f, 0.95f);
		style.SetCornerRadiusAll(8);
		style.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.35f);
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 18; style.ContentMarginRight = 18;
		style.ContentMarginTop = 16; style.ContentMarginBottom = 16;
		_settingsPanel.AddThemeStyleboxOverride("panel", style);
		AddChild(_settingsPanel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		_settingsPanel.AddChild(vbox);

		vbox.AddChild(UITheme.CreateTitle("Chat Settings", 12));

		// ─── IC Color section with full HSL color wheel ──────
		vbox.AddChild(UITheme.CreateDim("IC COLOR", 10));

		_colorWheel = new ColorWheelPicker();
		_colorWheel.SetColor(_playerIcColor);
		_colorWheel.ColorChanged += OnColorWheelChanged;
		// Set preview name from player data
		var active = Core.GameManager.Instance?.ActiveCharacter;
		if (active != null) _colorWheel.SetPreviewName(active.CharacterName);
		vbox.AddChild(_colorWheel);

		// ─── Text Size section with slider + buttons ─────────
		vbox.AddChild(UITheme.CreateSeparator());
		vbox.AddChild(UITheme.CreateDim("TEXT SIZE", 10));

		var sizeRow = new HBoxContainer();
		sizeRow.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(sizeRow);

		var minusBtn = MakeToolButton("A−", "Decrease text size");
		minusBtn.Pressed += () => AdjustFontSize(-1);
		sizeRow.AddChild(minusBtn);

		_fontSizeSlider = new HSlider();
		_fontSizeSlider.MinValue = 10;
		_fontSizeSlider.MaxValue = 18;
		_fontSizeSlider.Value = _chatFontSize;
		_fontSizeSlider.Step = 1;
		_fontSizeSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_fontSizeSlider.CustomMinimumSize = new Vector2(0, 14);
		_fontSizeSlider.ValueChanged += (double val) => SetFontSize((int)val);
		sizeRow.AddChild(_fontSizeSlider);

		var plusBtn = MakeToolButton("A+", "Increase text size");
		plusBtn.Pressed += () => AdjustFontSize(1);
		sizeRow.AddChild(plusBtn);

		_textSizeLabel = UITheme.CreateNumbers($"{_chatFontSize}px", 12, UITheme.TextDim);
		_textSizeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(_textSizeLabel);

		// Close
		var closeBtn = UITheme.CreateDim("Close", 10);
		closeBtn.HorizontalAlignment = HorizontalAlignment.Center;
		closeBtn.MouseFilter = MouseFilterEnum.Stop;
		closeBtn.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				ToggleSettings();
		};
		vbox.AddChild(closeBtn);
	}

	private void OnColorWheelChanged(Color color)
	{
		_playerIcColor = color;
		RefreshMessages();
		ResetIdle();
	}

	// ═════════════════════════════════════════════════════════
	//  INPUT HANDLING
	// ═════════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed) return;

		// Emote textarea: Ctrl+Enter sends, Esc closes
		if (_emoteTextarea?.HasFocus() == true)
		{
			if (key.Keycode == Key.Enter && key.CtrlPressed)
			{
				SendEmote();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (key.Keycode == Key.Escape)
			{
				_emoteTextarea.ReleaseFocus();
				ToggleEmotePanel();
				GetViewport().SetInputAsHandled();
				return;
			}
			// Consume all keys while emote textarea focused
			GetViewport().SetInputAsHandled();
			return;
		}

		// Chat input focused
		if (_chatInput?.HasFocus() == true)
		{
			if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
			{
				// Let TextSubmitted handle it
				return;
			}
			if (key.Keycode == Key.Tab)
			{
				CycleVerb();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (key.Keycode == Key.Escape)
			{
				_chatInput.ReleaseFocus();
				GetViewport().SetInputAsHandled();
				return;
			}
			// Consume ALL keys while chat is focused to prevent WASD movement
			GetViewport().SetInputAsHandled();
			return;
		}

		// Global shortcuts (chat not focused)
		if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
		{
			if (_collapsed) ToggleCollapse();
			_chatInput.GrabFocus();
			GetViewport().SetInputAsHandled();
		}
		else if (key.Keycode == Key.Escape && !_collapsed)
		{
			if (_settingsOpen) ToggleSettings();
			else if (_emoteOpen) ToggleEmotePanel();
			else ToggleCollapse();
			GetViewport().SetInputAsHandled();
		}
	}

	// ═════════════════════════════════════════════════════════
	//  ACTIONS
	// ═════════════════════════════════════════════════════════

	private void OnTextSubmitted(string text)
	{
		text = text.Trim();
		if (string.IsNullOrEmpty(text)) return;
		ResetIdle();

		string[] verbs = IsLeaderRank() ? _leaderVerbs : _baseVerbs;
		string verb = verbs[_currentVerb].ToLower();
		var player = Core.GameManager.Instance?.ActiveCharacter;
		string senderName = player?.CharacterName ?? "You";

		MsgType type = verb switch
		{
			"say" => MsgType.Say, "whisper" => MsgType.Whisper, "yell" => MsgType.Yell,
			"emote" => MsgType.Emote, "ooc" => MsgType.Ooc,
			"faction" => MsgType.Faction, "story" => MsgType.Story,
			_ => MsgType.Say
		};

		AddMessage(senderName, text, type, _playerIcColor);

		// Chat bubble for IC messages
		if (type != MsgType.Ooc && type != MsgType.System)
		{
			string bubbleText = type switch
			{
				MsgType.Say => $"\"{text}\"",
				MsgType.Yell => text.ToUpper(),
				MsgType.Emote => $"*{(text.Length > 40 ? text[..40] + "..." : text)}*",
				MsgType.Whisper => $"\"{text}\"",
				_ => text
			};
			ShowBubble(bubbleText);
		}

		_chatInput.Text = "";
	}

	private void SendEmote()
	{
		string text = _emoteTextarea.Text.Trim();
		if (string.IsNullOrEmpty(text)) return;
		ResetIdle();

		var player = Core.GameManager.Instance?.ActiveCharacter;
		string senderName = player?.CharacterName ?? "You";
		AddMessage(senderName, text, MsgType.Emote, _playerIcColor);

		// Bubble: show first quoted speech, or truncated emote
		var quoteMatch = System.Text.RegularExpressions.Regex.Match(text, "\"(.+?)\"");
		if (quoteMatch.Success)
			ShowBubble($"\"{quoteMatch.Groups[1].Value}\"");
		else
			ShowBubble($"*{(text.Length > 40 ? text[..40] + "..." : text)}*");

		_emoteTextarea.Text = "";
	}

	private void CycleVerb()
	{
		string[] verbs = IsLeaderRank() ? _leaderVerbs : _baseVerbs;
		_currentVerb = (_currentVerb + 1) % verbs.Length;
		string verb = verbs[_currentVerb];
		_verbBtn.Text = $"{verb} ▸";
		_verbBtn.AddThemeColorOverride("font_color", GetVerbColor(verb));
	}

	private void ToggleCollapse()
	{
		_collapsed = !_collapsed;
		_collapseBtn.Text = _collapsed ? "▲" : "▼";

		// Show/hide message area and input area
		var msgArea = _panelContent.GetNodeOrNull<MarginContainer>("MessageArea");
		var inputArea = _panelContent.GetNodeOrNull<MarginContainer>("InputArea");
		if (msgArea != null) msgArea.Visible = !_collapsed;
		if (inputArea != null) inputArea.Visible = !_collapsed;

		_panel.OffsetTop = _collapsed ? -28 : -_panelHeight;
	}

	private void ToggleEmotePanel()
	{
		_emoteOpen = !_emoteOpen;
		_emotePanel.Visible = _emoteOpen;
		_emoteExpandBtn.AddThemeColorOverride("font_color", _emoteOpen ? EmoteColor : UITheme.TextDim);
		if (_emoteOpen) _emoteTextarea.GrabFocus();
	}

	private void ToggleSettings()
	{
		_settingsOpen = !_settingsOpen;
		_settingsPanel.Visible = _settingsOpen;
		_settingsBtn.AddThemeColorOverride("font_color", _settingsOpen ? UITheme.Accent : UITheme.TextDim);
	}

	private void OnExportPressed()
	{
		var filtered = _messages.Where(m => PassesFilter(m.Type)).ToList();
		string text = string.Join("\n", filtered.Select(m =>
		{
			string prefix = m.Type switch
			{
				MsgType.System => "[System]",
				MsgType.Faction => $"[Faction] {m.Sender}:",
				MsgType.Emote => $"★ {m.Sender}",
				MsgType.Ooc => $"[OOC] {m.Sender}:",
				MsgType.Whisper => $"[Whisper] {m.Sender}:",
				MsgType.Yell => $"[Yell] {m.Sender}:",
				MsgType.Story => "[Story]",
				_ => $"{m.Sender}:"
			};
			return $"[{m.Time}] {prefix} {m.Text}";
		}));
		DisplayServer.ClipboardSet(text);
		GD.Print($"[Chat] Exported {filtered.Count} messages to clipboard.");

		// Show toast
		ShowExportToast(filtered.Count);

		// Brief visual feedback on button
		_exportBtn.AddThemeColorOverride("font_color", UITheme.Accent);
		GetTree().CreateTimer(2.0).Timeout += () =>
			_exportBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
	}

	private void AdjustFontSize(int delta)
	{
		SetFontSize(Math.Clamp(_chatFontSize + delta, 10, 18));
	}

	private void SetFontSize(int size)
	{
		_chatFontSize = Math.Clamp(size, 10, 18);
		_messageLog.AddThemeFontSizeOverride("normal_font_size", _chatFontSize);
		_chatInput.AddThemeFontSizeOverride("font_size", _chatFontSize);
		_emoteTextarea.AddThemeFontSizeOverride("font_size", _chatFontSize);
		_textSizeLabel.Text = $"{_chatFontSize}px";
		if (_fontSizeSlider != null) _fontSizeSlider.SetValueNoSignal(_chatFontSize);
	}

	private void OnNameClicked(Variant meta)
	{
		string name = meta.AsString();
		if (string.IsNullOrEmpty(name)) return;

		// Get approximate click position from mouse
		var mousePos = GetViewport().GetMousePosition();
		_profilePopup?.ShowForPlayer(name, mousePos);
	}

	private void BuildProfilePopup()
	{
		_profilePopup = new ProfilePopup();
		_profilePopup.Name = "ProfilePopup";
		// Add to parent so it renders above the chat panel
		CallDeferred(nameof(AddProfilePopupDeferred));
	}

	private void AddProfilePopupDeferred()
	{
		// Add to the HUDLayer (parent of ChatPanel) so it floats above everything
		var parent = GetParent();
		if (parent != null)
			parent.AddChild(_profilePopup);
		else
			AddChild(_profilePopup);
	}

	private void InsertFormat(string marker)
	{
		if (_emoteTextarea == null) return;
		// Simple: insert markers at caret
		int col = _emoteTextarea.GetCaretColumn();
		int line = _emoteTextarea.GetCaretLine();
		_emoteTextarea.InsertTextAtCaret(marker + marker);
		_emoteTextarea.SetCaretColumn(col + marker.Length);
		_emoteTextarea.GrabFocus();
	}

	private void InsertQuote()
	{
		if (_emoteTextarea == null) return;
		_emoteTextarea.InsertTextAtCaret("\"\"");
		int col = _emoteTextarea.GetCaretColumn();
		_emoteTextarea.SetCaretColumn(col - 1);
		_emoteTextarea.GrabFocus();
	}

	// ═════════════════════════════════════════════════════════
	//  MESSAGES
	// ═════════════════════════════════════════════════════════

	public void AddMessage(string sender, string text, MsgType type, Color? senderColor = null)
	{
		string time = DateTime.Now.ToString("HH:mm");
		var msg = new ChatMessage(sender, text, type, time, senderColor ?? SayColor);
		_messages.Add(msg);
		if (_messages.Count > 200) _messages.RemoveAt(0);

		// Faction unread dot
		if (type == MsgType.Faction && _currentTab != "fac")
			if (_tabUnreadDots.ContainsKey("fac")) _tabUnreadDots["fac"].Visible = true;

		if (PassesFilter(type))
			AppendFormatted(msg);
	}

	private bool PassesFilter(MsgType type) => _currentTab switch
	{
		"all" => true,
		"ic" => type is MsgType.Say or MsgType.Whisper or MsgType.Yell or MsgType.Emote or MsgType.Story,
		"ooc" => type == MsgType.Ooc,
		"fac" => type == MsgType.Faction,
		"sys" => type == MsgType.System,
		_ => true
	};

	private void RefreshMessages()
	{
		_messageLog.Clear();
		foreach (var msg in _messages)
			if (PassesFilter(msg.Type)) AppendFormatted(msg);
	}

	private void AppendFormatted(ChatMessage msg)
	{
		string t = $"[color=#78736966]{msg.Time}[/color] ";
		string sc = msg.SenderColor.ToHtml(false); // sender color hex
		string s = Esc(msg.Sender);
		string tx = Esc(msg.Text);

		// For emotes, parse formatting: **bold**, *italic*, "speech"
		string emoteText = ParseEmoteFormatting(msg.Text);

		string bbcode = msg.Type switch
		{
			MsgType.Say => $"{t}[url={s}][b][color=#{sc}]{s}[/color][/b][/url]: [color=#{sc}]\"{ParseFormatting(tx)}\"[/color]",
			MsgType.Whisper => $"{t}[color=#{WhisperColor.ToHtml(false)}][font_size=10][Whisper][/font_size] [url={s}][b]{s}[/b][/url]: {ParseFormatting(tx)}[/color]",
			MsgType.Yell => $"{t}[color=#{YellColor.ToHtml(false)}][font_size=10][Yell][/font_size] [url={s}][b]{s}[/b][/url]: {ParseFormatting(tx)}[/color]",
			MsgType.Emote => $"{t}[color=#{sc}][i]★ [url={s}][b]{s}[/b][/url] {emoteText}[/i][/color]",
			MsgType.Ooc => $"{t}[color=#{OocColor.ToHtml(false)}][font_size=10][OOC][/font_size] [url={s}][b]{s}[/b][/url]: {ParseFormatting(tx)}[/color]",
			MsgType.Faction => $"{t}[color=#{FactionColor.ToHtml(false)}]📢 [font_size=10][Faction][/font_size] [url={s}][b]{s}[/b][/url]: {Esc(msg.Text)}[/color]",
			MsgType.Story => $"{t}[color=#{StoryColor.ToHtml(false)}]📖 [font_size=10][Story][/font_size] [i]{emoteText}[/i][/color]",
			MsgType.System => $"{t}[color=#{SystemColor.ToHtml(false)}]{Esc(msg.Text)}[/color]",
			_ => $"{t}{Esc(msg.Text)}"
		};

		_messageLog.AppendText(bbcode + "\n");
	}

	/// <summary>Parse **bold** and *italic* in regular text.</summary>
	private string ParseFormatting(string text)
	{
		// Bold: **text**
		text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "[b]$1[/b]");
		// Italic: *text*
		text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "[i]$1[/i]");
		return text;
	}

	/// <summary>Parse emote text: "speech" in white, **bold**, *italic*.</summary>
	private string ParseEmoteFormatting(string text)
	{
		string html = Esc(text);
		// Speech quotes → white
		html = System.Text.RegularExpressions.Regex.Replace(html,
			"\"(.+?)\"",
			$"[/i][/color][color=#{SpeechWhite.ToHtml(false)}][b]\"$1\"[/b][/color][color=#{_playerIcColor.ToHtml(false)}][i]");
		// Bold
		html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "[b]$1[/b]");
		// Italic (already in italic context for emotes, so skip)
		return html;
	}

	private static string Esc(string text) => text.Replace("[", "[lb]").Replace("]", "[rb]");

	// ═════════════════════════════════════════════════════════
	//  CHAT BUBBLE
	// ═════════════════════════════════════════════════════════

	private void ShowBubble(string text)
	{
		if (ChatBubbleLabel == null) return;
		ChatBubbleLabel.Text = text;
		ChatBubbleLabel.Visible = true;
		// Also show the parent PanelContainer (BubbleBg)
		var parent = ChatBubbleLabel.GetParent<Control>();
		if (parent != null) { parent.Visible = true; parent.Modulate = Colors.White; }
		ChatBubbleLabel.Modulate = Colors.White;
		_bubbleTimer = 4f;
	}

	// ═════════════════════════════════════════════════════════
	//  HELPERS
	// ═════════════════════════════════════════════════════════

	private bool IsLeaderRank()
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return false;
		string rank = p.RpRank?.ToLower() ?? "";
		return rank is "justicar" or "banneret" or "lord commander" or "marshal" or "kage";
	}

	private string[] GetVerbs() => IsLeaderRank() ? _leaderVerbs : _baseVerbs;

	private static Color GetVerbColor(string verb) => verb switch
	{
		"Say" => SayColor, "Whisper" => WhisperColor, "Yell" => YellColor,
		"Emote" => EmoteColor, "OOC" => OocColor, "Faction" => FactionColor, "Story" => StoryColor,
		_ => SayColor
	};

	private void AddWelcomeMessages()
	{
		AddMessage("", "Welcome to Project Tactics. Press Enter to chat.", MsgType.System);
		AddMessage("", "Tab: cycle verb · ✎: emote panel · ⚙: settings · Esc: collapse", MsgType.System);
	}

	private static void ApplyGhostStyle(Button btn)
	{
		var s = new StyleBoxFlat();
		s.BgColor = Colors.Transparent;
		s.SetCornerRadiusAll(3);
		s.ContentMarginLeft = 8; s.ContentMarginRight = 8;
		s.ContentMarginTop = 2; s.ContentMarginBottom = 2;
		btn.AddThemeStyleboxOverride("normal", s);
		var h = (StyleBoxFlat)s.Duplicate();
		h.BgColor = new Color(1, 1, 1, 0.04f);
		btn.AddThemeStyleboxOverride("hover", h);
		btn.AddThemeStyleboxOverride("pressed", s);
	}

	private static Button MakeUtilButton(string text, string tooltip)
	{
		var btn = new Button();
		btn.Text = text;
		btn.TooltipText = tooltip;
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", UITheme.TextDim);
		btn.AddThemeColorOverride("font_hover_color", UITheme.Text);
		ApplyGhostStyle(btn);
		return btn;
	}

	private static Button MakeToolButton(string text, string tooltip)
	{
		var btn = new Button();
		btn.Text = text;
		btn.TooltipText = tooltip;
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", UITheme.TextDim);
		btn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
		if (UITheme.FontBody != null) btn.AddThemeFontOverride("font", UITheme.FontBody);
		ApplyGhostStyle(btn);
		return btn;
	}

	private static Control MakeToolSep()
	{
		var sep = new ColorRect();
		sep.CustomMinimumSize = new Vector2(1, 14);
		sep.Color = new Color(0.235f, 0.255f, 0.314f, 0.3f);
		sep.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		return sep;
	}
}
