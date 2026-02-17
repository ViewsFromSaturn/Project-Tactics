using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Settings — Audio, Display, Chat, Controls, Account.
/// Matches Taskade mockup (Image 2): section headers with icons,
/// card-wrapped rows, theme toggle, clean layout.
/// Hotkey: Esc
/// </summary>
public partial class SettingsPanel : WindowPanel
{
	public SettingsPanel()
	{
		WindowTitle = "Settings";
		DefaultSize = new Vector2(400, 580);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		// ═══ AUDIO ═══
		content.AddChild(SectionHeaderWithIcon("🔊", "Audio"));
		content.AddChild(VolumeSliderRow("Master Volume", 80));
		content.AddChild(Spacer(4));

		content.AddChild(ThinSeparator());

		// ═══ DISPLAY ═══
		content.AddChild(SectionHeaderWithIcon("🖥", "Display"));
		content.AddChild(CardToggleRow("Fullscreen Mode", false, OnFullscreenToggled));
		content.AddChild(CardRow("Resolution", "1920 x 1080 (recommended)"));
		content.AddChild(Spacer(4));

		// Theme toggle
		content.AddChild(SectionHeaderWithIcon("🎨", "Theme"));
		content.AddChild(CardToggleRow("Dark Mode", UITheme.IsDarkMode, OnThemeToggled));
		content.AddChild(Spacer(4));

		content.AddChild(ThinSeparator());

		// ═══ CHAT ═══
		content.AddChild(SectionHeaderWithIcon("💬", "Chat"));
		content.AddChild(CardToggleRow("Show Timestamps", true, (on) =>
		{
			GD.Print($"[Settings] Timestamps = {on}");
		}));
		content.AddChild(CardToggleRow("Fade When Idle", ChatPanel.FadeWhenIdle, (on) =>
		{
			ChatPanel.FadeWhenIdle = on;
			GD.Print($"[Settings] Chat fade = {on}");
		}));
		content.AddChild(Spacer(4));

		content.AddChild(ThinSeparator());

		// ═══ CONTROLS ═══
		content.AddChild(SectionHeaderWithIcon("⌨", "Controls"));
		var controlsGrid = new VBoxContainer();
		controlsGrid.AddThemeConstantOverride("separation", 2);
		controlsGrid.AddChild(KeybindRow("C", "Character Sheet"));
		controlsGrid.AddChild(KeybindRow("V", "Training"));
		controlsGrid.AddChild(KeybindRow("J", "Journal"));
		controlsGrid.AddChild(KeybindRow("M", "Map"));
		controlsGrid.AddChild(KeybindRow("I", "Inventory"));
		controlsGrid.AddChild(KeybindRow("N", "Mentorship"));
		controlsGrid.AddChild(KeybindRow("Esc", "Settings / Close"));
		controlsGrid.AddChild(KeybindRow("Enter", "Focus Chat"));
		controlsGrid.AddChild(KeybindRow("Tab", "Cycle Verb"));
		controlsGrid.AddChild(KeybindRow("F1", "Debug Overlay"));
		content.AddChild(controlsGrid);
		content.AddChild(Spacer(4));

		content.AddChild(ThinSeparator());

		// ═══ ACCOUNT ═══
		content.AddChild(SectionHeaderWithIcon("👤", "Account"));

		var api = Networking.ApiClient.Instance;
		string username = api?.Username ?? "Unknown";
		content.AddChild(CardRow("Logged in as", username));
		content.AddChild(CardRow("Change Password", ""));
		content.AddChild(Spacer(6));

		// Logout — pink/red card style matching Taskade mockup
		var logoutBtn = new Button();
		logoutBtn.Text = "Logout";
		logoutBtn.CustomMinimumSize = new Vector2(0, 48);
		logoutBtn.AddThemeFontSizeOverride("font_size", 14);
		if (UITheme.FontBodyMedium != null) logoutBtn.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		logoutBtn.AddThemeColorOverride("font_color", UITheme.AccentRed);
		logoutBtn.AddThemeColorOverride("font_hover_color", new Color("A03030"));

		var logoutNormal = new StyleBoxFlat();
		logoutNormal.BgColor = UITheme.IsDarkMode
			? new Color(0.784f, 0.314f, 0.314f, 0.12f)
			: new Color(0.95f, 0.85f, 0.85f, 1f);
		logoutNormal.SetCornerRadiusAll(10);
		logoutNormal.SetBorderWidthAll(1);
		logoutNormal.BorderColor = UITheme.IsDarkMode
			? new Color(0.784f, 0.314f, 0.314f, 0.25f)
			: new Color(0.9f, 0.75f, 0.75f, 1f);
		logoutNormal.ContentMarginLeft = 16; logoutNormal.ContentMarginRight = 16;
		logoutBtn.AddThemeStyleboxOverride("normal", logoutNormal);

		var logoutHover = (StyleBoxFlat)logoutNormal.Duplicate();
		logoutHover.BgColor = UITheme.IsDarkMode
			? new Color(0.784f, 0.314f, 0.314f, 0.20f)
			: new Color(0.92f, 0.80f, 0.80f, 1f);
		logoutBtn.AddThemeStyleboxOverride("hover", logoutHover);

		logoutBtn.Pressed += OnLogoutPressed;
		content.AddChild(logoutBtn);

		content.AddChild(Spacer(4));

		var quitBtn = UITheme.CreateGhostButton("Quit Game", 12, UITheme.AccentRed);
		quitBtn.Pressed += () => content.GetTree().Quit();
		content.AddChild(quitBtn);
	}

