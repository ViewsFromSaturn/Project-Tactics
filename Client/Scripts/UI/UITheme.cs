using Godot;

namespace ProjectTactics.UI;

/// <summary>
/// Shared UI theme with dark/light mode toggle.
///
/// DARK MODE  — from WPF UI Theme/Theme/Colors.cs (#080812, glass panels, violet/emerald/gold)
/// LIGHT MODE — from Taskade mockup (white panels, subtle shadows, dark text)
///
/// All colors are read through static properties that switch based on IsDarkMode.
/// Call UITheme.SetDarkMode(true/false) to toggle at runtime.
/// </summary>
public static class UITheme
{
	// ═══════════════════════════════════════════════════════════════
	//  MODE TOGGLE
	// ═══════════════════════════════════════════════════════════════

	private static bool _isDarkMode = true; // Default: dark (WPF theme)

	public static bool IsDarkMode => _isDarkMode;

	public static void SetDarkMode(bool dark)
	{
		_isDarkMode = dark;
		// TODO: emit signal / event for live UI refresh
	}

	/// <summary>Pick between dark and light color based on current mode.</summary>
	private static Color Pick(Color dark, Color light) => _isDarkMode ? dark : light;


	// ═══════════════════════════════════════════════════════════════
	//  COLORS — Dark values from WPF Colors.cs, Light from Taskade
	// ═══════════════════════════════════════════════════════════════

	// ─── Backgrounds ───
	// Dark: #080812 (WPF BackgroundDark)
	// Light: #F0F0F2 (Taskade page bg)
	public static Color BgPage       => Pick(new Color("080812"), new Color("F0F0F2"));

	// Dark: #D9080812 at 85% opacity (WPF BackgroundDarkTransparent)
	// Light: #FFFFFF
	public static Color BgPanel      => Pick(new Color(8/255f, 8/255f, 18/255f, 0.85f), new Color("FFFFFF"));
	public static Color BgWhite      => Pick(new Color(8/255f, 8/255f, 18/255f, 0.85f), new Color("FFFFFF"));

	// Dark: #1A3C4150 (WPF CardBackground — 10% opacity)
	// Light: #F5F5F7
	public static Color CardBg       => Pick(new Color(60/255f, 65/255f, 80/255f, 0.10f), new Color("F5F5F7"));

	// Dark: #263C4150 (WPF CardBackgroundHover — 15% opacity)
	// Light: rgba(0,0,0,0.04)
	public static Color CardHoverBg  => Pick(new Color(60/255f, 65/255f, 80/255f, 0.15f), new Color(0,0,0, 0.04f));

	public static Color BgInput      => Pick(new Color(60/255f, 65/255f, 80/255f, 0.10f), new Color("F5F5F7"));
	public static Color BgInputFocus => Pick(new Color(60/255f, 65/255f, 80/255f, 0.20f), new Color("FFFFFF"));
	public static Color BgSidebar    => Pick(new Color("080812"), new Color("F5F5F7"));
	public static Color BgIconBtn    => Pick(new Color(60/255f, 65/255f, 80/255f, 0.10f), new Color("F0F0F2"));
	public static Color BgIconBtnActive => Pick(new Color(60/255f, 65/255f, 80/255f, 0.20f), new Color("E0E0E4"));

	// Legacy aliases
	public static Color BgDark       => BgPage;
	public static Color BgLight      => BgPage;
	public static Color BgPanelHover => CardHoverBg;
	public static Color SecondaryBg  => BgInput;
	public static Color SecondaryHover => CardHoverBg;
	public static Color SecondaryPress => Pick(new Color(60/255f, 65/255f, 80/255f, 0.25f), new Color("E8E8EC"));

	// ─── Borders ───
	// Dark: WPF BorderSubtle #593C4150 (35%), BorderMedium (50%), BorderBright (70%)
	// Light: rgba black at low opacity
	public static Color BorderSubtle => Pick(
		new Color(60/255f, 65/255f, 80/255f, 0.35f),
		new Color(0, 0, 0, 0.06f));

