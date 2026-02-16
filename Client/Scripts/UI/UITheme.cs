using Godot;

namespace ProjectTactics.UI;

/// <summary>
/// Shared UI theme constants and factory methods.
/// Matches the HUD v4 mockup visual language.
/// Fonts: Outfit (titles), Source Sans 3 (body), Barlow Condensed (numbers/stats).
/// </summary>
public static class UITheme
{
    // ═══ COLORS (from HUD v4 CSS vars) ═══
    public static readonly Color BgDark       = new("080810");    // --hud-bg base
    public static readonly Color BgPanel      = new("0e0e16ee");  // panels with alpha
    public static readonly Color BgInput      = new("0c0c14d9");  // input fields
    public static readonly Color BgInputFocus = new("10101aF2");
    public static readonly Color Border       = new("3c41504d");  // --hud-border
    public static readonly Color BorderLight  = new("3c415030");
    public static readonly Color BorderFocus  = new("506e9659");

    public static readonly Color TextBright   = new("e8e4d8");    // --hud-text-bright
    public static readonly Color Text         = new("c8c4b8");    // --hud-text
    public static readonly Color TextDim      = new("6a6860");    // --hud-text-dim
    public static readonly Color Accent       = new("7a9468");    // --hud-accent (green)
    public static readonly Color AccentOrange = new("E87722");    // primary action orange
    public static readonly Color AccentHover  = new("FF8833");
    public static readonly Color AccentPress  = new("CC6611");
    public static readonly Color Error        = new("c85050");
    public static readonly Color SecondaryBg  = new("1a1a28");
    public static readonly Color SecondaryHover = new("252538");
    public static readonly Color SecondaryPress = new("121220");

    // Bar colors
    public static readonly Color HpBar    = new("48a848");
    public static readonly Color StaBar   = new("c89838");
    public static readonly Color EthBar   = new("4888d0");

    // ═══ FONTS ═══
    private static Font _fontTitle;
    private static Font _fontTitleMedium;
    private static Font _fontBody;
    private static Font _fontBodyMedium;
    private static Font _fontBodySemiBold;
    private static Font _fontNumbers;
    private static Font _fontNumbersMedium;

    public static Font FontTitle       => _fontTitle       ??= LoadFont("res://Assets/Fonts/Outfit-Regular.ttf");
    public static Font FontTitleMedium => _fontTitleMedium ??= LoadFont("res://Assets/Fonts/Outfit-Medium.ttf");
    public static Font FontBody        => _fontBody        ??= LoadFont("res://Assets/Fonts/SourceSans3-Regular.ttf");
    public static Font FontBodyMedium  => _fontBodyMedium  ??= LoadFont("res://Assets/Fonts/SourceSans3-Medium.ttf");
    public static Font FontBodySemiBold => _fontBodySemiBold ??= LoadFont("res://Assets/Fonts/SourceSans3-SemiBold.ttf");
    public static Font FontNumbers     => _fontNumbers     ??= LoadFont("res://Assets/Fonts/BarlowCondensed-Regular.ttf");
    public static Font FontNumbersMedium => _fontNumbersMedium ??= LoadFont("res://Assets/Fonts/BarlowCondensed-Medium.ttf");

    private static Font LoadFont(string path)
    {
        if (ResourceLoader.Exists(path))
            return GD.Load<Font>(path);
        GD.PrintErr($"[UITheme] Font not found: {path}");
        return null;
    }

    // ═══ FACTORY: LABELS ═══

    public static Label CreateTitle(string text, int size = 28)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", TextBright);
        label.AddThemeFontSizeOverride("font_size", size);
        if (FontTitleMedium != null) label.AddThemeFontOverride("font", FontTitleMedium);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    public static Label CreateAccentTitle(string text, int size = 28)
    {
        var label = CreateTitle(text, size);
        label.AddThemeColorOverride("font_color", AccentOrange);
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

    public static Label CreateDim(string text, int size = 12)
    {
        return CreateBody(text, size, TextDim);
    }

    public static Label CreateNumbers(string text, int size = 14, Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color ?? Text);
        label.AddThemeFontSizeOverride("font_size", size);
        if (FontNumbersMedium != null) label.AddThemeFontOverride("font", FontNumbersMedium);
        return label;
    }

    // ═══ FACTORY: INPUTS ═══

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
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        style.BorderColor = BorderLight;
        style.SetBorderWidthAll(1);
        input.AddThemeStyleboxOverride("normal", style);

        var focusStyle = (StyleBoxFlat)style.Duplicate();
        focusStyle.BgColor = BgInputFocus;
        focusStyle.BorderColor = BorderFocus;
        input.AddThemeStyleboxOverride("focus", focusStyle);

        input.AddThemeColorOverride("font_color", TextBright);
        input.AddThemeColorOverride("font_placeholder_color", TextDim);
        input.AddThemeColorOverride("caret_color", Accent);

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
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(10);
        style.BorderColor = BorderLight;
        style.SetBorderWidthAll(1);
        input.AddThemeStyleboxOverride("normal", style);

        var focusStyle = (StyleBoxFlat)style.Duplicate();
        focusStyle.BgColor = BgInputFocus;
        focusStyle.BorderColor = BorderFocus;
        input.AddThemeStyleboxOverride("focus", focusStyle);

