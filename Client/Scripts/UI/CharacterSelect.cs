using Godot;
using System.Text.Json;

namespace ProjectTactics.UI;

/// <summary>
/// Character Select Screen - Loads character slots from Flask API.
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

        var title = CreateLabel("CHARACTER SELECT", 32, "E87722");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(title);

        // Welcome message with username
        var api = Networking.ApiClient.Instance;
        string name = api != null ? api.Username : "Traveler";
        _welcomeLabel = CreateLabel($"Welcome back, {name}", 14, "888888");
        _welcomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(_welcomeLabel);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 20);
        mainVbox.AddChild(spacer);

        // Loading label
        _loadingLabel = CreateLabel("Loading characters...", 16, "888888");
        _loadingLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(_loadingLabel);

        // Slots container
        var centerContainer = new CenterContainer();
        centerContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(centerContainer);

        _slotsContainer = new VBoxContainer();
        _slotsContainer.CustomMinimumSize = new Vector2(500, 0);
        _slotsContainer.AddThemeConstantOverride("separation", 16);
        centerContainer.AddChild(_slotsContainer);

        // Bottom bar
        var bottomBar = new HBoxContainer();
        bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
        bottomBar.AddThemeConstantOverride("separation", 20);
        mainVbox.AddChild(bottomBar);

        var logoutBtn = CreateStyledButton("← LOGOUT", true);
        logoutBtn.Pressed += OnLogoutPressed;
        bottomBar.AddChild(logoutBtn);
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

        // Parse slot data
        using var doc = JsonDocument.Parse(resp.Body);
        var slots = doc.RootElement.GetProperty("slots");

        for (int i = 1; i <= MaxSlots; i++)
        {
            string key = i.ToString();
            var slotData = slots.GetProperty(key);

            if (slotData.ValueKind == JsonValueKind.Null)
            {
                _slotsContainer.AddChild(CreateEmptySlot(i));
            }
            else
            {
                _slotsContainer.AddChild(CreateOccupiedSlot(i, slotData));
            }
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

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(500, 100);
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color("2a2a3e");
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetContentMarginAll(16);
        panelStyle.BorderColor = new Color("E87722");
        panelStyle.BorderWidthBottom = 2;
        panelStyle.BorderWidthLeft = 2;
        panelStyle.BorderWidthRight = 2;
        panelStyle.BorderWidthTop = 2;
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hbox);

        // Character info
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        infoVbox.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(infoVbox);

        infoVbox.AddChild(CreateLabel($"Slot {slotNum}: {charName}", 20, "FFFFFF"));
        infoVbox.AddChild(CreateLabel($"Lv.{level}  |  {rank}  |  {city}", 14, "AAAAAA"));
        infoVbox.AddChild(CreateLabel($"STR:{str}  SPD:{spd}  AGI:{agi}  END:{end}  STA:{sta}  ETC:{etc}", 12, "888888"));

        // Buttons
        var btnVbox = new VBoxContainer();
        btnVbox.AddThemeConstantOverride("separation", 6);
        hbox.AddChild(btnVbox);

        var playBtn = CreateStyledButton("PLAY");
        playBtn.CustomMinimumSize = new Vector2(120, 35);
        playBtn.Pressed += () => OnPlayCharacter(charId, charName);
        btnVbox.AddChild(playBtn);

        var deleteBtn = CreateStyledButton("DELETE", true);
        deleteBtn.CustomMinimumSize = new Vector2(120, 35);
        deleteBtn.Pressed += () => OnDeleteCharacter(charId, charName);
        btnVbox.AddChild(deleteBtn);

        return panel;
    }

    private PanelContainer CreateEmptySlot(int slotNum)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(500, 100);
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color("222233");
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetContentMarginAll(16);
        panelStyle.BorderColor = new Color("444455");
        panelStyle.BorderWidthBottom = 1;
        panelStyle.BorderWidthLeft = 1;
        panelStyle.BorderWidthRight = 1;
        panelStyle.BorderWidthTop = 1;
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hbox);

        var emptyLabel = CreateLabel($"Slot {slotNum}: Empty", 20, "666666");
        emptyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        emptyLabel.VerticalAlignment = VerticalAlignment.Center;
        hbox.AddChild(emptyLabel);

        var createBtn = CreateStyledButton("CREATE NEW");
        createBtn.CustomMinimumSize = new Vector2(160, 45);
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

        // Parse into PlayerData
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
        // Simple confirmation via dialog
        var dialog = new ConfirmationDialog();
        dialog.DialogText = $"Delete {charName}? This cannot be undone!";
        dialog.Title = "Delete Character";
        dialog.Confirmed += async () =>
        {
            var api = Networking.ApiClient.Instance;
            var resp = await api.DeleteCharacter(charId);
            if (resp.Success)
            {
                GD.Print($"[CharacterSelect] Deleted {charName}");
                // Reload scene
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

    private Button CreateStyledButton(string text, bool secondary = false)
    {
        var btn = new Button();
        btn.Text = text;

        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = secondary ? new Color("333344") : new Color("E87722");
        styleNormal.SetCornerRadiusAll(6);
        styleNormal.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("normal", styleNormal);

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = secondary ? new Color("444455") : new Color("FF8833");
        styleHover.SetCornerRadiusAll(6);
        styleHover.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("hover", styleHover);

        var stylePressed = new StyleBoxFlat();
        stylePressed.BgColor = secondary ? new Color("222233") : new Color("CC6611");
        stylePressed.SetCornerRadiusAll(6);
        stylePressed.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("pressed", stylePressed);

        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeFontSizeOverride("font_size", 16);

        return btn;
    }
}
