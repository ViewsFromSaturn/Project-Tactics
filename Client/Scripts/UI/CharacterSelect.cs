using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace ProjectTactics.UI;

/// <summary>
/// Character Select Screen — loads character slots from Flask API.
/// Supports "Continue" quick-play for returning players.
/// Uses combined resume data when available to avoid extra API calls.
/// </summary>
public partial class CharacterSelect : Control
{
	private const int MaxSlots = 3;
	private VBoxContainer _mainVbox;
	private VBoxContainer _slotsContainer;
	private PanelContainer _continuePanel;
	private Label _welcomeLabel;
	private Label _loadingLabel;

	// Cache full character data from resume response
	private readonly Dictionary<string, JsonElement> _cachedCharacters = new();

	public override void _Ready()
	{
		BuildUI();
		LoadCharacters();
		GD.Print("[CharacterSelect] Ready.");
	}

	private void BuildUI()
	{
		AddChild(UITheme.CreateBackground());

		_mainVbox = new VBoxContainer();
		_mainVbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_mainVbox.AddThemeConstantOverride("separation", 6);
		_mainVbox.OffsetLeft = 60;
		_mainVbox.OffsetRight = -60;
		_mainVbox.OffsetTop = 40;
		_mainVbox.OffsetBottom = -40;
		AddChild(_mainVbox);

		var title = UITheme.CreateTitle("CHARACTER SELECT", 28);
		title.AddThemeColorOverride("font_color", UITheme.AccentOrange);
		_mainVbox.AddChild(title);

		// Welcome message
		var api = Networking.ApiClient.Instance;
		string name = api != null ? api.Username : "Traveler";
		_welcomeLabel = UITheme.CreateDim($"Welcome back, {name}", 13);
		_welcomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_mainVbox.AddChild(_welcomeLabel);

		_mainVbox.AddChild(UITheme.CreateSpacer(4));

		// Continue panel placeholder (built dynamically after loading)
		_continuePanel = new PanelContainer();
		_continuePanel.Visible = false;
		_mainVbox.AddChild(_continuePanel);

		_mainVbox.AddChild(UITheme.CreateSpacer(4));

		// Loading
		_loadingLabel = UITheme.CreateBody("Loading characters...", 14, UITheme.TextDim);
		_loadingLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_mainVbox.AddChild(_loadingLabel);

		// Slots container (centered)
		var centerContainer = new CenterContainer();
		centerContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		_mainVbox.AddChild(centerContainer);

		_slotsContainer = new VBoxContainer();
		_slotsContainer.CustomMinimumSize = new Vector2(520, 0);
		_slotsContainer.AddThemeConstantOverride("separation", 12);
		centerContainer.AddChild(_slotsContainer);

		// Bottom bar
		var bottomBar = new HBoxContainer();
		bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
		_mainVbox.AddChild(bottomBar);

		var logoutBtn = UITheme.CreateGhostButton("← Logout", 13);
		logoutBtn.Pressed += OnLogoutPressed;
		bottomBar.AddChild(logoutBtn);

		// Version
		AddChild(UITheme.CreateVersionLabel());
	}

	// ═════════════════════════════════════════════════════════
	//  LOAD FROM API
	// ═════════════════════════════════════════════════════════

	private async void LoadCharacters()
	{
		var api = Networking.ApiClient.Instance;
		if (api == null || !api.IsLoggedIn)
		{
			_loadingLabel.Text = "Not logged in.";
			return;
		}

		// Resume gives us account + slots + full character data in one call
		var resp = await api.Resume();

		_loadingLabel.Visible = false;

		if (!resp.Success)
		{
			_loadingLabel.Visible = true;
			_loadingLabel.Text = $"Error: {resp.Error}";
			return;
		}

		try
		{
			using var doc = JsonDocument.Parse(resp.Body);
			var root = doc.RootElement;
			var slots = root.GetProperty("slots");

			// Cache full character data
			if (root.TryGetProperty("characters", out var chars))
			{
				foreach (var prop in chars.EnumerateObject())
					_cachedCharacters[prop.Name] = prop.Value.Clone();
			}

			// Find last played character for Continue button
			string lastCharId = api.GetLastCharacterId();

			for (int i = 1; i <= MaxSlots; i++)
			{
				string key = i.ToString();
				if (!slots.TryGetProperty(key, out var slotData) || slotData.ValueKind == JsonValueKind.Null)
				{
					_slotsContainer.AddChild(CreateEmptySlot(i));
				}
				else
				{
					string charId = slotData.GetProperty("id").GetString();
					string charName = slotData.GetProperty("name").GetString();
					bool isLast = charId == lastCharId;

					_slotsContainer.AddChild(CreateOccupiedSlot(i, slotData, isLast));

					if (isLast)
						BuildContinuePanel(charId, charName, slotData);
				}
			}
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[CharacterSelect] Error loading characters: {ex.Message}");
			_loadingLabel.Visible = true;
			_loadingLabel.Text = "Error loading characters.";
			// Still show empty slots
			for (int i = 1; i <= MaxSlots; i++)
				_slotsContainer.AddChild(CreateEmptySlot(i));
		}
	}

