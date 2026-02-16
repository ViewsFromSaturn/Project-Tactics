using Godot;
using System;

namespace ProjectTactics.UI;

/// <summary>
/// IC Profile Popup — appears when clicking a player name in chat.
/// Shows portrait, name, rank, city, allegiance, bio, status.
/// Matches HUD v4 mockup styling.
/// </summary>
public partial class ProfilePopup : PanelContainer
{
    // ═══ STATE ═══
    private string _targetName = "";
    private bool _isVisible = false;

    // ═══ UI REFS ═══
    private Label _portraitIcon;
    private Label _nameLabel;
    private Label _rankLabel;
    private Label _cityLabel;
    private Label _bioLabel;
    private Label _statusLabel;
    private VBoxContainer _content;

    // ═══ STYLING CONSTANTS ═══
    private static readonly Color PopupBg = new(0.039f, 0.039f, 0.071f, 0.95f);
    private static readonly Color PopupBorder = new(0.235f, 0.255f, 0.314f, 0.4f);
    private static readonly Color SepColor = new(0.235f, 0.255f, 0.314f, 0.2f);

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(260, 0);

        // Panel style
        var style = new StyleBoxFlat();
        style.BgColor = PopupBg;
        style.SetCornerRadiusAll(8);
        style.BorderColor = PopupBorder;
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = 18;
        style.ContentMarginRight = 18;
        style.ContentMarginTop = 16;
        style.ContentMarginBottom = 16;
        AddThemeStyleboxOverride("panel", style);

