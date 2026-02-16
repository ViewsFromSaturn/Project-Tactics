using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Settings — audio, display, keybinds, logout.
/// Hotkey: Esc (second press) | Slides: Right
/// </summary>
public partial class SettingsPanel : SlidePanel
{
    public SettingsPanel()
    {
        PanelTitle = "Settings";
        Direction = SlideDirection.Right;
        PanelWidth = 340;
    }

    protected override void BuildContent(VBoxContainer content)
    {
        // ─── Audio ───
        content.AddChild(SectionHeader("Audio"));
        content.AddChild(SliderRow("Master Volume", 80));
        content.AddChild(SliderRow("Music", 60));
        content.AddChild(SliderRow("SFX", 80));

        content.AddChild(ThinSeparator());

        // ─── Display ───
        content.AddChild(SectionHeader("Display"));
        content.AddChild(ToggleRow("Fullscreen", false));
        content.AddChild(ToggleRow("VSync", true));
        content.AddChild(ToggleRow("Show FPS", false));

        content.AddChild(ThinSeparator());

        // ─── Chat ───
        content.AddChild(SectionHeader("Chat"));
        content.AddChild(ToggleRow("Chat Timestamps", true));
        content.AddChild(ToggleRow("Chat Fade When Idle", false));

        content.AddChild(ThinSeparator());

        // ─── Keybinds Reference ───
        content.AddChild(SectionHeader("Keybinds"));
        content.AddChild(KeybindRow("C", "Character Sheet"));
        content.AddChild(KeybindRow("V", "Training"));
        content.AddChild(KeybindRow("J", "Journal"));
        content.AddChild(KeybindRow("M", "Map"));
        content.AddChild(KeybindRow("I", "Inventory"));
        content.AddChild(KeybindRow("N", "Mentorship"));
        content.AddChild(KeybindRow("Esc", "Settings / Close"));
        content.AddChild(KeybindRow("Enter", "Focus Chat"));
        content.AddChild(KeybindRow("Tab", "Cycle Verb"));
        content.AddChild(KeybindRow("F1", "Debug Overlay"));

        content.AddChild(ThinSeparator());

        // ─── Account ───
        content.AddChild(SectionHeader("Account"));

        var api = Networking.ApiClient.Instance;
        string username = api?.Username ?? "Unknown";
        content.AddChild(UITheme.CreateBody($"Logged in as: {username}", 12, UITheme.TextDim));

        content.AddChild(UITheme.CreateSpacer(6));

        var logoutBtn = UITheme.CreateSecondaryButton("LOGOUT", 13);
        logoutBtn.Pressed += OnLogoutPressed;
        content.AddChild(logoutBtn);

        content.AddChild(UITheme.CreateSpacer(4));

        var quitBtn = UITheme.CreateGhostButton("Quit Game", 12, UITheme.Error);
        quitBtn.Pressed += () => content.GetTree().Quit();
        content.AddChild(quitBtn);
    }

    // ═══ WIDGET HELPERS ═══

    private static HBoxContainer SliderRow(string label, int defaultValue)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = UITheme.CreateBody(label, 12, UITheme.Text);
        nameLabel.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(nameLabel);

        var slider = new HSlider();
        slider.MinValue = 0;
        slider.MaxValue = 100;
        slider.Value = defaultValue;
        slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        slider.CustomMinimumSize = new Vector2(0, 20);
        row.AddChild(slider);

        var valLabel = UITheme.CreateNumbers($"{defaultValue}", 11, UITheme.TextDim);
        valLabel.CustomMinimumSize = new Vector2(28, 0);
        valLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(valLabel);

        slider.ValueChanged += (double val) =>
        {
            valLabel.Text = $"{(int)val}";
            // TODO: Apply volume setting
        };

        return row;
    }

    private static HBoxContainer ToggleRow(string label, bool defaultOn)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = UITheme.CreateBody(label, 12, UITheme.Text);
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(nameLabel);

        var toggle = new CheckButton();
        toggle.ButtonPressed = defaultOn;
        toggle.Pressed += () =>
        {
            GD.Print($"[Settings] {label} = {toggle.ButtonPressed}");
            // TODO: Apply setting
        };
        row.AddChild(toggle);

        return row;
    }

    private static HBoxContainer KeybindRow(string key, string action)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var keyLabel = UITheme.CreateNumbers(key, 11, UITheme.TextBright);
        keyLabel.CustomMinimumSize = new Vector2(50, 0);
        keyLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(keyLabel);

        row.AddChild(UITheme.CreateDim("—", 10));
        row.AddChild(UITheme.CreateBody(action, 11, UITheme.TextDim));

        return row;
    }

    private void OnLogoutPressed()
    {
        var api = Networking.ApiClient.Instance;
        api?.Logout();

        var gm = Core.GameManager.Instance;
        gm?.SetState(Core.GameManager.GameState.Login);
        gm?.ChangeScene(Core.GameManager.Scenes.Title);
    }
}
