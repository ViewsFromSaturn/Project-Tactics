using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Combat;

/// <summary>
/// Battle Manager — overlays on top of the overworld scene.
/// Call BattleManager.StartBattle() to add it, EndBattle() to remove it.
/// The overworld HUD sidebar collapses, identity bar hides, chat stays.
/// </summary>
public partial class BattleManager : Node3D
{
	public enum BattleState { Setup, CommandMenu, MovePhase, TargetPhase, Resolution, BattleOver, Animating }

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

	// ─── LOADOUT → COMBAT ───
	List<AbilityInfo> _loadoutAbilities = new();
	List<ItemInfo> _loadoutItems = new();

	// ─── PENDING ABILITY/ITEM (for target selection) ───
	AbilityInfo _pendingAbility;
	int _pendingAbilityIndex = -1;
	ItemInfo _pendingItem;
	int _pendingItemIndex = -1;
	enum TargetMode { None, Enemy, Ally, Self, AnyUnit, AnyTile }
	TargetMode _targetMode = TargetMode.None;

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

		// Lock progression panels during combat
		var hud = ProjectTactics.UI.OverworldHUD.Instance;
		if (hud != null) hud.InCombat = true;

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
		// Persist combat HP/STA/AE back to overworld character
		PersistCombatResources();

		// Unlock panels
		var hud = ProjectTactics.UI.OverworldHUD.Instance;
		if (hud != null) hud.InCombat = false;

