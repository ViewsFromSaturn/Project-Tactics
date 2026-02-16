using Godot;
using System;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Base class for slide-in panels (FFXIV-style).
/// Slides from left or right edge with animation.
/// Subclasses override BuildContent() to fill the panel body.
/// </summary>
public abstract partial class SlidePanel : Control
{
	public enum SlideDirection { Left, Right }

	// Config — set in subclass constructor or before _Ready
	protected string PanelTitle = "Panel";
	protected SlideDirection Direction = SlideDirection.Right;
	protected float PanelWidth = 340f;
	protected float AnimDuration = 0.25f;

	// State
	private bool _isOpen = false;
	public bool IsOpen => _isOpen;

	// UI refs
	private PanelContainer _panel;
	private VBoxContainer _content;
	private Tween _tween;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildPanel();
		// Start offscreen
		_panel.Position = GetClosedPosition();
		_panel.Visible = false;
	}

	// ═════════════════════════════════════════════════════════
	//  PANEL STRUCTURE
	// ═════════════════════════════════════════════════════════

	private void BuildPanel()
	{
		_panel = new PanelContainer();
		_panel.CustomMinimumSize = new Vector2(PanelWidth, 0);
		_panel.Size = new Vector2(PanelWidth, 0);
		_panel.MouseFilter = MouseFilterEnum.Stop;

		// Anchors: full height, fixed width
		_panel.AnchorTop = 0;
		_panel.AnchorBottom = 1;

		if (Direction == SlideDirection.Right)
		{
			_panel.AnchorLeft = 1;
			_panel.AnchorRight = 1;
			_panel.OffsetLeft = -PanelWidth;
			_panel.OffsetRight = 0;
		}
		else
		{
			_panel.AnchorLeft = 0;
			_panel.AnchorRight = 0;
			_panel.OffsetLeft = 0;
			_panel.OffsetRight = PanelWidth;
		}

		_panel.OffsetTop = 0;
		_panel.OffsetBottom = 0;

		// Style
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.035f, 0.035f, 0.063f, 0.94f);
		style.BorderColor = UITheme.BorderLight;

		if (Direction == SlideDirection.Right)
			style.BorderWidthLeft = 1;
		else
			style.BorderWidthRight = 1;

		style.ContentMarginLeft = 0;
		style.ContentMarginRight = 0;
		style.ContentMarginTop = 0;
		style.ContentMarginBottom = 0;
		_panel.AddThemeStyleboxOverride("panel", style);

		AddChild(_panel);

		// Inner layout
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		_panel.AddChild(vbox);

		// Header
		BuildHeader(vbox);

		// Separator
		var sep = new PanelContainer();
		sep.CustomMinimumSize = new Vector2(0, 1);
		var sepStyle = new StyleBoxFlat();
		sepStyle.BgColor = UITheme.BorderLight;
		sep.AddThemeStyleboxOverride("panel", sepStyle);
		vbox.AddChild(sep);

		// Scrollable content area
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(scroll);

		var contentMargin = new MarginContainer();
		contentMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		contentMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
		contentMargin.AddThemeConstantOverride("margin_left", 16);
		contentMargin.AddThemeConstantOverride("margin_right", 16);
		contentMargin.AddThemeConstantOverride("margin_top", 12);
		contentMargin.AddThemeConstantOverride("margin_bottom", 12);
		scroll.AddChild(contentMargin);

		_content = new VBoxContainer();
		_content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_content.AddThemeConstantOverride("separation", 8);
		contentMargin.AddChild(_content);

		// Let subclass fill content
		BuildContent(_content);
	}

	private void BuildHeader(VBoxContainer parent)
	{
		var headerMargin = new MarginContainer();
		headerMargin.AddThemeConstantOverride("margin_left", 16);
		headerMargin.AddThemeConstantOverride("margin_right", 10);
		headerMargin.AddThemeConstantOverride("margin_top", 10);
		headerMargin.AddThemeConstantOverride("margin_bottom", 10);
		parent.AddChild(headerMargin);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 8);
		headerMargin.AddChild(hbox);

		var titleLabel = new Label();
		titleLabel.Text = PanelTitle;
		titleLabel.AddThemeFontSizeOverride("font_size", 14);
		titleLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null)
			titleLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.AddChild(titleLabel);

		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.AddThemeFontSizeOverride("font_size", 13);
		closeBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
		closeBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);

		var closeBtnStyle = new StyleBoxFlat();
		closeBtnStyle.BgColor = Colors.Transparent;
		closeBtnStyle.ContentMarginLeft = 6;
		closeBtnStyle.ContentMarginRight = 6;
		closeBtnStyle.ContentMarginTop = 2;
		closeBtnStyle.ContentMarginBottom = 2;
		closeBtn.AddThemeStyleboxOverride("normal", closeBtnStyle);
		closeBtn.AddThemeStyleboxOverride("hover", closeBtnStyle);
		closeBtn.AddThemeStyleboxOverride("pressed", closeBtnStyle);

		closeBtn.Pressed += () => Close();
		hbox.AddChild(closeBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  ABSTRACT — Subclasses fill this
	// ═════════════════════════════════════════════════════════

	protected abstract void BuildContent(VBoxContainer content);

	/// <summary>Called each time the panel opens. Override to refresh data.</summary>
	protected virtual void OnOpen() { }

	/// <summary>Called each time the panel closes.</summary>
	protected virtual void OnClose() { }

	// ═════════════════════════════════════════════════════════
	//  OPEN / CLOSE / TOGGLE
	// ═════════════════════════════════════════════════════════

	public void Open()
	{
		if (_isOpen) return;
		_isOpen = true;
		_panel.Visible = true;
		OnOpen();
		AnimateTo(GetOpenPosition());
	}

	public void Close()
	{
		if (!_isOpen) return;
		_isOpen = false;
		OnClose();
		AnimateTo(GetClosedPosition(), () => _panel.Visible = false);
	}

	public void Toggle()
	{
		if (_isOpen) Close();
		else Open();
	}

	// ═════════════════════════════════════════════════════════
	//  ANIMATION
	// ═════════════════════════════════════════════════════════

	private Vector2 GetOpenPosition()
	{
		if (Direction == SlideDirection.Right)
			return new Vector2(GetViewportRect().Size.X - PanelWidth, 0);
		else
			return new Vector2(0, 0);
	}

	private Vector2 GetClosedPosition()
	{
		if (Direction == SlideDirection.Right)
			return new Vector2(GetViewportRect().Size.X, 0);
		else
			return new Vector2(-PanelWidth, 0);
	}

	private void AnimateTo(Vector2 target, Action onComplete = null)
	{
		_tween?.Kill();
		_tween = CreateTween();
		_tween.SetTrans(Tween.TransitionType.Cubic);
		_tween.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(_panel, "position", target, AnimDuration);
		if (onComplete != null)
			_tween.TweenCallback(Callable.From(onComplete));
	}

	// ═════════════════════════════════════════════════════════
	//  HELPERS for subclasses
	// ═════════════════════════════════════════════════════════

	protected static Label SectionHeader(string text)
	{
		var label = UITheme.CreateDim(text.ToUpper(), 10);
		label.AddThemeConstantOverride("margin_top", 8);
		return label;
	}

	protected static HSeparator ThinSeparator()
	{
		return UITheme.CreateSeparator();
	}

	protected static Label InfoRow(string label, string value)
	{
		var l = UITheme.CreateBody($"{label}: {value}", 13, UITheme.Text);
		return l;
	}

	protected static Label PlaceholderText(string text)
	{
		var l = UITheme.CreateBody(text, 13, UITheme.TextDim);
		l.AutowrapMode = TextServer.AutowrapMode.Word;
		return l;
	}
}
