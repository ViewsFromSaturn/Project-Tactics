using System;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Data-driven skill effect system. Each skill can have multiple effects
/// that modify how damage/healing/targeting works during ExecuteAbility.
/// This avoids 180 switch cases — effects are tags with parameters.
/// </summary>
public class SkillEffect
{
	public SkillEffectType Type;
	public float Value;     // primary value (percentage, flat amount, etc.)
	public float Value2;    // secondary (e.g. duration for status)
	public string Param;    // string param (e.g. stat name, element)
}

public enum SkillEffectType
{
	// ── DAMAGE MODIFIERS ──
	IgnoreDefPercent,       // Value = 0.3 → ignore 30% of target DEF
	BonusVsElement,         // Value = 0.5, Param = "Dark" → +50% vs Dark
	BonusMissingHpPercent,  // Bonus damage = target's missing HP% * Value
	GuaranteedCrit,         // Next attack is always crit
	GuaranteedHit,          // Cannot miss (100% accuracy)
	HitCount,               // Value = 2 → attack strikes twice
	Lifesteal,              // Value = 0.3 → heal for 30% of damage dealt
	AetherSteal,            // Value = 0.15 → steal 15% of dmg as Aether
	ScaleOffVit,            // Use VIT instead of ATK for damage calc
	SpellDmgBonus,          // Value = 0.4 → next spell +40% damage (buff)

	// ── SELF BUFFS ──
	AtkBuff,                // Value = 0.25, Value2 = 3 → ATK +25% for 3 turns
	DefBuff,                // Value = 0.25, Value2 = 3
	EdefBuff,               // Value = 0.25, Value2 = 3
	DmgReduction,           // Value = 0.4, Value2 = 1 → take 40% less for 1 turn
	Haste,                  // Value = 15, Value2 = 3 → RT -15 for 3 turns
	ElementEnchant,         // Param = element name, Value2 = 3 turns
	Stealth,                // Value2 = 2 turns
	BlockCounterattack,     // Cannot counter this turn
	BlockAttack,            // Cannot attack while active
	RangeBonus,             // Value = 2 → +2 range for next attack

	// ── ALLY BUFFS ──
	AllyAtkBuff,            // same as AtkBuff but on target ally
	AllyDefBuff,
	AllyHaste,
	AllyDmgReduction,
	AllyShield,             // Value = 0.25 → absorb 25% of max HP as shield
	AllyRtReset,            // Reset target RT to 0
	AetherRestore,          // Value = 30 → restore 30 Aether to target
	AllyHpRegen,            // Value = 0.03, Value2 = 3 → 3% HP/turn for 3 turns

	// ── DEBUFFS / STATUS ──
	InflictStatus,          // Param = "Poison"/"Stun"/"Fear" etc, Value = chance, Value2 = turns
	ReduceMovement,         // Value = 2, Value2 = 2 → MOVE -2 for 2 turns
	ReduceStat,             // Param = "ATK"/"DEF"/"DEX", Value = 0.2, Value2 = 3
	AddTargetRt,            // Value = 20 → add +20 RT to enemy
	ReduceTargetRt,         // Value = 15 → reduce ally RT by 15
	StripBuffs,             // Remove all buffs from target
	PreventRevive,          // Condemn — cannot be revived
	Provoke,                // Value2 = 2 → force target to attack caster for 2 turns

	// ── HEALING MODIFIERS ──
	SelfHealFlat,           // Value = 50 → heal self for flat + MND scaling
	SelfHealMndScale,       // Value = 2 → heal for MND * Value + Power
	RemoveDebuff,           // Remove 1 debuff (Cleanse)
	RemoveAllDebuffs,       // Remove all debuffs (Purify)
	Revive,                 // Revive at Value% HP (e.g. 0.3 = 30%)

	// ── RESOURCE ──
	RestoreAether,          // Value = flat aether restore to self
	MeditateSelf,           // Value = Aether to restore (self, no cost)

