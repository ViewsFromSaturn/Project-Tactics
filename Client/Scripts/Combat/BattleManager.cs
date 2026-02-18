using Godot;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

public partial class BattleManager : Node3D
{
	public enum BattleState { Setup, CommandMenu, MovePhase, TargetPhase, Resolution, BattleOver }

	private BattleGrid _grid;
	private TurnQueue _turnQueue;
	private IsoBattleRenderer _renderer;
	private BattleHUD _hud;
	private Camera3D _camera;
	private BattleState _state = BattleState.Setup;
	private BattleUnit _activeUnit;
	private readonly List<BattleUnit> _units = new();

	private Vector2I _turnStartPos;
	private float _cameraAngle = -45f;
	private float _cameraZoom = 8f;

	public override void _Ready()
	{
		SetupCamera();
		StartTestBattle();
	}

	// ═══════════════════════════════════════════════════════
	//  SETUP
	// ═══════════════════════════════════════════════════════

	private void SetupCamera()
	{
		_camera = new Camera3D();
		_camera.Projection = Camera3D.ProjectionType.Orthogonal;
		_camera.Size = _cameraZoom;
		AddChild(_camera);
	}

	private void UpdateCamera()
	{
		if (_camera == null || _grid == null) return;
		var center = IsoBattleRenderer.GridToWorld(_grid.Width / 2, _grid.Height / 2, 0);
		float rad = Mathf.DegToRad(_cameraAngle);
		float pitch = Mathf.DegToRad(45f);
		float dist = _cameraZoom;
		_camera.Position = center + new Vector3(
			Mathf.Sin(rad) * Mathf.Cos(pitch) * dist,
			Mathf.Sin(pitch) * dist,
			Mathf.Cos(rad) * Mathf.Cos(pitch) * dist);
		_camera.LookAt(center, Vector3.Up);
		_camera.Size = _cameraZoom;
	}

	private void StartTestBattle()
	{
		_grid = BattleGrid.GenerateTestMap(1);
		_turnQueue = new TurnQueue();

		// Renderer
		_renderer = new IsoBattleRenderer();
		AddChild(_renderer);
		_renderer.Initialize(_grid, _camera);
		_renderer.TileClicked += OnTileClicked;
		_renderer.TileHovered += OnTileHovered;

		// HUD
		_hud = new BattleHUD();
		AddChild(_hud);
		_hud.CommandSelected += OnHudCommand;
		_hud.CommandCancelled += OnHudCancel;
		_hud.AbilitySelected += OnAbilitySelected;
		_hud.ItemSelected += OnItemSelected;
		_hud.SetAbilities(AbilityInfo.GetTestAbilities());
		_hud.SetItems(ItemInfo.GetTestItems());

		// Units
		var unitA = BattleUnit.CreateDummy("Yumeno", UnitTeam.TeamA, new(1, 6), 5, 10);
		var unitB = BattleUnit.CreateDummy("Enemy", UnitTeam.TeamB, new(6, 1), 5, 20);

		foreach (var u in new[] { unitA, unitB })
		{
			_units.Add(u);
			_turnQueue.AddUnit(u);
			_grid.At(u.GridPosition).Occupant = u;
			_renderer.PlaceUnit(u);
		}

		// Environment
		var env = new Godot.Environment();
		env.BackgroundMode = Godot.Environment.BGMode.Color;
		env.BackgroundColor = new Color("1a1a2e");
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		env.AmbientLightColor = new Color("aaaacc");
		env.AmbientLightEnergy = 0.4f;
		var we = new WorldEnvironment();
		we.Environment = env;
		AddChild(we);

		UpdateCamera();

		_turnQueue.InitializeTurnOrder();
		NextTurn();

		foreach (var u in _units)
			GD.Print($"[Battle] {u.Name}: BaseWt={u.BaseWt} MoveRT/tile={u.MoveRtPerTile}");
	}

	// ═══════════════════════════════════════════════════════
	//  TURN FLOW
	// ═══════════════════════════════════════════════════════

