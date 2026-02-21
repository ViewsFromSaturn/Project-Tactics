using Godot;
using System;

namespace ProjectTactics.UI;

/// <summary>
/// Reusable floating window with glass effect and animations.
/// All colors routed through UITheme for light/dark mode support.
/// Draggable by title bar, closeable with ✕, resizable via corner handle.
/// </summary>
public partial class FloatingWindow : Control
{
	[Signal] public delegate void WindowClosedEventHandler();

	private string _title;
	private Control _content;
	private Vector2 _windowSize;
	private Vector2 _minSize = new(300, 300);

	private bool _dragging;
	private Vector2 _dragOffset;
	private bool _resizing;
	private Vector2 _resizeStart;
	private Vector2 _resizeStartSize;
	private float _fadeProgress = 0f;
	private bool _fadingIn = true;
	private bool _fadingOut = false;

	private Panel _outerPanel;
	private Panel _titleBar;
	private Label _titleLabel;
	private Button _closeBtn;
	private Control _contentContainer;
	private Control _resizeHandle;

	public FloatingWindow(string title, Control content, float width = 400, float height = 500)
	{
		_title = title;
		_content = content;
		_windowSize = new Vector2(width, height);
	}

	public static FloatingWindow Open(Node parent, string title, Control content, float width = 400, float height = 500)
	{
		var window = new FloatingWindow(title, content, width, height);
		parent.AddChild(window);
		return window;
	}

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		BuildWindow();
		_outerPanel.Modulate = new Color(1, 1, 1, 0);
		_fadingIn = true;
		_fadeProgress = 0f;

