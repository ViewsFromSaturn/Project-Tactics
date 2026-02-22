using Godot;
using System;
using ProjectTactics.UI.Panels;

namespace ProjectTactics.Core;

/// <summary>
/// Singleton game manager. Handles state, scene transitions, and active character.
/// Autoload: Project Settings → Autoload → GameManager.cs
/// </summary>
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// ─── GAME STATES ────────────────────────────────────────
	public enum GameState { Login, CharacterSelect, CharacterCreate, InWorld, InCombat }
	public GameState CurrentState { get; private set; } = GameState.Login;

	// ─── SCENE PATHS ────────────────────────────────────────
	public static class Scenes
	{
		public static string Title = "res://Scenes/Login/TitleScreen.tscn";
		public static string CharSelect = "res://Scenes/Login/CharacterSelect.tscn";
		public static string CharCreate = "res://Scenes/Login/CharacterCreate.tscn";
		public static string Overworld = "res://Scenes/World/Overworld.tscn";
		public static string Battle = "res://Scenes/Combat/BattleScene.tscn";
	}

	// ─── ACTIVE CHARACTER ───────────────────────────────────
	public PlayerData ActiveCharacter { get; private set; }
	public string ActiveCharacterId { get; private set; } = "";
	public string PendingSlot { get; set; } = "1";

	// ─── SHARED LOADOUT (skills, spells, equipment, inventory) ──
	public CharacterLoadout ActiveLoadout { get; private set; }

	public override void _Ready()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
		GD.Print("[GameManager] Initialized.");
	}

	// ═════════════════════════════════════════════════════════
	//  STATE MANAGEMENT
	// ═════════════════════════════════════════════════════════

	public void SetState(GameState newState)
	{
		GD.Print($"[GameManager] State: {CurrentState} → {newState}");
		CurrentState = newState;
	}

	// ═════════════════════════════════════════════════════════
	//  CHARACTER MANAGEMENT
	// ═════════════════════════════════════════════════════════

	/// <summary>Load a character from API data.</summary>
	public void LoadCharacter(PlayerData data, string characterId)
	{
		ActiveCharacter = data;
		ActiveCharacterId = characterId;

		// Fix -1 defaults → set to max
		data.InitializeCombatState();
		RaceData.ApplyRacePassives(data);

		// Create fresh loadout and load learned abilities from server
		ActiveLoadout = new CharacterLoadout();
		UI.Panels.AbilityShopPanel.LoadAbilitiesFromServer(ActiveLoadout);

		// Connect real-time socket if not already connected
		ConnectGameSocket();

		GD.Print($"[GameManager] Loaded: {data.CharacterName} (ID: {characterId})");
	}

	/// <summary>Load a character from a local save file (legacy/offline).</summary>
	public PlayerData LoadCharacterFromFile(string slot)
	{
		string path = $"user://saves/slot{slot}.tres";
		if (!ResourceLoader.Exists(path)) return null;

		var data = ResourceLoader.Load<PlayerData>(path);
		if (data != null)
		{
			ActiveCharacter = data;
			GD.Print($"[GameManager] Loaded from file: {data.CharacterName}");
		}
		return data;
	}

	/// <summary>Save character to local file (legacy/offline).</summary>
	public void SaveCharacter(PlayerData data, string slot)
	{
		DirAccess.MakeDirRecursiveAbsolute("user://saves");
		string path = $"user://saves/slot{slot}.tres";
		ResourceSaver.Save(data, path);
		GD.Print($"[GameManager] Saved: {data.CharacterName} → {path}");
	}

	/// <summary>Check if a local save exists.</summary>
	public bool SaveExists(string slot)
	{
		return ResourceLoader.Exists($"user://saves/slot{slot}.tres");
	}

	/// <summary>Delete a local save file.</summary>
	public void DeleteSave(string slot)
	{
		string path = $"user://saves/slot{slot}.tres";
		if (FileAccess.FileExists(path))
		{
			DirAccess.Open("user://saves").Remove($"slot{slot}.tres");
			GD.Print($"[GameManager] Deleted: {path}");
		}
	}

	// ═════════════════════════════════════════════════════════
	//  SCENE MANAGEMENT
	// ═════════════════════════════════════════════════════════

	public void ChangeScene(string scenePath)
	{
		GD.Print($"[GameManager] Loading scene: {scenePath}");
		GetTree().ChangeSceneToFile(scenePath);
	}

	// ═════════════════════════════════════════════════════════
	//  REAL-TIME SOCKET
	// ═════════════════════════════════════════════════════════

	private bool _socketConnected = false;

	private void ConnectGameSocket()
	{
		var socket = Networking.GameSocket.Instance;
		var api = Networking.ApiClient.Instance;
		if (socket == null || api == null || !api.IsLoggedIn) return;
		if (_socketConnected) return;

		socket.Connected += OnSocketConnected;
		socket.Connect("http://127.0.0.1:5000/api", api.AuthToken);
		_socketConnected = true;
	}

	private void OnSocketConnected()
	{
		// Join world with character position
		var socket = Networking.GameSocket.Instance;
		if (socket == null || string.IsNullOrEmpty(ActiveCharacterId)) return;

		// Get player position from scene (may not be loaded yet)
		var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Node2D;
		var pos = player?.GlobalPosition ?? Vector2.Zero;

		socket.JoinWorld(ActiveCharacterId, pos);
	}
}
