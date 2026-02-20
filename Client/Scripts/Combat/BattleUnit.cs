using Godot;
using System;

namespace ProjectTactics.Combat;

public enum Facing { North, East, South, West }
public enum UnitTeam { TeamA, TeamB }

/// <summary>
/// RT System (Tactics Ogre style):
/// 
/// WT (Wait Time) = base speed from stats + equipment weight.
///   This is how long you wait if you do NOTHING on your turn.
///   Formula: WT = clamp(125 - AGI - SPD/2 + EquipWeight, 60, 180)
/// 
/// RT (Recovery Time) = WT + movement cost + action cost.
///   Movement: each tile moved costs MoveRtPerTile (based on SPD).
///   Actions: each weapon/spell/ability has its own RT cost.
///   If you only move OR only act (not both): WT is reduced to 75%.
///   If you do neither (Wait): WT is reduced to 50%.
/// 
/// After your turn ends, your CurrentRt is set to the total.
/// All units tick down together. Lowest RT acts next.
/// </summary>
public class BattleUnit
{
	// Identity
	public string CharacterId;
	public string Name;
	public UnitTeam Team;

	// Grid state
	public Vector2I GridPosition;
	public Facing Facing = Facing.South;
	public bool HasMoved;
	public bool HasActed;
	public int TilesMoved;  // track how far we moved this turn

	// ─── BASE STATS (from PlayerData) ────────────────────
	public int Strength, Vitality, Dexterity, Agility, EtherControl, Mind;
	public int MaxHp, MaxAether;
	public int CurrentHp, CurrentAether;
	public int Atk, Def, Eatk, Edef, Avd, Acc;
	public float CritPercent;
	public int Move, Jump;

	// ─── WT/RT SYSTEM ────────────────────────────────────
	/// <summary>Base Wait Time — how long you wait if you do nothing.
	/// Lower = faster. Affected by AGI, SPD, and equipment weight.</summary>
	public int BaseWt;

	/// <summary>RT cost per tile moved. Lower SPD = higher cost.
	/// Formula: clamp(8 - SPD/5, 3, 8)</summary>
	public int MoveRtPerTile;

	/// <summary>Current countdown. When this hits 0, unit acts.</summary>
	public int CurrentRt;

	/// <summary>Equipment weight contribution to WT.</summary>
	public int EquipWeight;

	// ─── RT CALCULATION ──────────────────────────────────

	/// <summary>
	/// Calculate total RT after a turn ends.
	/// Mirrors Tactics Ogre: WT is base, reduced if you didn't do everything.
	/// Movement and action RT are added on top.
	/// </summary>
	public int CalculateTurnRt(int tilesMoved, int actionRt, bool didMove, bool didAct)
	{
		// Base WT modifier based on what you did
		float wtMultiplier;
		if (didMove && didAct)
			wtMultiplier = 1.0f;      // full WT — you did everything
		else if (didMove || didAct)
			wtMultiplier = 0.75f;     // only moved OR only acted
		else
			wtMultiplier = 0.50f;     // waited — quickest recovery

		int wt = (int)(BaseWt * wtMultiplier);
		int moveRt = tilesMoved * MoveRtPerTile;
		int totalRt = wt + moveRt + actionRt;

		return totalRt;
	}

	/// <summary>End turn: set RT based on what was done.</summary>
	public void EndTurn(int actionRt)
	{
		CurrentRt = CalculateTurnRt(TilesMoved, actionRt, HasMoved, HasActed);
		// Reset turn state
		HasMoved = false;
		HasActed = false;
		TilesMoved = 0;
	}

	/// <summary>Tick RT down by amount.</summary>
	public void TickRt(int amount)
	{
		CurrentRt = Math.Max(0, CurrentRt - amount);
	}

	// ─── DERIVED HELPERS ─────────────────────────────────
	public bool IsAlive => CurrentHp > 0;
	public float HpPercent => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0;
	public float AetherPercent => MaxAether > 0 ? (float)CurrentAether / MaxAether : 0;

	// ─── STATIC RT FORMULAS ──────────────────────────────

	/// <summary>Calculate base WT from stats + equipment weight.</summary>
	public static int CalcBaseWt(int agi, int spd, int equipWeight)
		=> Math.Clamp(125 - agi - spd / 2 + equipWeight, 60, 180);