	// ═════════════════════════════════════════════════════════
	//  CONTINUE PANEL (quick-play for returning players)
	// ═════════════════════════════════════════════════════════

	private void BuildContinuePanel(string charId, string charName, JsonElement data)
	{
		_continuePanel.Visible = true;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.047f, 0.047f, 0.078f, 0.85f);
		style.SetCornerRadiusAll(8);
		style.BorderColor = UITheme.Accent;
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 20; style.ContentMarginRight = 20;
		style.ContentMarginTop = 12; style.ContentMarginBottom = 12;
		_continuePanel.AddThemeStyleboxOverride("panel", style);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		_continuePanel.AddChild(hbox);

		// Info
		var infoVbox = new VBoxContainer();
		infoVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		infoVbox.AddThemeConstantOverride("separation", 2);
		hbox.AddChild(infoVbox);

		var label = UITheme.CreateDim("LAST PLAYED", 10);
		infoVbox.AddChild(label);

		string rank = data.GetProperty("rp_rank").GetString();
		string city = data.GetProperty("city").GetString();
		int level = data.GetProperty("character_level").GetInt32();

		var nameLabel = UITheme.CreateTitle(charName, 18);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		infoVbox.AddChild(nameLabel);

		var metaLabel = UITheme.CreateBody($"Lv.{level}  ·  {rank}  ·  {city}", 12, UITheme.TextDim);
		infoVbox.AddChild(metaLabel);

