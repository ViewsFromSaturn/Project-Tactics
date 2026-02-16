using Godot;
using System;

namespace ProjectTactics.UI;

/// <summary>
/// Reusable floating window with glass effect and animations.
/// Converted from WPF FloatingWindow.xaml — Taskade design, untouched.
/// Draggable by title bar, closeable with ✕, resizable via corner handle.
/// </summary>
public partial class FloatingWindow : Control
{
	// ═══ SIGNALS ═══
	[Signal] public delegate void WindowClosedEventHandler();

	// ═══ CONFIG ═══
	private string _title;
	private Control _content;
	private Vector2 _windowSize;
	private Vector2 _minSize = new(300, 300);

	// ═══ STATE ═══
	private bool _dragging;
	private Vector2 _dragOffset;
	private bool _resizing;
	private Vector2 _resizeStart;
	private Vector2 _resizeStartSize;
	private float _fadeProgress = 0f;
	private bool _fadingIn = true;
	private bool _fadingOut = false;

	// ═══ NODES ═══
	private Panel _outerPanel;
	private Panel _titleBar;
	private Label _titleLabel;
	private Button _closeBtn;
	private Control _contentContainer;
	private Control _resizeHandle;

	// ═══ THEME — matches Taskade Brushes.xaml exactly ═══
	static readonly Color BgGlass       = new(0.031f, 0.031f, 0.071f, 0.85f);  // #D9080812 @ 85%
	static readonly Color BgTitleBar    = new(0.031f, 0.031f, 0.071f, 0.08f);  // #14080812
	static readonly Color BorderColor   = new(0.235f, 0.255f, 0.314f, 0.35f);  // #593C4150
	static readonly Color TextTitle     = new("D4D2CC");
	static readonly Color TextDim       = new("64647A");
	static readonly Color CloseHoverBg  = new(0.784f, 0.314f, 0.314f, 0.2f);   // #33C85050
	static readonly Color CloseHoverFg  = new("C85050");
	static readonly Color AccentViolet  = new("8B5CF6");

	public FloatingWindow(string title, Control content, float width = 400, float height = 500)
	{
		_title = title;
		_content = content;
		_windowSize = new Vector2(width, height);
	}

	// ═══ STATIC FACTORY ═══

	/// <summary>
	/// Create and add a FloatingWindow to the given parent node.
	/// Returns the window instance for tracking/positioning.
	/// </summary>
	public static FloatingWindow Open(Node parent, string title, Control content, float width = 400, float height = 500)
	{
		var window = new FloatingWindow(title, content, width, height);
		parent.AddChild(window);
		return window;
	}

	public override void _Ready()
	{
		// Root control — full screen overlay, ignore mouse except on children
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		BuildWindow();

		// Start invisible for fade-in
		_outerPanel.Modulate = new Color(1, 1, 1, 0);
		_fadingIn = true;
		_fadeProgress = 0f;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Fade-in animation (200ms)
		if (_fadingIn)
		{
			_fadeProgress += dt / 0.2f;
			if (_fadeProgress >= 1f)
			{
				_fadeProgress = 1f;
				_fadingIn = false;
			}
			float ease = 1f - Mathf.Pow(1f - _fadeProgress, 3f); // cubic ease out
			_outerPanel.Modulate = new Color(1, 1, 1, ease);
		}

		// Fade-out animation (150ms)
		if (_fadingOut)
		{
			_fadeProgress -= dt / 0.15f;
			if (_fadeProgress <= 0f)
			{
				_fadeProgress = 0f;
				_fadingOut = false;
				EmitSignal(SignalName.WindowClosed);
				QueueFree();
				return;
			}
			float ease = Mathf.Pow(_fadeProgress, 2f); // cubic ease in
			_outerPanel.Modulate = new Color(1, 1, 1, ease);
		}
	}

	// ═══ BUILD ═══

	private void BuildWindow()
	{
		_outerPanel = new Panel();
		_outerPanel.MouseFilter = MouseFilterEnum.Stop;
		_outerPanel.CustomMinimumSize = _minSize;
		_outerPanel.Size = _windowSize;

		// Center on screen
		var viewport = GetViewportRect().Size;
		_outerPanel.Position = (viewport - _windowSize) / 2f;

		// Glass background + border + rounded corners
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = BgGlass;
		panelStyle.SetCornerRadiusAll(12);
		panelStyle.BorderColor = BorderColor;
		panelStyle.SetBorderWidthAll(1);
		// Shadow approximation via expand margin
		panelStyle.ShadowColor = new Color(0, 0, 0, 0.4f);
		panelStyle.ShadowSize = 15;
		panelStyle.ShadowOffset = Vector2.Zero;
		_outerPanel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_outerPanel);

