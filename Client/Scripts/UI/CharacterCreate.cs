using Godot;
using System;
using System.Text.Json;

namespace NarutoRP.UI;

/// <summary>
/// Character Creation Wizard - 3 steps: Name → Village → Bio.
/// Saves to Flask backend via ApiClient.
/// </summary>
public partial class CharacterCreate : Control
{
	private int _step = 1;
	private string _charName = "";
	private string _village = "";
	private string _bio = "";

	// UI references
	private VBoxContainer _stepContainer;
	private Label _titleLabel;
	private Label _stepLabel;
	private Label _errorLabel;

	// Step 1
	private LineEdit _nameInput;

	// Step 2
	private string _selectedVillage = "";
	private Button _konohaBtn;
	private Button _sunaBtn;
	private Button _kiriBtn;

	// Step 3
	private TextEdit _bioInput;
	private Label _charCountLabel;

	public override void _Ready()
	{
		BuildUI();
		ShowStep(1);
		GD.Print("[CharacterCreate] Ready.");
	}

	private void BuildUI()
	{
		var bg = new ColorRect();
		bg.Color = new Color("1a1a2e");
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var mainVbox = new VBoxContainer();
		mainVbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		mainVbox.AddThemeConstantOverride("separation", 10);
		mainVbox.OffsetLeft = 60;
		mainVbox.OffsetRight = -60;
		mainVbox.OffsetTop = 40;
		mainVbox.OffsetBottom = -40;
		AddChild(mainVbox);

		_titleLabel = CreateLabel("CHARACTER CREATION", 32, "E87722");
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_titleLabel);