	public static Color BorderMedium => Pick(
		new Color(60/255f, 65/255f, 80/255f, 0.50f),
		new Color(0, 0, 0, 0.10f));

	public static Color BorderBright => Pick(
		new Color(60/255f, 65/255f, 80/255f, 0.70f),
		new Color(0, 0, 0, 0.15f));

	public static Color Border       => BorderMedium;
	public static Color BorderLight  => BorderSubtle;
	public static Color BorderFocus  => Pick(
		new Color(139/255f, 92/255f, 246/255f, 0.5f),  // Violet focus in dark
		new Color(0.2f, 0.4f, 0.8f, 0.35f));            // Blue focus in light

	// Shadow
	public static Color Shadow => Pick(
		new Color(0, 0, 0, 0.8f),    // Heavy shadow in dark (WPF DropShadow Opacity=0.8)
		new Color(0, 0, 0, 0.08f));   // Subtle in light

	// ─── Text ───
	// Dark: WPF TextBright=#EEEEE8, TextPrimary=#D4D2CC, TextSecondary=#9090A0, TextDim=#64647A
	// Light: Dark text on white
	public static Color TextBright    => Pick(new Color("EEEEE8"), new Color("1A1A2E"));
	public static Color Text          => Pick(new Color("D4D2CC"), new Color("2D2D3D"));
	public static Color TextSecondary => Pick(new Color("9090A0"), new Color("6B6B7B"));
	public static Color TextDim       => Pick(new Color("64647A"), new Color("9B9BAB"));

	// ─── Accents (same in both modes — from WPF Colors.cs) ───
	public static readonly Color AccentViolet     = new("8B5CF6");
	public static readonly Color AccentVioletDim  = new Color(139/255f, 92/255f, 246/255f, 0.30f);
	public static readonly Color AccentEmerald    = new("50C878");
	public static readonly Color AccentEmeraldDim = new Color(80/255f, 200/255f, 120/255f, 0.30f);
	public static readonly Color AccentGold       = new("D4A843");
	public static readonly Color AccentGoldDim    = new Color(212/255f, 168/255f, 67/255f, 0.30f);
	public static readonly Color AccentRuby       = new("C85050");
	public static readonly Color AccentRubyDim    = new Color(200/255f, 80/255f, 80/255f, 0.20f);

	// Primary accent — violet in dark, dark text in light
	public static Color Accent      => Pick(AccentViolet, new Color("2D2D3D"));
	public static Color AccentHover => Pick(new Color("7C3AED"), new Color("1A1A2E"));
	public static Color AccentPress => Pick(new Color("6D28D9"), new Color("404058"));

	// Semantic aliases
	public static readonly Color AccentRed      = new("C85050");
	public static readonly Color AccentRedDim   = new Color(200/255f, 80/255f, 80/255f, 0.20f);
	public static readonly Color AccentBlue     = new("4080D0");
	public static readonly Color AccentBlueDim  = new Color(0.251f, 0.502f, 0.816f, 0.12f);
	public static readonly Color AccentGreen    = new("50C878");
	public static readonly Color AccentGreenDim = new Color(0.251f, 0.690f, 0.376f, 0.12f);
	public static readonly Color Error          = AccentRuby;
	public static readonly Color AccentOrange   = AccentGold;

	// ─── Resource bars (same both modes) ───
	public static readonly Color HpBar  = new("E04040");
	public static readonly Color StaBar = new("E8A030");
	public static readonly Color EthBar = new("4080D0");

	// ─── Rarity (from WPF Colors.cs) ───
	public static readonly Color RarityCommon    = new("9090A0");
	public static readonly Color RarityUncommon  = new("50C878");
	public static readonly Color RarityRare      = new("6496FF");
	public static readonly Color RarityEpic      = new("8B5CF6");
	public static readonly Color RarityLegendary = new("D4A843");

	// ─── Status ───
	public static readonly Color StatusOnline  = new("50C878");
	public static readonly Color StatusAway    = new("D4A843");
	public static readonly Color StatusOffline = new("64647A");


	// ═══════════════════════════════════════════════════════════════
	//  FONTS
	// ═══════════════════════════════════════════════════════════════