		// Internal layout: title bar + content
		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 0);
		_outerPanel.AddChild(vbox);

		// ─── Title Bar ───
		_titleBar = new Panel();
		_titleBar.CustomMinimumSize = new Vector2(0, 44);
		_titleBar.MouseFilter = MouseFilterEnum.Stop;

		var titleStyle = new StyleBoxFlat();
		titleStyle.BgColor = BgTitleBar;
		titleStyle.CornerRadiusTopLeft = 12;
		titleStyle.CornerRadiusTopRight = 12;
		titleStyle.BorderColor = BorderColor;
		titleStyle.BorderWidthBottom = 1;
		titleStyle.ContentMarginLeft = 16;
		titleStyle.ContentMarginRight = 16;
		titleStyle.ContentMarginTop = 12;
		titleStyle.ContentMarginBottom = 12;
		_titleBar.AddThemeStyleboxOverride("panel", titleStyle);
		vbox.AddChild(_titleBar);

		// Title text
		_titleLabel = new Label();
		_titleLabel.Text = _title;
		_titleLabel.AddThemeFontSizeOverride("font_size", 14);
		_titleLabel.AddThemeColorOverride("font_color", TextTitle);
		if (UITheme.FontTitleMedium != null)
			_titleLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		_titleLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
		_titleLabel.OffsetLeft = 16;
		_titleBar.AddChild(_titleLabel);

		// Close button ✕
		_closeBtn = new Button();
		_closeBtn.Text = "✕";
		_closeBtn.FocusMode = FocusModeEnum.None;
		_closeBtn.CustomMinimumSize = new Vector2(32, 32);
		_closeBtn.AddThemeFontSizeOverride("font_size", 16);
		_closeBtn.AddThemeColorOverride("font_color", TextDim);
		_closeBtn.AddThemeColorOverride("font_hover_color", CloseHoverFg);
		_closeBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight);
		_closeBtn.OffsetLeft = -48;
		_closeBtn.OffsetRight = -16;

		// Close button styles
		var closeBtnNormal = new StyleBoxFlat();
		closeBtnNormal.BgColor = Colors.Transparent;
		closeBtnNormal.SetCornerRadiusAll(4);
		_closeBtn.AddThemeStyleboxOverride("normal", closeBtnNormal);

		var closeBtnHover = new StyleBoxFlat();
		closeBtnHover.BgColor = CloseHoverBg;
		closeBtnHover.SetCornerRadiusAll(4);
		_closeBtn.AddThemeStyleboxOverride("hover", closeBtnHover);

		_closeBtn.AddThemeStyleboxOverride("pressed", closeBtnNormal);
		_closeBtn.Pressed += OnClosePressed;
		_titleBar.AddChild(_closeBtn);

		// Title bar drag handling
		_titleBar.GuiInput += OnTitleBarInput;

		// ─── Content Area ───
		_contentContainer = new MarginContainer();
		_contentContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		_contentContainer.AddThemeConstantOverride("margin_left", 0);
		_contentContainer.AddThemeConstantOverride("margin_right", 0);
		_contentContainer.AddThemeConstantOverride("margin_top", 0);
		_contentContainer.AddThemeConstantOverride("margin_bottom", 0);
		vbox.AddChild(_contentContainer);

		if (_content != null)
			_contentContainer.AddChild(_content);

		// ─── Resize Handle (bottom-right corner) ───
		_resizeHandle = new Control();
		_resizeHandle.CustomMinimumSize = new Vector2(16, 16);
		_resizeHandle.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_resizeHandle.OffsetLeft = -16;
		_resizeHandle.OffsetTop = -16;
		_resizeHandle.MouseFilter = MouseFilterEnum.Stop;
		_resizeHandle.MouseDefaultCursorShape = CursorShape.Fdiagsize;
		_resizeHandle.GuiInput += OnResizeInput;
		_outerPanel.AddChild(_resizeHandle);
	}

	// ═══ TITLE BAR DRAG ═══

	private void OnTitleBarInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_dragging = true;
					_dragOffset = GetGlobalMousePosition() - _outerPanel.GlobalPosition;
					// Bring to front
					MoveToFront();
				}
				else
				{
					_dragging = false;
				}
			}
		}
		else if (@event is InputEventMouseMotion && _dragging)
		{
			_outerPanel.GlobalPosition = GetGlobalMousePosition() - _dragOffset;
		}
	}

	// ═══ RESIZE HANDLE ═══

	private void OnResizeInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_resizing = true;
					_resizeStart = GetGlobalMousePosition();
					_resizeStartSize = _outerPanel.Size;
					MoveToFront();
				}
				else
				{
					_resizing = false;
				}
			}
		}
		else if (@event is InputEventMouseMotion && _resizing)
		{
			var delta = GetGlobalMousePosition() - _resizeStart;
			var newSize = _resizeStartSize + delta;
			newSize.X = Mathf.Max(newSize.X, _minSize.X);
			newSize.Y = Mathf.Max(newSize.Y, _minSize.Y);
			_outerPanel.Size = newSize;
		}
	}

	// ═══ CLOSE ═══

	private void OnClosePressed()
	{
		if (_fadingOut) return;
		_fadingOut = true;
		_fadeProgress = 1f;
	}

	/// <summary>Close the window with fade-out animation.</summary>
	public void CloseWindow()
	{
		OnClosePressed();
	}

	/// <summary>Close the window immediately without animation.</summary>
	public void CloseImmediate()
	{
		EmitSignal(SignalName.WindowClosed);
		QueueFree();
	}

	/// <summary>Set the window title.</summary>
	public void SetTitle(string title)
	{
		_title = title;
		if (_titleLabel != null) _titleLabel.Text = title;
	}

	/// <summary>Set window position on screen.</summary>
	public void SetWindowPosition(Vector2 pos)
	{
		if (_outerPanel != null) _outerPanel.Position = pos;
	}

	/// <summary>Bring this window to front of all siblings.</summary>
	public new void MoveToFront()
	{
		var parent = GetParent();
		if (parent != null)
		{
			parent.MoveChild(this, -1);
		}
	}
}
