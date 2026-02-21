using Godot;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Battle Manager — overlays on top of the overworld scene.
/// Call BattleManager.StartBattle() to add it, EndBattle() to remove it.
/// The overworld HUD sidebar collapses, identity bar hides, chat stays.
/// </summary>
public partial class BattleManager : Node3D
{
	public enum BattleState { Setup, CommandMenu, MovePhase, TargetPhase, Resolution, BattleOver }

	BattleGrid _grid;
	TurnQueue _turnQueue;
	IsoBattleRenderer _renderer;
	BattleHUD _hud;
	Camera3D _camera;
	BattleState _state = BattleState.Setup;
	BattleUnit _activeUnit;
	readonly List<BattleUnit> _units = new();
	Vector2I _turnStartPos;
	Vector2I _pendingMoveTile = new(-1, -1);
	List<Vector2I> _pendingMovePath;
	int _pendingActionRt = 0; // stored RT from combat action, applied at turn end
	ProjectTactics.UI.ChatPanel _chatPanel;

	// ─── COMBAT STATS ───
	readonly BattleStats _stats = new();

	// ─── CAMERA ───
	float _camAngle = -45f;       // horizontal orbit (degrees)
	float _camPitch = 45f;        // vertical pitch (degrees), adjustable
	float _camZoom = 8f;
	float _camAngleTarget = -45f;
	float _camPitchTarget = 45f;
	float _camZoomTarget = 8f;
	const float CamRotSpeed = 120f; // degrees per second when holding Q/E
	const float CamPitchSpeed = 60f;
	const float CamSmoothSpeed = 8f;
	const float CamPitchMin = 20f;
	const float CamPitchMax = 75f;
	const float CamZoomMin = 4f;
	const float CamZoomMax = 16f;

	// Track held keys for smooth rotation
	bool _rotLeftHeld, _rotRightHeld, _pitchUpHeld, _pitchDownHeld;

	// ─── OVERWORLD REFS ───
	Control _overworldIdentityBar;
	Control _overworldSidebar;
	readonly List<Node> _hiddenOverworldNodes = new();

	public override void _Ready()
	{
		SetupCamera();
		CollapseOverworldUI();
		_chatPanel = FindChatPanel(GetTree().CurrentScene);
		if (_chatPanel == null) GD.PrintErr("[Battle] ChatPanel not found — combat log will only print to console.");
		StartTestBattle();
	}

	static ProjectTactics.UI.ChatPanel FindChatPanel(Node root)
	{
		if (root is ProjectTactics.UI.ChatPanel cp) return cp;
		foreach (var child in root.GetChildren())
		{
			var found = FindChatPanel(child);
			if (found != null) return found;
		}
		return null;
	}

	public override void _ExitTree()
	{
		RestoreOverworldUI();
	}

	// ═══════════════════════════════════════════════════════
	//  OVERWORLD UI MANAGEMENT
	// ═══════════════════════════════════════════════════════

	void CollapseOverworldUI()
	{
		var scene = GetTree().CurrentScene;
		if (scene == null) return;

		// Hide all 2D world elements (tilemap, player, 2D camera)
		foreach (var child in scene.GetChildren())
		{
			// Skip CanvasLayers (HUDLayer, DebugLayer) — we want those to stay
			if (child is CanvasLayer) continue;
			// Skip ourselves
			if (child == this) continue;

			if (child is Node2D n2d)
			{
				if (n2d.Visible) { n2d.Visible = false; _hiddenOverworldNodes.Add(n2d); }
			}
			else if (child is TileMapLayer tml)
			{
				if (tml.Visible) { tml.Visible = false; _hiddenOverworldNodes.Add(tml); }
			}
		}

		// Disable the 2D camera so it doesn't interfere
		var cam2d = scene.FindChild("Camera", true, false) as Camera2D;
		if (cam2d != null) { cam2d.Enabled = false; _hiddenOverworldNodes.Add(cam2d); }

		// Find and collapse the overworld sidebar + identity bar
		var hud = scene.FindChild("OverworldHUD", true, false) as Control;
		if (hud != null)
		{
			foreach (var child in hud.GetChildren())
			{
				if (child is PanelContainer pc)
				{
					if (_overworldIdentityBar == null)
					{
						_overworldIdentityBar = pc;
						pc.Visible = false;
					}
				}
				else if (child is VBoxContainer vb)
				{
					_overworldSidebar = vb;
					vb.Visible = false;
				}
			}
		}

		GD.Print("[Battle] Collapsed overworld UI, hid 2D elements");
	}

