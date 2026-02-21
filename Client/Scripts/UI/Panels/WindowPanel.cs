using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Base class for floating window panel content.
/// Subclasses override BuildContent() to fill the panel body.
/// Override OnDataChanged() to refresh when PlayerData changes.
/// </summary>
public abstract partial class WindowPanel : MarginContainer
{
	public string PanelTitle { get; set; } = "Panel";

	public string WindowTitle
	{
		get => PanelTitle;
		set => PanelTitle = value;
	}

	public float DefaultWidth { get; set; } = 400f;
	public float DefaultHeight { get; set; } = 500f;

	/// <summary>
	/// If true, BuildContent receives a direct VBoxContainer with no outer ScrollContainer.
	/// Use for panels that manage their own scroll (e.g. AbilityShopPanel's 3-column layout).
	/// </summary>
	public bool ManagesOwnScroll { get; set; } = false;

	public Vector2 DefaultSize
	{
		get => new Vector2(DefaultWidth, DefaultHeight);
		set { DefaultWidth = value.X; DefaultHeight = value.Y; }
	}

	public Vector2 DefaultPosition { get; set; } = Vector2.Zero;

	public bool IsOpen { get; private set; } = false;

	private ScrollContainer _scroll;
	private VBoxContainer _content;
	private bool _built = false;
	private bool _subscribed = false;

	public override void _Ready()
	{
		AddThemeConstantOverride("margin_left", 0);
		AddThemeConstantOverride("margin_right", 0);
		AddThemeConstantOverride("margin_top", 0);
		AddThemeConstantOverride("margin_bottom", 0);

		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;

		Build();
	}

	public override void _ExitTree()
	{
		Unsubscribe();
	}

	private void Build()
	{
		if (_built) return;
		_built = true;

		if (ManagesOwnScroll)
		{
			// Panel manages its own scroll — give it a direct VBoxContainer
			_content = new VBoxContainer();
			_content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_content.SizeFlagsVertical = SizeFlags.ExpandFill;
			_content.AddThemeConstantOverride("separation", 8);
			AddChild(_content);
		}
		else
		{
			_scroll = new ScrollContainer();
			_scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			_scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			AddChild(_scroll);

			var margin = new MarginContainer();
			margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			margin.SizeFlagsVertical = SizeFlags.ExpandFill;
			margin.AddThemeConstantOverride("margin_left", 20);
			margin.AddThemeConstantOverride("margin_right", 20);
			margin.AddThemeConstantOverride("margin_top", 16);
			margin.AddThemeConstantOverride("margin_bottom", 16);
			_scroll.AddChild(margin);

			_content = new VBoxContainer();
			_content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_content.AddThemeConstantOverride("separation", 8);
			margin.AddChild(_content);
		}

		BuildContent(_content);
	}

	protected abstract void BuildContent(VBoxContainer content);

	/// <summary>Called when the panel is opened. Subscribe to data here.</summary>
	public virtual void OnOpen()
	{
		Subscribe();
	}

	/// <summary>Safe wrapper for CallDeferred from OverworldHUD.</summary>
	public void DeferredOpen() => OnOpen();

	/// <summary>Called when the panel is closed. Unsubscribe from data here.</summary>
	public virtual void OnClose()
	{
		Unsubscribe();
	}

	/// <summary>
	/// Called when PlayerData.DataChanged fires while the panel is open.
	/// Default implementation calls Refresh(). Override for selective updates.
	/// </summary>
	protected virtual void OnDataChanged()
	{
		Refresh();
	}

	private void Subscribe()
	{
		if (_subscribed) return;
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return;
		p.DataChanged += OnDataChanged;
		_subscribed = true;
	}

	private void Unsubscribe()
	{
		if (!_subscribed) return;
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p != null) p.DataChanged -= OnDataChanged;
		_subscribed = false;
	}

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

	/// <summary>Clear and rebuild all content. Called by OnDataChanged by default.</summary>
	public void Refresh()
	{
		if (_content == null) return;
		foreach (var child in _content.GetChildren())
			child.QueueFree();
		BuildContent(_content);
	}

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

	protected static Control ThinSeparator() => UITheme.CreateSeparator();

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
