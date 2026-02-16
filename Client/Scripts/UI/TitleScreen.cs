using Godot;
using System;

namespace ProjectTactics.UI;

/// <summary>
/// Title Screen with Login / Register forms.
/// Visual style matches HUD v4 mockup.
/// </summary>
public partial class TitleScreen : Control
{
	private enum Mode { Title, Login, Register }
	private Mode _mode = Mode.Title;

	private VBoxContainer _titlePanel;
	private VBoxContainer _formPanel;

	// Form inputs
	private LineEdit _usernameInput;
	private LineEdit _emailInput;
	private LineEdit _passwordInput;
	private LineEdit _confirmInput;
	private Label _errorLabel;
	private Button _submitButton;

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
		AddChild(UITheme.CreateBackground());

		// ─── TITLE PANEL ────────────────────────────────────
		_titlePanel = new VBoxContainer();
		_titlePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_titlePanel.GrowHorizontal = GrowDirection.Both;
		_titlePanel.GrowVertical = GrowDirection.Both;
		_titlePanel.CustomMinimumSize = new Vector2(360, 320);
		_titlePanel.Position = new Vector2(-180, -160);
		_titlePanel.AddThemeConstantOverride("separation", 6);
		AddChild(_titlePanel);

		// Game title
		var title = UITheme.CreateTitle("PROJECT TACTICS", 38);
		title.AddThemeColorOverride("font_color", UITheme.AccentOrange);
		_titlePanel.AddChild(title);

		var subtitle = UITheme.CreateDim("A Tactical RPG / Sandbox Roleplay Game", 13);
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		_titlePanel.AddChild(subtitle);

		_titlePanel.AddChild(UITheme.CreateSpacer(50));

		// Buttons
		var loginBtn = UITheme.CreatePrimaryButton("LOGIN", 16);
		loginBtn.CustomMinimumSize = new Vector2(280, 48);
		loginBtn.Pressed += () => ShowMode(Mode.Login);
		_titlePanel.AddChild(CenterWrap(loginBtn));

		_titlePanel.AddChild(UITheme.CreateSpacer(4));

		var registerBtn = UITheme.CreateSecondaryButton("REGISTER", 15);
		registerBtn.CustomMinimumSize = new Vector2(280, 44);
		registerBtn.Pressed += () => ShowMode(Mode.Register);
		_titlePanel.AddChild(CenterWrap(registerBtn));

		_titlePanel.AddChild(UITheme.CreateSpacer(4));

		var quitBtn = UITheme.CreateGhostButton("QUIT", 13);
		quitBtn.CustomMinimumSize = new Vector2(280, 36);
		quitBtn.Pressed += () => GetTree().Quit();
		_titlePanel.AddChild(CenterWrap(quitBtn));

		// ─── FORM PANEL ─────────────────────────────────────
		_formPanel = new VBoxContainer();
		_formPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_formPanel.GrowHorizontal = GrowDirection.Both;
		_formPanel.GrowVertical = GrowDirection.Both;
		_formPanel.CustomMinimumSize = new Vector2(380, 420);
		_formPanel.Position = new Vector2(-190, -210);
		_formPanel.AddThemeConstantOverride("separation", 6);
		_formPanel.Visible = false;
		AddChild(_formPanel);

		// Version label
		AddChild(UITheme.CreateVersionLabel());
	}

	// ═════════════════════════════════════════════════════════
	//  MODE SWITCHING
	// ═════════════════════════════════════════════════════════

	private void ShowMode(Mode mode)
	{
		_mode = mode;

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
		var titleLabel = UITheme.CreateTitle(formTitle, 24);
		titleLabel.AddThemeColorOverride("font_color", UITheme.AccentOrange);
		_formPanel.AddChild(titleLabel);

		_formPanel.AddChild(UITheme.CreateSpacer(14));

		// Username
		_formPanel.AddChild(UITheme.CreateBody("Username", 12, UITheme.TextDim));
		_usernameInput = UITheme.CreateInput("Enter username...");
		_formPanel.AddChild(_usernameInput);

		_formPanel.AddChild(UITheme.CreateSpacer(4));

		// Email (register only)
		if (isRegister)
		{
			_formPanel.AddChild(UITheme.CreateBody("Email", 12, UITheme.TextDim));
			_emailInput = UITheme.CreateInput("Enter email...");
			_formPanel.AddChild(_emailInput);
			_formPanel.AddChild(UITheme.CreateSpacer(4));
		}

		// Password
		_formPanel.AddChild(UITheme.CreateBody("Password", 12, UITheme.TextDim));
		_passwordInput = UITheme.CreateInput("Enter password...", secret: true);
		_formPanel.AddChild(_passwordInput);

		_formPanel.AddChild(UITheme.CreateSpacer(4));

		// Confirm password (register only)
		if (isRegister)
		{
			_formPanel.AddChild(UITheme.CreateBody("Confirm Password", 12, UITheme.TextDim));
			_confirmInput = UITheme.CreateInput("Confirm password...", secret: true);
			_formPanel.AddChild(_confirmInput);
		}

		// Error label
		_errorLabel = UITheme.CreateBody("", 13, UITheme.Error);
		_errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_formPanel.AddChild(_errorLabel);

		_formPanel.AddChild(UITheme.CreateSpacer(8));

		// Submit button
		_submitButton = UITheme.CreatePrimaryButton(isRegister ? "CREATE ACCOUNT" : "LOGIN", 15);
		_submitButton.CustomMinimumSize = new Vector2(360, 46);
		_submitButton.Pressed += OnSubmitPressed;
		_formPanel.AddChild(CenterWrap(_submitButton));

		_formPanel.AddChild(UITheme.CreateSpacer(4));

		// Switch mode
		string switchText = isRegister ? "Already have an account? Login" : "Need an account? Register";
		var switchBtn = UITheme.CreateGhostButton(switchText, 12, UITheme.Accent);
		switchBtn.Pressed += () => ShowMode(isRegister ? Mode.Login : Mode.Register);
		_formPanel.AddChild(CenterWrap(switchBtn));

		// Back
		var backBtn = UITheme.CreateGhostButton("← Back", 12);
		backBtn.Pressed += () => ShowMode(Mode.Title);
		_formPanel.AddChild(CenterWrap(backBtn));

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
			_errorLabel.Text = "ApiClient not initialized.";
			ResetSubmitButton();
			return;
		}

		string username = _usernameInput.Text.Trim();
		string password = _passwordInput.Text;

		if (_mode == Mode.Register)
		{
			string email = _emailInput?.Text.Trim() ?? "";
			string confirm = _confirmInput?.Text ?? "";

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
				GoToCharacterSelect();
			else
			{
				_errorLabel.Text = resp.Error;
				ResetSubmitButton();
			}
		}
		else
		{
			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
			{
				_errorLabel.Text = "Username and password required.";
				ResetSubmitButton();
				return;
			}

			var resp = await api.Login(username, password);
			if (resp.Success)
				GoToCharacterSelect();
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
	//  HELPERS
	// ═════════════════════════════════════════════════════════

	private static CenterContainer CenterWrap(Control child)
	{
		var cc = new CenterContainer();
		cc.AddChild(child);
		return cc;
	}
}