	/// <summary>Calculate per-tile movement RT cost from speed.</summary>
	public static int CalcMoveRtPerTile(int spd)
		=> Math.Clamp(8 - spd / 5, 3, 8);

	// ─── FACTORY ─────────────────────────────────────────

	public static BattleUnit FromPlayerData(Core.PlayerData p, UnitTeam team, Vector2I spawnPos, int equipWeight = 0)
	{
		var unit = new BattleUnit
		{
			CharacterId = Core.GameManager.Instance?.ActiveCharacterId ?? "",
			Name = p.CharacterName,
			Team = team,
			GridPosition = spawnPos,
			Facing = team == UnitTeam.TeamA ? Facing.North : Facing.South,

			Strength = p.Strength, Dexterity = p.Dexterity, Agility = p.Agility,
			Vitality = p.Vitality, Mind = p.Mind, EtherControl = p.EtherControl,

			MaxHp = p.MaxHp, MaxAether = p.MaxAether,
			CurrentHp = p.MaxHp, CurrentAether = p.MaxAether,

			Atk = p.Atk, Def = p.Def, Eatk = p.Eatk, Edef = p.Edef,
			Avd = p.Avd, Acc = p.Acc, CritPercent = p.CritPercent,
			Move = p.Move, Jump = p.Jump,

			EquipWeight = equipWeight,
			CurrentRt = 0
		};
		unit.BaseWt = CalcBaseWt(p.Agility, p.Agility, equipWeight);
		unit.MoveRtPerTile = CalcMoveRtPerTile(p.Agility);
		return unit;
	}

	public static BattleUnit CreateDummy(string name, UnitTeam team, Vector2I pos, int statLevel = 5, int equipWeight = 10)
	{
		int str = statLevel, spd = statLevel, agi = statLevel;
		int end = statLevel, sta = statLevel, etc = statLevel;

		var unit = new BattleUnit
		{
			CharacterId = Guid.NewGuid().ToString(),
			Name = name, Team = team, GridPosition = pos,
			Facing = team == UnitTeam.TeamA ? Facing.North : Facing.South,

			Strength = str, Dexterity = spd, Agility = agi,
			Vitality = end, Mind = sta, EtherControl = etc,

			MaxHp = 200 + end * 15 + sta * 8,
			MaxAether = 100 + etc * 20 + sta * 5,
			CurrentHp = 200 + end * 15 + sta * 8,
			CurrentAether = 100 + etc * 20 + sta * 5,

			Atk = (int)(str * 2.5f + spd * 0.5f),
			Def = (int)(end * 2.0f + sta * 0.5f),
			Eatk = (int)(etc * 2.5f + agi * 0.3f),
			Edef = etc + end,
			Avd = (int)(agi * 1.5f + spd * 1.0f),
			Acc = (int)(agi * 1.0f + spd * 0.5f),
			CritPercent = spd * 0.3f + agi * 0.2f,
			Move = 4 + spd / 15,
			Jump = 2 + str / 20,

			EquipWeight = equipWeight,
			CurrentRt = 0
		};
		unit.BaseWt = CalcBaseWt(agi, spd, equipWeight);
		unit.MoveRtPerTile = CalcMoveRtPerTile(spd);
		return unit;
	}
}

/// <summary>
/// Action RT costs — each weapon/spell/ability defines its own RT.
/// These are reference constants; actual abilities will be data-driven.
/// </summary>
public static class ActionRt
{
	// ─── BASIC ACTIONS ───────────────────────────────────
	public const int Wait     = 0;   // did nothing — WT at 50%
	public const int Defend   = 5;   // block stance, minimal RT

	// ─── PHYSICAL ATTACKS (weapon-dependent in future) ───
	public const int LightAttack  = 15;  // dagger, fist
	public const int MediumAttack = 25;  // sword, spear
	public const int HeavyAttack  = 35;  // 2H axe, greatsword

	// ─── ETHER ABILITIES ─────────────────────────────────
	public const int MinorAbility  = 15;  // heal, buff
	public const int MediumAbility = 25;  // fireball, debuff
	public const int MajorAbility  = 35;  // large AoE
	public const int FinishingMove = 50;  // ultimate, requires charge

	// ─── ITEMS ───────────────────────────────────────────
	public const int UseItem = 20;
}