        BuildContent();
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!_isVisible) return;

        // Close on click outside or Escape
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (!GetGlobalRect().HasPoint(mb.GlobalPosition))
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    // ═════════════════════════════════════════════════════════
    //  BUILD
    // ═════════════════════════════════════════════════════════

    private void BuildContent()
    {
        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 0);
        AddChild(_content);

        // ─── Header row: portrait + identity ─────────────
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        _content.AddChild(header);

        // Portrait placeholder
        var portraitFrame = new PanelContainer();
        portraitFrame.CustomMinimumSize = new Vector2(48, 48);
        var portraitStyle = new StyleBoxFlat();
        portraitStyle.BgColor = new Color(0.157f, 0.157f, 0.216f, 0.6f);
        portraitStyle.SetCornerRadiusAll(5);
        portraitStyle.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.3f);
        portraitStyle.SetBorderWidthAll(1);
        portraitFrame.AddThemeStyleboxOverride("panel", portraitStyle);
        header.AddChild(portraitFrame);

        _portraitIcon = new Label();
        _portraitIcon.Text = "⚔";
        _portraitIcon.AddThemeFontSizeOverride("font_size", 22);
        _portraitIcon.AddThemeColorOverride("font_color", UITheme.TextDim);
        _portraitIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _portraitIcon.VerticalAlignment = VerticalAlignment.Center;
        portraitFrame.AddChild(_portraitIcon);

        // Identity column
        var identity = new VBoxContainer();
        identity.AddThemeConstantOverride("separation", 2);
        identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(identity);

        _nameLabel = new Label();
        _nameLabel.Text = "Unknown";
        _nameLabel.AddThemeFontSizeOverride("font_size", 15);
        _nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
        if (UITheme.FontTitleMedium != null) _nameLabel.AddThemeFontOverride("font", UITheme.FontTitleMedium);
        identity.AddChild(_nameLabel);

        _rankLabel = new Label();
        _rankLabel.Text = "ASPIRANT";
        _rankLabel.AddThemeFontSizeOverride("font_size", 10);
        _rankLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
        if (UITheme.FontBody != null) _rankLabel.AddThemeFontOverride("font", UITheme.FontBody);
        identity.AddChild(_rankLabel);

        _cityLabel = new Label();
        _cityLabel.Text = "Lumere";
        _cityLabel.AddThemeFontSizeOverride("font_size", 10);
        _cityLabel.AddThemeColorOverride("font_color", UITheme.Accent);
        if (UITheme.FontBody != null) _cityLabel.AddThemeFontOverride("font", UITheme.FontBody);
        identity.AddChild(_cityLabel);

        // ─── Separator ──────────────────────────────────────
        _content.AddChild(CreatePopupSpacer(10));
        _content.AddChild(CreatePopupSep());
        _content.AddChild(CreatePopupSpacer(8));

        // ─── Bio ────────────────────────────────────────────
        _bioLabel = new Label();
        _bioLabel.Text = "";
        _bioLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _bioLabel.AddThemeFontSizeOverride("font_size", 12);
        _bioLabel.AddThemeColorOverride("font_color", UITheme.Text);
        if (UITheme.FontBody != null) _bioLabel.AddThemeFontOverride("font", UITheme.FontBody);
        _content.AddChild(_bioLabel);

        // ─── Status ─────────────────────────────────────────
        _statusLabel = new Label();
        _statusLabel.Text = "";
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
        if (UITheme.FontBody != null) _statusLabel.AddThemeFontOverride("font", UITheme.FontBody);
        _content.AddChild(_statusLabel);

        // ─── Bottom separator + actions ─────────────────────
        _content.AddChild(CreatePopupSpacer(10));
        _content.AddChild(CreatePopupSep());
        _content.AddChild(CreatePopupSpacer(8));

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _content.AddChild(actions);

        // "View full profile" link
        var viewLink = new Button();
        viewLink.Text = "View full profile →";
        viewLink.AddThemeFontSizeOverride("font_size", 11);
        viewLink.AddThemeColorOverride("font_color", UITheme.Accent);
        viewLink.AddThemeColorOverride("font_hover_color", UITheme.TextBright);
        if (UITheme.FontBody != null) viewLink.AddThemeFontOverride("font", UITheme.FontBody);
        ApplyGhostStyle(viewLink);
        viewLink.Pressed += OnViewFullProfile;
        actions.AddChild(viewLink);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(spacer);

        // Close text
        var closeLabel = new Button();
        closeLabel.Text = "Close";
        closeLabel.AddThemeFontSizeOverride("font_size", 10);
        closeLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
        closeLabel.AddThemeColorOverride("font_hover_color", UITheme.Text);
        if (UITheme.FontBody != null) closeLabel.AddThemeFontOverride("font", UITheme.FontBody);
        ApplyGhostStyle(closeLabel);
        closeLabel.Pressed += Hide;
        actions.AddChild(closeLabel);
    }

    // ═════════════════════════════════════════════════════════
    //  SHOW / HIDE
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Show the popup for a given player name, positioned near a screen coordinate.
    /// </summary>
    public void ShowForPlayer(string playerName, Vector2 screenPosition)
    {
        _targetName = playerName;

        // Look up player data — for now, check if it's the active character
        var active = Core.GameManager.Instance?.ActiveCharacter;
        if (active != null && active.CharacterName == playerName)
        {
            PopulateFromPlayerData(active);
        }
        else
        {
            // Other player — show name with placeholder data
            // In multiplayer, this would query the server
            PopulatePlaceholder(playerName);
        }

        // Position: try to place near the click, but keep on screen
        var viewport = GetViewportRect().Size;
        float popupWidth = 260f;
        float popupHeight = Size.Y > 0 ? Size.Y : 200f;

        float x = Math.Clamp(screenPosition.X + 10, 0, viewport.X - popupWidth - 10);
        float y = Math.Clamp(screenPosition.Y - popupHeight / 2, 10, viewport.Y - popupHeight - 10);

        Position = new Vector2(x, y);
        Size = new Vector2(popupWidth, 0); // Let it auto-size height

        Visible = true;
        _isVisible = true;
    }

    public new void Hide()
    {
        Visible = false;
        _isVisible = false;
    }

    // ═════════════════════════════════════════════════════════
    //  POPULATE
    // ═════════════════════════════════════════════════════════

    private void PopulateFromPlayerData(Core.PlayerData data)
    {
        _nameLabel.Text = data.CharacterName;
        _rankLabel.Text = (data.RpRank ?? "Aspirant").ToUpper();
        _cityLabel.Text = data.City ?? "Unknown";

        if (!string.IsNullOrEmpty(data.Bio))
        {
            _bioLabel.Text = data.Bio;
            _bioLabel.Visible = true;
        }
        else
        {
            _bioLabel.Text = "No biography set.";
            _bioLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
            _bioLabel.Visible = true;
        }

        // Status — placeholder for now
        _statusLabel.Text = "Training at the grounds";
        _statusLabel.Visible = true;
    }

    private void PopulatePlaceholder(string name)
    {
        _nameLabel.Text = name;
        _rankLabel.Text = "UNKNOWN RANK";
        _cityLabel.Text = "Unknown city";
        _bioLabel.Text = "Character data not available.";
        _bioLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
        _bioLabel.Visible = true;
        _statusLabel.Visible = false;
    }

    // ═════════════════════════════════════════════════════════
    //  ACTIONS
    // ═════════════════════════════════════════════════════════

    private void OnViewFullProfile()
    {
        GD.Print($"[ProfilePopup] View full profile: {_targetName}");
        // Future: open the character sheet slide panel for this player
        Hide();
    }

    // ═════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════

    private static Control CreatePopupSpacer(float height)
    {
        var s = new Control();
        s.CustomMinimumSize = new Vector2(0, height);
        return s;
    }

    private static PanelContainer CreatePopupSep()
    {
        var sep = new PanelContainer();
        sep.CustomMinimumSize = new Vector2(0, 1);
        var style = new StyleBoxFlat();
        style.BgColor = SepColor;
        sep.AddThemeStyleboxOverride("panel", style);
        return sep;
    }

    private static void ApplyGhostStyle(Button btn)
    {
        var s = new StyleBoxFlat();
        s.BgColor = Colors.Transparent;
        s.SetCornerRadiusAll(3);
        s.ContentMarginLeft = 4;
        s.ContentMarginRight = 4;
        s.ContentMarginTop = 2;
        s.ContentMarginBottom = 2;
        btn.AddThemeStyleboxOverride("normal", s);
        var h = (StyleBoxFlat)s.Duplicate();
        h.BgColor = new Color(1, 1, 1, 0.04f);
        btn.AddThemeStyleboxOverride("hover", h);
        btn.AddThemeStyleboxOverride("pressed", s);
    }
}
