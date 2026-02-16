using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Base class for floating window panel content.
/// Replaces SlidePanel — no slide animation, just builds a scrollable content area
/// that gets dropped into a FloatingWindow.
/// 
/// Subclasses override BuildContent() to fill the panel body.
/// Call Refresh() to rebuild content (e.g., when data changes).
/// </summary>
public abstract partial class WindowPanel : MarginContainer
{
	// ═══ CONFIG — set in subclass constructor ═══
	public string PanelTitle { get; set; } = "Panel";
	public float DefaultWidth { get; set; } = 400f;
	public float DefaultHeight { get; set; } = 500f;

	// ═══ INTERNAL ═══
	private ScrollContainer _scroll;
	private VBoxContainer _content;
	private bool _built = false;

	public override void _Ready()
	{
		// Margins around content inside the FloatingWindow
		AddThemeConstantOverride("margin_left", 0);
		AddThemeConstantOverride("margin_right", 0);
		AddThemeConstantOverride("margin_top", 0);
		AddThemeConstantOverride("margin_bottom", 0);

		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;

		Build();
	}

	private void Build()
	{
		if (_built) return;
		_built = true;

		// Scroll container fills the window
		_scroll = new ScrollContainer();
		_scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		_scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(_scroll);

		// Content margin inside scroll
		var margin = new MarginContainer();
		margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		margin.SizeFlagsVertical = SizeFlags.ExpandFill;
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		_scroll.AddChild(margin);

		// VBox for stacking content
		_content = new VBoxContainer();
		_content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_content.AddThemeConstantOverride("separation", 8);
		margin.AddChild(_content);

		// Let subclass fill it
		BuildContent(_content);
	}

	// ═══ ABSTRACT ═══

	/// <summary>Fill the content VBox with your panel's UI.</summary>
	protected abstract void BuildContent(VBoxContainer content);

	/// <summary>Called when the panel is opened or refreshed. Override to update data.</summary>
	public virtual void OnOpen() { }

	// ═══ PUBLIC API ═══

	/// <summary>Clear and rebuild all content.</summary>
	public void Refresh()
	{
		if (_content == null) return;

		foreach (var child in _content.GetChildren())
			child.QueueFree();

		BuildContent(_content);
	}

	/// <summary>Get the content VBox for direct manipulation.</summary>
	protected VBoxContainer ContentBox => _content;

	// ═══ HELPERS for subclasses (ported from SlidePanel) ═══

	protected static Label SectionHeader(string text)
	{
		var label = new Label();
		label.Text = text.ToUpper();
		label.AddThemeColorOverride("font_color", TaskadeTheme.TextDim);
		label.AddThemeFontSizeOverride("font_size", 10);
		if (UITheme.FontBodySemiBold != null)
			label.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		return label;
	}

	protected static Control ThinSeparator()
	{
		return TaskadeTheme.CreateSeparator();
	}

	protected static Label InfoRow(string label, string value)
	{
		var l = new Label();
		l.Text = $"{label}: {value}";
		l.AddThemeColorOverride("font_color", TaskadeTheme.TextPrimary);
		l.AddThemeFontSizeOverride("font_size", 13);
		if (UITheme.FontBody != null) l.AddThemeFontOverride("font", UITheme.FontBody);
		return l;
	}

	protected static Label PlaceholderText(string text)
	{
		var l = new Label();
		l.Text = text;
		l.AddThemeColorOverride("font_color", TaskadeTheme.TextDim);
		l.AddThemeFontSizeOverride("font_size", 13);
		if (UITheme.FontBody != null) l.AddThemeFontOverride("font", UITheme.FontBody);
		l.AutowrapMode = TextServer.AutowrapMode.Word;
		return l;
	}

	protected static Control Spacer(float height = 8)
	{
		var s = new Control();
		s.CustomMinimumSize = new Vector2(0, height);
		return s;
	}
}