		UITheme.ThemeChanged += OnThemeChanged;
	}

	public override void _ExitTree()
	{
		UITheme.ThemeChanged -= OnThemeChanged;
	}

	private void OnThemeChanged(bool _dark)
	{
		// Update outer panel glass style in-place
		if (_outerPanel != null)
		{
			var panelStyle = _outerPanel.GetThemeStylebox("panel") as StyleBoxFlat;
			if (panelStyle != null)
			{
				panelStyle.BgColor = UITheme.BgGlass;
				panelStyle.BorderColor = UITheme.GlassBorder;
				panelStyle.ShadowColor = UITheme.GlassShadow;
			}
		}

		// Update title bar style in-place
		if (_titleBar != null)
		{
			var titleStyle = _titleBar.GetThemeStylebox("panel") as StyleBoxFlat;
			if (titleStyle != null)
			{
				titleStyle.BgColor = UITheme.BgTitleBar;
				titleStyle.BorderColor = UITheme.GlassBorder;
			}
		}

		// Update title label color
		if (_titleLabel != null)
			_titleLabel.AddThemeColorOverride("font_color", UITheme.TextBright);

		// Update close button colors
		if (_closeBtn != null)
		{
			_closeBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
			var hoverStyle = _closeBtn.GetThemeStylebox("hover") as StyleBoxFlat;
			if (hoverStyle != null) hoverStyle.BgColor = UITheme.AccentRedDim;
		}

		// Tell the content panel to rebuild with new colors
		if (_content is Panels.WindowPanel wp)
			wp.Refresh();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_fadingIn)
		{
			_fadeProgress += dt / 0.2f;
			if (_fadeProgress >= 1f) { _fadeProgress = 1f; _fadingIn = false; }
			float ease = 1f - Mathf.Pow(1f - _fadeProgress, 3f);
			_outerPanel.Modulate = new Color(1, 1, 1, ease);
		}

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
			float ease = Mathf.Pow(_fadeProgress, 2f);
			_outerPanel.Modulate = new Color(1, 1, 1, ease);
		}
	}

	private void BuildWindow()
	{
		_outerPanel = new Panel();
		_outerPanel.MouseFilter = MouseFilterEnum.Stop;
		_outerPanel.CustomMinimumSize = _minSize;
		_outerPanel.Size = _windowSize;

		var viewport = GetViewportRect().Size;
		_outerPanel.Position = (viewport - _windowSize) / 2f;

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = UITheme.BgGlass;
		panelStyle.SetCornerRadiusAll(12);
		panelStyle.BorderColor = UITheme.GlassBorder;
		panelStyle.SetBorderWidthAll(1);
		panelStyle.ShadowColor = UITheme.GlassShadow;
		panelStyle.ShadowSize = 15;
		panelStyle.ShadowOffset = Vector2.Zero;
		_outerPanel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_outerPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 0);
		_outerPanel.AddChild(vbox);

		// ─── Title Bar ───
		_titleBar = new Panel();
		_titleBar.CustomMinimumSize = new Vector2(0, 44);
		_titleBar.MouseFilter = MouseFilterEnum.Stop;

		var titleStyle = new StyleBoxFlat();
		titleStyle.BgColor = UITheme.BgTitleBar;
		titleStyle.CornerRadiusTopLeft = 12;
		titleStyle.CornerRadiusTopRight = 12;
		titleStyle.BorderColor = UITheme.GlassBorder;
		titleStyle.BorderWidthBottom = 1;
		titleStyle.ContentMarginLeft = 16;
		titleStyle.ContentMarginRight = 8;
		titleStyle.ContentMarginTop = 6;
		titleStyle.ContentMarginBottom = 6;
		_titleBar.AddThemeStyleboxOverride("panel", titleStyle);
		vbox.AddChild(_titleBar);

		// Use HBoxContainer so title and close button share space properly
		var titleRow = new HBoxContainer();
		titleRow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		titleRow.OffsetLeft = 16;
		titleRow.OffsetRight = -8;
		titleRow.AddThemeConstantOverride("separation", 8);
		_titleBar.AddChild(titleRow);

		_titleLabel = new Label();
		_titleLabel.Text = _title;
		_titleLabel.AddThemeFontSizeOverride("font_size", 14);
		_titleLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_titleLabel.VerticalAlignment = VerticalAlignment.Center;
		_titleLabel.ClipText = true;
		if (UITheme.FontTitleMedium != null)
			_titleLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		titleRow.AddChild(_titleLabel);

		// Close button ✕
		_closeBtn = new Button();
		_closeBtn.Text = "✕";
		_closeBtn.FocusMode = FocusModeEnum.None;
		_closeBtn.CustomMinimumSize = new Vector2(32, 32);
		_closeBtn.AddThemeFontSizeOverride("font_size", 16);
		_closeBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
		_closeBtn.AddThemeColorOverride("font_hover_color", UITheme.AccentRed);
		_closeBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var closeBtnNormal = new StyleBoxFlat();
		closeBtnNormal.BgColor = Colors.Transparent;
		closeBtnNormal.SetCornerRadiusAll(4);
		_closeBtn.AddThemeStyleboxOverride("normal", closeBtnNormal);

		var closeBtnHover = new StyleBoxFlat();
		closeBtnHover.BgColor = UITheme.AccentRedDim;
		closeBtnHover.SetCornerRadiusAll(4);
		_closeBtn.AddThemeStyleboxOverride("hover", closeBtnHover);
		_closeBtn.AddThemeStyleboxOverride("pressed", closeBtnNormal);

		_closeBtn.Pressed += OnClosePressed;
		titleRow.AddChild(_closeBtn);

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

		// ─── Resize Handle ───
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
					MoveToFront();
				}
				else _dragging = false;
			}
		}
		else if (@event is InputEventMouseMotion && _dragging)
		{
			_outerPanel.GlobalPosition = GetGlobalMousePosition() - _dragOffset;
		}
	}

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
				else _resizing = false;
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

	private void OnClosePressed()
	{
		if (_fadingOut) return;
		_fadingOut = true;
		_fadeProgress = 1f;
	}

	public void CloseWindow() => OnClosePressed();

	public void CloseImmediate()
	{
		EmitSignal(SignalName.WindowClosed);
		QueueFree();
	}

	public void SetTitle(string title)
	{
		_title = title;
		if (_titleLabel != null) _titleLabel.Text = title;
	}

	public void SetWindowPosition(Vector2 pos)
	{
		if (_outerPanel != null) _outerPanel.Position = pos;
	}

	public new void MoveToFront()
	{
		var parent = GetParent();
		if (parent != null) parent.MoveChild(this, -1);
	}
}