	// ── SPECIAL ──
	OncePerBattle,          // Can only be used once
	KillHealFull,           // If this kills target, heal to full
	AoeDamageAlly,          // AoE that also heals allies in range
	GuaranteedStatusOnHit,  // Next attack guarantees status proc
}

/// <summary>
/// Registry mapping skill IDs → their special effects.
/// Skills not in this registry use the generic damage/heal formula.
/// </summary>
public static class SkillEffectRegistry
{
	static Dictionary<string, List<SkillEffect>> _effects;
	public static List<SkillEffect> GetEffects(string skillId)
	{
		_effects ??= Build();
		return _effects.TryGetValue(skillId, out var list) ? list : null;
	}

	static SkillEffect E(SkillEffectType t, float v = 0, float v2 = 0, string p = null) =>
		new() { Type = t, Value = v, Value2 = v2, Param = p };

	static Dictionary<string, List<SkillEffect>> Build()
	{
		var d = new Dictionary<string, List<SkillEffect>>();

		// ═══════════════════════════════════════════════════════
		//  VANGUARD
		// ═══════════════════════════════════════════════════════
		d["VAN_MIGHTY_IMPACT"] = new() {
			E(SkillEffectType.GuaranteedCrit),
			E(SkillEffectType.GuaranteedHit),
			E(SkillEffectType.BlockCounterattack),
		};
		d["VAN_DOUBLE_STRIKE"] = new() { E(SkillEffectType.HitCount, 2) };
		d["VAN_OVERPOWER"] = new() { E(SkillEffectType.IgnoreDefPercent, 0.30f) };
		d["VAN_WARPATH"] = new() {
			E(SkillEffectType.AtkBuff, 0.25f, 3),
			E(SkillEffectType.DefBuff, -0.15f, 3), // negative = debuff self
		};

		// ═══════════════════════════════════════════════════════
		//  MARKSMAN
		// ═══════════════════════════════════════════════════════
		d["MRK_EAGLE_EYE"] = new() { E(SkillEffectType.GuaranteedHit), E(SkillEffectType.RangeBonus, 0) };
		d["MRK_TREMENDOUS_SHOT"] = new() { E(SkillEffectType.GuaranteedStatusOnHit) };
		d["MRK_SNIPE"] = new() {
			E(SkillEffectType.GuaranteedHit),
			E(SkillEffectType.RangeBonus, 2),
		};
		d["MRK_BARRAGE"] = new() { E(SkillEffectType.HitCount, 3) }; // line hit up to 3
		d["MRK_SUPPRESSIVE_FIRE"] = new() { E(SkillEffectType.ReduceMovement, 2, 2) };
		d["MRK_PINNING_SHOT"] = new() { E(SkillEffectType.InflictStatus, 0.30f, 1, "Immobilize") };
		d["MRK_DOUBLE_SHOT"] = new() { E(SkillEffectType.HitCount, 2) };
		d["MRK_CRIPPLING_ARROW"] = new() { E(SkillEffectType.ReduceStat, 0.20f, 3, "DEX") };

		// ═══════════════════════════════════════════════════════
		//  EVOKER
		// ═══════════════════════════════════════════════════════
		d["EVO_MEDITATE"] = new() { E(SkillEffectType.MeditateSelf, 35) }; // avg rank
		d["EVO_ELEMENTAL_SURGE"] = new() { E(SkillEffectType.SpellDmgBonus, 0.40f) };
		d["EVO_CHAIN_CAST"] = new() { E(SkillEffectType.HitCount, 2) }; // second at 60%
		d["EVO_NULLIFY"] = new() { E(SkillEffectType.StripBuffs) };
		d["EVO_AETHER_DRAIN"] = new() { E(SkillEffectType.AetherSteal, 20) };

		// ═══════════════════════════════════════════════════════
		//  MENDER
		// ═══════════════════════════════════════════════════════
		d["MND_CLEANSE"] = new() { E(SkillEffectType.RemoveDebuff) };
		d["MND_PURIFY"] = new() { E(SkillEffectType.RemoveAllDebuffs) };
		d["MND_REVITALIZE"] = new() { E(SkillEffectType.RemoveDebuff) }; // heal + cleanse
		d["MND_BOON_SWIFTNESS"] = new() { E(SkillEffectType.AllyHaste, 15, 3) };
		d["MND_AEGIS_WARD"] = new() { E(SkillEffectType.AllyShield, 0.25f) };
		d["MND_SPIRITSURGE"] = new() { E(SkillEffectType.AetherRestore, 30) };
		d["MND_LAST_RITES"] = new() {
			E(SkillEffectType.Revive, 0.30f),
			E(SkillEffectType.OncePerBattle),
		};
		d["MND_EXORCISM"] = new() { E(SkillEffectType.BonusVsElement, 0.50f, 0, "Dark") };

		// ═══════════════════════════════════════════════════════
		//  RUNEBLADE
		// ═══════════════════════════════════════════════════════
		d["RUN_AETHER_EDGE"] = new() { E(SkillEffectType.AtkBuff, 0.30f, 1) }; // +30% EATK as bonus
		d["RUN_INSTILL_ELEMENT"] = new() { E(SkillEffectType.ElementEnchant, 0, 3) };
		d["RUN_FIELD_BARRIER"] = new() { E(SkillEffectType.EdefBuff, 0.25f, 3) };
		d["RUN_NATURES_TOUCH"] = new() { E(SkillEffectType.SelfHealMndScale, 2) }; // MND*2 + 50
		d["RUN_VELOCITY_SHIFT"] = new() {
			E(SkillEffectType.AllyRtReset),
			E(SkillEffectType.OncePerBattle),
		};

		// ═══════════════════════════════════════════════════════
		//  BULWARK
		// ═══════════════════════════════════════════════════════
		d["BLW_PHALANX"] = new() {
			E(SkillEffectType.DmgReduction, 0.40f, 1),
			E(SkillEffectType.BlockAttack),
		};
		d["BLW_GUARDIAN_LOCK"] = new() {
			E(SkillEffectType.DmgReduction, 0.70f, 1),
			E(SkillEffectType.BlockCounterattack),
		};
		d["BLW_PROVOKE"] = new() { E(SkillEffectType.Provoke, 0, 2) };
		d["BLW_SHIELD_BASH"] = new() {
			E(SkillEffectType.ScaleOffVit),
			E(SkillEffectType.InflictStatus, 0.30f, 1, "Stun"),
		};
		d["BLW_WARD_OF_STEEL"] = new() { E(SkillEffectType.AllyDefBuff, 0.20f, 2) };
		d["BLW_BRACE"] = new() { E(SkillEffectType.DmgReduction, 0.60f, 1) };

		// ═══════════════════════════════════════════════════════
		//  SHADOWSTEP
		// ═══════════════════════════════════════════════════════
		d["SHD_STEALTH"] = new() { E(SkillEffectType.Stealth, 0, 2) };
		d["SHD_POISON_STRIKE"] = new() { E(SkillEffectType.InflictStatus, 0.40f, 4, "Poison") };
		d["SHD_SHADOWSTRIKE"] = new() { }; // 200% power already in skill Power field
		d["SHD_FEINT"] = new() { E(SkillEffectType.ReduceStat, 0.15f, 2, "AVD") };
		d["SHD_ASSASSINATE"] = new() { E(SkillEffectType.IgnoreDefPercent, 0.50f) };

		// ═══════════════════════════════════════════════════════
		//  DREADNOUGHT
		// ═══════════════════════════════════════════════════════
		d["DRD_DRAIN_HEART"] = new() { E(SkillEffectType.Lifesteal, 0.30f) };
		d["DRD_DRAIN_AETHER"] = new() { E(SkillEffectType.AetherSteal, 0.15f) };
		d["DRD_INTIMIDATE"] = new() { E(SkillEffectType.ReduceStat, 0.20f, 3, "ATK") };
		d["DRD_SANGUINE_ASSAULT"] = new() { E(SkillEffectType.KillHealFull) };
		d["DRD_TERROR_STANCE"] = new() { E(SkillEffectType.InflictStatus, 0.25f, 2, "Fear") };
		d["DRD_WEAKEN"] = new() { E(SkillEffectType.ReduceStat, 0.20f, 3, "DEF") };
		d["DRD_WITHERING_GAZE"] = new() {
			E(SkillEffectType.ReduceStat, 0.15f, 2, "ATK"),
			E(SkillEffectType.ReduceStat, 0.15f, 2, "DEF"),
		};
		d["DRD_ABYSSAL_STRIKE"] = new() { E(SkillEffectType.BonusMissingHpPercent, 1.0f) };

		// ═══════════════════════════════════════════════════════
		//  WARSINGER
		// ═══════════════════════════════════════════════════════
		d["WAR_BATTLE_HYMN"] = new() { E(SkillEffectType.AllyAtkBuff, 0.10f, 3) };
		d["WAR_SHIELD_CHANT"] = new() { E(SkillEffectType.AllyDefBuff, 0.10f, 3) };
		d["WAR_DIRGE_DESPAIR"] = new() { E(SkillEffectType.ReduceStat, 0.10f, 3, "ATK") };
		d["WAR_TEMPO_SHIFT"] = new() { E(SkillEffectType.AllyHaste, 10, 3) };
		d["WAR_LULLABY"] = new() { E(SkillEffectType.InflictStatus, 0.30f, 2, "Sleep") };
		d["WAR_DISSONANCE"] = new() { E(SkillEffectType.InflictStatus, 0.25f, 2, "Silence") };
		d["WAR_CADENCE_HASTE"] = new() { E(SkillEffectType.AllyHaste, 15, 2) };
		d["WAR_SOOTHING_MELODY"] = new() { }; // heal AoE — Power handles it
		d["WAR_POIGNANT_MELODY"] = new() { E(SkillEffectType.InflictStatus, 0.25f, 2, "Charm") };
		d["WAR_SILENCE_SONG"] = new() { E(SkillEffectType.InflictStatus, 0.25f, 2, "Silence") };
		d["WAR_REQUIEM"] = new() {
			E(SkillEffectType.BonusVsElement, 0.50f, 0, "Dark"), // +50% vs undead/dark
			E(SkillEffectType.AoeDamageAlly),                     // also heals allies
		};

		// ═══════════════════════════════════════════════════════
		//  TEMPLAR
		// ═══════════════════════════════════════════════════════
		d["TMP_SMITE"] = new() { E(SkillEffectType.BonusVsElement, 0.50f, 0, "Dark") };
		d["TMP_LAY_ON_HANDS"] = new() { E(SkillEffectType.SelfHealMndScale, 1.5f) };
		d["TMP_CONSECRATE"] = new() { E(SkillEffectType.AllyHpRegen, 0.03f, 3) };
		d["TMP_HOLY_SHIELD"] = new() { }; // Dark immunity — needs element resistance system
		d["TMP_JUDGMENT"] = new() { E(SkillEffectType.IgnoreDefPercent, 0.20f) };
		d["TMP_BLESSED_WEAPON"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Light") };
		d["TMP_OATH_PROTECTION"] = new() { E(SkillEffectType.AllyDmgReduction, 0.30f, 3) };
		d["TMP_RADIANT_BURST"] = new() { E(SkillEffectType.InflictStatus, 0.30f, 2, "Blind") };
		d["TMP_RESURRECT"] = new() {
			E(SkillEffectType.Revive, 0.50f),
			E(SkillEffectType.OncePerBattle),
		};

		// ═══════════════════════════════════════════════════════
		//  HEXER
		// ═══════════════════════════════════════════════════════
		d["HEX_CURSE"] = new() { }; // heal reduction — needs buff/debuff system
		d["HEX_ENFEEBLE"] = new() { E(SkillEffectType.ReduceStat, 0.20f, 3, "HIGHEST") };
		d["HEX_CONDEMN"] = new() { E(SkillEffectType.PreventRevive) };
		d["HEX_WITHER"] = new() { E(SkillEffectType.InflictStatus, 1.0f, 3, "Bleed") };
		d["HEX_HEX_WARD"] = new() { E(SkillEffectType.ReduceStat, 0.15f, 3, "EDEF") };
		d["HEX_SOUL_SIPHON"] = new() { E(SkillEffectType.Lifesteal, 0.40f) };
		d["HEX_LEECH_AETHER"] = new() { E(SkillEffectType.AetherSteal, 25) };
		d["HEX_BLIGHT"] = new() { E(SkillEffectType.InflictStatus, 0.30f, 3, "Poison") };
		d["HEX_PETRIFY"] = new() { E(SkillEffectType.InflictStatus, 0.25f, 2, "Petrify") };
		d["HEX_MIASMA"] = new() { E(SkillEffectType.InflictStatus, 0.20f, 3, "Poison") };

		// ═══════════════════════════════════════════════════════
		//  TACTICIAN
		// ═══════════════════════════════════════════════════════
		d["TAC_TRAP"] = new() { E(SkillEffectType.InflictStatus, 1.0f, 1, "Immobilize") };
		d["TAC_QUICKEN"] = new() { E(SkillEffectType.ReduceTargetRt, 15) };
		d["TAC_DELAY_TACTICS"] = new() { E(SkillEffectType.AddTargetRt, 20) };
		d["TAC_ANALYZE"] = new() { }; // reveal stats — UI feature

		// ═══════════════════════════════════════════════════════
		//  SPELLS (matched by SpellDefinition.Id)
		// ═══════════════════════════════════════════════════════

		// Instill spells — element enchant weapon for 3 turns
		d["FIRE_INSTILL"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Fire") };
		d["ICE_INSTILL"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Ice") };
		d["LTN_INSTILL"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Lightning") };
		d["ERT_INSTILL"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Earth") };
		d["WND_INSTILL"] = new() { E(SkillEffectType.ElementEnchant, 0, 3, "Wind") };

		// Flameguard / elemental guards — EDEF buff + element resistance
		d["FIRE_GUARD"] = new() { E(SkillEffectType.EdefBuff, 0.15f, 3) };
		d["ICE_GUARD"] = new() { E(SkillEffectType.EdefBuff, 0.15f, 3) };
		d["LTN_GUARD"] = new() { E(SkillEffectType.EdefBuff, 0.15f, 3) };
		d["ERT_GUARD"] = new() { E(SkillEffectType.EdefBuff, 0.15f, 3) };
		d["WND_GUARD"] = new() { E(SkillEffectType.EdefBuff, 0.15f, 3) };

		// Heals with cleanse
		d["LIGHT_PURIFY"] = new() { E(SkillEffectType.RemoveAllDebuffs) };
		d["LIGHT_CLEANSE"] = new() { E(SkillEffectType.RemoveDebuff) };
		d["LIGHT_RESURRECT"] = new() { E(SkillEffectType.Revive, 0.30f), E(SkillEffectType.OncePerBattle) };

		// Dark spells — status effects
		d["DARK_FEAR"] = new() { E(SkillEffectType.InflictStatus, 0.35f, 2, "Fear") };
		d["DARK_SILENCE"] = new() { E(SkillEffectType.InflictStatus, 0.30f, 2, "Silence") };
		d["DARK_DRAIN"] = new() { E(SkillEffectType.Lifesteal, 0.25f) };

		return d;
	}
}