	private void NextTurn()
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
		GD.Print($"[Battle] {_activeUnit.Name}'s turn (HP:{_activeUnit.CurrentHp}/{_activeUnit.MaxHp})");
	}

	private void ShowCommandMenu()
	{
		_state = BattleState.CommandMenu;
		_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()}'S TURN — SELECT ACTION");
		_hud.ShowCommandMenu(_activeUnit);
		RefreshTurnOrder();
		_renderer.ClearAllHighlights();
	}

	private void RefreshTurnOrder()
	{
		var order = _turnQueue.GetTurnOrder(10);
		var ordered = new List<BattleUnit>();
		foreach (var (unit, _) in order) ordered.Add(unit);
		_hud.UpdateTurnOrder(ordered, _activeUnit);
	}

	private void EndTurnWithAction(int actionRt)
	{
		_renderer.ClearAllHighlights();
		_hud.HideCommandMenu();
		_turnQueue.EndTurn(_activeUnit, actionRt);
		NextTurn();
	}

	// ═══════════════════════════════════════════════════════
	//  HUD COMMAND HANDLERS
	// ═══════════════════════════════════════════════════════

	private void OnHudCommand(string cmd)
	{
		if (_activeUnit == null) return;

		switch (cmd)
		{
			case "Move":
				_state = BattleState.MovePhase;
				_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — SELECT TILE TO MOVE");
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

			case "Wait":
				GD.Print($"[Battle] {_activeUnit.Name} waits.");
				EndTurnWithAction(ActionRt.Wait);
				break;

			case "Flee":
				GD.Print($"[Battle] {_activeUnit.Name} flees! (not implemented)");
				EndTurnWithAction(ActionRt.Wait);
				break;
		}
	}

	private void OnHudCancel()
	{
		if (_state == BattleState.MovePhase || _state == BattleState.TargetPhase)
		{
			// Return to command menu
			ShowCommandMenu();
		}
	}

	private void OnAbilitySelected(int index)
	{
		var abilities = AbilityInfo.GetTestAbilities();
		if (index < 0 || index >= abilities.Count) return;
		var ab = abilities[index];

		_activeUnit.HasActed = true;
		_activeUnit.CurrentEther = Mathf.Max(0, _activeUnit.CurrentEther - ab.EtherCost);
		GD.Print($"[Battle] {_activeUnit.Name} uses {ab.Name}! (-{ab.EtherCost} EP)");
		EndTurnWithAction(ab.RtCost);
	}

	private void OnItemSelected(int index)
	{
		var items = ItemInfo.GetTestItems();
		if (index < 0 || index >= items.Count) return;
		var it = items[index];

		_activeUnit.HasActed = true;
		GD.Print($"[Battle] {_activeUnit.Name} uses {it.Name}!");
		EndTurnWithAction(it.RtCost);
	}

	// ═══════════════════════════════════════════════════════
	//  TILE INTERACTION
	// ═══════════════════════════════════════════════════════

	private void OnTileClicked(int x, int y)
	{
		var pos = new Vector2I(x, y);
		var tile = _grid.At(pos);
		if (tile == null || _activeUnit == null) return;

		if (_state == BattleState.MovePhase)
		{
			// Click on self = cancel move
			if (pos == _activeUnit.GridPosition)
			{
				ShowCommandMenu();
				return;
			}

			var moveRange = _grid.GetMovementRange(_activeUnit.GridPosition, _activeUnit.Move, _activeUnit.Jump);
			foreach (var t in moveRange)
			{
				if (t.GridPos == pos && tile.Occupant == null)
				{
					var path = _grid.FindPath(_activeUnit.GridPosition, pos, _activeUnit.Jump);
					int tilesMoved = path?.Count ?? 0;

					_grid.At(_activeUnit.GridPosition).Occupant = null;
					_activeUnit.GridPosition = pos;
					tile.Occupant = _activeUnit;
					_activeUnit.HasMoved = true;
					_activeUnit.TilesMoved = tilesMoved;
					_renderer.MoveUnitVisual(_activeUnit);

					GD.Print($"[Battle] {_activeUnit.Name} moved {tilesMoved} tiles.");

					// Return to command menu after moving
					ShowCommandMenu();
					return;
				}
			}
		}
		else if (_state == BattleState.TargetPhase)
		{
			// Click self = cancel
			if (pos == _activeUnit.GridPosition)
			{
				ShowCommandMenu();
				return;
			}

			// Click enemy in range = attack
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

	private void OnTileHovered(int x, int y)
	{
		var tile = _grid.At(x, y);
		_hud.ShowTileInfo(tile);

		// Show target info when hovering an enemy
		if (tile?.Occupant != null && tile.Occupant != _activeUnit)
			_hud.UpdateTargetInfo(tile.Occupant);
		else
			_hud.UpdateTargetInfo(null);
	}

	// ═══════════════════════════════════════════════════════
	//  COMBAT
	// ═══════════════════════════════════════════════════════

	private void DoAttack(BattleUnit atk, BattleUnit def)
	{
		float raw = atk.Atk * 1.5f;
		int dmg = (int)Mathf.Max(raw - def.Def * 0.8f, 1);
		dmg = Mathf.Min(dmg, (int)(def.MaxHp * 0.6f));

		float dodge = Mathf.Clamp((def.Avd * 0.4f + def.Agility * 0.2f - atk.Acc * 0.3f) / 100f, 0f, 0.75f);
		var rng = new System.Random();
		if (rng.NextDouble() < dodge)
		{
			GD.Print($"[Battle] {def.Name} dodged!");
			return;
		}

		bool crit = rng.NextDouble() * 100 < atk.CritPercent;
		if (crit) dmg = (int)(dmg * 1.5f);
		def.CurrentHp = Mathf.Max(0, def.CurrentHp - dmg);

		GD.Print($"[Battle] {atk.Name} → {def.Name}: {dmg} dmg{(crit ? " CRIT!" : "")} ({def.CurrentHp}/{def.MaxHp})");
		if (!def.IsAlive) GD.Print($"[Battle] {def.Name} defeated!");
	}

	// ═══════════════════════════════════════════════════════
	//  CAMERA INPUT
	// ═══════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
			{ _cameraZoom = Mathf.Max(4f, _cameraZoom - 0.5f); UpdateCamera(); }
			else if (mb.ButtonIndex == MouseButton.WheelDown)
			{ _cameraZoom = Mathf.Min(16f, _cameraZoom + 0.5f); UpdateCamera(); }
		}
		else if (ev is InputEventKey key && key.Pressed)
		{
			if (key.Keycode == Key.Q) { _cameraAngle -= 15; UpdateCamera(); }
			if (key.Keycode == Key.E) { _cameraAngle += 15; UpdateCamera(); }
			if (key.Keycode == Key.Escape && _state == BattleState.BattleOver)
				GetTree().ChangeSceneToFile("res://Scenes/World/Overworld.tscn");
		}
	}
}