	// ─── Section header with emoji icon ───
	private static HBoxContainer SectionHeaderWithIcon(string icon, string text)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);

		var iconLabel = new Label();
		iconLabel.Text = icon;
		iconLabel.AddThemeFontSizeOverride("font_size", 14);
		row.AddChild(iconLabel);

		var title = new Label();
		title.Text = text;
		title.AddThemeFontSizeOverride("font_size", 15);
		title.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) title.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		row.AddChild(title);

		return row;
	}

	// ─── Card-wrapped toggle row ───
	private static PanelContainer CardToggleRow(string label, bool defaultOn, System.Action<bool> onChanged)
	{
		var card = new PanelContainer();
		var cardStyle = UITheme.CreateCardStyle();
		cardStyle.SetCornerRadiusAll(10);
		cardStyle.ContentMarginLeft = 14; cardStyle.ContentMarginRight = 14;
		cardStyle.ContentMarginTop = 10; cardStyle.ContentMarginBottom = 10;
		card.AddThemeStyleboxOverride("panel", cardStyle);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		card.AddChild(row);

		var nameLabel = UITheme.CreateBody(label, 13, UITheme.Text);
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(nameLabel);

		var toggle = new CheckButton();
		toggle.ButtonPressed = defaultOn;
		toggle.Pressed += () => onChanged?.Invoke(toggle.ButtonPressed);
		row.AddChild(toggle);

		return card;
	}

	// ─── Card-wrapped info row (label + subtitle) ───
	private static PanelContainer CardRow(string label, string subtitle)
	{
		var card = new PanelContainer();
		var cardStyle = UITheme.CreateCardStyle();
		cardStyle.SetCornerRadiusAll(10);
		cardStyle.ContentMarginLeft = 14; cardStyle.ContentMarginRight = 14;
		cardStyle.ContentMarginTop = 10; cardStyle.ContentMarginBottom = 10;
		card.AddThemeStyleboxOverride("panel", cardStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		card.AddChild(vbox);

		vbox.AddChild(UITheme.CreateBody(label, 13, UITheme.Text));
		if (!string.IsNullOrEmpty(subtitle))
			vbox.AddChild(UITheme.CreateDim(subtitle, 11));

		return card;
	}

	// ─── Volume slider with 0% / value% / 100% labels ───
	private static VBoxContainer VolumeSliderRow(string label, int defaultValue)
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);

		vbox.AddChild(UITheme.CreateBody(label, 13, UITheme.TextBright));

		var slider = new HSlider();
		slider.MinValue = 0;
		slider.MaxValue = 100;
		slider.Value = defaultValue;
		slider.CustomMinimumSize = new Vector2(0, 20);
		vbox.AddChild(slider);

		var labels = new HBoxContainer();
		labels.AddChild(UITheme.CreateDim("0%", 10));
		var valLabel = UITheme.CreateNumbers($"{defaultValue}%", 10, UITheme.TextSecondary);
		valLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		valLabel.HorizontalAlignment = HorizontalAlignment.Center;
		labels.AddChild(valLabel);
		labels.AddChild(UITheme.CreateDim("100%", 10));
		vbox.AddChild(labels);

		slider.ValueChanged += (double val) => valLabel.Text = $"{(int)val}%";

		return vbox;
	}

	// ─── Keybind row ───
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

	// ═══ CALLBACKS ═══

	private void OnFullscreenToggled(bool on)
	{
		DisplayServer.WindowSetMode(on
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);
		GD.Print($"[Settings] Fullscreen = {on}");
	}

	private void OnThemeToggled(bool dark)
	{
		UITheme.SetDarkMode(dark);
		GD.Print($"[Settings] Dark mode = {dark}");
		// NOTE: Full UI refresh requires re-opening panels.
		// A real implementation would emit a signal that all panels listen to.
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
