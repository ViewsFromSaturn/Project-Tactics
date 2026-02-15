using Godot;
using System;

namespace NarutoRP.UI;

/// <summary>
/// Title Screen with Login / Register forms.
/// Connects to Flask backend via ApiClient.
/// </summary>
public partial class TitleScreen : Control
{
    private enum Mode { Title, Login, Register }
    private Mode _mode = Mode.Title;

    // Title elements
    private VBoxContainer _titlePanel;
    private VBoxContainer _formPanel;

    // Form inputs
    private LineEdit _usernameInput;
    private LineEdit _emailInput;    // Register only
    private LineEdit _passwordInput;
    private LineEdit _confirmInput;  // Register only
    private Label _errorLabel;
    private Button _submitButton;
    private Button _switchButton;
    private Button _backButton;

    public override void _Ready()
    {
        BuildUI();
        ShowMode(Mode.Title);
        GD.Print("[TitleScreen] Ready.");
    }

    // ═════════════════════════════════════════════════════════
    //  BUILD UI
    // ═════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // Background
        var bg = new ColorRect();
        bg.Color = new Color("1a1a2e");
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // ─── TITLE PANEL ────────────────────────────────────
        _titlePanel = new VBoxContainer();
        _titlePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _titlePanel.GrowHorizontal = GrowDirection.Both;
        _titlePanel.GrowVertical = GrowDirection.Both;
        _titlePanel.CustomMinimumSize = new Vector2(400, 300);
        _titlePanel.Position = new Vector2(-200, -150);
        _titlePanel.AddThemeConstantOverride("separation", 12);
        AddChild(_titlePanel);

        var title = CreateLabel("NARUTO TACTICS", 42, "E87722");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _titlePanel.AddChild(title);