	private static Font _fontTitle, _fontTitleMedium, _fontTitleSemiBold;
	private static Font _fontBody, _fontBodyMedium, _fontBodySemiBold;
	private static Font _fontNumbers, _fontNumbersMedium;

	// From WPF UI Theme: Georgia for headings, Segoe UI for body/numbers.
	public static Font FontTitle         => _fontTitle         ??= LoadFont("res://Assets/Fonts/georgia.ttf");
	public static Font FontTitleMedium   => _fontTitleMedium   ??= LoadFont("res://Assets/Fonts/georgiab.ttf");
	public static Font FontTitleSemiBold => _fontTitleSemiBold ??= LoadFont("res://Assets/Fonts/georgiab.ttf");
	public static Font FontBody          => _fontBody          ??= LoadFont("res://Assets/Fonts/segoeui.ttf");
	public static Font FontBodyMedium    => _fontBodyMedium    ??= LoadFont("res://Assets/Fonts/seguisb.ttf");
	public static Font FontBodySemiBold  => _fontBodySemiBold  ??= LoadFont("res://Assets/Fonts/seguisb.ttf");
	public static Font FontNumbers       => _fontNumbers       ??= LoadFont("res://Assets/Fonts/segoeui.ttf");
	public static Font FontNumbersMedium => _fontNumbersMedium ??= LoadFont("res://Assets/Fonts/seguisb.ttf");

	private static Font LoadFont(string path)
	{
		if (ResourceLoader.Exists(path))
			return GD.Load<Font>(path);
		GD.PrintErr($"[UITheme] Font not found: {path}");
		return null;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: STYLE BOXES (window, card, etc.)
	// ═══════════════════════════════════════════════════════════════

	/// <summary>
	/// Floating window style — matches WPF FloatingWindow.xaml.
	/// Dark: glass bg (#D9080812), border (#593C4150), heavy drop shadow.
	/// Light: white, subtle border and shadow.
	/// </summary>
	public static StyleBoxFlat CreateWindowStyle()
	{
		var style = new StyleBoxFlat();
		style.BgColor = BgPanel;
		style.SetCornerRadiusAll(12);
		style.BorderColor = BorderSubtle;
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 0;
		style.ContentMarginRight = 0;
		style.ContentMarginTop = 0;
		style.ContentMarginBottom = 0;
		style.ShadowColor = Shadow;
		style.ShadowSize = _isDarkMode ? 30 : 8;
		style.ShadowOffset = new Vector2(0, _isDarkMode ? 0 : 2);
		return style;
	}

	/// <summary>
	/// Card style — for embedded cards within panels.
	/// Dark: WPF CardContainer (CardBackground, BorderSubtle, CornerRadius=8).
	/// Light: light gray bg.
	/// </summary>
	public static StyleBoxFlat CreateCardStyle()
	{
		var style = new StyleBoxFlat();
		style.BgColor = CardBg;
		style.SetCornerRadiusAll(8);
		style.BorderColor = BorderSubtle;
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 16;
		style.ContentMarginRight = 16;
		style.ContentMarginTop = 14;
		style.ContentMarginBottom = 14;
		return style;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: LABELS
	// ═══════════════════════════════════════════════════════════════

	public static Label CreateHeading1(string text, int size = 24)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", TextBright);
		label.AddThemeFontSizeOverride("font_size", size);
		if (FontTitleSemiBold != null) label.AddThemeFontOverride("font", FontTitleSemiBold);
		return label;
	}

	public static Label CreateHeading2(string text, int size = 18)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", TextBright);
		label.AddThemeFontSizeOverride("font_size", size);
		if (FontTitleMedium != null) label.AddThemeFontOverride("font", FontTitleMedium);
		return label;
	}