	void RestoreOverworldUI()
	{
		// Restore hidden 2D nodes
		foreach (var node in _hiddenOverworldNodes)
		{
			if (node is Camera2D cam)
			{
				cam.Enabled = true;
				cam.MakeCurrent();
			}
			else if (node is Node2D n2d) n2d.Visible = true;
			else if (node is TileMapLayer tml) tml.Visible = true;
		}
		_hiddenOverworldNodes.Clear();

		if (_overworldIdentityBar != null) _overworldIdentityBar.Visible = true;
		if (_overworldSidebar != null) _overworldSidebar.Visible = true;

		// Force the viewport back to 2D rendering
		var viewport = GetTree()?.Root;
		if (viewport != null)
		{
			viewport.World3D = null;
		}

		GD.Print("[Battle] Restored overworld UI");
	}

	// ═══════════════════════════════════════════════════════
	//  CAMERA
	// ═══════════════════════════════════════════════════════

	void SetupCamera()
	{
		_camera = new Camera3D();
		_camera.Projection = Camera3D.ProjectionType.Orthogonal;
		_camera.Size = _camZoom;
		AddChild(_camera);
	}

	void UpdateCamera(float dt)
	{
		if (_camera == null || _grid == null) return;

		// Smooth interpolation
		_camAngle = Mathf.Lerp(_camAngle, _camAngleTarget, dt * CamSmoothSpeed);
		_camPitch = Mathf.Lerp(_camPitch, _camPitchTarget, dt * CamSmoothSpeed);
		_camZoom = Mathf.Lerp(_camZoom, _camZoomTarget, dt * CamSmoothSpeed);

		var center = IsoBattleRenderer.GridToWorld(_grid.Width / 2, _grid.Height / 2, 0);
		float rad = Mathf.DegToRad(_camAngle);
		float pitch = Mathf.DegToRad(_camPitch);
		float dist = _camZoom;

		_camera.Position = center + new Vector3(
			Mathf.Sin(rad) * Mathf.Cos(pitch) * dist,
			Mathf.Sin(pitch) * dist,
			Mathf.Cos(rad) * Mathf.Cos(pitch) * dist);
		_camera.LookAt(center, Vector3.Up);
		_camera.Size = _camZoom;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Smooth rotation from held keys
		if (_rotLeftHeld) _camAngleTarget -= CamRotSpeed * dt;
		if (_rotRightHeld) _camAngleTarget += CamRotSpeed * dt;
		if (_pitchUpHeld) _camPitchTarget = Mathf.Clamp(_camPitchTarget + CamPitchSpeed * dt, CamPitchMin, CamPitchMax);
		if (_pitchDownHeld) _camPitchTarget = Mathf.Clamp(_camPitchTarget - CamPitchSpeed * dt, CamPitchMin, CamPitchMax);

		UpdateCamera(dt);
	}

	// ═══════════════════════════════════════════════════════
	//  BATTLE SETUP
	// ═══════════════════════════════════════════════════════

