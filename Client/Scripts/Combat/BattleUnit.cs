using Godot;
using System;
using System.Collections.Generic;

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

	// ─── PASSIVE / AUTO SKILLS ────────────────────────────
	public SkillDefinition PassiveSkill1;
	public SkillDefinition PassiveSkill2;
	public SkillDefinition AutoSkill;

	// Passive stat bonuses (applied once at battle start)
	public float PassiveAtkMod = 1f;      // Strengthen
	public float PassiveDefMod = 1f;      // Fortify
	public float PassiveMaxHpMod = 1f;    // Constitution
	public float PassiveAccMod = 1f;      // Trueflight / Weapon Mastery
	public float PassiveDmgMod = 1f;      // Weapon Mastery damage
	public float PassiveEdefMod = 1f;     // Templar Ward
	public int PassiveRangeBonus = 0;     // Trajectory
	public float PassiveItemEff = 1f;     // Field Alchemy

	// Auto-trigger chances
	public float AutoCounterChance = 0f;
	public float AutoKnockbackChance = 0f;
	public float AutoParryChance = 0f;
	public float AutoDeflectChance = 0f;
	public float AutoSidestepChance = 0f;
	public float AutoReflectDmg = 0f;
	public float AutoReflectMagic = 0f;
	public bool AutoIronWill = false;
	public bool IronWillUsed = false;
	public int AutoConserveRt = 0;
	public bool AutoDreadHarvest = false;
	public bool AutoStalwartFaith = false;

	// ─── ACTIVE BUFFS / STATUS EFFECTS ────────────────────
	public List<ActiveBuff> Buffs = new();

	/// <summary>Add a timed buff. Stacks by default; same-source replaces.</summary>
	public void AddBuff(string name, string stat, float value, int turns, string source = "")
	{
		// Replace existing from same source
		Buffs.RemoveAll(b => b.Source == source && source != "");
		Buffs.Add(new ActiveBuff { Name = name, Stat = stat, Value = value, TurnsLeft = turns, Source = source });
	}

	/// <summary>Get total buff modifier for a stat (multiplicative).</summary>
	public float GetBuffMod(string stat)
	{
		float mod = 1f;
		foreach (var b in Buffs)
			if (b.Stat == stat) mod += b.Value;
		return mod;
	}

	/// <summary>Check if unit has a specific status.</summary>
	public bool HasStatus(string status)
	{
		foreach (var b in Buffs)
			if (b.Stat == "STATUS" && b.Name == status && b.TurnsLeft > 0) return true;
		return false;
	}

	/// <summary>Remove first debuff found.</summary>
	public bool RemoveOneDebuff()
	{
		for (int i = 0; i < Buffs.Count; i++)
			if (Buffs[i].Value < 0 || Buffs[i].Stat == "STATUS")
			{ Buffs.RemoveAt(i); return true; }
		return false;
	}

	public void RemoveAllDebuffs()
	{
		Buffs.RemoveAll(b => b.Value < 0 || b.Stat == "STATUS");
	}

	/// <summary>Tick all buffs down by 1 turn. Remove expired ones. Returns expired names for logging.</summary>
	public List<string> TickBuffs()
	{
		var expired = new List<string>();
		for (int i = Buffs.Count - 1; i >= 0; i--)
		{
			Buffs[i].TurnsLeft--;
			if (Buffs[i].TurnsLeft <= 0)
			{
				expired.Add(Buffs[i].Name);
				Buffs.RemoveAt(i);
			}
		}
		return expired;
	}

	// ─── NEXT-ACTION FLAGS (set by self-buffs, consumed on next attack) ──
	public bool NextAttackGuaranteedCrit;
	public bool NextAttackGuaranteedHit;
	public int NextAttackHitCount = 1;
	public float NextSpellDmgBonus;
	public bool NextAttackGuaranteedStatus;
	public float NextAttackDmgReduction;   // Phalanx/Brace: reduce incoming dmg
	public int DmgReductionTurns;

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

	// ─── PASSIVE / AUTO APPLICATION ─────────────────────

	/// <summary>Apply all passive skill stat modifiers. Call AFTER creating unit.</summary>
	public void ApplyPassives()
	{
		ApplyOnePassive(PassiveSkill1);
		ApplyOnePassive(PassiveSkill2);
		ApplyOneAutoSkill(AutoSkill);

		// Apply modifiers to stats
		Atk = (int)(Atk * PassiveAtkMod * PassiveDmgMod);
		Def = (int)(Def * PassiveDefMod);
		Edef = (int)(Edef * PassiveEdefMod);
		Acc = (int)(Acc * PassiveAccMod);
		MaxHp = (int)(MaxHp * PassiveMaxHpMod);
		CurrentHp = MaxHp; // refresh to new max
	}

	void ApplyOnePassive(SkillDefinition sk)
	{
		if (sk == null) return;
		// Parse rank from MaxRank (rank 1-4 maps to scaling values)
		int rank = sk.MaxRank;
		float pct = rank * 0.05f; // 5%/10%/15%/20% per rank

		switch (sk.Id)
		{
			// Vanguard
			case "VAN_STRENGTHEN":   PassiveAtkMod += pct; break;
			case "VAN_FORTIFY":      PassiveDefMod += pct; break;
			case "VAN_CONSTITUTION": PassiveMaxHpMod += pct; break;
			case "VAN_WEAPON_MASTERY": PassiveAccMod += pct; PassiveDmgMod += pct; break;
			case "VAN_PINCER_ATTACK": break; // positional — needs flanking system
			case "VAN_RAMPART_AURA":  break; // zone control — needs movement system hook

			// Marksman
			case "MRK_TRAJECTORY":   PassiveRangeBonus += 1; break;
			case "MRK_TRUEFLIGHT":   PassiveAccMod += pct; break;
			case "MRK_FOCUS_SHOT":   break; // conditional — check HasMoved at attack time
			case "MRK_HIGH_GROUND":  break; // conditional — check elevation
			case "MRK_LOBBER":       break; // arc — needs LOS system

			// Mender
			case "MND_TRIAGE":       break; // heal priority — AI hint
			case "MND_SOOTHING_AURA":break; // aura — needs proximity check
			case "MND_SHIELD_OF_FAITH": PassiveEdefMod += pct; break;

			// Bulwark
			case "BLK_SHIELD_MASTERY":  PassiveDefMod += pct; break;
			case "BLK_STALWART":        break; // knockback immunity

			// Tactician
			case "TAC_FIELD_ALCHEMY":   PassiveItemEff += rank * 0.25f; break;
			case "TAC_EXPLOIT_OPENING": break; // conditional — check status
			case "TAC_TERRAIN_MASTERY": break; // movement — needs terrain system
			case "TAC_TREASURE_HUNT":   break; // loot bonus
			case "TAC_MAX_TP":          break; // out of combat

			// Hexer
			case "HEX_DARK_PACT":    break; // aether cost reduction
			case "HEX_MALICE":       break; // debuff duration

			// Templar
			case "TMP_DIVINE_WARD":  PassiveEdefMod += pct; break;
			case "TMP_INQUISITORS_EYE": break; // stealth detection
		}
	}

	void ApplyOneAutoSkill(SkillDefinition sk)
	{
		if (sk == null) return;
		int rank = sk.MaxRank;

		switch (sk.Id)
		{
			case "VAN_COUNTERATTACK": AutoCounterChance = rank switch { 1=>0.15f, 2=>0.25f, 3=>0.35f, _=>0.50f }; break;
			case "VAN_KNOCKBACK":     AutoKnockbackChance = rank switch { 1=>0.10f, 2=>0.20f, 3=>0.30f, _=>0.40f }; break;
			case "VAN_IRON_WILL":     AutoIronWill = true; break;
			case "VAN_PARRY":         AutoParryChance = 0.25f; break;
			case "VAN_DEFLECT":       AutoDeflectChance = 0.25f; break;
			case "MRK_SIDESTEP":      AutoSidestepChance = rank switch { 1=>0.10f, 2=>0.20f, 3=>0.30f, _=>0.40f }; break;
			case "MRK_CONSERVE_RT":   AutoConserveRt = 3; break;
			case "TAC_REFLECT_DAMAGE": AutoReflectDmg = rank * 0.05f; break;
			case "TAC_REFLECT_MAGIC":  AutoReflectMagic = rank * 0.05f; break;
			case "HEX_DREAD_HARVEST":  AutoDreadHarvest = true; break;
			case "TMP_STALWART_FAITH": AutoStalwartFaith = true; break;
		}
	}

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

/// <summary>A timed buff or status effect on a unit.</summary>
public class ActiveBuff
{
	public string Name;       // Display name ("ATK +25%", "Poison", etc.)
	public string Stat;       // What it modifies: "ATK", "DEF", "EDEF", "RT", "STATUS", "SHIELD", "REGEN"
	public float Value;       // Modifier value (0.25 = +25%, -0.15 = -15%)
	public int TurnsLeft;     // Remaining turns
	public string Source;     // Source skill ID (for replacing on recast)
	public int ShieldHp;      // For SHIELD type: remaining absorb HP
}
