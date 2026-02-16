using Godot;
using System.Text.Json;

namespace ProjectTactics.UI;

/// <summary>
/// Character Select Screen — loads character slots from Flask API.
/// Visual style matches HUD v4 mockup.
/// </summary>
public partial class CharacterSelect : Control
{
	private const int MaxSlots = 3;
	private VBoxContainer _slotsContainer;
	private Label _welcomeLabel;
	private Label _loadingLabel;

	public override void _Ready()
	{
		BuildUI();
		LoadCharacters();
		GD.Print("[CharacterSelect] Ready.");
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

		var title = UITheme.CreateTitle("CHARACTER SELECT", 28);
		title.AddThemeColorOverride("font_color", UITheme.AccentOrange);
		mainVbox.AddChild(title);

		// Welcome message
		var api = Networking.ApiClient.Instance;
		string name = api != null ? api.Username : "Traveler";
		_welcomeLabel = UITheme.CreateDim($"Welcome back, {name}", 13);
		_welcomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_welcomeLabel);

		mainVbox.AddChild(UITheme.CreateSpacer(12));

		// Loading
		_loadingLabel = UITheme.CreateBody("Loading characters...", 14, UITheme.TextDim);
		_loadingLabel.HorizontalAlignment = HorizontalAlignment.Center;
		mainVbox.AddChild(_loadingLabel);

		// Slots container (centered)
		var centerContainer = new CenterContainer();
		centerContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		mainVbox.AddChild(centerContainer);

		_slotsContainer = new VBoxContainer();
		_slotsContainer.CustomMinimumSize = new Vector2(520, 0);
		_slotsContainer.AddThemeConstantOverride("separation", 12);
		centerContainer.AddChild(_slotsContainer);

		// Bottom bar
		var bottomBar = new HBoxContainer();
		bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
		mainVbox.AddChild(bottomBar);

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

		var resp = await api.GetCharacters();

		_loadingLabel.Visible = false;

		if (!resp.Success)
		{
			_loadingLabel.Visible = true;
			_loadingLabel.Text = $"Error: {resp.Error}";
			return;
		}

		using var doc = JsonDocument.Parse(resp.Body);
		var slots = doc.RootElement.GetProperty("slots");

		for (int i = 1; i <= MaxSlots; i++)
		{
			string key = i.ToString();
			var slotData = slots.GetProperty(key);

			if (slotData.ValueKind == JsonValueKind.Null)
				_slotsContainer.AddChild(CreateEmptySlot(i));
			else
				_slotsContainer.AddChild(CreateOccupiedSlot(i, slotData));
		}
	}

	// ═════════════════════════════════════════════════════════
	//  SLOT UI
	// ═════════════════════════════════════════════════════════

	private PanelContainer CreateOccupiedSlot(int slotNum, JsonElement data)
	{
		string charId = data.GetProperty("id").GetString();
		string charName = data.GetProperty("name").GetString();
		string city = data.GetProperty("city").GetString();
		string rank = data.GetProperty("rp_rank").GetString();
		int level = data.GetProperty("character_level").GetInt32();
		int str = data.GetProperty("strength").GetInt32();
		int spd = data.GetProperty("speed").GetInt32();
		int agi = data.GetProperty("agility").GetInt32();
		int end = data.GetProperty("endurance").GetInt32();
		int sta = data.GetProperty("stamina").GetInt32();
		int etc = data.GetProperty("ether_control").GetInt32();

		var panel = UITheme.CreatePanel(borderColor: UITheme.Border);
		panel.CustomMinimumSize = new Vector2(520, 90);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(hbox);

		// Character info
		var infoVbox = new VBoxContainer();
		infoVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		infoVbox.AddThemeConstantOverride("separation", 3);
		hbox.AddChild(infoVbox);

		// Name row
		var nameLabel = UITheme.CreateTitle($"{charName}", 18);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
		infoVbox.AddChild(nameLabel);

		// Rank / City row
		var metaLabel = UITheme.CreateBody($"Lv.{level}  ·  {rank}  ·  {city}", 12, UITheme.TextDim);
		infoVbox.AddChild(metaLabel);

		// Stats row
		var statsText = $"STR {str}   SPD {spd}   AGI {agi}   END {end}   STA {sta}   ETH {etc}";
		var statsLabel = UITheme.CreateNumbers(statsText, 11, UITheme.TextDim);
		infoVbox.AddChild(statsLabel);

		// Buttons column
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
		var resp = await api.GetCharacter(charId);

		if (!resp.Success)
		{
			GD.PrintErr($"[CharacterSelect] Failed to load: {resp.Error}");
			return;
		}

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
			Speed = c.GetProperty("speed").GetInt32(),
			Agility = c.GetProperty("agility").GetInt32(),
			Endurance = c.GetProperty("endurance").GetInt32(),
			Stamina = c.GetProperty("stamina").GetInt32(),
			EtherControl = c.GetProperty("ether_control").GetInt32(),
			DailyPointsRemaining = c.GetProperty("daily_points_remaining").GetInt32(),
			LastTrainingDate = c.GetProperty("last_training_date").GetString(),
			CurrentHp = c.GetProperty("current_hp").GetInt32(),
			CurrentEther = c.GetProperty("current_ether").GetInt32(),
		};

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
