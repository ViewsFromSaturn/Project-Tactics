using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.UI;

/// <summary>
/// Overworld HUD — Identity bar (top-left), quick action icons (bottom-right),
/// panel manager with slide-in/out and hotkeys, chat bubble on player.
/// Attach to a Control node inside a CanvasLayer (Layer = 10).
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
    private VBoxContainer _quickActionsContainer;
    private float _hudIdleTimer = 0f;
    private const float HudIdleTimeout = 8f;

    // ═══ PANEL SYSTEM ═══
    private readonly Dictionary<string, Panels.SlidePanel> _panels = new();
    private string _activePanel = null;

    private readonly List<(string id, string icon, string tooltip, Key hotkey)> _panelDefs = new()
    {
        ("charsheet", "C",  "Character Sheet",  Key.C),
        ("training",  "T",  "Training",         Key.V),
        ("journal",   "J",  "Journal",          Key.J),
        ("map",       "M",  "Map",              Key.M),
        ("inventory", "I",  "Inventory",        Key.I),
        ("mentor",    "⚒",  "Mentorship",       Key.N),
        ("settings",  "⚙",  "Settings",         Key.Escape),
    };

    // Chat bubble (positioned above player sprite)
    private Label _chatBubble;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildIdentityBar();
        BuildQuickActions();
        BuildPanels();
        HideDebugOverlay();
        SetupChatBubble();
    }

    public override void _Process(double delta)
    {
        UpdateBars();

        // Idle fade for identity bar and quick actions (matches mockup)
        _hudIdleTimer += (float)delta;
        bool isIdle = _hudIdleTimer > HudIdleTimeout && !ChatPanel.IsUiFocused;
        float targetAlpha = isIdle ? 0.35f : 1.0f;

        if (_identityBarPanel != null)
            _identityBarPanel.Modulate = _identityBarPanel.Modulate.Lerp(new Color(1, 1, 1, targetAlpha), (float)delta * 3);
        if (_quickActionsContainer != null)
            _quickActionsContainer.Modulate = _quickActionsContainer.Modulate.Lerp(new Color(1, 1, 1, isIdle ? 0.3f : 1f), (float)delta * 3);
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouseMotion || ev is InputEventKey)
            _hudIdleTimer = 0f;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is not InputEventKey key || !key.Pressed || key.Echo) return;

        // ═══ CRITICAL: Don't process hotkeys when chat/emote/settings fields are focused ═══
        if (ChatPanel.IsUiFocused) return;

        // Esc special: close active panel first, then open settings
        if (key.Keycode == Key.Escape)
        {
            if (_activePanel != null && _activePanel != "settings")
            {
                CloseActivePanel();
                GetViewport().SetInputAsHandled();
                return;
            }
            TogglePanel("settings");
            GetViewport().SetInputAsHandled();
            return;
        }

        // Other hotkeys
        foreach (var (id, _, _, hotkey) in _panelDefs)
        {
            if (key.Keycode == hotkey && hotkey != Key.Escape)
            {
                TogglePanel(id);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    // ═════════════════════════════════════════════════════════
    //  IDENTITY BAR (top-left)
    // ═════════════════════════════════════════════════════════

    private void BuildIdentityBar()
    {
        var panel = new PanelContainer();
        _identityBarPanel = panel;        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        panel.CustomMinimumSize = new Vector2(280, 0);
        panel.MouseFilter = MouseFilterEnum.Stop;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.031f, 0.031f, 0.063f, 0.62f);
        panelStyle.CornerRadiusBottomRight = 10;
        panelStyle.BorderWidthRight = 1;
        panelStyle.BorderWidthBottom = 1;
        panelStyle.BorderColor = UITheme.BorderLight;
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

        topRow.AddChild(UITheme.CreateBody("⚔", 14, UITheme.Accent));

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

        // Bars
        var barsVbox = new VBoxContainer();
        barsVbox.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(barsVbox);

        (_hpFill, _hpValue) = CreateBar(barsVbox, "HP",
            new Color("388838"), new Color("48a848"), new Color("58c058"),
            new Color(0.157f, 0.314f, 0.157f, 0.25f),
            new Color(0.235f, 0.549f, 0.235f, 0.3f), new Color(0.282f, 0.659f, 0.282f, 0.75f));

        (_staFill, _staValue) = CreateBar(barsVbox, "STA",
            new Color("a07828"), new Color("c89838"), new Color("d8a840"),
            new Color(0.471f, 0.353f, 0.118f, 0.2f),
            new Color(0.706f, 0.549f, 0.196f, 0.25f), new Color(0.784f, 0.596f, 0.22f, 0.65f));

        (_ethFill, _ethValue) = CreateBar(barsVbox, "ETH",
            new Color("3060a0"), new Color("4888d0"), new Color("58a0e0"),
            new Color(0.157f, 0.235f, 0.471f, 0.2f),
            new Color(0.235f, 0.431f, 0.745f, 0.25f), new Color(0.282f, 0.533f, 0.816f, 0.65f));
    }

    private (TextureRect fill, Label value) CreateBar(VBoxContainer parent, string label,
        Color fillColorStart, Color fillColorMid, Color fillColorEnd,
        Color trackBg, Color trackBorder, Color textColor)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.CustomMinimumSize = new Vector2(0, 14);
        parent.AddChild(row);

        var lbl = new Label();
        lbl.Text = label;
        lbl.CustomMinimumSize = new Vector2(26, 0);
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        lbl.AddThemeFontSizeOverride("font_size", 9);
        lbl.AddThemeColorOverride("font_color", textColor);
        if (UITheme.FontNumbersMedium != null) lbl.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
        row.AddChild(lbl);

        var track = new PanelContainer();
        track.CustomMinimumSize = new Vector2(BarTrackWidth, 8);
        track.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        track.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var trackStyle = new StyleBoxFlat();
        trackStyle.BgColor = trackBg;
        trackStyle.SetCornerRadiusAll(2);
        trackStyle.BorderColor = trackBorder;
        trackStyle.SetBorderWidthAll(1);
        track.AddThemeStyleboxOverride("panel", trackStyle);
        row.AddChild(track);

        // Gradient fill using TextureRect + GradientTexture1D
        var fill = new TextureRect();
        fill.CustomMinimumSize = new Vector2(0, 6);
        fill.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        fill.StretchMode = TextureRect.StretchModeEnum.Scale;
        fill.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        fill.OffsetTop = 1;
        fill.OffsetBottom = -1;
        fill.OffsetLeft = 1;

        // Create gradient: start → mid → end (like the mockup's linear-gradient)
        var gradient = new Gradient();
        gradient.SetColor(0, fillColorStart);
        gradient.AddPoint(0.6f, fillColorMid);
        gradient.SetColor(gradient.GetPointCount() - 1, fillColorEnd);

        var gradTex = new GradientTexture1D();
        gradTex.Gradient = gradient;
        gradTex.Width = 128;
        fill.Texture = gradTex;
        track.AddChild(fill);

        // Shine overlay (top highlight like mockup's ::after)
        var shine = new ColorRect();
        shine.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        shine.OffsetBottom = 3; // 40% of bar height
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
    //  QUICK ACTION ICONS (bottom-right, vertical stack)
    // ═════════════════════════════════════════════════════════

    private void BuildQuickActions()
    {
        var vbox = new VBoxContainer();
        _quickActionsContainer = vbox;
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        vbox.GrowHorizontal = GrowDirection.Begin;
        vbox.GrowVertical = GrowDirection.Begin;
        vbox.OffsetRight = -12;
        vbox.OffsetBottom = -12;
        vbox.OffsetLeft = -44;
        vbox.OffsetTop = -((32 + 4) * _panelDefs.Count);
        AddChild(vbox);

        foreach (var (id, icon, tooltip, hotkey) in _panelDefs)
        {
            string hotkeyStr = hotkey == Key.Escape ? "Esc" : hotkey.ToString();
            var btn = CreateIconButton(icon, $"{tooltip}  [{hotkeyStr}]");
            btn.Pressed += () => TogglePanel(id);
            vbox.AddChild(btn);
        }
    }

    private Button CreateIconButton(string label, string tooltip)
    {
        var btn = new Button();
        btn.Text = label;
        btn.TooltipText = tooltip;
        btn.CustomMinimumSize = new Vector2(32, 32);
        btn.MouseFilter = MouseFilterEnum.Stop;

        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeColorOverride("font_color", UITheme.TextDim);
        btn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
        if (UITheme.FontTitleMedium != null) btn.AddThemeFontOverride("font", UITheme.FontTitleMedium);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.031f, 0.031f, 0.063f, 0.62f);
        style.SetCornerRadiusAll(5);
        style.BorderColor = UITheme.BorderLight;
        style.SetBorderWidthAll(1);
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(0.07f, 0.07f, 0.12f, 0.8f);
        hover.BorderColor = UITheme.Border;
        btn.AddThemeStyleboxOverride("hover", hover);

        var press = (StyleBoxFlat)style.Duplicate();
        press.BgColor = new Color(0.04f, 0.04f, 0.08f, 0.9f);
        btn.AddThemeStyleboxOverride("pressed", press);

        return btn;
    }

    // ═════════════════════════════════════════════════════════
    //  PANEL MANAGEMENT
    // ═════════════════════════════════════════════════════════

    private void BuildPanels()
    {
        RegisterPanel("charsheet", new Panels.CharacterSheetPanel());
        RegisterPanel("training",  new Panels.TrainingPanel());
        RegisterPanel("journal",   new Panels.JournalPanel());
        RegisterPanel("map",       new Panels.MapPanel());
        RegisterPanel("inventory", new Panels.InventoryPanel());
        RegisterPanel("mentor",    new Panels.MentorPanel());
        RegisterPanel("settings",  new Panels.SettingsPanel());
    }

    private void RegisterPanel(string id, Panels.SlidePanel panel)
    {
        _panels[id] = panel;
        AddChild(panel);
    }

    private void TogglePanel(string id)
    {
        if (!_panels.ContainsKey(id)) return;

        if (_activePanel == id)
        {
            _panels[id].Close();
            _activePanel = null;
        }
        else
        {
            if (_activePanel != null && _panels.ContainsKey(_activePanel))
                _panels[_activePanel].Close();

            _panels[id].Open();
            _activePanel = id;
        }
    }

    private void CloseActivePanel()
    {
        if (_activePanel != null && _panels.ContainsKey(_activePanel))
        {
            _panels[_activePanel].Close();
            _activePanel = null;
        }
    }

    // ═════════════════════════════════════════════════════════
    //  CHAT BUBBLE (on player sprite)
    // ═════════════════════════════════════════════════════════

    private void SetupChatBubble()
    {
        // Find ChatPanel sibling and link the bubble label
        CallDeferred(nameof(LinkChatBubble));
    }

    private void LinkChatBubble()
    {
        var chatPanel = GetParent()?.GetNodeOrNull<ChatPanel>("ChatPanel");
        if (chatPanel == null) return;

        // Create bubble label as a child of the Player node (in world space)
        // The HUDLayer is a CanvasLayer, so we need to find the player in the scene tree
        var player = GetTree().Root.GetNodeOrNull<Node2D>("Overworld/Player");
        if (player == null)
        {
            // Try other common paths
            player = GetTree().Root.FindChild("Player", true, false) as Node2D;
        }

        if (player != null)
        {
            _chatBubble = new Label();
            _chatBubble.Text = "";
            _chatBubble.Visible = false;
            _chatBubble.HorizontalAlignment = HorizontalAlignment.Center;
            _chatBubble.AddThemeFontSizeOverride("font_size", 12);
            _chatBubble.AddThemeColorOverride("font_color", new Color("d4d2cc"));
            if (UITheme.FontBody != null) _chatBubble.AddThemeFontOverride("font", UITheme.FontBody);

            // Position above sprite
            _chatBubble.Position = new Vector2(-80, -60);
            _chatBubble.CustomMinimumSize = new Vector2(160, 0);

            // Background style
            var bg = new PanelContainer();
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.039f, 0.039f, 0.071f, 0.82f);
            bgStyle.SetCornerRadiusAll(6);
            bgStyle.BorderColor = new Color(0.314f, 0.333f, 0.392f, 0.25f);
            bgStyle.SetBorderWidthAll(1);
            bgStyle.ContentMarginLeft = 10; bgStyle.ContentMarginRight = 10;
            bgStyle.ContentMarginTop = 4; bgStyle.ContentMarginBottom = 4;
            bg.AddThemeStyleboxOverride("panel", bgStyle);
            bg.Position = new Vector2(-80, -64);
            bg.Visible = false;
            bg.Name = "BubbleBg";
            bg.AddChild(_chatBubble);
            _chatBubble.Position = Vector2.Zero; // relative to bg now

            player.AddChild(bg);

            // Link to ChatPanel — the panel will set visibility and text
            chatPanel.ChatBubbleLabel = _chatBubble;
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
        // Find and hide the old debug overlay so it doesn't overlap
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
