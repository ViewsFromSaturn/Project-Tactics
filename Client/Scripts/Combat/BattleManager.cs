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

	public override void _Ready()
	{
		SetupCamera();
		CollapseOverworldUI();
		StartTestBattle();
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
		// Find and collapse the overworld sidebar + identity bar
		var hud = FindOverworldHUD();
		if (hud == null) return;

		// The identity bar is the first PanelContainer child
		foreach (var child in hud.GetChildren())
		{
			if (child is PanelContainer pc && child.Name != "ChatPanel")
			{
				if (_overworldIdentityBar == null)
				{
					_overworldIdentityBar = pc;
					pc.Visible = false;
					continue;
				}
			}
			if (child is VBoxContainer vb)
			{
				_overworldSidebar = vb;
				vb.Visible = false;
			}
		}
		GD.Print("[Battle] Collapsed overworld UI");
	}

	void RestoreOverworldUI()
	{
		if (_overworldIdentityBar != null) _overworldIdentityBar.Visible = true;
		if (_overworldSidebar != null) _overworldSidebar.Visible = true;
		GD.Print("[Battle] Restored overworld UI");
	}

	Control FindOverworldHUD()
	{
		// Walk up to find HUDLayer, then find OverworldHUD
		var root = GetTree().Root;
		return root.FindChild("OverworldHUD", true, false) as Control;
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
		_hud.SetAbilities(AbilityInfo.GetTestAbilities());
		_hud.SetItems(ItemInfo.GetTestItems());

		var unitA = BattleUnit.CreateDummy("Yumeno", UnitTeam.TeamA, new(1, 6), 5, 10);
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
		NextTurn();
	}

	// ═══════════════════════════════════════════════════════
	//  TURN FLOW
	// ═══════════════════════════════════════════════════════

	void NextTurn()
	{
		_turnQueue.AdvanceTime();
		_activeUnit = _turnQueue.GetActiveUnit();

		if (_activeUnit == null || _turnQueue.GetWinningTeam() != null)
		{
			_state = BattleState.BattleOver;
			_hud.HideCommandMenu();
			_hud.SetPhaseText($"BATTLE OVER — {_turnQueue.GetWinningTeam()} WINS");
			return;
		}

		_activeUnit.HasMoved = false;
		_activeUnit.HasActed = false;
		_activeUnit.TilesMoved = 0;
		_turnStartPos = _activeUnit.GridPosition;
		ShowCommandMenu();
	}

	void ShowCommandMenu()
	{
		_state = BattleState.CommandMenu;
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
				_activeUnit.HasActed = true;
				GD.Print($"[Battle] {_activeUnit.Name} defends.");
				EndTurnWithAction(ActionRt.Defend);
				break;

			case "End Turn":
				GD.Print($"[Battle] {_activeUnit.Name} ends turn.");
				EndTurnWithAction(ActionRt.Wait);
				break;

			case "Flee":
				GD.Print($"[Battle] {_activeUnit.Name} flees! (not implemented)");
				EndTurnWithAction(ActionRt.Wait);
				break;
		}
	}

	void OnHudCancel()
	{
		if (_state is BattleState.MovePhase or BattleState.TargetPhase)
			ShowCommandMenu();
	}

	void OnAbilitySelected(int index)
	{
		var abilities = AbilityInfo.GetTestAbilities();
		if (index < 0 || index >= abilities.Count) return;
		var ab = abilities[index];
		_activeUnit.HasActed = true;
		_activeUnit.CurrentEther = Mathf.Max(0, _activeUnit.CurrentEther - ab.EtherCost);
		GD.Print($"[Battle] {_activeUnit.Name} uses {ab.Name}! (-{ab.EtherCost} EP)");
		EndTurnWithAction(ab.RtCost);
	}

	void OnItemSelected(int index)
	{
		var items = ItemInfo.GetTestItems();
		if (index < 0 || index >= items.Count) return;
		_activeUnit.HasActed = true;
		GD.Print($"[Battle] {_activeUnit.Name} uses {items[index].Name}!");
		EndTurnWithAction(items[index].RtCost);
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
		if (tile == null || _activeUnit == null) return;

		if (_state == BattleState.MovePhase)
		{
			if (pos == _activeUnit.GridPosition) { ShowCommandMenu(); return; }

			var moveRange = _grid.GetMovementRange(_activeUnit.GridPosition, _activeUnit.Move, _activeUnit.Jump);
			foreach (var t in moveRange)
			{
				if (t.GridPos == pos && tile.Occupant == null)
				{
					var path = _grid.FindPath(_activeUnit.GridPosition, pos, _activeUnit.Jump);
					int moved = path?.Count ?? 0;
					_grid.At(_activeUnit.GridPosition).Occupant = null;
					_activeUnit.GridPosition = pos;
					tile.Occupant = _activeUnit;
					_activeUnit.HasMoved = true;
					_activeUnit.TilesMoved = moved;
					_renderer.MoveUnitVisual(_activeUnit);
					GD.Print($"[Battle] {_activeUnit.Name} moved {moved} tiles.");
					ShowCommandMenu();
					return;
				}
			}
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
						_activeUnit.HasActed = true;
						DoAttack(_activeUnit, tile.Occupant);
						EndTurnWithAction(ActionRt.MediumAttack);
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

	void DoAttack(BattleUnit atk, BattleUnit def)
	{
		float raw = atk.Atk * 1.5f;
		int dmg = (int)Mathf.Max(raw - def.Def * 0.8f, 1);
		dmg = Mathf.Min(dmg, (int)(def.MaxHp * 0.6f));

		float dodge = Mathf.Clamp((def.Avd * 0.4f + def.Agility * 0.2f - atk.Acc * 0.3f) / 100f, 0f, 0.75f);
		var rng = new System.Random();
		if (rng.NextDouble() < dodge) { GD.Print($"[Battle] {def.Name} dodged!"); return; }

		bool crit = rng.NextDouble() * 100 < atk.CritPercent;
		if (crit) dmg = (int)(dmg * 1.5f);
		def.CurrentHp = Mathf.Max(0, def.CurrentHp - dmg);

		GD.Print($"[Battle] {atk.Name} → {def.Name}: {dmg} dmg{(crit ? " CRIT!" : "")} ({def.CurrentHp}/{def.MaxHp})");
		if (!def.IsAlive) GD.Print($"[Battle] {def.Name} defeated!");
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
