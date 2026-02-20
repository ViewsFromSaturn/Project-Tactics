using Godot;
using System;
using System.Text.Json;

namespace ProjectTactics.UI;

/// <summary>
/// Character Creation Wizard — 3 steps: Name → City → Bio.
/// Visual style matches HUD v4 mockup.
/// </summary>
public partial class CharacterCreate : Control
{
	private int _step = 1;
	private string _charName = "";
	private string _city = "";
	private string _bio = "";

	private VBoxContainer _stepContainer;
	private Label _titleLabel;
	private Label _stepLabel;
	private Label _errorLabel;

	// Step 1
	private LineEdit _nameInput;

	// Step 2
	private string _selectedCity = "";
	private PanelContainer _lumerePanel;
	private PanelContainer _praevenPanel;
	private PanelContainer _caldrisPanel;

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
		AddChild(UITheme.CreateBackground());

		var mainVbox = new VBoxContainer();
		mainVbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		mainVbox.AddThemeConstantOverride("separation", 6);
		mainVbox.OffsetLeft = 60;
		mainVbox.OffsetRight = -60;
		mainVbox.OffsetTop = 40;
		mainVbox.OffsetBottom = -40;
		AddChild(mainVbox);

		_titleLabel = UITheme.CreateTitle("CHARACTER CREATION", 28);
		_titleLabel.AddThemeColorOverride("font_color", UITheme.AccentOrange);
		mainVbox.AddChild(_titleLabel);