		RestoreOverworldUI();
	}

	/// <summary>Write BattleUnit's remaining resources back to PlayerData. KO'd = 1 HP.</summary>
	void PersistCombatResources()
	{
		var gm = Core.GameManager.Instance;
		var p = gm?.ActiveCharacter;
		if (p == null) return;

		// Find the player's BattleUnit
		BattleUnit playerUnit = null;
		foreach (var u in _units)
		{
			if (u.Team == UnitTeam.TeamA) { playerUnit = u; break; }
		}
		if (playerUnit == null) return;

		if (playerUnit.IsAlive)
		{
			p.CurrentHp = playerUnit.CurrentHp;
			p.CurrentStamina = playerUnit.CurrentStamina;
			p.CurrentAether = playerUnit.CurrentAether;
			GD.Print($"[Battle] Persisted HP:{p.CurrentHp}/{p.MaxHp} STA:{p.CurrentStamina} AE:{p.CurrentAether}");
		}
		else
		{
			// KO'd — come back with 1 HP, empty pools
			p.CurrentHp = 1;
			p.CurrentStamina = 0;
			p.CurrentAether = 0;
			GD.Print("[Battle] Player KO'd — revived at 1 HP.");
		}
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
			if (!IsInstanceValid(node)) continue;
			if (node is Camera2D cam)
			{
				cam.Enabled = true;
				cam.MakeCurrent();
			}
			else if (node is Node2D n2d) n2d.Visible = true;
			else if (node is TileMapLayer tml) tml.Visible = true;
		}
		_hiddenOverworldNodes.Clear();

		if (_overworldIdentityBar != null && IsInstanceValid(_overworldIdentityBar))
			_overworldIdentityBar.Visible = true;
		if (_overworldSidebar != null && IsInstanceValid(_overworldSidebar))
			_overworldSidebar.Visible = true;

		// Defer World3D cleanup — must happen AFTER QueueFree finishes
		// so 3D nodes aren't accessing a null world
		CallDeferred(nameof(ClearWorld3D));

		GD.Print("[Battle] Restored overworld UI");
	}

	void ClearWorld3D()
	{
		var viewport = GetTree()?.Root;
		if (viewport != null)
			viewport.World3D = null;
	}

	// ═══════════════════════════════════════════════════════
	//  LOADOUT → COMBAT CONVERSION
	// ═══════════════════════════════════════════════════════

	static string TreeIcon(SkillTree t) => t switch
	{
		SkillTree.Vanguard => "⚔", SkillTree.Marksman => "🏹", SkillTree.Evoker => "✦",
		SkillTree.Mender => "✚", SkillTree.Runeblade => "◈", SkillTree.Bulwark => "🛡",
		SkillTree.Shadowstep => "🗡", SkillTree.Dreadnought => "💀", SkillTree.Warsinger => "🎵",
		SkillTree.Templar => "✦", SkillTree.Hexer => "🔮", SkillTree.Tactician => "⚙", _ => "◆"
	};

	static readonly Dictionary<Element, string> ElIcons = new()
	{
		{Element.None, "◇"}, {Element.Fire, "🔥"}, {Element.Ice, "❄"},
		{Element.Lightning, "⚡"}, {Element.Earth, "🌍"}, {Element.Wind, "🌬"},
		{Element.Water, "🌊"}, {Element.Light, "✦"}, {Element.Dark, "🔮"}
	};

	List<AbilityInfo> BuildAbilitiesFromLoadout()
	{
		var list = new List<AbilityInfo>();
		var loadout = Core.GameManager.Instance?.ActiveLoadout;
		if (loadout == null)
		{
			GD.Print("[Battle] No loadout available.");
			_loadoutAbilities = list;
			return list;
		}

		// Equipped active skills (5 slots)
		foreach (var skill in loadout.ActiveSkills)
		{
			if (skill == null) continue;
			list.Add(SkillToAbility(skill));
		}

		// Equipped spells (4 slots)
		foreach (var spell in loadout.EquippedSpells)
		{
			if (spell == null) continue;
			list.Add(SpellToAbility(spell));
		}

		GD.Print($"[Battle] Loaded {list.Count} abilities from loadout.");
		_loadoutAbilities = list;
		return list;
	}

	static AbilityInfo SkillToAbility(SkillDefinition s)
	{
		var ab = new AbilityInfo
		{
			Name = s.Name,
			Icon = TreeIcon(s.Tree),
			SourceId = s.Id,
			Category = s.Resource switch
			{
				ResourceType.Aether => SkillCategory.Ether,
				ResourceType.Both => SkillCategory.Hybrid,
				_ => SkillCategory.Physical,
			},
			SlotType = s.Slot,
			Description = s.Description ?? "",
			ResourceType = s.Resource,
			StaminaCost = s.StaminaCost,
			AetherCost = s.AetherCost,
			Power = s.Power,
			Range = s.Range,
			RtCost = s.RtCost,
			TargetType = s.Target switch
			{
				TargetType.Self => "Self",
				TargetType.Diamond => "AOE Diamond",
				TargetType.Line => "Line",
				TargetType.Ring => "Ring",
				TargetType.Cross => "Cross",
				_ => "Single",
			},
			Element = s.Element,
			RequiredWeapon = s.Weapon == WeaponReq.None ? null : s.Weapon.ToString(),
			SkillTree = s.Tree.ToString(),
			IsUsable = true,
		};

		// ═══ DERIVE INTENT FROM ACTUAL SKILL DATA ═══
		ab.Intent = DeriveSkillIntent(s);
		return ab;
	}

	/// <summary>Determine what a skill actually DOES based on its data, tree, target, and description.</summary>
	static AbilityIntent DeriveSkillIntent(SkillDefinition s)
	{
		string desc = (s.Description ?? "").ToLower();

		// Self-target: buffs/utility (Mighty Impact, Eagle Eye, Warpath, Double Strike, etc.)
		if (s.Target == TargetType.Self)
			return AbilityIntent.BuffSelf;

		// Mender tree: heals and ally support
		if (s.Tree == SkillTree.Mender)
		{
			if (desc.Contains("heal") || desc.Contains("restore") || desc.Contains("hp")
				|| desc.Contains("cure") || desc.Contains("revive") || desc.Contains("cleanse"))
				return AbilityIntent.HealAlly;
			if (desc.Contains("ally") || desc.Contains("barrier") || desc.Contains("regen")
				|| desc.Contains("protect") || desc.Contains("resist") || desc.Contains("ward"))
				return AbilityIntent.BuffAlly;
		}

		// Templar tree: mixed — check for ally/heal keywords
		if (s.Tree == SkillTree.Templar)
		{
			if (desc.Contains("revive") || desc.Contains("resurrect") || desc.Contains("heal"))
				return AbilityIntent.HealAlly;
			if (desc.Contains("ally") || desc.Contains("protection") || desc.Contains("sanctified")
				|| desc.Contains("redirect"))
				return AbilityIntent.BuffAlly;
		}

		// Warsinger tree: songs often buff allies
		if (s.Tree == SkillTree.Warsinger)
		{
			if (desc.Contains("ally") || desc.Contains("allies") || desc.Contains("party")
				|| desc.Contains("atk +") || desc.Contains("def +") || desc.Contains("mov +")
				|| desc.Contains("anthem") || desc.Contains("march") || desc.Contains("requiem"))
				return AbilityIntent.BuffAlly;
			if (desc.Contains("enemy") || desc.Contains("enemies") || desc.Contains("lullaby")
				|| desc.Contains("damage"))
				return s.Power > 0 ? AbilityIntent.DamageEnemy : AbilityIntent.DebuffEnemy;
		}

		// Tactician: many ally-support utilities
		if (s.Tree == SkillTree.Tactician)
		{
			if (desc.Contains("ally") || desc.Contains("swap") || desc.Contains("quicken")
				|| desc.Contains("reduce") && desc.Contains("rt"))
				return AbilityIntent.BuffAlly;
			if (desc.Contains("enemy") || desc.Contains("target enemy") || desc.Contains("add")
				&& desc.Contains("rt"))
				return AbilityIntent.DebuffEnemy;
			if (desc.Contains("trap") || desc.Contains("barricade") || desc.Contains("obstacle"))
				return AbilityIntent.Utility;
		}

		// Hexer: debuffs and dark damage
		if (s.Tree == SkillTree.Hexer)
		{
			if (s.Power > 0)
				return AbilityIntent.DamageEnemy;
			return AbilityIntent.DebuffEnemy;
		}

		// Any skill with "ally" in description that isn't from a damage tree
		if (desc.Contains("ally") && !desc.Contains("damage") && s.Power == 0)
			return AbilityIntent.BuffAlly;

		// Power 0, targets enemy = debuff
		if (s.Power == 0 && s.Target == TargetType.Single && s.Range > 0)
		{
			if (desc.Contains("reduce") || desc.Contains("debuff") || desc.Contains("lower")
				|| desc.Contains("slow") || desc.Contains("weaken") || desc.Contains("immobilize")
				|| desc.Contains("stun") || desc.Contains("silence"))
				return AbilityIntent.DebuffEnemy;
		}

		// Default: if it has Power > 0 and targets single/area = damage enemy
		if (s.Power > 0)
			return AbilityIntent.DamageEnemy;

		// Fallback: utility
		return AbilityIntent.Utility;
	}

	static AbilityInfo SpellToAbility(SpellDefinition sp)
	{
		var ab = new AbilityInfo
		{
			Name = sp.Name,
			Icon = ElIcons.GetValueOrDefault(sp.Element, "◇"),
			SourceId = sp.Id,
			Category = sp.CastType == SpellCastType.Healing ? SkillCategory.Heal : SkillCategory.Ether,
			SlotType = SkillSlotType.Active,
			Description = sp.Description ?? "",
			ResourceType = ResourceType.Aether,
			StaminaCost = 0,
			AetherCost = sp.AetherCost,
			Power = sp.Power,
			Range = sp.RangeMax,
			RtCost = sp.RtCost,
			TargetType = sp.Target switch
			{
				TargetType.Self => "Self",
				TargetType.Diamond => "AOE Diamond",
				TargetType.Line => "Line",
				TargetType.Ring => "Ring",
				TargetType.Cross => "Cross",
				_ => "Single",
			},
			Element = sp.Element,
			RequiredWeapon = null,
			SkillTree = sp.Element.ToString(),
			IsUsable = true,
		};

		// Spell intent from CastType
		ab.Intent = sp.CastType switch
		{
			SpellCastType.Healing => AbilityIntent.HealAlly,
			SpellCastType.Status => AbilityIntent.DebuffEnemy,
			SpellCastType.Transfer => sp.Power > 0 ? AbilityIntent.DamageEnemy : AbilityIntent.DebuffEnemy,
			SpellCastType.Utility => sp.Target == TargetType.Self ? AbilityIntent.BuffSelf : AbilityIntent.BuffAlly,
			_ => sp.Power > 0 ? AbilityIntent.DamageEnemy : AbilityIntent.DebuffEnemy,
		};

		return ab;
	}

	List<ItemInfo> BuildItemsFromLoadout()
	{
		var list = new List<ItemInfo>();
		var loadout = Core.GameManager.Instance?.ActiveLoadout;
		if (loadout == null)
		{
			GD.Print("[Battle] No loadout — no items.");
			_loadoutItems = list;
			return list;
		}

		foreach (var inv in loadout.Inventory)
		{
			if (inv == null || !inv.IsConsumable) continue;
			list.Add(new ItemInfo
			{
				Name = inv.Name,
				Icon = inv.Icon ?? "◆",
				Description = inv.Description ?? "",
				Quantity = 1, // single-use in battle
				RtCost = inv.RtCost > 0 ? inv.RtCost : 20,
				TargetType = inv.TargetType ?? "Self",
				IsUsable = true,
			});
		}

		GD.Print($"[Battle] Loaded {list.Count} consumable items.");
		_loadoutItems = list;
		return list;
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
		_hud.SetAbilities(BuildAbilitiesFromLoadout());
		_hud.SetItems(BuildItemsFromLoadout());

		// ─── PLAYER UNIT (from overworld character) ─────────
		BattleUnit unitA;
		var gm = Core.GameManager.Instance;
		if (gm?.ActiveCharacter != null)
		{
			unitA = BattleUnit.FromPlayerData(gm.ActiveCharacter, UnitTeam.TeamA, new(1, 6));

			// Load passive/auto skills from loadout
			var loadout = gm.ActiveLoadout;
			if (loadout != null)
			{
				unitA.PassiveSkill1 = loadout.PassiveSkills[0];
				unitA.PassiveSkill2 = loadout.PassiveSkills[1];
				unitA.AutoSkill = loadout.AutoSkill;
			}
			unitA.ApplyPassives();

			GD.Print($"[Battle] Loaded player: {unitA.Name} HP:{unitA.MaxHp} STA:{unitA.MaxStamina} AE:{unitA.MaxAether}");
			if (unitA.PassiveSkill1 != null) GD.Print($"[Battle] Passive 1: {unitA.PassiveSkill1.Name}");
			if (unitA.PassiveSkill2 != null) GD.Print($"[Battle] Passive 2: {unitA.PassiveSkill2.Name}");
			if (unitA.AutoSkill != null) GD.Print($"[Battle] Auto: {unitA.AutoSkill.Name}");
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

		// Tick buffs/status effects
		var expired = _activeUnit.TickBuffs();
		foreach (var name in expired)
			CombatLog($"  ◎ {_activeUnit.Name}'s {name} has worn off.");

		// Process per-turn buff effects (Regen, Poison/Bleed, etc.)
		foreach (var b in _activeUnit.Buffs)
		{
			if (b.Stat == "REGEN" && b.Value > 0)
			{
				int regen = (int)(_activeUnit.MaxHp * b.Value);
				int actual = Mathf.Min(regen, _activeUnit.MaxHp - _activeUnit.CurrentHp);
				_activeUnit.CurrentHp = Mathf.Min(_activeUnit.MaxHp, _activeUnit.CurrentHp + regen);
				if (actual > 0)
				{
					CombatLog($"  ✚ {_activeUnit.Name} regenerates {actual} HP");
					_renderer.SpawnDamageNumber(_activeUnit, actual, false, isHeal: true);
					_renderer.RefreshUnitHpBar(_activeUnit);
				}
			}
			else if (b.Stat == "STATUS" && (b.Name == "Poison" || b.Name == "Bleed"))
			{
				int dot = (int)(_activeUnit.MaxHp * 0.03f);
				_activeUnit.CurrentHp = Mathf.Max(1, _activeUnit.CurrentHp - dot);
				CombatLog($"  ☠ {_activeUnit.Name} takes {dot} {b.Name} damage!");
				_renderer.SpawnDamageNumber(_activeUnit, dot, false);
				_renderer.FlashUnitHit(_activeUnit);
				_renderer.RefreshUnitHpBar(_activeUnit);
			}
		}

		// Check if stunned/sleeping/petrified — skip turn
		if (_activeUnit.HasStatus("Stun") || _activeUnit.HasStatus("Sleep") || _activeUnit.HasStatus("Petrify"))
		{
			string status = _activeUnit.HasStatus("Stun") ? "Stunned" : _activeUnit.HasStatus("Sleep") ? "Asleep" : "Petrified";
			CombatLog($"✗ {_activeUnit.Name} is {status} — turn skipped!");
			_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()} — {status.ToUpper()}!");
			CompleteCombatAction(ActionRt.Wait);
			return;
		}

		// Auto: Undying Rage (Dreadnought below 25% HP: ATK +30%)
		// Auto: Unyielding (Bulwark below 30% HP: DEF/EDEF +25%)
		// These are checked per-turn so they auto-apply/remove
		if (_activeUnit.AutoSkill?.Id == "DRD_UNDYING_RAGE" && _activeUnit.HpPercent < 0.25f)
		{
			if (!_activeUnit.Buffs.Exists(b => b.Source == "DRD_UNDYING_RAGE"))
			{
				_activeUnit.AddBuff("Undying Rage (ATK +30%)", "ATK", 0.30f, 99, "DRD_UNDYING_RAGE");
				CombatLog($"💀 {_activeUnit.Name}'s Undying Rage activates! ATK +30%!");
			}
		}
		if (_activeUnit.AutoSkill?.Id == "BLW_UNYIELDING" && _activeUnit.HpPercent < 0.30f)
		{
			if (!_activeUnit.Buffs.Exists(b => b.Source == "BLW_UNYIELDING"))
			{
				_activeUnit.AddBuff("Unyielding (DEF +25%)", "DEF", 0.25f, 99, "BLW_UNYIELDING");
				_activeUnit.AddBuff("Unyielding (EDEF +25%)", "EDEF", 0.25f, 99, "BLW_UNYIELDING_E");
				CombatLog($"🛡 {_activeUnit.Name}'s Unyielding activates! DEF/EDEF +25%!");
			}
		}

		// Auto: HP Infusion (recover MAX HP at start of turn)
		if (_activeUnit.AutoSkill?.Id == "MND_HP_INFUSION")
		{
			float pct = _activeUnit.AutoSkill.MaxRank >= 2 ? 0.05f : 0.03f;
			int regen = (int)(_activeUnit.MaxHp * pct);
			int actual = Mathf.Min(regen, _activeUnit.MaxHp - _activeUnit.CurrentHp);
			if (actual > 0)
			{
				_activeUnit.CurrentHp += actual;
				CombatLog($"  ✚ {_activeUnit.Name}'s HP Infusion: +{actual} HP");
				_renderer.SpawnDamageNumber(_activeUnit, actual, false, isHeal: true);
				_renderer.RefreshUnitHpBar(_activeUnit);
			}
		}

		ShowCommandMenu();
	}

	void ShowCommandMenu()
	{
		_state = BattleState.CommandMenu;
		_pendingMoveTile = new(-1, -1);
		_pendingMovePath = null;
		_hud.SetPhaseText($"⚔ {_activeUnit.Name.ToUpper()}'S TURN — SELECT ACTION");
		_hud.ShowCommandMenu(_activeUnit, _stats.TurnsElapsed);
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
				_stats.DefendCount++;
				CombatLog($"🛡 {_activeUnit.Name} defends. (DEF +50%, AVD +15% until next turn)");
				CompleteCombatAction(ActionRt.Defend);
				break;

			case "End Turn":
				CombatLog($"◎ {_activeUnit.Name} ends turn.");
				EndTurnWithAction(_pendingActionRt);
				break;

			case "Flee":
				// Can't flee until 8 turns have passed
				if (_stats.TurnsElapsed < 8)
				{
					CombatLog($"✗ Cannot flee yet! ({_stats.TurnsElapsed}/8 turns)");
					ShowCommandMenu();
					return;
				}
				// Flee chance = 40% + (AGI diff × 1.5)%, capped at 85%
				int fleeAgi = _activeUnit.Agility;
				int enemyAvgAgi = 0;
				int enemyCount = 0;
				foreach (var u in _units)
				{
					if (u.Team != _activeUnit.Team && u.IsAlive)
					{
						enemyAvgAgi += u.Agility;
						enemyCount++;
					}
				}
				if (enemyCount > 0) enemyAvgAgi /= enemyCount;

				float fleeChance = 40f + (fleeAgi - enemyAvgAgi) * 1.5f;
				fleeChance = Mathf.Clamp(fleeChance, 10f, 85f);
				float roll = GD.Randf() * 100f;

				if (roll < fleeChance)
				{
					CombatLog($"↺ {_activeUnit.Name} flees the battle! ({(int)fleeChance}% chance)");
					_state = BattleState.BattleOver;
					_hud.HideCommandMenu();
					_renderer.ClearAllHighlights();
					_hud.SetPhaseText("⚔ FLED FROM BATTLE");
					var fleeTimer = GetTree().CreateTimer(1.5f);
					fleeTimer.Timeout += () => ShowBattleReport(BattleReport.BattleResult.Fled);
				}
				else
				{
					CombatLog($"✗ {_activeUnit.Name} failed to flee! ({(int)fleeChance}% chance, rolled {(int)roll})");
					CompleteCombatAction(ActionRt.Defend); // wastes turn on failed flee
				}
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
		if (_state == BattleState.TargetPhase && (_pendingAbility != null || _pendingItem != null))
		{
			CancelTargeting();
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

		// Update grid occupancy immediately
		_grid.At(_activeUnit.GridPosition).Occupant = null;
		_activeUnit.GridPosition = _pendingMoveTile;
		targetTile.Occupant = _activeUnit;
		_activeUnit.HasMoved = true;
		_activeUnit.TilesMoved = moved;

		CombatLog($"→ {_activeUnit.Name} moves to ({_pendingMoveTile.X},{_pendingMoveTile.Y}) [{moved} tiles]");

		var path = _pendingMovePath;
		_pendingMoveTile = new(-1, -1);
		_pendingMovePath = null;
		_renderer.ClearMovePreview();
		_hud.HideMoveConfirm();

		// Animate movement along the path, then continue
		if (path != null && path.Count > 0)
		{
			_state = BattleState.Animating;
			_renderer.AnimateMoveAlongPath(_activeUnit, path, () =>
			{
				CallDeferred(nameof(OnMoveAnimComplete));
			});
		}
		else
		{
			_renderer.MoveUnitVisual(_activeUnit);
			CheckAutoEndTurn();
		}
	}

	void OnMoveAnimComplete()
	{
		if (_state != BattleState.Animating) return;
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
		var abilities = _loadoutAbilities;
		if (index < 0 || index >= abilities.Count) return;
		var ab = abilities[index];
		if (!ab.CanAfford(_activeUnit.CurrentStamina, _activeUnit.CurrentAether))
		{
			CombatLog($"✗ {_activeUnit.Name} can't afford {ab.Name}! ({ab.CostString()})");
			return;
		}

		// Self-buff/utility: execute immediately on self
		if (ab.Intent == AbilityIntent.BuffSelf || ab.TargetType == "Self")
		{
			ExecuteAbility(ab, _activeUnit, _activeUnit);
			return;
		}

		// Determine target mode from INTENT
		_targetMode = ab.Intent switch
		{
			AbilityIntent.HealAlly => TargetMode.Ally,
			AbilityIntent.BuffAlly => TargetMode.Ally,
			AbilityIntent.DebuffEnemy => TargetMode.Enemy,
			AbilityIntent.DamageEnemy => TargetMode.Enemy,
			AbilityIntent.Utility => TargetMode.AnyTile,
			_ => TargetMode.Enemy,
		};

		// Store pending and enter target phase
		_pendingAbility = ab;
		_pendingAbilityIndex = index;
		_state = BattleState.TargetPhase;

		string modeLabel = _targetMode switch
		{
			TargetMode.Ally => "SELECT ALLY",
			TargetMode.AnyTile => "SELECT TILE",
			_ => "SELECT TARGET",
		};
		_hud.SetPhaseText($"✦ {_activeUnit.Name.ToUpper()} — {ab.Name} — {modeLabel}");
		_hud.HideCommandMenu();
		_renderer.ClearAllHighlights();

		// Show range tiles
		int range = ab.Range > 0 ? ab.Range : 1;
		int minRange = ab.TargetType == "Line" ? 1 : 0;
		var rangeArea = _grid.GetAttackRange(_activeUnit.GridPosition, minRange, range);
		if (_targetMode == TargetMode.Ally)
			_renderer.ShowMoveRange(rangeArea); // blue for ally
		else
			_renderer.ShowAttackRange(rangeArea); // red for enemy
	}

	void OnItemSelected(int index)
	{
		if (index < 0 || index >= _loadoutItems.Count) return;
		var item = _loadoutItems[index];
		if (!item.IsUsable) { CombatLog($"✗ {item.Name} already used!"); return; }

		if (item.TargetType == "Self")
		{
			ExecuteItem(item, index, _activeUnit);
			return;
		}

		// Enter targeting for items
		_pendingItem = item;
		_pendingItemIndex = index;
		_targetMode = TargetMode.Ally; // most consumables target allies
		_state = BattleState.TargetPhase;
		_hud.SetPhaseText($"◆ {_activeUnit.Name.ToUpper()} — {item.Name} — SELECT TARGET");
		_hud.HideCommandMenu();
		_renderer.ClearAllHighlights();
		var rangeArea = _grid.GetAttackRange(_activeUnit.GridPosition, 0, 3);
		_renderer.ShowMoveRange(rangeArea);
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
		if (tile == null || _activeUnit == null || _state == BattleState.BattleOver || _state == BattleState.Animating) return;

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
			// Clicking self cancels targeting (unless ability targets self)
			if (pos == _activeUnit.GridPosition)
			{
				// Allow self-targeting for ally abilities (heal self, buff self)
				if (_pendingAbility != null && _targetMode == TargetMode.Ally && tile.Occupant == _activeUnit)
				{
					ExecuteAbility(_pendingAbility, _activeUnit, _activeUnit);
					return;
				}
				CancelTargeting();
				return;
			}

			int dist = Mathf.Abs(pos.X - _activeUnit.GridPosition.X) + Mathf.Abs(pos.Y - _activeUnit.GridPosition.Y);

			// ── ABILITY TARGETING ──
			if (_pendingAbility != null)
			{
				int range = _pendingAbility.Range > 0 ? _pendingAbility.Range : 1;
				if (dist > range) { CombatLog("✗ Out of range!"); return; }

				if (_targetMode == TargetMode.Enemy)
				{
					if (tile.Occupant == null || tile.Occupant.Team == _activeUnit.Team)
					{ CombatLog("✗ Must target an enemy!"); return; }
					ExecuteAbility(_pendingAbility, _activeUnit, tile.Occupant);
				}
				else if (_targetMode == TargetMode.Ally)
				{
					if (tile.Occupant == null || tile.Occupant.Team != _activeUnit.Team)
					{ CombatLog("✗ Must target an ally!"); return; }
					ExecuteAbility(_pendingAbility, _activeUnit, tile.Occupant);
				}
				else if (_targetMode == TargetMode.AnyTile)
				{
					// Utility: place on tile (trap, barricade, etc.)
					var utilTarget = tile.Occupant ?? _activeUnit;
					ExecuteAbility(_pendingAbility, _activeUnit, utilTarget);
				}
				return;
			}

			// ── ITEM TARGETING ──
			if (_pendingItem != null)
			{
				if (dist > 3) { CombatLog("✗ Out of range!"); return; }
				if (tile.Occupant == null) return;
				ExecuteItem(_pendingItem, _pendingItemIndex, tile.Occupant);
				return;
			}

			// ── BASIC ATTACK ──
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
		// Apply buff modifiers to basic attack
		float atkMod = atk.GetBuffMod("ATK");
		float defMod = def.GetBuffMod("DEF");
		float raw = atk.Atk * atkMod * 1.5f;
		float defValue = def.Def * defMod * (def.IsDefending ? 1.5f : 1.0f);
		int dmg = (int)Mathf.Max(raw - defValue * 0.8f, 1);
		dmg = Mathf.Min(dmg, (int)(def.MaxHp * 0.6f));

		// Damage reduction buffs on defender
		foreach (var b in def.Buffs)
			if (b.Stat == "DMGREDUCE") dmg = (int)(dmg * (1f - b.Value));
		if (def.NextAttackDmgReduction > 0)
		{
			dmg = (int)(dmg * (1f - def.NextAttackDmgReduction));
			def.DmgReductionTurns--;
			if (def.DmgReductionTurns <= 0) def.NextAttackDmgReduction = 0;
		}

		// Shield absorb
		var shield = def.Buffs.Find(b => b.Stat == "SHIELD" && b.ShieldHp > 0);
		if (shield != null)
		{
			int absorbed = Mathf.Min(dmg, shield.ShieldHp);
			shield.ShieldHp -= absorbed;
			dmg -= absorbed;
			CombatLog($"🛡 Shield absorbs {absorbed} damage! ({shield.ShieldHp} remaining)");
			if (shield.ShieldHp <= 0) def.Buffs.Remove(shield);
		}

		// AUTO: Parry
		var rng = new System.Random();
		if (def.AutoParryChance > 0 && rng.NextDouble() < def.AutoParryChance)
		{
			int mitigated = dmg / 2;
			dmg -= mitigated;
			CombatLog($"🛡 {def.Name} parries! (-{mitigated} dmg)");
			_stats.ParryCount++;
		}

		// Track defend mitigation
		if (def.IsDefending)
		{
			int baseDmg = (int)Mathf.Max(atk.Atk * atkMod * 1.5f - def.Def * defMod * 0.8f, 1);
			int mitigated = Mathf.Max(baseDmg - dmg, 0);
			_stats.DamageMitigatedByDefend += mitigated;
		}

		// Dodge check — guaranteed hit from self-buffs bypasses
		bool guaranteedHit = atk.NextAttackGuaranteedHit;
		if (atk.NextAttackGuaranteedHit) atk.NextAttackGuaranteedHit = false;

		float dodge = 0;
		if (!guaranteedHit)
		{
			dodge = Mathf.Clamp((def.Avd * 0.4f + def.Agility * 0.2f - atk.Acc * 0.3f) / 100f, 0f, 0.75f);
			if (def.IsDefending) dodge = Mathf.Min(dodge + 0.15f, 0.75f);
		}

		if (!guaranteedHit && rng.NextDouble() < dodge)
		{
			CombatLog($"↺ {def.Name} dodged {atk.Name}'s attack!");
			_renderer.SpawnDamageNumber(def, 0, false, isDodge: true);
			_stats.RecordDodge(def);
			return;
		}

		// Guaranteed crit from self-buffs (Mighty Impact)
		bool guaranteedCrit = atk.NextAttackGuaranteedCrit;
		if (atk.NextAttackGuaranteedCrit) atk.NextAttackGuaranteedCrit = false;
		bool crit = guaranteedCrit || rng.NextDouble() * 100 < atk.CritPercent;
		if (crit) dmg = (int)(dmg * 1.5f);

		// Hit count from self-buffs (Double Strike = 2 hits)
		int hitCount = atk.NextAttackHitCount;
		atk.NextAttackHitCount = 1; // consume
		int totalDmg = dmg;
		if (hitCount > 1)
		{
			totalDmg = dmg * hitCount;
			CombatLog($"  ⚔ {hitCount}x hit!");
		}

		// ── AUTO: Iron Will (survive lethal at 1 HP, once per battle) ──
		if (def.CurrentHp - totalDmg <= 0 && def.AutoIronWill && !def.IronWillUsed && def.HpPercent > 0.25f)
		{
			totalDmg = def.CurrentHp - 1;
			def.IronWillUsed = true;
			CombatLog($"💪 {def.Name}'s Iron Will activates! Survived at 1 HP!");
			_stats.IronWillProcs++;
		}

		def.CurrentHp = Mathf.Max(0, def.CurrentHp - totalDmg);

		// ── AUTO: Reflect Damage ──
		if (def.AutoReflectDmg > 0 && def.IsAlive)
		{
			int reflected = (int)(totalDmg * def.AutoReflectDmg);
			if (reflected > 0)
			{
				atk.CurrentHp = Mathf.Max(0, atk.CurrentHp - reflected);
				CombatLog($"↩ {def.Name} reflects {reflected} dmg back to {atk.Name}!");
				_renderer.SpawnDamageNumber(atk, reflected, false);
			}
		}

		// Track stats
		_stats.RecordDamage(atk, totalDmg, "Basic Strike", crit);

		string critTag = crit ? " CRIT!" : "";
		string defendTag = def.IsDefending ? " [Defending]" : "";
		string hitTag = hitCount > 1 ? $" ({hitCount}x)" : "";
		CombatLog($"⚔ {atk.Name} attacks {def.Name} with Basic Strike →{critTag}{defendTag}{hitTag} {totalDmg} dmg ({def.CurrentHp}/{def.MaxHp} HP)");

		// Visual feedback
		_renderer.SpawnDamageNumber(def, totalDmg, crit);
		_renderer.FlashUnitHit(def);
		_renderer.RefreshUnitHpBar(def);
		_hud.UpdateUnitInfo(_activeUnit);

		if (!def.IsAlive)
		{
			CombatLog($"💀 {def.Name} has been defeated!");
			_stats.RecordKill(atk, def);

			var defTile = _grid.At(def.GridPosition);
			if (defTile != null) defTile.Occupant = null;
			_renderer.RemoveUnitVisual(def);

			// ── AUTO: Dread Harvest (recover aether on nearby kill) ──
			CheckDreadHarvest(def);
			return; // dead — no counter
		}

		// ── AUTO: Counterattack (after surviving melee hit) ──
		if (def.AutoCounterChance > 0 && rng.NextDouble() < def.AutoCounterChance)
		{
			// Check adjacency
			int dist = Mathf.Abs(def.GridPosition.X - atk.GridPosition.X) + Mathf.Abs(def.GridPosition.Y - atk.GridPosition.Y);
			if (dist <= 1 && atk.IsAlive)
			{
				int counterDmg = (int)Mathf.Max(def.Atk * 1.0f - atk.Def * 0.6f, 1);
				atk.CurrentHp = Mathf.Max(0, atk.CurrentHp - counterDmg);
				CombatLog($"⚔ {def.Name} counterattacks {atk.Name}! → {counterDmg} dmg ({atk.CurrentHp}/{atk.MaxHp} HP)");
				_renderer.SpawnDamageNumber(atk, counterDmg, false);
				_renderer.FlashUnitHit(atk);
				_renderer.RefreshUnitHpBar(atk);
				_stats.CounterattackCount++;

				if (!atk.IsAlive)
				{
					CombatLog($"💀 {atk.Name} killed by counterattack!");
					_stats.RecordKill(def, atk);
					var atkTile = _grid.At(atk.GridPosition);
					if (atkTile != null) atkTile.Occupant = null;
					_renderer.RemoveUnitVisual(atk);
				}
			}
		}
	}

	/// <summary>Check all units for Dread Harvest (recover aether when enemy dies within 3 tiles).</summary>
	void CheckDreadHarvest(BattleUnit deadUnit)
	{
		foreach (var u in _units)
		{
			if (!u.IsAlive || u.Team == deadUnit.Team || !u.AutoDreadHarvest) continue;
			int dist = Mathf.Abs(u.GridPosition.X - deadUnit.GridPosition.X) + Mathf.Abs(u.GridPosition.Y - deadUnit.GridPosition.Y);
			if (dist <= 3)
			{
				int restore = 20;
				u.CurrentAether = Mathf.Min(u.MaxAether, u.CurrentAether + restore);
				CombatLog($"🔮 {u.Name}'s Dread Harvest: +{restore} Aether!");
			}
		}
	}

	void CancelTargeting()
	{
		_pendingAbility = null;
		_pendingAbilityIndex = -1;
		_pendingItem = null;
		_pendingItemIndex = -1;
		_targetMode = TargetMode.None;
		_renderer.ClearAllHighlights();
		ShowCommandMenu();
	}

	// ═══════════════════════════════════════════════════════
	//  ABILITY EXECUTION
	// ═══════════════════════════════════════════════════════

	void ExecuteAbility(AbilityInfo ab, BattleUnit caster, BattleUnit target)
	{
		// Spend resources (check Conserve Aether auto)
		var rng = new System.Random();
		bool freecast = false;
		if (caster.AutoSkill?.Id == "EVO_CONSERVE_AETHER" && ab.ResourceType == ResourceType.Aether)
			freecast = rng.NextDouble() < 0.15;

		if (!freecast)
		{
			caster.SpendResource(ab);
			if (ab.ResourceType == ResourceType.Stamina || ab.ResourceType == ResourceType.Both)
				_stats.RecordStaminaSpent(ab.StaminaCost);
			if (ab.ResourceType == ResourceType.Aether || ab.ResourceType == ResourceType.Both)
				_stats.RecordAetherSpent(ab.AetherCost);
		}
		else
			CombatLog($"✦ {caster.Name}'s Conserve Aether triggers — free cast!");

		_renderer.ClearAllHighlights();

		// Load effects from registry
		var effects = SkillEffectRegistry.GetEffects(ab.SourceId);
		bool hasEffect(SkillEffectType t) => effects?.Exists(e => e.Type == t) ?? false;
		SkillEffect getEffect(SkillEffectType t) => effects?.Find(e => e.Type == t);

		switch (ab.Intent)
		{
			case AbilityIntent.BuffSelf:
				ApplySelfBuff(ab, caster, effects, rng);
				break;

			case AbilityIntent.BuffAlly:
				ApplyAllyBuff(ab, caster, target, effects, rng);
				break;

			case AbilityIntent.HealAlly:
				ApplyHeal(ab, caster, target, effects, rng);
				break;

			case AbilityIntent.DebuffEnemy:
				ApplyDebuff(ab, caster, target, effects, rng);
				break;

			case AbilityIntent.Utility:
				ApplyUtility(ab, caster, target, effects, rng);
				break;

			case AbilityIntent.DamageEnemy:
				ApplyDamage(ab, caster, target, effects, rng);
				break;
		}

		_hud.UpdateUnitInfo(_activeUnit);
		_pendingAbility = null;
		_pendingAbilityIndex = -1;
		_targetMode = TargetMode.None;

		// Auto: Conserve RT
		int rtCost = ab.RtCost;
		if (caster.AutoConserveRt > 0 && ab.Range > 1)
			rtCost = Mathf.Max(0, rtCost - caster.AutoConserveRt);
		// Auto: Quickcast (spells cost 5 less RT)
		if (caster.AutoSkill?.Id == "EVO_QUICKCAST" && ab.Category == SkillCategory.Ether)
			rtCost = Mathf.Max(0, rtCost - 5);
		CompleteCombatAction(rtCost);
	}

	// ═══════════════════════════════════════════════════════════════
	//  EFFECT PROCESSORS
	// ═══════════════════════════════════════════════════════════════

	void ApplySelfBuff(AbilityInfo ab, BattleUnit caster, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		CombatLog($"↑ {caster.Name} uses {ab.Name}!");
		_renderer.SpawnDamageNumber(caster, 0, false);

		if (effects == null || effects.Count == 0) return;

		foreach (var fx in effects)
		{
			switch (fx.Type)
			{
				case SkillEffectType.GuaranteedCrit:
					caster.NextAttackGuaranteedCrit = true;
					CombatLog($"  → Next attack is a guaranteed critical!");
					break;
				case SkillEffectType.GuaranteedHit:
					caster.NextAttackGuaranteedHit = true;
					CombatLog($"  → Next attack cannot miss!");
					break;
				case SkillEffectType.HitCount:
					caster.NextAttackHitCount = (int)fx.Value;
					CombatLog($"  → Next attack strikes {(int)fx.Value}x!");
					break;
				case SkillEffectType.SpellDmgBonus:
					caster.NextSpellDmgBonus = fx.Value;
					CombatLog($"  → Next spell +{(int)(fx.Value*100)}% damage!");
					break;
				case SkillEffectType.GuaranteedStatusOnHit:
					caster.NextAttackGuaranteedStatus = true;
					CombatLog($"  → Next attack guarantees status effect!");
					break;
				case SkillEffectType.RangeBonus:
					CombatLog($"  → Range +{(int)fx.Value} for next attack!");
					break;
				case SkillEffectType.BlockCounterattack:
					CombatLog($"  → Cannot counterattack this turn.");
					break;
				case SkillEffectType.AtkBuff:
					caster.AddBuff($"ATK {(fx.Value>0?"+":"")}{(int)(fx.Value*100)}%", "ATK", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → ATK {(fx.Value>0?"+":"")}{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.DefBuff:
					caster.AddBuff($"DEF {(fx.Value>0?"+":"")}{(int)(fx.Value*100)}%", "DEF", fx.Value, (int)fx.Value2, ab.SourceId + "_DEF");
					CombatLog($"  → DEF {(fx.Value>0?"+":"")}{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.EdefBuff:
					caster.AddBuff($"EDEF +{(int)(fx.Value*100)}%", "EDEF", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → EDEF +{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.DmgReduction:
					caster.NextAttackDmgReduction = fx.Value;
					caster.DmgReductionTurns = (int)fx.Value2;
					CombatLog($"  → Damage taken reduced by {(int)(fx.Value*100)}% for {(int)fx.Value2} turn(s)");
					break;
				case SkillEffectType.Stealth:
					caster.AddBuff("Stealth", "STATUS", 0, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → Invisible for {(int)fx.Value2} turns!");
					break;
				case SkillEffectType.ElementEnchant:
					string elName = fx.Param ?? "Aether";
					caster.AddBuff($"Enchant: {elName}", "ENCHANT", 0, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → Weapon enchanted with {elName} for {(int)fx.Value2} turns!");
					break;
				case SkillEffectType.MeditateSelf:
				{
					int restore = (int)fx.Value;
					caster.CurrentAether = Mathf.Min(caster.MaxAether, caster.CurrentAether + restore);
					CombatLog($"  → Restored {restore} Aether! ({caster.CurrentAether}/{caster.MaxAether})");
					break;
				}
				case SkillEffectType.SelfHealMndScale:
				{
					int heal = (int)(caster.Mind * fx.Value) + ab.Power;
					int actual = Mathf.Min(heal, caster.MaxHp - caster.CurrentHp);
					caster.CurrentHp = Mathf.Min(caster.MaxHp, caster.CurrentHp + heal);
					CombatLog($"  → Healed self for {actual} HP ({caster.CurrentHp}/{caster.MaxHp})");
					_renderer.SpawnDamageNumber(caster, actual, false, isHeal: true);
					_renderer.RefreshUnitHpBar(caster);
					_stats.HealingDone += actual;
					break;
				}
			}
		}
	}

	void ApplyAllyBuff(AbilityInfo ab, BattleUnit caster, BattleUnit target, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		CombatLog($"↑ {caster.Name} uses {ab.Name} on {target.Name}!");
		_renderer.SpawnDamageNumber(target, 0, false);

		if (effects == null) return;
		foreach (var fx in effects)
		{
			switch (fx.Type)
			{
				case SkillEffectType.AllyAtkBuff:
					target.AddBuff($"ATK +{(int)(fx.Value*100)}%", "ATK", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} ATK +{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.AllyDefBuff:
					target.AddBuff($"DEF +{(int)(fx.Value*100)}%", "DEF", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} DEF +{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.AllyHaste:
					target.AddBuff($"Haste (RT -{(int)fx.Value})", "RT", -fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} gains Haste (RT -{(int)fx.Value}) for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.AllyDmgReduction:
					target.AddBuff($"Protection ({(int)(fx.Value*100)}%)", "DMGREDUCE", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} takes {(int)(fx.Value*100)}% less damage for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.AllyShield:
				{
					int shieldHp = (int)(target.MaxHp * fx.Value);
					var buff = new ActiveBuff { Name = $"Shield ({shieldHp} HP)", Stat = "SHIELD", Value = 0, TurnsLeft = 99, Source = ab.SourceId, ShieldHp = shieldHp };
					target.Buffs.RemoveAll(b => b.Stat == "SHIELD");
					target.Buffs.Add(buff);
					CombatLog($"  → {target.Name} gains a shield absorbing {shieldHp} damage!");
					break;
				}
				case SkillEffectType.AllyRtReset:
					target.CurrentRt = 0;
					CombatLog($"  → {target.Name}'s RT reset to 0!");
					break;
				case SkillEffectType.AetherRestore:
				{
					int restore = (int)fx.Value;
					target.CurrentAether = Mathf.Min(target.MaxAether, target.CurrentAether + restore);
					CombatLog($"  → {target.Name} restores {restore} Aether ({target.CurrentAether}/{target.MaxAether})");
					break;
				}
				case SkillEffectType.ReduceTargetRt:
					target.CurrentRt = Mathf.Max(0, target.CurrentRt - (int)fx.Value);
					CombatLog($"  → {target.Name}'s RT reduced by {(int)fx.Value}!");
					break;
				case SkillEffectType.AllyHpRegen:
					target.AddBuff($"Regen {(int)(fx.Value*100)}%", "REGEN", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} gains HP regen for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.RemoveDebuff:
					if (target.RemoveOneDebuff())
						CombatLog($"  → Removed 1 debuff from {target.Name}!");
					else
						CombatLog($"  → {target.Name} has no debuffs to remove.");
					break;
				case SkillEffectType.RemoveAllDebuffs:
					target.RemoveAllDebuffs();
					CombatLog($"  → Purified all debuffs from {target.Name}!");
					break;
				case SkillEffectType.ElementEnchant:
				{
					string elName = fx.Param ?? "Aether";
					target.AddBuff($"Enchant: {elName}", "ENCHANT", 0, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name}'s weapon enchanted with {elName} for {(int)fx.Value2} turns!");
					break;
				}
				case SkillEffectType.EdefBuff:
					target.AddBuff($"EDEF +{(int)(fx.Value*100)}%", "EDEF", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} EDEF +{(int)(fx.Value*100)}% for {(int)fx.Value2} turns");
					break;
				case SkillEffectType.DmgReduction:
					target.AddBuff($"Protection ({(int)(fx.Value*100)}%)", "DMGREDUCE", fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} takes {(int)(fx.Value*100)}% less damage for {(int)fx.Value2} turns");
					break;
			}
		}
	}

	void ApplyHeal(AbilityInfo ab, BattleUnit caster, BattleUnit target, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		// Base heal formula
		float healScale = ab.Power * (caster.Mind * 0.5f + caster.EtherControl * 0.3f) / 10f;
		int heal = Mathf.Max((int)healScale, Mathf.Max(ab.Power, 1));

		// Healing Arts passive bonus
		var loadout = Core.GameManager.Instance?.ActiveLoadout;
		if (loadout != null)
		{
			var p1 = loadout.PassiveSkills[0];
			var p2 = loadout.PassiveSkills[1];
			if (p1?.Id == "MND_HEALING_ARTS") heal = (int)(heal * (1f + p1.MaxRank * 0.10f));
			if (p2?.Id == "MND_HEALING_ARTS") heal = (int)(heal * (1f + p2.MaxRank * 0.10f));
		}

		int actualHeal = Mathf.Min(heal, target.MaxHp - target.CurrentHp);
		target.CurrentHp = Mathf.Min(target.MaxHp, target.CurrentHp + heal);

		CombatLog($"✚ {caster.Name} casts {ab.Name} on {target.Name} → +{actualHeal} HP ({target.CurrentHp}/{target.MaxHp})");
		_renderer.SpawnDamageNumber(target, actualHeal, false, isHeal: true);
		_renderer.RefreshUnitHpBar(target);
		_stats.HealingDone += actualHeal;

		// Process additional heal effects
		if (effects != null)
		{
			foreach (var fx in effects)
			{
				switch (fx.Type)
				{
					case SkillEffectType.RemoveDebuff:
						if (target.RemoveOneDebuff())
							CombatLog($"  → Removed 1 debuff from {target.Name}!");
						break;
					case SkillEffectType.RemoveAllDebuffs:
						target.RemoveAllDebuffs();
						CombatLog($"  → Purified all debuffs!");
						break;
					case SkillEffectType.Revive:
						// TODO: target selection for dead allies
						CombatLog($"  → Revive effect ready (needs KO ally target).");
						break;
				}
			}
		}
	}

	void ApplyDebuff(AbilityInfo ab, BattleUnit caster, BattleUnit target, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		CombatLog($"↓ {caster.Name} uses {ab.Name} on {target.Name}!");
		_renderer.FlashUnitHit(target);

		if (effects == null || effects.Count == 0)
		{
			_renderer.SpawnDamageNumber(target, 0, false);
			return;
		}

		foreach (var fx in effects)
		{
			switch (fx.Type)
			{
				case SkillEffectType.ReduceStat:
				{
					string stat = fx.Param ?? "ATK";
					if (stat == "HIGHEST")
					{
						// Find highest stat
						int max = Mathf.Max(target.Atk, Mathf.Max(target.Def, Mathf.Max(target.Eatk, target.Edef)));
						stat = max == target.Atk ? "ATK" : max == target.Def ? "DEF" : max == target.Eatk ? "EATK" : "EDEF";
					}
					target.AddBuff($"{stat} -{(int)(fx.Value*100)}%", stat, -fx.Value, (int)fx.Value2, ab.SourceId + "_" + stat);
					CombatLog($"  → {target.Name}'s {stat} reduced by {(int)(fx.Value*100)}% for {(int)fx.Value2} turns!");
					break;
				}
				case SkillEffectType.InflictStatus:
				{
					float chance = fx.Value;
					// Status resist check
					float resist = target.StatusResist * 0.01f;
					if (rng.NextDouble() < chance * (1f - resist))
					{
						target.AddBuff(fx.Param, "STATUS", 0, (int)fx.Value2, ab.SourceId);
						CombatLog($"  → {target.Name} is {fx.Param}ed for {(int)fx.Value2} turns!");
					}
					else
						CombatLog($"  → {target.Name} resisted {fx.Param}!");
					break;
				}
				case SkillEffectType.ReduceMovement:
					target.AddBuff($"MOVE -{(int)fx.Value}", "MOVE", -fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name}'s MOVE reduced by {(int)fx.Value} for {(int)fx.Value2} turns!");
					break;
				case SkillEffectType.AddTargetRt:
					target.CurrentRt += (int)fx.Value;
					CombatLog($"  → {target.Name}'s RT increased by {(int)fx.Value}!");
					break;
				case SkillEffectType.StripBuffs:
					int removed = target.Buffs.RemoveAll(b => b.Value > 0 && b.Stat != "STATUS");
					CombatLog($"  → Stripped {removed} buff(s) from {target.Name}!");
					break;
				case SkillEffectType.PreventRevive:
					target.AddBuff("Condemned", "STATUS", 0, 99, ab.SourceId);
					CombatLog($"  → {target.Name} is Condemned — cannot be revived!");
					break;
				case SkillEffectType.Provoke:
					target.AddBuff("Provoked", "STATUS", 0, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name} is Provoked! Must attack {caster.Name} for {(int)fx.Value2} turns!");
					break;
				case SkillEffectType.AetherSteal:
				{
					int steal = (int)fx.Value;
					int actual = Mathf.Min(steal, target.CurrentAether);
					target.CurrentAether -= actual;
					caster.CurrentAether = Mathf.Min(caster.MaxAether, caster.CurrentAether + actual);
					CombatLog($"  → Stole {actual} Aether from {target.Name}!");
					break;
				}
			}
		}
		_renderer.SpawnDamageNumber(target, 0, false);
	}

	void ApplyUtility(AbilityInfo ab, BattleUnit caster, BattleUnit target, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		CombatLog($"⚙ {caster.Name} uses {ab.Name}!");
		// TODO: Barricade placement, trap placement, analyze reveal
	}

	void ApplyDamage(AbilityInfo ab, BattleUnit caster, BattleUnit target, System.Collections.Generic.List<SkillEffect> effects, System.Random rng)
	{
		bool hasEffect(SkillEffectType t) => effects?.Exists(e => e.Type == t) ?? false;
		SkillEffect getEffect(SkillEffectType t) => effects?.Find(e => e.Type == t);
		var breakdown = new System.Text.StringBuilder();

		// ── Stat scaling ──
		float atkStat, defStat;
		bool scaleVit = hasEffect(SkillEffectType.ScaleOffVit);
		string scalingType;

		if (ab.Category == SkillCategory.Ether)
		{
			atkStat = caster.EtherControl * 0.6f + caster.Mind * 0.4f;
			defStat = target.Edef * (target.IsDefending ? 1.5f : 1.0f);
			scalingType = "Ether";
		}
		else if (ab.Category == SkillCategory.Hybrid)
		{
			atkStat = (caster.Atk + caster.EtherControl) * 0.5f;
			defStat = (target.Def + target.Edef) * 0.5f * (target.IsDefending ? 1.5f : 1.0f);
			scalingType = "Hybrid";
		}
		else
		{
			atkStat = scaleVit ? caster.Vitality * 2.0f : (float)caster.Atk;
			defStat = target.Def * (target.IsDefending ? 1.5f : 1.0f);
			scalingType = scaleVit ? "VIT-scaled" : "Physical";
		}
		breakdown.Append($"  ├ {scalingType}: ATK {atkStat:F0} vs DEF {defStat:F0}");

		// Apply buff modifiers
		float atkMod = caster.GetBuffMod("ATK");
		if (atkMod != 1f)
		{
			atkStat *= atkMod;
			breakdown.Append($" (ATK buff {(atkMod > 1 ? "+" : "")}{(int)((atkMod - 1) * 100)}%)");
		}

		float defMod = ab.Category == SkillCategory.Ether ? target.GetBuffMod("EDEF") : target.GetBuffMod("DEF");
		if (defMod != 1f)
		{
			defStat *= defMod;
			breakdown.Append($" (DEF mod {(int)((defMod - 1) * 100)}%)");
		}

		// Spell damage bonus
		float spellBonus = 1f;
		if (ab.Category == SkillCategory.Ether && caster.NextSpellDmgBonus > 0)
		{
			spellBonus += caster.NextSpellDmgBonus;
			breakdown.Append($"\n  ├ +Elemental Surge: +{(int)(caster.NextSpellDmgBonus * 100)}% spell dmg");
			caster.NextSpellDmgBonus = 0;
		}

		// DEF ignore
		var ignDef = getEffect(SkillEffectType.IgnoreDefPercent);
		if (ignDef != null)
		{
			defStat *= (1f - ignDef.Value);
			breakdown.Append($"\n  ├ +Ignore DEF: {(int)(ignDef.Value * 100)}% pierce");
		}

		// Base damage calc
		float raw = (ab.Power * 0.5f + atkStat * 1.2f) * spellBonus;
		int dmg = (int)Mathf.Max(raw - defStat * 0.8f, 1);
		dmg = Mathf.Min(dmg, (int)(target.MaxHp * 0.6f));
		int baseDmg = dmg;
		breakdown.Append($"\n  ├ Base: POW {ab.Power}×0.5 + ATK×1.2 = {(int)raw} - DEF×0.8 = {baseDmg}");

		// Bonus vs element
		var bonusEl = getEffect(SkillEffectType.BonusVsElement);
		if (bonusEl != null)
		{
			int before = dmg;
			dmg = (int)(dmg * (1f + bonusEl.Value));
			breakdown.Append($"\n  ├ +Element bonus ({bonusEl.Param}): +{(int)(bonusEl.Value * 100)}% → {dmg}");
		}

		// Bonus missing HP
		var bonusMissing = getEffect(SkillEffectType.BonusMissingHpPercent);
		if (bonusMissing != null)
		{
			float missingPct = 1f - target.HpPercent;
			int bonus = (int)(dmg * missingPct * bonusMissing.Value);
			dmg += bonus;
			breakdown.Append($"\n  ├ +Missing HP bonus: {(int)(missingPct * 100)}% → +{bonus}");
		}

		// Damage reduction buffs
		foreach (var b in target.Buffs)
		{
			if (b.Stat == "DMGREDUCE")
			{
				int before = dmg;
				dmg = (int)(dmg * (1f - b.Value));
				breakdown.Append($"\n  ├ -{b.Name}: -{before - dmg}");
			}
		}
		if (target.NextAttackDmgReduction > 0)
		{
			int before = dmg;
			dmg = (int)(dmg * (1f - target.NextAttackDmgReduction));
			breakdown.Append($"\n  ├ -Dmg Reduction: -{before - dmg}");
			target.DmgReductionTurns--;
			if (target.DmgReductionTurns <= 0) target.NextAttackDmgReduction = 0;
		}

		// Shield absorb
		var shield = target.Buffs.Find(b => b.Stat == "SHIELD" && b.ShieldHp > 0);
		if (shield != null)
		{
			int absorbed = Mathf.Min(dmg, shield.ShieldHp);
			shield.ShieldHp -= absorbed;
			dmg -= absorbed;
			breakdown.Append($"\n  ├ -Shield absorb: -{absorbed} ({shield.ShieldHp} left)");
			if (shield.ShieldHp <= 0) target.Buffs.Remove(shield);
		}

		// Defend mitigation tracking
		if (target.IsDefending)
		{
			float rawUndefended = ab.Category == SkillCategory.Ether
				? (float)target.Edef : ab.Category == SkillCategory.Hybrid
				? (target.Def + target.Edef) * 0.5f : (float)target.Def;
			int undefDmg = (int)Mathf.Max(ab.Power * 0.5f + atkStat * 1.2f - rawUndefended * 0.8f, 1);
			_stats.DamageMitigatedByDefend += Mathf.Max(undefDmg - dmg, 0);
			breakdown.Append($"\n  ├ -Defending: DEF×1.5, AVD+15%");
		}

		// Parry
		if (target.AutoParryChance > 0 && ab.Category != SkillCategory.Ether && ab.Range <= 1)
		{
			if (rng.NextDouble() < target.AutoParryChance)
			{
				int mitigated = dmg / 2;
				dmg -= mitigated;
				breakdown.Append($"\n  ├ -Parry: -{mitigated}");
				_stats.ParryCount++;
			}
		}

		// Hit / Dodge
		bool guaranteedHit = caster.NextAttackGuaranteedHit || hasEffect(SkillEffectType.GuaranteedHit);
		if (caster.NextAttackGuaranteedHit) caster.NextAttackGuaranteedHit = false;

		float dodge = 0;
		if (!guaranteedHit)
		{
			dodge = Mathf.Clamp((target.Avd * 0.3f + target.Agility * 0.15f - caster.Acc * 0.3f) / 100f, 0f, 0.5f);
			if (target.IsDefending) dodge = Mathf.Min(dodge + 0.1f, 0.5f);
			if (ab.Category == SkillCategory.Ether) dodge *= 0.5f;
			if (target.AutoSidestepChance > 0 && ab.Range > 1)
				dodge = Mathf.Min(dodge + target.AutoSidestepChance, 0.6f);
		}
		else
			breakdown.Append($"\n  ├ +Guaranteed hit!");

		if (!guaranteedHit && rng.NextDouble() < dodge)
		{
			CombatLog($"↺ {target.Name} evaded {caster.Name}'s {ab.Name}! ({(int)(dodge * 100)}% dodge)");
			_renderer.SpawnDamageNumber(target, 0, false, isDodge: true);
			_stats.RecordDodge(target);
			return;
		}

		// Crit
		bool guaranteedCrit = caster.NextAttackGuaranteedCrit;
		if (caster.NextAttackGuaranteedCrit) caster.NextAttackGuaranteedCrit = false;
		bool crit = guaranteedCrit || rng.NextDouble() * 100 < caster.CritPercent;
		if (crit)
		{
			dmg = (int)(dmg * 1.5f);
			breakdown.Append($"\n  ├ +CRIT: ×1.5 → {dmg}");
		}

		// Iron Will
		if (target.CurrentHp - dmg <= 0 && target.AutoIronWill && !target.IronWillUsed && target.HpPercent > 0.25f)
		{
			dmg = target.CurrentHp - 1;
			target.IronWillUsed = true;
			breakdown.Append($"\n  ├ ★Iron Will: survived at 1 HP!");
			_stats.IronWillProcs++;
		}

		target.CurrentHp = Mathf.Max(0, target.CurrentHp - dmg);
		_stats.RecordDamage(caster, dmg, ab.Name, crit);

		// Main hit line
		string critTag = crit ? " CRIT!" : "";
		string elTag = ab.Element != Element.None ? $" [{ab.Element}]" : "";
		string defendTag = target.IsDefending ? " [Defending]" : "";
		CombatLog($"✦ {caster.Name} → {ab.Name}{elTag} on {target.Name}{critTag}{defendTag} = {dmg} dmg ({target.CurrentHp}/{target.MaxHp} HP)");
		CombatLog(breakdown.ToString());

		_renderer.SpawnDamageNumber(target, dmg, crit);
		_renderer.FlashUnitHit(target);
		_renderer.RefreshUnitHpBar(target);

		// ── POST-HIT EFFECTS ──

		// Lifesteal
		var lifesteal = getEffect(SkillEffectType.Lifesteal);
		if (lifesteal != null && dmg > 0)
		{
			int healAmt = (int)(dmg * lifesteal.Value);
			caster.CurrentHp = Mathf.Min(caster.MaxHp, caster.CurrentHp + healAmt);
			CombatLog($"  → {caster.Name} drains {healAmt} HP!");
			_renderer.SpawnDamageNumber(caster, healAmt, false, isHeal: true);
			_renderer.RefreshUnitHpBar(caster);
		}

		// Aether steal (percentage of damage)
		var aeSteal = getEffect(SkillEffectType.AetherSteal);
		if (aeSteal != null && dmg > 0)
		{
			int steal = aeSteal.Value >= 1 ? (int)aeSteal.Value : (int)(dmg * aeSteal.Value);
			int actual = Mathf.Min(steal, target.CurrentAether);
			target.CurrentAether -= actual;
			caster.CurrentAether = Mathf.Min(caster.MaxAether, caster.CurrentAether + actual);
			CombatLog($"  → Stole {actual} Aether!");
		}

		// Status effects on hit
		if (effects != null)
		{
			foreach (var fx in effects)
			{
				if (fx.Type == SkillEffectType.InflictStatus)
				{
					float chance = caster.NextAttackGuaranteedStatus ? 1f : fx.Value;
					float resist = target.StatusResist * 0.01f;
					if (rng.NextDouble() < chance * (1f - resist))
					{
						target.AddBuff(fx.Param, "STATUS", 0, (int)fx.Value2, ab.SourceId);
						CombatLog($"  → {target.Name} is {fx.Param}ed!");
					}
				}
				else if (fx.Type == SkillEffectType.ReduceStat)
				{
					target.AddBuff($"{fx.Param} -{(int)(fx.Value*100)}%", fx.Param, -fx.Value, (int)fx.Value2, ab.SourceId + "_" + fx.Param);
					CombatLog($"  → {target.Name}'s {fx.Param} reduced by {(int)(fx.Value*100)}%!");
				}
				else if (fx.Type == SkillEffectType.ReduceMovement)
				{
					target.AddBuff($"MOVE -{(int)fx.Value}", "MOVE", -fx.Value, (int)fx.Value2, ab.SourceId);
					CombatLog($"  → {target.Name}'s movement reduced!");
				}
			}
		}
		caster.NextAttackGuaranteedStatus = false;

		// AUTO: Reflect
		float reflectPct = ab.Category == SkillCategory.Ether ? target.AutoReflectMagic : target.AutoReflectDmg;
		if (reflectPct > 0 && target.IsAlive)
		{
			int reflected = (int)(dmg * reflectPct);
			if (reflected > 0)
			{
				caster.CurrentHp = Mathf.Max(0, caster.CurrentHp - reflected);
				CombatLog($"↩ {target.Name} reflects {reflected} dmg back!");
				_renderer.SpawnDamageNumber(caster, reflected, false);
			}
		}

		// ── KILL CHECK ──
		if (!target.IsAlive)
		{
			CombatLog($"💀 {target.Name} has been defeated by {ab.Name}!");
			_stats.RecordKill(caster, target);
			var defTile = _grid.At(target.GridPosition);
			if (defTile != null) defTile.Occupant = null;
			_renderer.RemoveUnitVisual(target);
			CheckDreadHarvest(target);

			// Kill heal full (Sanguine Assault)
			if (hasEffect(SkillEffectType.KillHealFull))
			{
				caster.CurrentHp = caster.MaxHp;
				CombatLog($"  → {caster.Name} heals to full HP from the kill!");
				_renderer.SpawnDamageNumber(caster, caster.MaxHp, false, isHeal: true);
				_renderer.RefreshUnitHpBar(caster);
			}
		}
	}

	// ═══════════════════════════════════════════════════════
	//  ITEM EXECUTION
	// ═══════════════════════════════════════════════════════

	void ExecuteItem(ItemInfo item, int index, BattleUnit target)
	{
		_renderer.ClearAllHighlights();

		// Items heal HP/STA/AE based on their description (simple keyword parsing)
		string desc = item.Description.ToLower();
		bool didSomething = false;

		if (desc.Contains("hp") || desc.Contains("health") || desc.Contains("tonic"))
		{
			int heal = 80; // default heal amount
			int actual = Mathf.Min(heal, target.MaxHp - target.CurrentHp);
			target.CurrentHp = Mathf.Min(target.MaxHp, target.CurrentHp + heal);
			CombatLog($"◆ {_activeUnit.Name} uses {item.Name} on {target.Name} → +{actual} HP");
			_renderer.SpawnDamageNumber(target, actual, false, isHeal: true);
			didSomething = true;
		}

		if (desc.Contains("stamina") || desc.Contains("draft"))
		{
			int restore = 50;
			target.CurrentStamina = Mathf.Min(target.MaxStamina, target.CurrentStamina + restore);
			CombatLog($"◆ {_activeUnit.Name} uses {item.Name} on {target.Name} → +{restore} STA");
			didSomething = true;
		}

		if (desc.Contains("aether") || desc.Contains("vial"))
		{
			int restore = 50;
			target.CurrentAether = Mathf.Min(target.MaxAether, target.CurrentAether + restore);
			CombatLog($"◆ {_activeUnit.Name} uses {item.Name} on {target.Name} → +{restore} AE");
			didSomething = true;
		}

		if (!didSomething)
		{
			CombatLog($"◆ {_activeUnit.Name} uses {item.Name} on {target.Name}!");
		}

		_renderer.RefreshUnitHpBar(target);
		_hud.UpdateUnitInfo(_activeUnit);

		// Mark as used (single use in battle)
		item.IsUsable = false;
		item.Quantity = 0;
		_hud.SetItems(_loadoutItems); // refresh display

		// Clear pending
		_pendingItem = null;
		_pendingItemIndex = -1;
		_targetMode = TargetMode.None;

		CompleteCombatAction(item.RtCost);
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
				GetViewport().SetInputAsHandled();
				QueueFree(); // _ExitTree → RestoreOverworldUI → deferred ClearWorld3D
			}
		}
	}
}