		// Big continue button
		var continueBtn = UITheme.CreatePrimaryButton("CONTINUE", 15);
		continueBtn.CustomMinimumSize = new Vector2(160, 46);
		continueBtn.Pressed += () => OnPlayCharacter(charId, charName);
		hbox.AddChild(continueBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  SLOT UI
	// ═════════════════════════════════════════════════════════

	private PanelContainer CreateOccupiedSlot(int slotNum, JsonElement data, bool isLastPlayed)
	{
		string charId = data.GetProperty("id").GetString();
		string charName = data.GetProperty("name").GetString();
		string city = data.GetProperty("city").GetString();
		string rank = data.GetProperty("rp_rank").GetString();
		int level = data.GetProperty("character_level").GetInt32();
		int str = data.GetProperty("strength").GetInt32();
		int vit = data.GetProperty("vitality").GetInt32();
		int dex = data.GetProperty("dexterity").GetInt32();
		int agi = data.GetProperty("agility").GetInt32();
		int etc = data.GetProperty("ether_control").GetInt32();
		int mnd = data.GetProperty("mind").GetInt32();

		var borderColor = isLastPlayed ? UITheme.Accent : UITheme.Border;
		var panel = UITheme.CreatePanel(borderColor: borderColor);
		panel.CustomMinimumSize = new Vector2(520, 90);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(hbox);

		var infoVbox = new VBoxContainer();
		infoVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		infoVbox.AddThemeConstantOverride("separation", 3);
		hbox.AddChild(infoVbox);

		var nameLabel = UITheme.CreateTitle($"{charName}", 18);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		infoVbox.AddChild(nameLabel);

		var metaLabel = UITheme.CreateBody($"Lv.{level}  ·  {rank}  ·  {city}", 12, UITheme.TextDim);
		infoVbox.AddChild(metaLabel);

		var statsText = $"STR {str}   VIT {vit}   DEX {dex}   AGI {agi}   ETC {etc}   MND {mnd}";
		var statsLabel = UITheme.CreateNumbers(statsText, 11, UITheme.TextDim);
		infoVbox.AddChild(statsLabel);

		var btnVbox = new VBoxContainer();
		btnVbox.AddThemeConstantOverride("separation", 6);
		hbox.AddChild(btnVbox);

		var playBtn = UITheme.CreatePrimaryButton("PLAY", 13);
		playBtn.CustomMinimumSize = new Vector2(110, 34);
		playBtn.Pressed += () => OnPlayCharacter(charId, charName);
		btnVbox.AddChild(playBtn);

		var deleteBtn = UITheme.CreateGhostButton("Delete", 11, UITheme.Error);
		deleteBtn.CustomMinimumSize = new Vector2(110, 28);
		deleteBtn.Pressed += () => OnDeleteCharacter(charId, charName);
		btnVbox.AddChild(deleteBtn);

		return panel;
	}

	private PanelContainer CreateEmptySlot(int slotNum)
	{
		var panel = UITheme.CreatePanel(new Color("0a0a12cc"), UITheme.BorderLight);
		panel.CustomMinimumSize = new Vector2(520, 90);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(hbox);

		var emptyLabel = UITheme.CreateBody($"Slot {slotNum} — Empty", 16, UITheme.TextDim);
		emptyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		emptyLabel.VerticalAlignment = VerticalAlignment.Center;
		hbox.AddChild(emptyLabel);

		var createBtn = UITheme.CreateSecondaryButton("CREATE NEW", 13);
		createBtn.CustomMinimumSize = new Vector2(140, 40);
		createBtn.Pressed += () => OnCreateNew(slotNum);
		hbox.AddChild(createBtn);

		return panel;
	}

	// ═════════════════════════════════════════════════════════
	//  BUTTON HANDLERS
	// ═════════════════════════════════════════════════════════

	private async void OnPlayCharacter(string charId, string charName)
	{
		GD.Print($"[CharacterSelect] Loading {charName}...");

		var api = Networking.ApiClient.Instance;

		// Use cached data from resume if available, otherwise fetch
		JsonElement c;
		if (_cachedCharacters.ContainsKey(charId))
		{
			c = _cachedCharacters[charId];
			GD.Print($"[CharacterSelect] Using cached data for {charName}");
		}
		else
		{
			var resp = await api.GetCharacter(charId);
			if (!resp.Success)
			{
				GD.PrintErr($"[CharacterSelect] Failed to load: {resp.Error}");
				return;
			}
			using var doc = JsonDocument.Parse(resp.Body);
			c = doc.RootElement.GetProperty("character").Clone();
		}

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
			LastResetDate = c.GetProperty("last_reset_date").GetString(),
			CurrentHp = c.GetProperty("current_hp").GetInt32(),
			CurrentAether = c.GetProperty("current_aether").GetInt32(),
		};

		// Save this as last played character
		api.SaveSession(charId);

		var gm = Core.GameManager.Instance;
		gm.LoadCharacter(data, charId);
		gm.SetState(Core.GameManager.GameState.InWorld);
		gm.ChangeScene(Core.GameManager.Scenes.Overworld);
	}

	private void OnCreateNew(int slotNum)
	{
		GD.Print($"[CharacterSelect] Creating in slot {slotNum}");
		var gm = Core.GameManager.Instance;
		gm.PendingSlot = slotNum.ToString();
		gm.SetState(Core.GameManager.GameState.CharacterCreate);
		gm.ChangeScene(Core.GameManager.Scenes.CharCreate);
	}

	private async void OnDeleteCharacter(string charId, string charName)
	{
		var dialog = new ConfirmationDialog();
		dialog.DialogText = $"Delete {charName}? This cannot be undone.";
		dialog.Title = "Delete Character";
		dialog.Confirmed += async () =>
		{
			var api = Networking.ApiClient.Instance;
			var resp = await api.DeleteCharacter(charId);
			if (resp.Success)
			{
				GD.Print($"[CharacterSelect] Deleted {charName}");
				GetTree().ReloadCurrentScene();
			}
			else
			{
				GD.PrintErr($"[CharacterSelect] Delete failed: {resp.Error}");
			}
		};
		AddChild(dialog);
		dialog.PopupCentered();
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
