using Godot;
using System;

namespace ProjectTactics.Combat;

public enum Facing { North, East, South, West }
public enum UnitTeam { TeamA, TeamB }

/// <summary>
/// RT System (Tactics Ogre style) — v3.0:
/// 
/// WT (Wait Time) = base from DEX + AGI + equipment weight.
///   Formula: WT = clamp(125 - DEX - AGI/2 + EquipWeight, 60, 180)
/// 
/// RT (Recovery Time) = WT + movement cost + action cost.
///   Movement: each tile moved costs MoveRtPerTile (based on AGI).
///   If you only move OR only act (not both): WT reduced to 75%.
///   If you do neither (Wait): WT reduced to 50%.
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
	public int TilesMoved;
	public bool IsDefending;

	// ─── BASE STATS (from PlayerData) ────────────────────
	public int Strength, Vitality, Dexterity, Agility, EtherControl, Mind;

	// ─── RESOURCE POOLS (v3.0: HP / Stamina / Aether) ────
	public int MaxHp, MaxStamina, MaxAether;
	public int CurrentHp, CurrentStamina, CurrentAether;

	// ─── REGEN (per combat turn) ─────────────────────────
	public int HpRegen, StaminaRegen, AetherRegen;

	// ─── DERIVED COMBAT STATS ────────────────────────────
	public int Atk, Def, Eatk, Edef, Avd, Acc;
	public float CritPercent;
	public int HealPower, StatusResist;
	public int Move, Jump;

	// ─── WT/RT SYSTEM ────────────────────────────────────
	public int BaseWt;
	public int MoveRtPerTile;
	public int CurrentRt;
	public int EquipWeight;

	// ─── RT CALCULATION ──────────────────────────────────

	public int CalculateTurnRt(int tilesMoved, int actionRt, bool didMove, bool didAct)
	{
		float wtMult;
		if (didMove && didAct)       wtMult = 1.0f;
		else if (didMove || didAct)  wtMult = 0.75f;
		else                         wtMult = 0.50f;

		return (int)(BaseWt * wtMult) + tilesMoved * MoveRtPerTile + actionRt;
	}

	public void EndTurn(int actionRt)
	{
		CurrentRt = CalculateTurnRt(TilesMoved, actionRt, HasMoved, HasActed);
		HasMoved = false;
		HasActed = false;
		TilesMoved = 0;
	}

	/// <summary>Apply per-turn regen (called at start of this unit's turn).</summary>
	public void ApplyRegen()
	{
		CurrentHp      = Math.Min(CurrentHp + HpRegen, MaxHp);
		CurrentStamina = Math.Min(CurrentStamina + StaminaRegen, MaxStamina);
		CurrentAether  = Math.Min(CurrentAether + AetherRegen, MaxAether);
	}

	/// <summary>Deduct resource cost for an ability.</summary>
	public void SpendResource(AbilityInfo ability)
	{
		switch (ability.ResourceType)
		{
			case ResourceType.Stamina:
				CurrentStamina = Math.Max(0, CurrentStamina - ability.StaminaCost);
				break;
			case ResourceType.Aether:
				CurrentAether = Math.Max(0, CurrentAether - ability.AetherCost);
				break;
			case ResourceType.Both:
				CurrentStamina = Math.Max(0, CurrentStamina - ability.StaminaCost);
				CurrentAether  = Math.Max(0, CurrentAether - ability.AetherCost);
				break;
		}
	}

	public void TickRt(int amount) => CurrentRt = Math.Max(0, CurrentRt - amount);

	// ─── DERIVED HELPERS ─────────────────────────────────
	public bool IsAlive         => CurrentHp > 0;
	public float HpPercent      => MaxHp > 0      ? (float)CurrentHp / MaxHp           : 0;
	public float StaminaPercent => MaxStamina > 0  ? (float)CurrentStamina / MaxStamina : 0;
	public float AetherPercent  => MaxAether > 0   ? (float)CurrentAether / MaxAether   : 0;

	// ─── STATIC RT FORMULAS (v3.0) ───────────────────────

	/// <summary>WT = 125 - DEX - AGI/2 + EquipWeight, clamped 60-180.</summary>
	public static int CalcBaseWt(int dex, int agi, int equipWeight)
		=> Math.Clamp(125 - dex - agi / 2 + equipWeight, 60, 180);

	/// <summary>Move RT per tile: 8 - AGI/5, clamped 3-8.</summary>
	public static int CalcMoveRtPerTile(int agi)
		=> Math.Clamp(8 - agi / 5, 3, 8);

	// ─── FACTORY ─────────────────────────────────────────

	public static BattleUnit FromPlayerData(Core.PlayerData p, UnitTeam team, Vector2I spawnPos, int equipWeight = 0)
	{
		var unit = new BattleUnit
		{
			CharacterId = Core.GameManager.Instance?.ActiveCharacterId ?? "",
			Name = p.CharacterName, Team = team, GridPosition = spawnPos,
			Facing = team == UnitTeam.TeamA ? Facing.North : Facing.South,

			Strength = p.Strength, Vitality = p.Vitality, Dexterity = p.Dexterity,
			Agility = p.Agility, EtherControl = p.EtherControl, Mind = p.Mind,

			MaxHp = p.MaxHp, MaxStamina = p.MaxStamina, MaxAether = p.MaxAether,
			CurrentHp = p.MaxHp, CurrentStamina = p.MaxStamina, CurrentAether = p.MaxAether,

			HpRegen = p.HpRegen, StaminaRegen = p.StaminaRegen, AetherRegen = p.AetherRegen,

			Atk = p.Atk, Def = p.Def, Eatk = p.Eatk, Edef = p.Edef,
			Avd = p.Avd, Acc = p.Acc, CritPercent = p.CritPercent,
			HealPower = p.HealPower, StatusResist = p.StatusResist,
			Move = p.Move, Jump = p.Jump,

			EquipWeight = equipWeight, CurrentRt = 0
		};
		unit.BaseWt = CalcBaseWt(p.Dexterity, p.Agility, equipWeight);
		unit.MoveRtPerTile = CalcMoveRtPerTile(p.Agility);
		return unit;
	}

	public static BattleUnit CreateDummy(string name, UnitTeam team, Vector2I pos, int statLevel = 5, int equipWeight = 10)
	{
		int str = statLevel, vit = statLevel, dex = statLevel;
		int agi = statLevel, etc = statLevel, mnd = statLevel;

		var unit = new BattleUnit
		{
			CharacterId = Guid.NewGuid().ToString(),
			Name = name, Team = team, GridPosition = pos,
			Facing = team == UnitTeam.TeamA ? Facing.North : Facing.South,

			Strength = str, Vitality = vit, Dexterity = dex,
			Agility = agi, EtherControl = etc, Mind = mnd,

			MaxHp      = 200 + vit * 15 + mnd * 8,
			MaxStamina = 100 + str * 12 + vit * 8,
			MaxAether  = 100 + etc * 20 + mnd * 5,

			HpRegen      = (int)(mnd * 0.4f),
			StaminaRegen = (int)(vit * 0.3f),
			AetherRegen  = (int)(etc * 0.8f),

			Atk  = (int)(str * 2.5f),
			Def  = (int)(vit * 2.0f + mnd * 0.5f),
			Eatk = (int)(etc * 2.5f + mnd * 0.3f),
			Edef = (int)(etc * 1.0f + vit * 0.8f + mnd * 0.5f),
			Avd  = (int)(agi * 1.5f + dex * 0.5f),
			Acc  = (int)(agi * 1.2f + dex * 0.3f),
			CritPercent  = dex * 0.4f + agi * 0.1f,
			HealPower    = (int)(mnd * 1.5f + etc * 0.5f),
			StatusResist = (int)(mnd * 0.5f + vit * 0.2f),
			Move = Math.Min(3 + agi / 12, 7),
			Jump = Math.Min(2 + str / 20, 5),

			EquipWeight = equipWeight, CurrentRt = 0
		};
		unit.CurrentHp      = unit.MaxHp;
		unit.CurrentStamina = unit.MaxStamina;
		unit.CurrentAether  = unit.MaxAether;
		unit.BaseWt = CalcBaseWt(dex, agi, equipWeight);
		unit.MoveRtPerTile = CalcMoveRtPerTile(agi);
		return unit;
	}
}

/// <summary>Action RT costs — reference constants.</summary>
public static class ActionRt
{
	public const int Wait     = 0;
	public const int Defend   = 5;

	public const int LightAttack  = 15;
	public const int MediumAttack = 25;
	public const int HeavyAttack  = 35;

	public const int MinorAbility  = 15;
	public const int MediumAbility = 25;
	public const int MajorAbility  = 35;
	public const int FinishingMove = 50;

	public const int UseItem = 20;
}