        var subtitle = CreateLabel("A Tactical RPG / Sandbox Roleplay Game", 14, "888888");
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _titlePanel.AddChild(subtitle);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 60);
        _titlePanel.AddChild(spacer);

        var loginBtn = CreateStyledButton("LOGIN");
        loginBtn.Pressed += () => ShowMode(Mode.Login);
        _titlePanel.AddChild(loginBtn);

        var registerBtn = CreateStyledButton("REGISTER", true);
        registerBtn.Pressed += () => ShowMode(Mode.Register);
        _titlePanel.AddChild(registerBtn);

        var quitBtn = CreateStyledButton("QUIT", true);
        quitBtn.Pressed += () => GetTree().Quit();
        _titlePanel.AddChild(quitBtn);

        // ─── FORM PANEL ─────────────────────────────────────
        _formPanel = new VBoxContainer();
        _formPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _formPanel.GrowHorizontal = GrowDirection.Both;
        _formPanel.GrowVertical = GrowDirection.Both;
        _formPanel.CustomMinimumSize = new Vector2(400, 400);
        _formPanel.Position = new Vector2(-200, -200);
        _formPanel.AddThemeConstantOverride("separation", 10);
        _formPanel.Visible = false;
        AddChild(_formPanel);

        // Version label
        var version = CreateLabel("v1.0 - Phase 3", 12, "555555");
        version.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        version.Position += new Vector2(-120, -30);
        AddChild(version);
    }

    // ═════════════════════════════════════════════════════════
    //  MODE SWITCHING
    // ═════════════════════════════════════════════════════════

    private void ShowMode(Mode mode)
    {
        _mode = mode;

        // Clear form panel
        foreach (var child in _formPanel.GetChildren())
            child.QueueFree();

        if (mode == Mode.Title)
        {
            _titlePanel.Visible = true;
            _formPanel.Visible = false;
            return;
        }

        _titlePanel.Visible = false;
        _formPanel.Visible = true;

        bool isRegister = mode == Mode.Register;
        string formTitle = isRegister ? "CREATE ACCOUNT" : "LOGIN";

        // Form title
        var titleLabel = CreateLabel(formTitle, 28, "E87722");
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _formPanel.AddChild(titleLabel);

        var spacer1 = new Control();
        spacer1.CustomMinimumSize = new Vector2(0, 20);
        _formPanel.AddChild(spacer1);

        // Username
        _formPanel.AddChild(CreateLabel("Username", 14, "AAAAAA"));
        _usernameInput = CreateInput("Enter username...");
        _formPanel.AddChild(_usernameInput);

        // Email (register only)
        if (isRegister)
        {
            _formPanel.AddChild(CreateLabel("Email", 14, "AAAAAA"));
            _emailInput = CreateInput("Enter email...");
            _formPanel.AddChild(_emailInput);
        }

        // Password
        _formPanel.AddChild(CreateLabel("Password", 14, "AAAAAA"));
        _passwordInput = CreateInput("Enter password...", secret: true);
        _formPanel.AddChild(_passwordInput);

        // Confirm password (register only)
        if (isRegister)
        {
            _formPanel.AddChild(CreateLabel("Confirm Password", 14, "AAAAAA"));
            _confirmInput = CreateInput("Confirm password...", secret: true);
            _formPanel.AddChild(_confirmInput);
        }

        // Error label
        _errorLabel = CreateLabel("", 14, "FF4444");
        _errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _formPanel.AddChild(_errorLabel);

        var spacer2 = new Control();
        spacer2.CustomMinimumSize = new Vector2(0, 10);
        _formPanel.AddChild(spacer2);

        // Submit button
        _submitButton = CreateStyledButton(isRegister ? "CREATE ACCOUNT" : "LOGIN");
        _submitButton.Pressed += OnSubmitPressed;
        _formPanel.AddChild(_submitButton);

        // Switch mode button
        string switchText = isRegister ? "Already have an account? Login" : "Need an account? Register";
        _switchButton = CreateStyledButton(switchText, true);
        _switchButton.Pressed += () => ShowMode(isRegister ? Mode.Login : Mode.Register);
        _formPanel.AddChild(_switchButton);

        // Back button
        _backButton = CreateStyledButton("← BACK", true);
        _backButton.Pressed += () => ShowMode(Mode.Title);
        _formPanel.AddChild(_backButton);

        // Focus username field
        _usernameInput.CallDeferred("grab_focus");
    }

    // ═════════════════════════════════════════════════════════
    //  SUBMIT
    // ═════════════════════════════════════════════════════════

    private async void OnSubmitPressed()
    {
        _errorLabel.Text = "";
        _submitButton.Disabled = true;
        _submitButton.Text = "Please wait...";

        var api = Networking.ApiClient.Instance;
        if (api == null)
        {
            _errorLabel.Text = "ApiClient not initialized. Add it as an Autoload.";
            ResetSubmitButton();
            return;
        }

        string username = _usernameInput.Text.Trim();
        string password = _passwordInput.Text;

        if (_mode == Mode.Register)
        {
            string email = _emailInput?.Text.Trim() ?? "";
            string confirm = _confirmInput?.Text ?? "";

            // Validate
            if (username.Length < 3)
            {
                _errorLabel.Text = "Username must be at least 3 characters.";
                ResetSubmitButton();
                return;
            }
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                _errorLabel.Text = "Please enter a valid email.";
                ResetSubmitButton();
                return;
            }
            if (password.Length < 6)
            {
                _errorLabel.Text = "Password must be at least 6 characters.";
                ResetSubmitButton();
                return;
            }
            if (password != confirm)
            {
                _errorLabel.Text = "Passwords do not match.";
                ResetSubmitButton();
                return;
            }

            var resp = await api.Register(username, email, password);
            if (resp.Success)
            {
                GoToCharacterSelect();
            }
            else
            {
                _errorLabel.Text = resp.Error;
                ResetSubmitButton();
            }
        }
        else // Login
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _errorLabel.Text = "Username and password required.";
                ResetSubmitButton();
                return;
            }

            var resp = await api.Login(username, password);
            if (resp.Success)
            {
                GoToCharacterSelect();
            }
            else
            {
                _errorLabel.Text = resp.Error;
                ResetSubmitButton();
            }
        }
    }

    private void ResetSubmitButton()
    {
        _submitButton.Disabled = false;
        _submitButton.Text = _mode == Mode.Register ? "CREATE ACCOUNT" : "LOGIN";
    }

    private void GoToCharacterSelect()
    {
        var gm = Core.GameManager.Instance;
        if (gm != null)
        {
            gm.SetState(Core.GameManager.GameState.CharacterSelect);
            gm.ChangeScene(Core.GameManager.Scenes.CharSelect);
        }
    }

    // ═════════════════════════════════════════════════════════
    //  UI HELPERS
    // ═════════════════════════════════════════════════════════

    private Label CreateLabel(string text, int size, string color)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", new Color(color));
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private LineEdit CreateInput(string placeholder, bool secret = false)
    {
        var input = new LineEdit();
        input.PlaceholderText = placeholder;
        input.Secret = secret;
        input.CustomMinimumSize = new Vector2(380, 38);
        input.AddThemeFontSizeOverride("font_size", 16);

        var style = new StyleBoxFlat();
        style.BgColor = new Color("2a2a3e");
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(8);
        style.BorderColor = new Color("555566");
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthTop = 2;
        input.AddThemeStyleboxOverride("normal", style);
        input.AddThemeColorOverride("font_color", Colors.White);
        input.AddThemeColorOverride("font_placeholder_color", new Color("666666"));

        return input;
    }

    private Button CreateStyledButton(string text, bool secondary = false)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(280, 45);

        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = secondary ? new Color("333344") : new Color("E87722");
        styleNormal.SetCornerRadiusAll(6);
        styleNormal.SetContentMarginAll(12);
        btn.AddThemeStyleboxOverride("normal", styleNormal);

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = secondary ? new Color("444455") : new Color("FF8833");
        styleHover.SetCornerRadiusAll(6);
        styleHover.SetContentMarginAll(12);
        btn.AddThemeStyleboxOverride("hover", styleHover);

        var stylePressed = new StyleBoxFlat();
        stylePressed.BgColor = secondary ? new Color("222233") : new Color("CC6611");
        stylePressed.SetCornerRadiusAll(6);
        stylePressed.SetContentMarginAll(12);
        btn.AddThemeStyleboxOverride("pressed", stylePressed);

        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 18);

        return btn;
    }
}