	void StartTestBattle()
	{
		_grid = BattleGrid.GenerateTestMap(1);
		_turnQueue = new TurnQueue();

		_renderer = new IsoBattleRenderer();
		AddChild(_renderer);
		_renderer.Initialize(_grid, _camera);
		_renderer.TileClicked += OnTileClicked;
		_renderer.TileHovered += OnTileHovered;
		_renderer.TileRightClicked += OnTileRightClicked;

		_hud = new BattleHUD();
		AddChild(_hud);
		_hud.CommandSelected += OnHudCommand;
		_hud.CommandCancelled += OnHudCancel;
		_hud.AbilitySelected += OnAbilitySelected;
		_hud.ItemSelected += OnItemSelected;
		_hud.MoveConfirmed += OnMoveConfirmed;
		_hud.SetAbilities(AbilityInfo.GetTestAbilities());
		_hud.SetItems(ItemInfo.GetTestItems());

		// ─── PLAYER UNIT (from overworld character) ─────────
		BattleUnit unitA;
		var gm = Core.GameManager.Instance;
		if (gm?.ActiveCharacter != null)
		{
			unitA = BattleUnit.FromPlayerData(gm.ActiveCharacter, UnitTeam.TeamA, new(1, 6));
			GD.Print($"[Battle] Loaded player: {unitA.Name} HP:{unitA.MaxHp} STA:{unitA.MaxStamina} AE:{unitA.MaxAether}");
		}
		else
		{
			// Fallback dummy if no character loaded (editor testing)
			unitA = BattleUnit.CreateDummy("Player", UnitTeam.TeamA, new(1, 6), 5, 10);
			GD.Print("[Battle] No active character — using dummy.");
		}

		// ─── ENEMY UNIT (dummy for now, will be NPC/PvP later) ──
		var unitB = BattleUnit.CreateDummy("Enemy", UnitTeam.TeamB, new(6, 1), 5, 20);

		foreach (var u in new[] { unitA, unitB })
		{
			_units.Add(u);
			_turnQueue.AddUnit(u);
			_grid.At(u.GridPosition).Occupant = u;
			_renderer.PlaceUnit(u);
		}

		// Lighting
		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-45, -30, 0);
		light.LightEnergy = 0.8f;
		light.ShadowEnabled = true;
		AddChild(light);

		var env = new Godot.Environment();
		env.BackgroundMode = Godot.Environment.BGMode.Color;
		env.BackgroundColor = new Color("1a1a2e");
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		env.AmbientLightColor = new Color("aaaacc");
		env.AmbientLightEnergy = 0.4f;
		var we = new WorldEnvironment();
		we.Environment = env;
		AddChild(we);

		_turnQueue.InitializeTurnOrder();

		// Record battle start time
		_stats.BattleStartTime = (float)Time.GetTicksMsec() / 1000f;