        input.AddThemeColorOverride("font_color", TextBright);
        input.AddThemeColorOverride("font_placeholder_color", TextDim);

        return input;
    }

    // ═══ FACTORY: BUTTONS ═══

    public static Button CreatePrimaryButton(string text, int fontSize = 14)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(0, 42);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        if (FontBodyMedium != null) btn.AddThemeFontOverride("font", FontBodyMedium);
        btn.AddThemeColorOverride("font_color", TextBright);

        btn.AddThemeStyleboxOverride("normal", MakeButtonStyle(AccentOrange));
        btn.AddThemeStyleboxOverride("hover", MakeButtonStyle(AccentHover));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonStyle(AccentPress));
        btn.AddThemeStyleboxOverride("disabled", MakeButtonStyle(new Color("4a3a20")));

        return btn;
    }

    public static Button CreateSecondaryButton(string text, int fontSize = 14)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(0, 42);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        if (FontBodyMedium != null) btn.AddThemeFontOverride("font", FontBodyMedium);
        btn.AddThemeColorOverride("font_color", TextDim);
        btn.AddThemeColorOverride("font_hover_color", Text);

        btn.AddThemeStyleboxOverride("normal", MakeButtonStyle(SecondaryBg, BorderLight));
        btn.AddThemeStyleboxOverride("hover", MakeButtonStyle(SecondaryHover, Border));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonStyle(SecondaryPress, BorderLight));

        return btn;
    }

    /// <summary>Ghost button — transparent background, subtle border on hover only.</summary>
    public static Button CreateGhostButton(string text, int fontSize = 13, Color? color = null)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(0, 36);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        if (FontBody != null) btn.AddThemeFontOverride("font", FontBody);
        btn.AddThemeColorOverride("font_color", color ?? TextDim);
        btn.AddThemeColorOverride("font_hover_color", Text);

        var empty = new StyleBoxFlat();
        empty.BgColor = Colors.Transparent;
        empty.SetCornerRadiusAll(4);
        empty.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("normal", empty);

        var hover = (StyleBoxFlat)empty.Duplicate();
        hover.BgColor = new Color("ffffff08");
        btn.AddThemeStyleboxOverride("hover", hover);

        var press = (StyleBoxFlat)empty.Duplicate();
        press.BgColor = new Color("ffffff04");
        btn.AddThemeStyleboxOverride("pressed", press);

        return btn;
    }

    // ═══ FACTORY: PANELS ═══

    public static PanelContainer CreatePanel(Color? bgColor = null, Color? borderColor = null, int borderWidth = 1, int cornerRadius = 6)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat();
        style.BgColor = bgColor ?? BgPanel;
        style.SetCornerRadiusAll(cornerRadius);
        style.SetContentMarginAll(16);
        style.BorderColor = borderColor ?? BorderLight;
        style.SetBorderWidthAll(borderWidth);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    /// <summary>A highlighted (selected) panel with accent border.</summary>
    public static void SetPanelSelected(PanelContainer panel, bool selected)
    {
        var style = new StyleBoxFlat();
        style.BgColor = selected ? new Color("0e0e16F2") : BgPanel;
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(16);
        style.BorderColor = selected ? AccentOrange : BorderLight;
        style.SetBorderWidthAll(selected ? 2 : 1);
        panel.AddThemeStyleboxOverride("panel", style);
    }

    // ═══ FACTORY: SEPARATORS ═══

    public static Control CreateSpacer(float height = 20)
    {
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, height);
        return spacer;
    }

    public static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", MakeSepStyle());
        return sep;
    }

    // ═══ BACKGROUNDS ═══

    /// <summary>Full-screen dark background matching HUD.</summary>
    public static ColorRect CreateBackground()
    {
        var bg = new ColorRect();
        bg.Color = BgDark;
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return bg;
    }

    /// <summary>Subtle vignette overlay for visual depth.</summary>
    public static ColorRect CreateVignette()
    {
        // For now, just a slightly lighter edge — can be replaced with shader later
        var vig = new ColorRect();
        vig.Color = new Color(0, 0, 0, 0);
        vig.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vig.MouseFilter = Control.MouseFilterEnum.Ignore;
        return vig;
    }

    // ═══ VERSION LABEL ═══

    public static Label CreateVersionLabel(string version = "v1.0 — Phase 3")
    {
        var label = CreateDim(version, 10);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        label.OffsetLeft = -160;
        label.OffsetTop = -28;
        return label;
    }

    // ═══ PRIVATE HELPERS ═══

    private static StyleBoxFlat MakeButtonStyle(Color bg, Color? border = null, int radius = 4)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.SetCornerRadiusAll(radius);
        style.ContentMarginLeft = 20;
        style.ContentMarginRight = 20;
        style.ContentMarginTop = 10;
        style.ContentMarginBottom = 10;
        if (border.HasValue)
        {
            style.BorderColor = border.Value;
            style.SetBorderWidthAll(1);
        }
        return style;
    }

    private static StyleBoxFlat MakeSepStyle()
    {
        var style = new StyleBoxFlat();
        style.BgColor = BorderLight;
        style.ContentMarginTop = 0;
        style.ContentMarginBottom = 0;
        return style;
    }
}