	public static Label CreateHeading3(string text, int size = 14)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", TextBright);
		label.AddThemeFontSizeOverride("font_size", size);
		if (FontBodySemiBold != null) label.AddThemeFontOverride("font", FontBodySemiBold);
		return label;
	}

	public static Label CreateTitle(string text, int size = 28)
	{
		var label = CreateHeading1(text, size);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		return label;
	}

	public static Label CreateAccentTitle(string text, int size = 28)
	{
		var label = CreateTitle(text, size);
		label.AddThemeColorOverride("font_color", Accent);
		return label;
	}

	public static Label CreateBody(string text, int size = 14, Color? color = null)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", color ?? Text);
		label.AddThemeFontSizeOverride("font_size", size);
		if (FontBody != null) label.AddThemeFontOverride("font", FontBody);
		return label;
	}

	public static Label CreateSecondary(string text, int size = 12)
		=> CreateBody(text, size, TextSecondary);

	public static Label CreateDim(string text, int size = 12)
		=> CreateBody(text, size, TextDim);

	public static Label CreateNumbers(string text, int size = 14, Color? color = null)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", color ?? Text);
		label.AddThemeFontSizeOverride("font_size", size);
		if (FontNumbersMedium != null) label.AddThemeFontOverride("font", FontNumbersMedium);
		return label;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: INPUTS
	// ═══════════════════════════════════════════════════════════════

	public static LineEdit CreateInput(string placeholder, bool secret = false, int fontSize = 14)
	{
		var input = new LineEdit();
		input.PlaceholderText = placeholder;
		input.Secret = secret;
		input.CustomMinimumSize = new Vector2(0, 40);
		input.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBody != null) input.AddThemeFontOverride("font", FontBody);

		var style = new StyleBoxFlat();
		style.BgColor = BgInput;
		style.SetCornerRadiusAll(8);
		style.ContentMarginLeft = 12; style.ContentMarginRight = 12;
		style.ContentMarginTop = 8; style.ContentMarginBottom = 8;
		style.BorderColor = BorderSubtle;
		style.SetBorderWidthAll(1);
		input.AddThemeStyleboxOverride("normal", style);

		var focusStyle = (StyleBoxFlat)style.Duplicate();
		focusStyle.BgColor = BgInputFocus;
		focusStyle.BorderColor = BorderFocus;
		focusStyle.SetBorderWidthAll(2);
		input.AddThemeStyleboxOverride("focus", focusStyle);

		input.AddThemeColorOverride("font_color", TextBright);
		input.AddThemeColorOverride("font_placeholder_color", TextDim);
		input.AddThemeColorOverride("caret_color", _isDarkMode ? AccentViolet : AccentBlue);

		return input;
	}

	public static TextEdit CreateTextArea(string placeholder, int fontSize = 13)
	{
		var input = new TextEdit();
		input.PlaceholderText = placeholder;
		input.WrapMode = TextEdit.LineWrappingMode.Boundary;
		input.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBody != null) input.AddThemeFontOverride("font", FontBody);

		var style = new StyleBoxFlat();
		style.BgColor = BgInput;
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(10);
		style.BorderColor = BorderSubtle;
		style.SetBorderWidthAll(1);
		input.AddThemeStyleboxOverride("normal", style);

		var focusStyle = (StyleBoxFlat)style.Duplicate();
		focusStyle.BgColor = BgInputFocus;
		focusStyle.BorderColor = BorderFocus;
		focusStyle.SetBorderWidthAll(2);
		input.AddThemeStyleboxOverride("focus", focusStyle);

		input.AddThemeColorOverride("font_color", TextBright);
		input.AddThemeColorOverride("font_placeholder_color", TextDim);

		return input;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: BUTTONS
	// ═══════════════════════════════════════════════════════════════

	/// <summary>Primary button — WPF AccentButton style in dark, dark fill in light.</summary>
	public static Button CreatePrimaryButton(string text, int fontSize = 14)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 42);
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBodyMedium != null) btn.AddThemeFontOverride("font", FontBodyMedium);
		btn.AddThemeColorOverride("font_color", _isDarkMode ? TextBright : new Color("FFFFFF"));
		btn.AddThemeColorOverride("font_hover_color", _isDarkMode ? TextBright : new Color("FFFFFF"));

		btn.AddThemeStyleboxOverride("normal",   MakeButtonStyle(Accent, null, 8));
		btn.AddThemeStyleboxOverride("hover",    MakeButtonStyle(AccentHover, null, 8));
		btn.AddThemeStyleboxOverride("pressed",  MakeButtonStyle(AccentPress, null, 8));
		btn.AddThemeStyleboxOverride("disabled", MakeButtonStyle(TextDim, null, 8));

		return btn;
	}

	/// <summary>Secondary / glass button — WPF GlassButton style.</summary>
	public static Button CreateSecondaryButton(string text, int fontSize = 14)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 42);
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBodyMedium != null) btn.AddThemeFontOverride("font", FontBodyMedium);
		btn.AddThemeColorOverride("font_color", Text);
		btn.AddThemeColorOverride("font_hover_color", TextBright);

		btn.AddThemeStyleboxOverride("normal",  MakeButtonStyle(CardBg, BorderSubtle, 6));
		btn.AddThemeStyleboxOverride("hover",   MakeButtonStyle(CardHoverBg, BorderMedium, 6));
		btn.AddThemeStyleboxOverride("pressed", MakeButtonStyle(SecondaryPress, BorderMedium, 6));

		return btn;
	}

	/// <summary>Ghost / text button — transparent bg.</summary>
	public static Button CreateGhostButton(string text, int fontSize = 13, Color? color = null)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 36);
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBody != null) btn.AddThemeFontOverride("font", FontBody);
		btn.AddThemeColorOverride("font_color", color ?? TextSecondary);
		btn.AddThemeColorOverride("font_hover_color", TextBright);

		var empty = new StyleBoxFlat();
		empty.BgColor = Colors.Transparent;
		empty.SetCornerRadiusAll(6);
		empty.SetContentMarginAll(8);
		btn.AddThemeStyleboxOverride("normal", empty);

		var hover = (StyleBoxFlat)empty.Duplicate();
		hover.BgColor = CardHoverBg;
		btn.AddThemeStyleboxOverride("hover", hover);

		var press = (StyleBoxFlat)empty.Duplicate();
		press.BgColor = _isDarkMode ? BorderSubtle : new Color(0, 0, 0, 0.06f);
		btn.AddThemeStyleboxOverride("pressed", press);

		return btn;
	}

	/// <summary>Close button (✕) — WPF CloseButton style.</summary>
	public static Button CreateCloseButton()
	{
		var btn = new Button();
		btn.Text = "✕";
		btn.CustomMinimumSize = new Vector2(32, 32);
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", TextDim);
		btn.AddThemeColorOverride("font_hover_color", AccentRuby);

		var normal = new StyleBoxFlat();
		normal.BgColor = Colors.Transparent;
		normal.SetCornerRadiusAll(4);
		normal.SetContentMarginAll(4);
		btn.AddThemeStyleboxOverride("normal", normal);

		// WPF: hover = #33C85050 bg, #C85050 text
		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = AccentRubyDim;
		btn.AddThemeStyleboxOverride("hover", hover);

		var press = (StyleBoxFlat)normal.Duplicate();
		press.BgColor = AccentRuby;
		btn.AddThemeStyleboxOverride("pressed", press);

		return btn;
	}

	/// <summary>Icon button for sidebar — matches Taskade Image 2 rounded squares.</summary>
	public static Button CreateIconButton(string label, string tooltip, bool active = false)
	{
		var btn = new Button();
		btn.Text = label;
		btn.TooltipText = tooltip;
		btn.CustomMinimumSize = new Vector2(44, 44);
		btn.MouseFilter = Control.MouseFilterEnum.Stop;

		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", active ? TextBright : TextSecondary);
		btn.AddThemeColorOverride("font_hover_color", TextBright);
		if (FontTitleMedium != null) btn.AddThemeFontOverride("font", FontTitleMedium);

		var style = new StyleBoxFlat();
		style.BgColor = active ? BgIconBtnActive : BgIconBtn;
		style.SetCornerRadiusAll(10);
		style.BorderColor = BorderSubtle;
		style.SetBorderWidthAll(1);
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = BgIconBtnActive;
		hover.BorderColor = BorderMedium;
		btn.AddThemeStyleboxOverride("hover", hover);

		var press = (StyleBoxFlat)style.Duplicate();
		press.BgColor = _isDarkMode ? BorderSubtle : new Color("D8D8DC");
		btn.AddThemeStyleboxOverride("pressed", press);

		return btn;
	}

	/// <summary>Danger / red button (Logout)</summary>
	public static Button CreateDangerButton(string text, int fontSize = 14)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 42);
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		if (FontBodyMedium != null) btn.AddThemeFontOverride("font", FontBodyMedium);
		btn.AddThemeColorOverride("font_color", AccentRuby);
		btn.AddThemeColorOverride("font_hover_color", _isDarkMode ? TextBright : new Color("FFFFFF"));

		btn.AddThemeStyleboxOverride("normal",  MakeButtonStyle(AccentRubyDim, AccentRuby, 8));
		btn.AddThemeStyleboxOverride("hover",   MakeButtonStyle(AccentRuby, AccentRuby, 8));
		btn.AddThemeStyleboxOverride("pressed", MakeButtonStyle(new Color("A03030"), null, 8));

		return btn;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: PANELS
	// ═══════════════════════════════════════════════════════════════

	public static PanelContainer CreatePanel(Color? bgColor = null, Color? borderColor = null, int borderWidth = 0, int cornerRadius = 12)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = bgColor ?? BgPanel;
		style.SetCornerRadiusAll(cornerRadius);
		style.SetContentMarginAll(16);

		if (borderColor.HasValue || borderWidth > 0)
		{
			style.BorderColor = borderColor ?? BorderSubtle;
			style.SetBorderWidthAll(borderWidth > 0 ? borderWidth : 1);
		}

		style.ShadowColor = Shadow;
		style.ShadowSize = _isDarkMode ? 30 : 8;
		style.ShadowOffset = new Vector2(0, _isDarkMode ? 0 : 2);

		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	public static PanelContainer CreateStatBadge()
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = CardBg;
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 12; style.ContentMarginRight = 12;
		style.ContentMarginTop = 8; style.ContentMarginBottom = 8;
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}


	// ═══════════════════════════════════════════════════════════════
	//  FACTORY: SEPARATORS / SPACING
	// ═══════════════════════════════════════════════════════════════

	public static Control CreateSpacer(float height = 20)
	{
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, height);
		return spacer;
	}

	public static HSeparator CreateSeparator()
	{
		var sep = new HSeparator();
		var style = new StyleBoxFlat();
		style.BgColor = BorderSubtle;
		style.ContentMarginTop = 0;
		style.ContentMarginBottom = 0;
		sep.AddThemeStyleboxOverride("separator", style);
		return sep;
	}

	public static ColorRect CreateBackground()
	{
		var bg = new ColorRect();
		bg.Color = BgPage;
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return bg;
	}

	public static ColorRect CreateVignette()
	{
		var vig = new ColorRect();
		vig.Color = new Color(0, 0, 0, 0);
		vig.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vig.MouseFilter = Control.MouseFilterEnum.Ignore;
		return vig;
	}

	public static Label CreateVersionLabel(string version = "v1.0 — Phase 3")
	{
		var label = CreateDim(version, 10);
		label.HorizontalAlignment = HorizontalAlignment.Right;
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
		label.OffsetLeft = -160;
		label.OffsetTop = -28;
		return label;
	}


	// ═══════════════════════════════════════════════════════════════
	//  PRIVATE HELPERS
	// ═══════════════════════════════════════════════════════════════

	private static StyleBoxFlat MakeButtonStyle(Color bg, Color? border = null, int radius = 8)
	{
		var style = new StyleBoxFlat();
		style.BgColor = bg;
		style.SetCornerRadiusAll(radius);
		style.ContentMarginLeft = 16; style.ContentMarginRight = 16;
		style.ContentMarginTop = 8; style.ContentMarginBottom = 8;
		if (border.HasValue)
		{
			style.BorderColor = border.Value;
			style.SetBorderWidthAll(1);
		}
		return style;
	}
}