		// Play intro animation, then start first turn
		string playerName = _units.Find(u => u.Team == UnitTeam.TeamA)?.Name ?? "PLAYER";
		var intro = new BattleIntro(playerName.ToUpper(), "ENEMY FORCES");
		AddChild(intro);
		intro.IntroFinished += () => NextTurn();
	}

	// ═══════════════════════════════════════════════════════
	//  TURN FLOW
	// ═══════════════════════════════════════════════════════

	void NextTurn()
	{
		_turnQueue.AdvanceTime();
		_activeUnit = _turnQueue.GetActiveUnit();
		_stats.TurnsElapsed++;

		var winner = _turnQueue.GetWinningTeam();
		if (_activeUnit == null || winner != null)
		{
			_state = BattleState.BattleOver;
			_hud.HideCommandMenu();
			_renderer.ClearAllHighlights();

			bool playerWon = winner == UnitTeam.TeamA;
			_hud.SetPhaseText(playerWon ? "⚔ VICTORY" : "⚔ DEFEAT");

			var resultType = playerWon ? BattleReport.BattleResult.Victory : BattleReport.BattleResult.Defeat;
			var timer = GetTree().CreateTimer(1.5f);
			timer.Timeout += () => ShowBattleReport(resultType);
			return;
		}

		_activeUnit.HasMoved = false;
		_activeUnit.HasActed = false;
		_activeUnit.TilesMoved = 0;
		_activeUnit.IsDefending = false;
		_pendingActionRt = 0;
		_turnStartPos = _activeUnit.GridPosition;

		// v3.0: Apply per-turn regen at start of unit's turn
		_activeUnit.ApplyRegen();

		ShowCommandMenu();
	}

	void ShowCommandMenu()
	{
		_state = BattleState.CommandMenu;
		_pendingMoveTile = new(-1, -1);
		_pendingMovePath = null;
		_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()}'S TURN — SELECT ACTION");
		_hud.ShowCommandMenu(_activeUnit);
		RefreshTurnOrder();
		_renderer.ClearAllHighlights();
	}

	void RefreshTurnOrder()
	{
		var order = _turnQueue.GetTurnOrder(10);
		var ordered = new List<BattleUnit>();
		foreach (var (unit, _) in order) ordered.Add(unit);
		_hud.UpdateTurnOrder(ordered, _activeUnit);
	}

	void EndTurnWithAction(int actionRt)
	{
		_renderer.ClearAllHighlights();
		_hud.HideCommandMenu();
		_turnQueue.EndTurn(_activeUnit, actionRt);
		NextTurn();
	}

	/// <summary>Called after a combat action (attack/ability/item/defend). 
	/// Stores RT, returns to menu if move still available, else ends turn.</summary>
	void CompleteCombatAction(int actionRt)
	{
		_activeUnit.HasActed = true;
		_pendingActionRt = actionRt;
		_renderer.ClearAllHighlights();

		if (_activeUnit.HasMoved)
		{
			// Both actions spent → end turn
			EndTurnWithAction(_pendingActionRt);
		}
		else
		{
			// Can still move → return to command menu
			ShowCommandMenu();
		}
	}

	/// <summary>Called after move confirm. Checks if turn should auto-end.</summary>
	void CheckAutoEndTurn()
	{
		if (_activeUnit.HasMoved && _activeUnit.HasActed)
			EndTurnWithAction(_pendingActionRt);
		else
			ShowCommandMenu();
	}

	// ═══════════════════════════════════════════════════════
	//  HUD HANDLERS
	// ═══════════════════════════════════════════════════════

	void OnHudCommand(string cmd)
	{
		if (_activeUnit == null) return;

		switch (cmd)
		{
			case "Move":
				_state = BattleState.MovePhase;
				_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — SELECT TILE");
				_hud.HideCommandMenu();
				_renderer.ClearAllHighlights();
				var moveRange = _grid.GetMovementRange(_activeUnit.GridPosition, _activeUnit.Move, _activeUnit.Jump);
				_renderer.ShowMoveRange(moveRange);
				break;

			case "Attack":
				_state = BattleState.TargetPhase;
				_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — SELECT TARGET");
				_hud.HideCommandMenu();
				_renderer.ClearAllHighlights();
				var atkRange = _grid.GetAttackRange(_activeUnit.GridPosition, 1, 1);
				_renderer.ShowAttackRange(atkRange);
				break;

			case "Defend":
				_activeUnit.IsDefending = true;
				CombatLog($"🛡 {_activeUnit.Name} defends. (DEF +50% until next turn)");
				CompleteCombatAction(ActionRt.Defend);
				break;

			case "End Turn":
				CombatLog($"◎ {_activeUnit.Name} ends turn.");
				EndTurnWithAction(_pendingActionRt);
				break;

			case "Flee":
				CombatLog($"↺ {_activeUnit.Name} flees the battle!");
				_state = BattleState.BattleOver;
				_hud.HideCommandMenu();
				_renderer.ClearAllHighlights();
				_hud.SetPhaseText("⚔ FLED FROM BATTLE");
				var fleeTimer = GetTree().CreateTimer(1.5f);
				fleeTimer.Timeout += () => ShowBattleReport(BattleReport.BattleResult.Fled);
				break;
		}
	}

	void OnHudCancel()
	{
		if (_state == BattleState.MovePhase && _pendingMoveTile != new Vector2I(-1, -1))
		{
			CancelMovePreview();
			return;
		}
		if (_state is BattleState.MovePhase or BattleState.TargetPhase)
			ShowCommandMenu();
	}

	void OnMoveConfirmed()
	{
		if (_state == BattleState.MovePhase && _pendingMoveTile != new Vector2I(-1, -1))
			ConfirmMove();
	}

	void ConfirmMove()
	{
		if (_activeUnit == null || _pendingMoveTile == new Vector2I(-1, -1)) return;

		var targetTile = _grid.At(_pendingMoveTile);
		if (targetTile == null) return;

		int moved = _pendingMovePath?.Count ?? 0;
		_stats.RecordMove(moved);
		_grid.At(_activeUnit.GridPosition).Occupant = null;
		_activeUnit.GridPosition = _pendingMoveTile;
		targetTile.Occupant = _activeUnit;
		_activeUnit.HasMoved = true;
		_activeUnit.TilesMoved = moved;
		_renderer.MoveUnitVisual(_activeUnit);
		CombatLog($"→ {_activeUnit.Name} moves to ({_pendingMoveTile.X},{_pendingMoveTile.Y}) [{moved} tiles]");

		_pendingMoveTile = new(-1, -1);
		_pendingMovePath = null;
		_renderer.ClearMovePreview();
		_hud.HideMoveConfirm();
		CheckAutoEndTurn();
	}

	void CancelMovePreview()
	{
		_pendingMoveTile = new(-1, -1);
		_pendingMovePath = null;
		_renderer.ClearMovePreview();
		_hud.HideMoveConfirm();
		_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — SELECT TILE");
	}

	void OnAbilitySelected(int index)
	{
		var abilities = AbilityInfo.GetTestAbilities();
		if (index < 0 || index >= abilities.Count) return;
		var ab = abilities[index];
		if (!ab.CanAfford(_activeUnit.CurrentStamina, _activeUnit.CurrentAether))
		{
			CombatLog($"✗ {_activeUnit.Name} can't afford {ab.Name}! ({ab.CostString()})");
			return;
		}
		_activeUnit.SpendResource(ab);
		if (ab.ResourceType == ResourceType.Stamina || ab.ResourceType == ResourceType.Both)
			_stats.RecordStaminaSpent(ab.StaminaCost);
		if (ab.ResourceType == ResourceType.Aether || ab.ResourceType == ResourceType.Both)
			_stats.RecordAetherSpent(ab.AetherCost);
		CombatLog($"✦ {_activeUnit.Name} uses {ab.Name}! (-{ab.CostString()})");
		CompleteCombatAction(ab.RtCost);
	}

	void OnItemSelected(int index)
	{
		var items = ItemInfo.GetTestItems();
		if (index < 0 || index >= items.Count) return;
		CombatLog($"◆ {_activeUnit.Name} uses {items[index].Name}!");
		CompleteCombatAction(items[index].RtCost);
	}

	void OnTileRightClicked(int x, int y)
	{
		var tile = _grid.At(x, y);
		if (tile?.Occupant != null) _hud.InspectUnit(tile.Occupant);
	}

	// ═══════════════════════════════════════════════════════
	//  TILE INTERACTION
	// ═══════════════════════════════════════════════════════

	void OnTileClicked(int x, int y)
	{
		var pos = new Vector2I(x, y);
		var tile = _grid.At(pos);
		if (tile == null || _activeUnit == null || _state == BattleState.BattleOver) return;

		if (_state == BattleState.MovePhase)
		{
			if (pos == _activeUnit.GridPosition) { CancelMovePreview(); ShowCommandMenu(); return; }

			var moveRange = _grid.GetMovementRange(_activeUnit.GridPosition, _activeUnit.Move, _activeUnit.Jump);

			// Tile must be unoccupied — one unit per tile
			if (tile.Occupant != null) return;

			// Check if clicked tile is in move range
			bool validTarget = false;
			foreach (var t in moveRange)
			{
				if (t.GridPos == pos) { validTarget = true; break; }
			}
			if (!validTarget) return;

			// Single click → show preview + confirm button
			_renderer.ClearMovePreview();
			var path = _grid.FindPath(_activeUnit.GridPosition, pos, _activeUnit.Jump);
			_pendingMoveTile = pos;
			_pendingMovePath = path;

			// Show purple path preview
			_renderer.ShowMovePreview(pos, path ?? new List<Vector2I>());

			// Show confirm/cancel buttons
			_hud.ShowMoveConfirm(pos.X, pos.Y);
			_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — MOVE TO ({pos.X},{pos.Y})? CONFIRM OR CANCEL");
		}
		else if (_state == BattleState.TargetPhase)
		{
			if (pos == _activeUnit.GridPosition) { ShowCommandMenu(); return; }

			if (tile.Occupant != null && tile.Occupant.Team != _activeUnit.Team)
			{
				var atkRange = _grid.GetAttackRange(_activeUnit.GridPosition, 1, 1);
				foreach (var t in atkRange)
				{
					if (t.GridPos == pos)
					{
						DoAttack(_activeUnit, tile.Occupant);
						CompleteCombatAction(ActionRt.MediumAttack);
						return;
					}
				}
			}
		}
	}

	void OnTileHovered(int x, int y)
	{
		var tile = _grid.At(x, y);
		_hud.ShowTileInfo(tile);
		if (tile?.Occupant != null && tile.Occupant != _activeUnit)
			_hud.UpdateTargetInfo(tile.Occupant);
		else
			_hud.UpdateTargetInfo(null);
	}

	// ═══════════════════════════════════════════════════════
	//  COMBAT
	// ═══════════════════════════════════════════════════════

	void CombatLog(string text)
	{
		GD.Print($"[Battle] {text}");
		_chatPanel?.AddCombatMessage(text);
	}

	void DoAttack(BattleUnit atk, BattleUnit def)
	{
		float raw = atk.Atk * 1.5f;
		float defValue = def.Def * (def.IsDefending ? 1.5f : 1.0f);
		int dmg = (int)Mathf.Max(raw - defValue * 0.8f, 1);
		dmg = Mathf.Min(dmg, (int)(def.MaxHp * 0.6f));

		float dodge = Mathf.Clamp((def.Avd * 0.4f + def.Agility * 0.2f - atk.Acc * 0.3f) / 100f, 0f, 0.75f);
		if (def.IsDefending) dodge = Mathf.Min(dodge + 0.15f, 0.75f);
		var rng = new System.Random();
		if (rng.NextDouble() < dodge)
		{
			CombatLog($"↺ {def.Name} dodged {atk.Name}'s attack!");
			_renderer.SpawnDamageNumber(def, 0, false, isDodge: true);
			_stats.RecordDodge(def);
			return;
		}

		bool crit = rng.NextDouble() * 100 < atk.CritPercent;
		if (crit) dmg = (int)(dmg * 1.5f);
		def.CurrentHp = Mathf.Max(0, def.CurrentHp - dmg);

		// Track stats
		_stats.RecordDamage(atk, dmg, "Basic Strike", crit);

		string critTag = crit ? " CRIT!" : "";
		CombatLog($"⚔ {atk.Name} attacks {def.Name} with Basic Strike →{critTag} {dmg} dmg ({def.CurrentHp}/{def.MaxHp} HP)");

		// Visual feedback
		_renderer.SpawnDamageNumber(def, dmg, crit);
		_renderer.FlashUnitHit(def);
		_renderer.RefreshUnitHpBar(def);
		_hud.UpdateUnitInfo(_activeUnit);

		if (!def.IsAlive)
		{
			CombatLog($"💀 {def.Name} has been defeated!");
			_stats.RecordKill(atk, def);

			// Remove from grid
			var defTile = _grid.At(def.GridPosition);
			if (defTile != null) defTile.Occupant = null;

			// Fade out and remove visual after short delay
			_renderer.RemoveUnitVisual(def);
		}
	}

	// ═══════════════════════════════════════════════════════
	//  BATTLE RESULTS
	// ═══════════════════════════════════════════════════════

	void ShowBattleReport(BattleReport.BattleResult result)
	{
		float now = (float)Time.GetTicksMsec() / 1000f;
		var gm = Core.GameManager.Instance;
		var player = new BattleReport.PlayerInfo
		{
			Name = gm?.ActiveCharacter?.CharacterName ?? "Player",
			Race = gm?.ActiveCharacter?.RaceName ?? "",
			Rank = gm?.ActiveCharacter?.RpRank ?? "Aspirant",
			SpriteSheetPath = "res://Assets/Sprites/test_base_clean.png"
		};
		var reportLayer = BattleReport.Build(result, _stats, player, now, () =>
		{
			CombatLog("◎ Returning to overworld...");
			QueueFree();
		});
		AddChild(reportLayer);
	}

	// ═══════════════════════════════════════════════════════
	//  INPUT — camera rotation + zoom
	// ═══════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				_camZoomTarget = Mathf.Max(CamZoomMin, _camZoomTarget - 0.5f);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				_camZoomTarget = Mathf.Min(CamZoomMax, _camZoomTarget + 0.5f);
		}
		else if (ev is InputEventKey key)
		{
			// Track held state for smooth rotation
			if (key.Keycode == Key.Q) _rotLeftHeld = key.Pressed;
			if (key.Keycode == Key.E) _rotRightHeld = key.Pressed;
			if (key.Keycode == Key.R) _pitchUpHeld = key.Pressed;
			if (key.Keycode == Key.F) _pitchDownHeld = key.Pressed;

			// Battle exit (debug)
			if (key.Pressed && key.Keycode == Key.F5)
			{
				QueueFree(); // Remove battle overlay, return to overworld
				GetViewport().SetInputAsHandled();
			}
		}
	}
}