		// Step indicator
		_stepLabel = UITheme.CreateDim("Step 1 of 3", 12);
		_stepLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_stepLabel);

		mainVbox.AddChild(UITheme.CreateSpacer(4));

		// Error label
		_errorLabel = UITheme.CreateBody("", 13, UITheme.Error);
		_errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_errorLabel);

		// Step content (centered)
		var center = new CenterContainer();
		center.SizeFlagsVertical = SizeFlags.ExpandFill;
		mainVbox.AddChild(center);

		_stepContainer = new VBoxContainer();
		_stepContainer.CustomMinimumSize = new Vector2(500, 300);
		_stepContainer.AddThemeConstantOverride("separation", 10);
		center.AddChild(_stepContainer);

		// Version
		AddChild(UITheme.CreateVersionLabel());
	}

	// ═════════════════════════════════════════════════════════
	//  STEP MANAGEMENT
	// ═════════════════════════════════════════════════════════

	private void ShowStep(int step)
	{
		_step = step;
		_stepLabel.Text = $"Step {step} of 3";
		_errorLabel.Text = "";

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
		var header = UITheme.CreateTitle("Choose Your Name", 20);
		_stepContainer.AddChild(header);

		var desc = UITheme.CreateDim("This will be your character's name in the world.", 13);
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		_stepContainer.AddChild(UITheme.CreateSpacer(16));

		_nameInput = UITheme.CreateInput("Enter character name...", fontSize: 16);
		_nameInput.Text = _charName;
		_nameInput.MaxLength = 30;
		_nameInput.CustomMinimumSize = new Vector2(400, 44);
		_stepContainer.AddChild(_nameInput);

		_stepContainer.AddChild(UITheme.CreateSpacer(12));

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 16);
		_stepContainer.AddChild(btnRow);

		var backBtn = UITheme.CreateGhostButton("← Back", 13);
		backBtn.Pressed += () =>
		{
			var gm = Core.GameManager.Instance;
			gm?.ChangeScene(Core.GameManager.Scenes.CharSelect);
		};
		btnRow.AddChild(backBtn);

		var nextBtn = UITheme.CreatePrimaryButton("NEXT →", 14);
		nextBtn.CustomMinimumSize = new Vector2(140, 42);
		nextBtn.Pressed += () =>
		{
			_charName = _nameInput.Text.Trim();
			if (_charName.Length < 2 || _charName.Length > 30)
			{
				_errorLabel.Text = "Name must be 2–30 characters.";
				return;
			}
			ShowStep(2);
		};
		btnRow.AddChild(nextBtn);

		_nameInput.CallDeferred("grab_focus");
	}

	// ─── STEP 2: CITY ─────────────────────────────────────

	private void BuildStep2()
	{
		var header = UITheme.CreateTitle("Choose Your Starting City", 20);
		_stepContainer.AddChild(header);

		var desc = UITheme.CreateDim("This is where your journey begins.", 13);
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		_stepContainer.AddChild(UITheme.CreateSpacer(8));

		// City cards
		_lumerePanel = CreateCityCard("Lumere", "The City of Light — Capital of the Empire");
		_stepContainer.AddChild(_lumerePanel);

		_praevenPanel = CreateCityCard("Praeven", "The City Before the Fall — Twin Seat of the Empire");
		_stepContainer.AddChild(_praevenPanel);

		_caldrisPanel = CreateCityCard("Caldris", "The Free City — Outside the Empire");
		_stepContainer.AddChild(_caldrisPanel);

		// Restore selection
		if (_selectedCity == "Lumere") UITheme.SetPanelSelected(_lumerePanel, true);
		else if (_selectedCity == "Praeven") UITheme.SetPanelSelected(_praevenPanel, true);
		else if (_selectedCity == "Caldris") UITheme.SetPanelSelected(_caldrisPanel, true);

		_stepContainer.AddChild(UITheme.CreateSpacer(8));

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 16);
		_stepContainer.AddChild(btnRow);

		var backBtn = UITheme.CreateGhostButton("← Back", 13);
		backBtn.Pressed += () => ShowStep(1);
		btnRow.AddChild(backBtn);

		var nextBtn = UITheme.CreatePrimaryButton("NEXT →", 14);
		nextBtn.CustomMinimumSize = new Vector2(140, 42);
		nextBtn.Pressed += () =>
		{
			if (string.IsNullOrEmpty(_selectedCity))
			{
				_errorLabel.Text = "Please select a city.";
				return;
			}
			_city = _selectedCity;
			ShowStep(3);
		};
		btnRow.AddChild(nextBtn);
	}

	private PanelContainer CreateCityCard(string name, string subtitle)
	{
		var panel = UITheme.CreatePanel();
		panel.CustomMinimumSize = new Vector2(480, 0);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		panel.AddChild(vbox);

		var nameLabel = UITheme.CreateTitle(name, 16);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		vbox.AddChild(nameLabel);

		var subLabel = UITheme.CreateDim(subtitle, 12);
		vbox.AddChild(subLabel);

		// Make the whole panel clickable via gui_input
		panel.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				SelectCity(name);
			}
		};
		panel.MouseDefaultCursorShape = CursorShape.PointingHand;

		return panel;
	}

	private void SelectCity(string city)
	{
		_selectedCity = city;
		UITheme.SetPanelSelected(_lumerePanel, city == "Lumere");
		UITheme.SetPanelSelected(_praevenPanel, city == "Praeven");
		UITheme.SetPanelSelected(_caldrisPanel, city == "Caldris");
	}

	// ─── STEP 3: BIO + CREATE ────────────────────────────────

	private void BuildStep3()
	{
		var header = UITheme.CreateTitle("Character Background", 20);
		_stepContainer.AddChild(header);

		var desc = UITheme.CreateDim("Write a brief backstory for your character. (Optional)", 13);
		desc.HorizontalAlignment = HorizontalAlignment.Center;
		_stepContainer.AddChild(desc);

		_stepContainer.AddChild(UITheme.CreateSpacer(6));

		_bioInput = UITheme.CreateTextArea("A traveler arriving in the city...");
		_bioInput.Text = _bio;
		_bioInput.CustomMinimumSize = new Vector2(480, 120);
		_stepContainer.AddChild(_bioInput);

		_charCountLabel = UITheme.CreateNumbers($"{_bio.Length} / 500", 11, UITheme.TextDim);
		_charCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_stepContainer.AddChild(_charCountLabel);

		_bioInput.TextChanged += () =>
		{
			if (_bioInput.Text.Length > 500)
				_bioInput.Text = _bioInput.Text.Substring(0, 500);
			_charCountLabel.Text = $"{_bioInput.Text.Length} / 500";
		};

		// Summary panel
		_stepContainer.AddChild(UITheme.CreateSpacer(4));

		var summaryPanel = UITheme.CreatePanel(new Color("0a0a12cc"));
		summaryPanel.CustomMinimumSize = new Vector2(480, 0);
		_stepContainer.AddChild(summaryPanel);

		var summaryVbox = new VBoxContainer();
		summaryVbox.AddThemeConstantOverride("separation", 3);
		summaryPanel.AddChild(summaryVbox);

		summaryVbox.AddChild(UITheme.CreateDim("SUMMARY", 10));

		var summaryName = UITheme.CreateBody($"Name: {_charName}", 13, UITheme.TextBright);
		summaryVbox.AddChild(summaryName);

		var summaryCity = UITheme.CreateBody($"City: {_city}", 13, UITheme.Text);
		summaryVbox.AddChild(summaryCity);

		var summaryRank = UITheme.CreateBody("Starting Rank: Aspirant", 13, UITheme.TextDim);
		summaryVbox.AddChild(summaryRank);

		// Buttons
		_stepContainer.AddChild(UITheme.CreateSpacer(6));

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 16);
		_stepContainer.AddChild(btnRow);

		var backBtn = UITheme.CreateGhostButton("← Back", 13);
		backBtn.Pressed += () =>
		{
			_bio = _bioInput.Text;
			ShowStep(2);
		};
		btnRow.AddChild(backBtn);

		var createBtn = UITheme.CreatePrimaryButton("CREATE CHARACTER", 14);
		createBtn.CustomMinimumSize = new Vector2(200, 44);
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

		var resp = await api.CreateCharacter(_charName, _city, _bio, slot);

		if (resp.Success)
		{
			GD.Print($"[CharacterCreate] Created {_charName} in slot {slot}");

			using var doc = JsonDocument.Parse(resp.Body);
			var c = doc.RootElement.GetProperty("character");

			var data = new Core.PlayerData
			{
				CharacterName = c.GetProperty("name").GetString(),
				RaceName = c.GetProperty("race").GetString(),
				City = c.GetProperty("city").GetString(),
				Allegiance = c.GetProperty("allegiance").GetString(),
				RpRank = c.GetProperty("rp_rank").GetString(),
				Bio = c.GetProperty("bio").GetString(),
				Strength = c.GetProperty("strength").GetInt32(),
				Vitality = c.GetProperty("vitality").GetInt32(),
				Agility = c.GetProperty("agility").GetInt32(),
				Dexterity = c.GetProperty("dexterity").GetInt32(),
				Mind = c.GetProperty("mind").GetInt32(),
				EtherControl = c.GetProperty("ether_control").GetInt32(),
				TrainingPointsBank = c.GetProperty("training_points_bank").GetInt32(),
				CurrentHp = c.GetProperty("current_hp").GetInt32(),
				CurrentAether = c.GetProperty("current_aether").GetInt32(),
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
}
