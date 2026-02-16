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
	// Supports both naming conventions (PanelTitle/WindowTitle, etc.)

	public string PanelTitle { get; set; } = "Panel";

	/// <summary>Alias for PanelTitle — some panels use this name.</summary>
	public string WindowTitle
	{
		get => PanelTitle;
		set => PanelTitle = value;
	}

	public float DefaultWidth { get; set; } = 400f;
	public float DefaultHeight { get; set; } = 500f;

	/// <summary>Set both width and height at once via Vector2.</summary>
	public Vector2 DefaultSize
	{
		get => new Vector2(DefaultWidth, DefaultHeight);
		set { DefaultWidth = value.X; DefaultHeight = value.Y; }
	}

	/// <summary>Optional default screen position. Zero = center.</summary>
	public Vector2 DefaultPosition { get; set; } = Vector2.Zero;

	// ═══ OPEN / CLOSE STATE ═══
	public bool IsOpen { get; private set; } = false;

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

	/// <summary>Called when the panel is closed. Override for cleanup.</summary>
	public virtual void OnClose() { }

	// ═══ OPEN / CLOSE / TOGGLE ═══

	public void Open()
	{
		if (IsOpen) return;
		IsOpen = true;
		Visible = true;
		OnOpen();
	}

	public void Close()
	{
		if (!IsOpen) return;
		IsOpen = false;
		OnClose();
		Visible = false;
	}

	public void Toggle()
	{
		if (IsOpen) Close();
		else Open();
	}

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

	// ═══ HELPERS for subclasses ═══

	protected static Label SectionHeader(string text)
	{
		var label = new Label();
		label.Text = text.ToUpper();
		label.AddThemeColorOverride("font_color", UITheme.TextDim);
		label.AddThemeFontSizeOverride("font_size", 10);
		if (UITheme.FontBodySemiBold != null)
			label.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		return label;
	}

	protected static Control ThinSeparator()
	{
		return UITheme.CreateSeparator();
	}

	protected static Label InfoRow(string label, string value)
	{
		var l = new Label();
		l.Text = $"{label}: {value}";
		l.AddThemeColorOverride("font_color", UITheme.Text);
		l.AddThemeFontSizeOverride("font_size", 13);
		if (UITheme.FontBody != null) l.AddThemeFontOverride("font", UITheme.FontBody);
		return l;
	}

	protected static Label PlaceholderText(string text)
	{
		var l = new Label();
		l.Text = text;
		l.AddThemeColorOverride("font_color", UITheme.TextDim);
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