		_stepLabel = CreateLabel("Step 1 of 3", 14, "888888");
		_stepLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_stepLabel);

		// Separator
		var sep = new HSeparator();
		mainVbox.AddChild(sep);

		// Error label
		_errorLabel = CreateLabel("", 14, "FF4444");
		_errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_errorLabel);

		// Step content container
		var center = new CenterContainer();
		center.SizeFlagsVertical = SizeFlags.ExpandFill;
		mainVbox.AddChild(center);

		_stepContainer = new VBoxContainer();
		_stepContainer.CustomMinimumSize = new Vector2(500, 300);
		_stepContainer.AddThemeConstantOverride("separation", 12);
		center.AddChild(_stepContainer);
	}

	// ═════════════════════════════════════════════════════════
	//  STEP MANAGEMENT
	// ═════════════════════════════════════════════════════════

	private void ShowStep(int step)
	{
		_step = step;
		_stepLabel.Text = $"Step {step} of 3";
		_errorLabel.Text = "";

		// Clear step container
		foreach (var child in _stepContainer.GetChildren())
			child.QueueFree();

		switch (step)
		{
			case 1: BuildStep1(); break;
			case 2: BuildStep2(); break;
			case 3: BuildStep3(); break;
		}
	}

	// ─── STEP 1: NAME ────────────────────────────────────────

	private void BuildStep1()
	{
		var header = CreateLabel("Choose Your Name", 22, "FFFFFF");
		header.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(header);

		var desc = CreateLabel("This will be your ninja's name. Must be unique.", 14, "888888");
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 20);
		_stepContainer.AddChild(spacer);

		_nameInput = new LineEdit();
		_nameInput.PlaceholderText = "Enter character name...";
		_nameInput.Text = _charName;
		_nameInput.MaxLength = 30;
		_nameInput.CustomMinimumSize = new Vector2(400, 40);
		_nameInput.AddThemeFontSizeOverride("font_size", 18);
		StyleInput(_nameInput);
		_stepContainer.AddChild(_nameInput);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		_stepContainer.AddChild(btnRow);

		var backBtn = CreateStyledButton("← BACK", true);
		backBtn.Pressed += () =>
		{
			var gm = Core.GameManager.Instance;
			gm?.ChangeScene(Core.GameManager.Scenes.CharSelect);
		};
		btnRow.AddChild(backBtn);

		var nextBtn = CreateStyledButton("NEXT →");
		nextBtn.Pressed += () =>
		{
			_charName = _nameInput.Text.Trim();
			if (_charName.Length < 2 || _charName.Length > 30)
			{
				_errorLabel.Text = "Name must be 2-30 characters.";
				return;
			}
			ShowStep(2);
		};
		btnRow.AddChild(nextBtn);

		_nameInput.CallDeferred("grab_focus");
	}

	// ─── STEP 2: VILLAGE ─────────────────────────────────────

	private void BuildStep2()
	{
		var header = CreateLabel("Choose Your Village", 22, "FFFFFF");
		header.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(header);

		var desc = CreateLabel("This is where your ninja journey begins.", 14, "888888");
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 10);
		_stepContainer.AddChild(spacer);

		_konohaBtn = CreateVillageButton("Konohagakure", "Village Hidden in the Leaves");
		_konohaBtn.Pressed += () => SelectVillage("Konohagakure", _konohaBtn);
		_stepContainer.AddChild(_konohaBtn);

		_sunaBtn = CreateVillageButton("Sunagakure", "Village Hidden in the Sand");
		_sunaBtn.Pressed += () => SelectVillage("Sunagakure", _sunaBtn);
		_stepContainer.AddChild(_sunaBtn);

		_kiriBtn = CreateVillageButton("Kirigakure", "Village Hidden in the Mist");
		_kiriBtn.Pressed += () => SelectVillage("Kirigakure", _kiriBtn);
		_stepContainer.AddChild(_kiriBtn);

		// Restore selection
		if (_selectedVillage == "Konohagakure") HighlightVillage(_konohaBtn);
		else if (_selectedVillage == "Sunagakure") HighlightVillage(_sunaBtn);
		else if (_selectedVillage == "Kirigakure") HighlightVillage(_kiriBtn);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		_stepContainer.AddChild(btnRow);

		var backBtn = CreateStyledButton("← BACK", true);
		backBtn.Pressed += () => ShowStep(1);
		btnRow.AddChild(backBtn);

		var nextBtn = CreateStyledButton("NEXT →");
		nextBtn.Pressed += () =>
		{
			if (string.IsNullOrEmpty(_selectedVillage))
			{
				_errorLabel.Text = "Please select a village.";
				return;
			}
			_village = _selectedVillage;
			ShowStep(3);
		};
		btnRow.AddChild(nextBtn);
	}

	private void SelectVillage(string village, Button btn)
	{
		_selectedVillage = village;
		// Reset all
		ResetVillageButton(_konohaBtn);
		ResetVillageButton(_sunaBtn);
		ResetVillageButton(_kiriBtn);
		// Highlight selected
		HighlightVillage(btn);
	}

	private void HighlightVillage(Button btn)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color("2a2a3e");
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(12);
		style.BorderColor = new Color("E87722");
		style.BorderWidthBottom = 2;
		style.BorderWidthLeft = 2;
		style.BorderWidthRight = 2;
		style.BorderWidthTop = 2;
		btn.AddThemeStyleboxOverride("normal", style);
	}

	private void ResetVillageButton(Button btn)
	{
		if (btn == null) return;
		var style = new StyleBoxFlat();
		style.BgColor = new Color("2a2a3e");
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(12);
		style.BorderColor = new Color("444455");
		style.BorderWidthBottom = 1;
		style.BorderWidthLeft = 1;
		style.BorderWidthRight = 1;
		style.BorderWidthTop = 1;
		btn.AddThemeStyleboxOverride("normal", style);
	}

	private Button CreateVillageButton(string name, string subtitle)
	{
		var btn = new Button();
		btn.Text = $"{name}\n{subtitle}";
		btn.CustomMinimumSize = new Vector2(400, 50);
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", Colors.White);
		ResetVillageButton(btn);
		return btn;
	}

	// ─── STEP 3: BIO + CREATE ────────────────────────────────

	private void BuildStep3()
	{
		var header = CreateLabel("Character Background", 22, "FFFFFF");
		header.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(header);

		var desc = CreateLabel("Write a brief backstory for your ninja. (Optional)", 14, "888888");
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		_bioInput = new TextEdit();
		_bioInput.PlaceholderText = "A young ninja from the village...";
		_bioInput.Text = _bio;
		_bioInput.CustomMinimumSize = new Vector2(500, 120);
		_bioInput.WrapMode = TextEdit.LineWrappingMode.Boundary;
		_bioInput.AddThemeFontSizeOverride("font_size", 14);

		var bioStyle = new StyleBoxFlat();
		bioStyle.BgColor = new Color("2a2a3e");
		bioStyle.SetCornerRadiusAll(6);
		bioStyle.SetContentMarginAll(8);
		bioStyle.BorderColor = new Color("555566");
		bioStyle.BorderWidthBottom = 2;
		bioStyle.BorderWidthLeft = 2;
		bioStyle.BorderWidthRight = 2;
		bioStyle.BorderWidthTop = 2;
		_bioInput.AddThemeStyleboxOverride("normal", bioStyle);
		_bioInput.AddThemeColorOverride("font_color", Colors.White);
		_stepContainer.AddChild(_bioInput);

		_charCountLabel = CreateLabel($"{_bio.Length} / 500", 12, "888888");
		_charCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_stepContainer.AddChild(_charCountLabel);

		_bioInput.TextChanged += () =>
		{
			if (_bioInput.Text.Length > 500)
				_bioInput.Text = _bioInput.Text.Substring(0, 500);
			_charCountLabel.Text = $"{_bioInput.Text.Length} / 500";
		};

		// Summary
		var sep = new HSeparator();
		_stepContainer.AddChild(sep);

		var summary = CreateLabel($"— SUMMARY —\nName: {_charName}\nVillage: {_village}\nStarting Rank: Academy Student", 14, "AAAAAA");
		_stepContainer.AddChild(summary);

		// Buttons
		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		_stepContainer.AddChild(btnRow);

		var backBtn = CreateStyledButton("← BACK", true);
		backBtn.Pressed += () =>
		{
			_bio = _bioInput.Text;
			ShowStep(2);
		};
		btnRow.AddChild(backBtn);

		var createBtn = CreateStyledButton("CREATE CHARACTER");
		createBtn.Pressed += () => OnCreatePressed(createBtn);
		btnRow.AddChild(createBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  CREATE CHARACTER (API)
	// ═════════════════════════════════════════════════════════

	private async void OnCreatePressed(Button btn)
	{
		_errorLabel.Text = "";
		btn.Disabled = true;
		btn.Text = "Creating...";

		_bio = _bioInput?.Text ?? "";

		var api = Networking.ApiClient.Instance;
		var gm = Core.GameManager.Instance;

		if (api == null || !api.IsLoggedIn)
		{
			_errorLabel.Text = "Not logged in.";
			btn.Disabled = false;
			btn.Text = "CREATE CHARACTER";
			return;
		}

		int slot = 1;
		if (gm != null && int.TryParse(gm.PendingSlot, out int parsed))
			slot = parsed;

		var resp = await api.CreateCharacter(_charName, _village, _bio, slot);

		if (resp.Success)
		{
			GD.Print($"[CharacterCreate] Created {_charName} in slot {slot}");

			// Parse response and load into game
			using var doc = JsonDocument.Parse(resp.Body);
			var c = doc.RootElement.GetProperty("character");

			var data = new Core.PlayerData
			{
				CharacterName = c.GetProperty("name").GetString(),
				ClanName = c.GetProperty("clan").GetString(),
				Village = c.GetProperty("village").GetString(),
				Allegiance = c.GetProperty("allegiance").GetString(),
				RpRank = c.GetProperty("rp_rank").GetString(),
				Bio = c.GetProperty("bio").GetString(),
				Strength = c.GetProperty("strength").GetInt32(),
				Speed = c.GetProperty("speed").GetInt32(),
				Agility = c.GetProperty("agility").GetInt32(),
				Endurance = c.GetProperty("endurance").GetInt32(),
				Stamina = c.GetProperty("stamina").GetInt32(),
				ChakraControl = c.GetProperty("chakra_control").GetInt32(),
				DailyPointsRemaining = c.GetProperty("daily_points_remaining").GetInt32(),
				CurrentHp = c.GetProperty("current_hp").GetInt32(),
				CurrentChakra = c.GetProperty("current_chakra").GetInt32(),
			};

			string charId = c.GetProperty("id").GetString();
			gm.LoadCharacter(data, charId);
			gm.SetState(Core.GameManager.GameState.InWorld);
			gm.ChangeScene(Core.GameManager.Scenes.Overworld);
		}
		else
		{
			_errorLabel.Text = resp.Error;
			btn.Disabled = false;
			btn.Text = "CREATE CHARACTER";
		}
	}

	// ═════════════════════════════════════════════════════════
	//  UI HELPERS
	// ═════════════════════════════════════════════════════════

	private void StyleInput(LineEdit input)
	{
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
	}

	private Label CreateLabel(string text, int size, string color)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeColorOverride("font_color", new Color(color));
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	private Button CreateStyledButton(string text, bool secondary = false)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(180, 42);

		var styleNormal = new StyleBoxFlat();
		styleNormal.BgColor = secondary ? new Color("333344") : new Color("E87722");
		styleNormal.SetCornerRadiusAll(6);
		styleNormal.SetContentMarginAll(10);
		btn.AddThemeStyleboxOverride("normal", styleNormal);

		var styleHover = new StyleBoxFlat();
		styleHover.BgColor = secondary ? new Color("444455") : new Color("FF8833");
		styleHover.SetCornerRadiusAll(6);
		styleHover.SetContentMarginAll(10);
		btn.AddThemeStyleboxOverride("hover", styleHover);

		var stylePressed = new StyleBoxFlat();
		stylePressed.BgColor = secondary ? new Color("222233") : new Color("CC6611");
		stylePressed.SetCornerRadiusAll(6);
		stylePressed.SetContentMarginAll(10);
		btn.AddThemeStyleboxOverride("pressed", stylePressed);

		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeFontSizeOverride("font_size", 16);

		return btn;
	}
}
