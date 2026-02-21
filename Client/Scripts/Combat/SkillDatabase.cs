using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Combat;

/// <summary>All 180 skill tree abilities. 12 trees × 15 skills each.</summary>
public static class SkillDatabase
{
    private static List<SkillDefinition> _all;
    public static List<SkillDefinition> All => _all ??= Build();
    public static List<SkillDefinition> GetTree(SkillTree tree) => All.Where(s => s.Tree == tree).ToList();
    public static SkillDefinition Get(string id) => All.FirstOrDefault(s => s.Id == id);

    // Helpers
    static SkillDefinition A(string id, string name, SkillTree tree, int tier,
        int sta, int ae, ResourceType res, int range, TargetType tgt, int area,
        int rt, int power, WeaponReq wpn, string desc, Element el = Element.None) =>
        new() { Id = id, Name = name, Tree = tree, Slot = SkillSlotType.Active, Tier = tier, MaxRank = 1,
            StaminaCost = sta, AetherCost = ae, Resource = res, Range = range, Target = tgt, AreaSize = area,
            RtCost = rt, Power = power, Weapon = wpn, Description = desc, Element = el };

    static SkillDefinition P(string id, string name, SkillTree tree, int tier, int maxRank,
        WeaponReq wpn, string desc, string scaling = "") =>
        new() { Id = id, Name = name, Tree = tree, Slot = SkillSlotType.Passive, Tier = tier, MaxRank = maxRank,
            Resource = ResourceType.None, Weapon = wpn, Description = desc, ScalingNote = scaling };

    static SkillDefinition Au(string id, string name, SkillTree tree, int tier, int maxRank,
        WeaponReq wpn, string desc, string scaling = "") =>
        new() { Id = id, Name = name, Tree = tree, Slot = SkillSlotType.Auto, Tier = tier, MaxRank = maxRank,
            Resource = ResourceType.None, Weapon = wpn, Description = desc, ScalingNote = scaling };

    static List<SkillDefinition> Build()
    {
        var s = new List<SkillDefinition>(180);

        // ═══════════════════════════════════════════════════════
        // 1. VANGUARD — Frontline Fighter (STR, VIT)
        // ═══════════════════════════════════════════════════════
        s.Add(P("VAN_WEAPON_MASTERY", "Weapon Mastery", SkillTree.Vanguard, 1, 4, WeaponReq.Any,
            "Increases accuracy and damage of equipped weapon type.", "5%/10%/15%/20%"));
        s.Add(A("VAN_MIGHTY_IMPACT", "Mighty Impact", SkillTree.Vanguard, 1,
            25, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 17, 0, WeaponReq.AnyMelee,
            "Guarantees next attack is a critical hit with 100% accuracy. Cannot counterattack this turn."));
        s.Add(A("VAN_DOUBLE_STRIKE", "Double Strike", SkillTree.Vanguard, 2,
            35, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 20, 0, WeaponReq.Sword,
            "Attack twice in one turn. Requires one-handed blade."));
        s.Add(P("VAN_PINCER_ATTACK", "Pincer Attack", SkillTree.Vanguard, 1, 2, WeaponReq.AnyMelee,
            "When behind an enemy, friendly melee attacks trigger a bonus attack.", "75%/100% damage"));
        s.Add(P("VAN_STRENGTHEN", "Strengthen", SkillTree.Vanguard, 1, 4, WeaponReq.None,
            "Increases physical ATK.", "5%/10%/15%/20%"));
        s.Add(P("VAN_FORTIFY", "Fortify", SkillTree.Vanguard, 1, 4, WeaponReq.None,
            "Increases physical DEF.", "5%/10%/15%/20%"));
        s.Add(Au("VAN_COUNTERATTACK", "Counterattack", SkillTree.Vanguard, 1, 4, WeaponReq.AnyMelee,
            "When struck by melee, chance to counter.", "15%/25%/35%/50%"));
        s.Add(Au("VAN_KNOCKBACK", "Knockback", SkillTree.Vanguard, 1, 4, WeaponReq.AnyMelee,
            "Melee attacks have chance to push target 1 tile.", "10%/20%/30%/40%"));
        s.Add(P("VAN_CONSTITUTION", "Constitution", SkillTree.Vanguard, 1, 4, WeaponReq.None,
            "Increases MAX HP.", "5%/10%/15%/20%"));
        s.Add(Au("VAN_IRON_WILL", "Iron Will", SkillTree.Vanguard, 1, 1, WeaponReq.None,
            "Survive a lethal blow once per battle at 1 HP (HP must be above 25%)."));
        s.Add(A("VAN_OVERPOWER", "Overpower", SkillTree.Vanguard, 3,
            40, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 25, 150, WeaponReq.Greatsword,
            "150% weapon damage, ignoring 30% of target DEF."));
        s.Add(A("VAN_WARPATH", "Warpath", SkillTree.Vanguard, 2,
            30, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 15, 0, WeaponReq.AnyMelee,
            "Next 3 turns: ATK +25% but DEF -15%."));
        s.Add(P("VAN_RAMPART_AURA", "Rampart Aura", SkillTree.Vanguard, 1, 2, WeaponReq.None,
            "Zone around unit halts enemy movement.", "1/2 tiles"));
        s.Add(Au("VAN_PARRY", "Parry", SkillTree.Vanguard, 1, 1, WeaponReq.Sword,
            "Chance to reduce incoming melee damage by 50%."));
        s.Add(Au("VAN_DEFLECT", "Deflect", SkillTree.Vanguard, 1, 1, WeaponReq.AnyPlusShield,
            "Chance to reduce incoming ranged damage by 50%."));

        // ═══════════════════════════════════════════════════════
        // 2. MARKSMAN — Ranged Specialist (DEX, AGI)
        // ═══════════════════════════════════════════════════════
        s.Add(A("MRK_EAGLE_EYE", "Eagle Eye", SkillTree.Marksman, 1,
            20, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 15, 0, WeaponReq.AnyRanged,
            "Next ranged attack has 100% accuracy, ignores elevation penalties."));
        s.Add(A("MRK_TREMENDOUS_SHOT", "Tremendous Shot", SkillTree.Marksman, 2,
            35, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 20, 0, WeaponReq.AnyRanged,
            "Next ranged attack guarantees any on-hit status effect."));
        s.Add(P("MRK_TRAJECTORY", "Trajectory", SkillTree.Marksman, 1, 1, WeaponReq.Bow,
            "Increases ranged attack range by 1 tile."));
        s.Add(A("MRK_SNIPE", "Snipe", SkillTree.Marksman, 3,
            45, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 20, 130, WeaponReq.Fusil,
            "Next attack deals 130% damage, cannot miss. +2 range."));
        s.Add(P("MRK_TRUEFLIGHT", "Trueflight", SkillTree.Marksman, 1, 4, WeaponReq.AnyRanged,
            "Increases ranged accuracy.", "5%/10%/15%/20%"));
        s.Add(Au("MRK_SIDESTEP", "Sidestep", SkillTree.Marksman, 1, 4, WeaponReq.None,
            "Chance to dodge incoming ranged attacks.", "10%/20%/30%/40%"));
        s.Add(A("MRK_BARRAGE", "Barrage", SkillTree.Marksman, 2,
            30, 0, ResourceType.Stamina, 4, TargetType.Line, 3, 25, 80, WeaponReq.Bow,
            "Fire arrows in a line hitting up to 3 targets."));
        s.Add(A("MRK_SUPPRESSIVE_FIRE", "Suppressive Fire", SkillTree.Marksman, 1,
            20, 0, ResourceType.Stamina, 4, TargetType.Single, 0, 17, 60, WeaponReq.Fusil,
            "Ranged attack that reduces target MOVE by 2 for 2 turns."));
        s.Add(A("MRK_PINNING_SHOT", "Pinning Shot", SkillTree.Marksman, 1,
            15, 0, ResourceType.Stamina, 4, TargetType.Single, 0, 17, 50, WeaponReq.Bow,
            "Ranged attack with 30% chance to Immobilize for 1 turn."));
        s.Add(P("MRK_FOCUS_SHOT", "Focus Shot", SkillTree.Marksman, 1, 1, WeaponReq.AnyRanged,
            "If unit did not move this turn, ranged damage +20%."));
        s.Add(P("MRK_HIGH_GROUND", "High Ground", SkillTree.Marksman, 1, 1, WeaponReq.AnyRanged,
            "Ranged damage +10% from higher elevation."));
        s.Add(Au("MRK_CONSERVE_RT", "Conserve RT", SkillTree.Marksman, 1, 1, WeaponReq.AnyRanged,
            "Ranged attacks reduce RT cost by 3."));
        s.Add(P("MRK_LOBBER", "Lobber", SkillTree.Marksman, 1, 1, WeaponReq.Bow,
            "Ranged attacks arc over obstacles. -10% accuracy."));
        s.Add(A("MRK_DOUBLE_SHOT", "Double Shot", SkillTree.Marksman, 3,
            50, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 25, 0, WeaponReq.Bow,
            "Fire two shots at different targets within range."));
        s.Add(A("MRK_CRIPPLING_ARROW", "Crippling Arrow", SkillTree.Marksman, 2,
            25, 0, ResourceType.Stamina, 5, TargetType.Single, 0, 17, 60, WeaponReq.Bow,
            "Ranged attack reducing target DEX by 20% for 3 turns."));

        // ═══════════════════════════════════════════════════════
        // 3. EVOKER — Offensive Spellcaster (ETC)
        // ═══════════════════════════════════════════════════════
        s.Add(A("EVO_MEDITATE", "Meditate", SkillTree.Evoker, 1,
            0, 0, ResourceType.None, 0, TargetType.Self, 0, 10, 0, WeaponReq.None,
            "Instantly recover Aether. No resource cost."));
        s[s.Count - 1].MaxRank = 4;
        s[s.Count - 1].ScalingNote = "15/25/35/50 Aether";
        s.Add(P("EVO_ENGULF", "Engulf", SkillTree.Evoker, 1, 2, WeaponReq.Tome,
            "Increases spell range.", "1/2 tiles"));
        s.Add(P("EVO_CONCENTRATION", "Concentration", SkillTree.Evoker, 1, 1, WeaponReq.Tome,
            "Increases spell accuracy and debuff application rate by 15%."));
        s.Add(P("EVO_SPELLCRAFT", "Spellcraft", SkillTree.Evoker, 1, 4, WeaponReq.Tome,
            "Increases spell damage.", "5%/10%/15%/20%"));
        s.Add(P("EVO_SPELLSTRIKE", "Spellstrike", SkillTree.Evoker, 1, 4, WeaponReq.None,
            "Increases spell accuracy.", "5%/10%/15%/20%"));
        s.Add(Au("EVO_SPELL_WARD", "Spell Ward", SkillTree.Evoker, 1, 4, WeaponReq.None,
            "Chance to reduce incoming spell damage by 50%.", "10%/20%/30%/40%"));
        s.Add(Au("EVO_CONSERVE_AETHER", "Conserve Aether", SkillTree.Evoker, 1, 1, WeaponReq.None,
            "15% chance that a spell costs no Aether."));
        s.Add(A("EVO_ELEMENTAL_SURGE", "Elemental Surge", SkillTree.Evoker, 2,
            0, 30, ResourceType.Aether, 0, TargetType.Self, 0, 15, 0, WeaponReq.Tome,
            "Next spell deals +40% damage. Costs Aether to activate plus spell cost."));
        s.Add(A("EVO_CHAIN_CAST", "Chain Cast", SkillTree.Evoker, 3,
            0, 50, ResourceType.Aether, 0, TargetType.Self, 0, 10, 0, WeaponReq.Tome,
            "Cast the same spell twice. Second cast at 60% power."));
        s.Add(A("EVO_NULLIFY", "Nullify", SkillTree.Evoker, 2,
            0, 25, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.None,
            "Remove all buffs from a single target."));
        s.Add(A("EVO_AETHER_DRAIN", "Aether Drain", SkillTree.Evoker, 2,
            0, 15, ResourceType.Aether, 3, TargetType.Single, 0, 17, 30, WeaponReq.None,
            "Deal minor damage and steal 20 Aether from target."));
        s.Add(Au("EVO_BACKLASH", "Backlash", SkillTree.Evoker, 1, 1, WeaponReq.None,
            "When hit by a spell, recover 10% of damage taken as Aether."));
        s.Add(P("EVO_INSIGHT", "Insight", SkillTree.Evoker, 1, 2, WeaponReq.None,
            "Increases MAX Aether.", "10%/20%"));
        s.Add(P("EVO_EXPAND_MIND", "Expand Mind", SkillTree.Evoker, 1, 2, WeaponReq.Tome,
            "Increases AoE spell radius.", "+1/+2 tiles"));
        s.Add(Au("EVO_QUICKCAST", "Quickcast", SkillTree.Evoker, 1, 1, WeaponReq.None,
            "Spells cost 5 less RT."));

        // ═══════════════════════════════════════════════════════
        // 4. MENDER — Healer/Support (MND)
        // ═══════════════════════════════════════════════════════
        s.Add(P("MND_HEALING_ARTS", "Healing Arts", SkillTree.Mender, 1, 4, WeaponReq.None,
            "Increases healing effectiveness.", "10%/20%/30%/50%"));
        s.Add(P("MND_SANCTUARY", "Sanctuary", SkillTree.Mender, 1, 2, WeaponReq.None,
            "Prevents undead/corrupted from entering adjacent tiles.", "1/2 tiles"));
        s.Add(A("MND_EXORCISM", "Exorcism", SkillTree.Mender, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Single, 0, 17, 80, WeaponReq.None,
            "Heavy Light damage to undead/corrupted. Minor heal to living.", Element.Light));
        s.Add(A("MND_CLEANSE", "Cleanse", SkillTree.Mender, 1,
            0, 15, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Remove 1 debuff from target ally."));
        s.Add(A("MND_PURIFY", "Purify", SkillTree.Mender, 2,
            0, 30, ResourceType.Aether, 3, TargetType.Diamond, 1, 20, 0, WeaponReq.None,
            "Remove all debuffs from target and adjacent allies."));
        s.Add(A("MND_REVITALIZE", "Revitalize", SkillTree.Mender, 2,
            0, 35, ResourceType.Aether, 3, TargetType.Single, 0, 20, 60, WeaponReq.None,
            "Heal target and remove 1 debuff simultaneously."));
        s.Add(A("MND_MASS_HEAL", "Mass Heal", SkillTree.Mender, 3,
            0, 50, ResourceType.Aether, 3, TargetType.Diamond, 2, 25, 40, WeaponReq.Tome,
            "Heal all allies in area. Lower potency than single-target."));
        s.Add(A("MND_BOON_SWIFTNESS", "Boon of Swiftness", SkillTree.Mender, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Grant target ally Haste (RT -15) for 3 turns."));
        s.Add(A("MND_AEGIS_WARD", "Aegis Ward", SkillTree.Mender, 2,
            0, 35, ResourceType.Aether, 3, TargetType.Single, 0, 20, 0, WeaponReq.None,
            "Shield absorbing 25% of target's max HP."));
        s.Add(A("MND_SPIRITSURGE", "Spiritsurge", SkillTree.Mender, 2,
            0, 25, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.None,
            "Restore 30 Aether to target ally."));
        s.Add(P("MND_RESILIENCE", "Resilience", SkillTree.Mender, 1, 2, WeaponReq.None,
            "Status Resist.", "+10%/+20%"));
        s.Add(Au("MND_HP_INFUSION", "HP Infusion", SkillTree.Mender, 1, 2, WeaponReq.None,
            "Recover MAX HP at start of each turn.", "3%/5%"));
        s.Add(P("MND_FIELD_MEDIC", "Field Medic", SkillTree.Mender, 1, 1, WeaponReq.None,
            "Healing items 50% more effective."));
        s.Add(Au("MND_DIVINE_GRACE", "Divine Grace", SkillTree.Mender, 3, 1, WeaponReq.None,
            "When healing an ally, 20% chance to grant ATK +10% for 2 turns."));
        s.Add(A("MND_LAST_RITES", "Last Rites", SkillTree.Mender, 3,
            0, 60, ResourceType.Aether, 3, TargetType.Single, 0, 25, 0, WeaponReq.None,
            "Revive defeated ally at 30% HP. Once per battle."));

        // ═══════════════════════════════════════════════════════
        // 5. RUNEBLADE — Hybrid Melee-Magic (STR, ETC)
        // ═══════════════════════════════════════════════════════
        s.Add(A("RUN_AETHER_EDGE", "Aether Edge", SkillTree.Runeblade, 1,
            8, 7, ResourceType.Both, 0, TargetType.Self, 0, 15, 0, WeaponReq.Sword,
            "Infuse next melee attack with element. +30% EATK as bonus."));
        s.Add(A("RUN_INSTILL_ELEMENT", "Instill Element", SkillTree.Runeblade, 1,
            0, 20, ResourceType.Aether, 0, TargetType.Self, 0, 15, 0, WeaponReq.AnyMelee,
            "Enchant weapon with chosen element for 3 turns. Grants Attuned and Touched."));
        s.Add(A("RUN_FIELD_BARRIER", "Field Barrier", SkillTree.Runeblade, 1,
            0, 20, ResourceType.Aether, 0, TargetType.Self, 0, 15, 0, WeaponReq.None,
            "Grant self EDEF +25% for 3 turns."));
        s.Add(A("RUN_AETHER_BLADE", "Aether Blade", SkillTree.Runeblade, 2,
            15, 15, ResourceType.Both, 1, TargetType.Single, 0, 20, 100, WeaponReq.Sword,
            "Hybrid physical+aether damage (50/50 split)."));
        s.Add(A("RUN_NATURES_TOUCH", "Nature's Touch", SkillTree.Runeblade, 2,
            0, 25, ResourceType.Aether, 0, TargetType.Self, 0, 17, 0, WeaponReq.None,
            "Self-heal for MND×2 + 50. Cannot target allies."));
        s.Add(Au("RUN_ABSORB_AETHER", "Absorb Aether", SkillTree.Runeblade, 1, 1, WeaponReq.AnyMelee,
            "Melee attacks recover 5% of damage dealt as Aether."));
        s.Add(Au("RUN_RUNIC_SHIELD", "Runic Shield", SkillTree.Runeblade, 1, 2, WeaponReq.None,
            "Chance to nullify incoming spell damage.", "10%/20%"));
        s.Add(A("RUN_ELEMENTAL_STRIKE", "Elemental Strike", SkillTree.Runeblade, 2,
            15, 10, ResourceType.Both, 1, TargetType.Single, 0, 17, 80, WeaponReq.Sword,
            "Melee attack using element. Weapon + EATK×0.5."));
        s.Add(P("RUN_SPELLBLADE_MASTERY", "Spellblade Mastery", SkillTree.Runeblade, 1, 1, WeaponReq.AnyMelee,
            "Enchanted weapon attacks gain +10% damage."));
        s.Add(P("RUN_RESISTANCE", "Resistance", SkillTree.Runeblade, 1, 4, WeaponReq.None,
            "Reduces elemental damage taken.", "5%/10%/15%/20%"));
        s.Add(P("RUN_AUGMENT_ELEMENT", "Augment Element", SkillTree.Runeblade, 1, 1, WeaponReq.None,
            "Attacks matching your element deal +15% damage."));
        s.Add(P("RUN_ATTENUATE_ELEMENT", "Attenuate Element", SkillTree.Runeblade, 1, 1, WeaponReq.None,
            "Reduces damage from attacks strong vs your element by 15%."));
        s.Add(A("RUN_VELOCITY_SHIFT", "Velocity Shift", SkillTree.Runeblade, 3,
            0, 40, ResourceType.Aether, 3, TargetType.Single, 0, 20, 0, WeaponReq.None,
            "Reset target ally's RT to 0. Once per battle."));
        s.Add(A("RUN_AETHERIC_BURST", "Aetheric Burst", SkillTree.Runeblade, 3,
            25, 25, ResourceType.Both, 1, TargetType.Diamond, 1, 25, 120, WeaponReq.Sword,
            "Melee AoE elemental explosion."));
        s.Add(P("RUN_CHANNELING", "Channeling", SkillTree.Runeblade, 1, 2, WeaponReq.None,
            "Aether regen per turn.", "+15%/+30%"));

        // ═══════════════════════════════════════════════════════
        // 6. BULWARK — Tank/Protector (VIT, MND)
        // ═══════════════════════════════════════════════════════
        s.Add(A("BLW_PHALANX", "Phalanx", SkillTree.Bulwark, 1,
            20, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 15, 0, WeaponReq.AnyPlusShield,
            "Reduce all damage taken by 40% until next turn. Cannot attack."));
        s.Add(P("BLW_RAMPART_AURA", "Rampart Aura", SkillTree.Bulwark, 1, 2, WeaponReq.None,
            "Halts enemy movement within tiles.", "1/2 tiles"));
        s.Add(A("BLW_GUARDIAN_LOCK", "Guardian Lock", SkillTree.Bulwark, 1,
            15, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 10, 0, WeaponReq.AnyPlusShield,
            "Reduce HP damage by 70% until next turn. Cannot counterattack."));
        s.Add(A("BLW_PROVOKE", "Provoke", SkillTree.Bulwark, 1,
            10, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Force target enemy to attack this unit for 2 turns."));
        s.Add(A("BLW_SHIELD_BASH", "Shield Bash", SkillTree.Bulwark, 1,
            15, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 70, WeaponReq.AnyPlusShield,
            "VIT-based damage. 30% chance to Stun."));
        s.Add(Au("BLW_INTERVENE", "Intervene", SkillTree.Bulwark, 1, 1, WeaponReq.None,
            "When adjacent ally would take lethal damage, absorb 50%."));
        s.Add(P("BLW_STEADFAST", "Steadfast", SkillTree.Bulwark, 1, 1, WeaponReq.None,
            "Cannot be knocked back or pulled."));
        s.Add(P("BLW_ARMOR_MASTER", "Armor Master", SkillTree.Bulwark, 1, 2, WeaponReq.None,
            "Heavy armor DEF bonus.", "+10%/+20%"));
        s.Add(A("BLW_WARD_OF_STEEL", "Ward of Steel", SkillTree.Bulwark, 2,
            30, 0, ResourceType.Stamina, 0, TargetType.Diamond, 1, 20, 0, WeaponReq.None,
            "Self and adjacent allies DEF +20% for 2 turns."));
        s.Add(Au("BLW_UNYIELDING", "Unyielding", SkillTree.Bulwark, 1, 1, WeaponReq.None,
            "Below 30% HP: DEF and EDEF +25%."));
        s.Add(P("BLW_STALWART", "Stalwart", SkillTree.Bulwark, 1, 1, WeaponReq.None,
            "Reduces critical hit damage taken by 50%."));
        s.Add(A("BLW_BRACE", "Brace", SkillTree.Bulwark, 1,
            10, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 10, 0, WeaponReq.AnyPlusShield,
            "Next attack reduced by 60%. Stacks with armor."));
        s.Add(A("BLW_SHIELD_WALL", "Shield Wall", SkillTree.Bulwark, 3,
            45, 0, ResourceType.Stamina, 0, TargetType.Line, 3, 25, 0, WeaponReq.AnyPlusShield,
            "Defensive barrier. Allies behind take 30% less for 2 turns."));
        s.Add(Au("BLW_RETRIBUTION", "Retribution", SkillTree.Bulwark, 2, 1, WeaponReq.AnyMelee,
            "When hit, next melee attack deals +30% damage."));
        s.Add(P("BLW_BASTION", "Bastion", SkillTree.Bulwark, 3, 1, WeaponReq.None,
            "Above 50% HP: adjacent allies take 10% less damage."));

        // ═══════════════════════════════════════════════════════
        // 7. SHADOWSTEP — Assassin/Rogue (DEX, AGI)
        // ═══════════════════════════════════════════════════════
        s.Add(P("SHD_DOUBLE_ATTACK", "Double Attack", SkillTree.Shadowstep, 1, 1, WeaponReq.Dagger,
            "Attack twice when dual-wielding one-handed weapons."));
        s.Add(Au("SHD_DODGE", "Dodge", SkillTree.Shadowstep, 1, 4, WeaponReq.None,
            "Chance to evade melee attacks.", "10%/20%/30%/40%"));
        s.Add(A("SHD_STEALTH", "Stealth", SkillTree.Shadowstep, 1,
            20, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 15, 0, WeaponReq.None,
            "Invisible for 2 turns. Broken by attacking or taking damage."));
        s.Add(P("SHD_BACKSTAB", "Backstab", SkillTree.Shadowstep, 1, 1, WeaponReq.Dagger,
            "Attacks from behind deal +30% damage."));
        s.Add(A("SHD_POISON_STRIKE", "Poison Strike", SkillTree.Shadowstep, 1,
            15, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 60, WeaponReq.Dagger,
            "Melee attack with 40% chance to Poison (4 turns)."));
        s.Add(A("SHD_SMOKE_BOMB", "Smoke Bomb", SkillTree.Shadowstep, 1,
            15, 0, ResourceType.Stamina, 0, TargetType.Self, 1, 10, 0, WeaponReq.Thrown,
            "Smoke at position. Blocks ranged targeting for 2 turns."));
        s.Add(A("SHD_SHADOWSTRIKE", "Shadowstrike", SkillTree.Shadowstep, 2,
            30, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 20, 200, WeaponReq.Dagger,
            "From stealth or behind: 200% weapon damage."));
        s.Add(A("SHD_FEINT", "Feint", SkillTree.Shadowstep, 1,
            10, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 10, 0, WeaponReq.AnyMelee,
            "Reduce target ACC by 30% for 2 turns."));
        s.Add(P("SHD_SWIFTFOOT", "Swiftfoot", SkillTree.Shadowstep, 1, 2, WeaponReq.None,
            "MOVE bonus.", "+1/+2"));
        s.Add(P("SHD_JUMP", "Jump", SkillTree.Shadowstep, 1, 2, WeaponReq.None,
            "JUMP bonus.", "+1/+2"));
        s.Add(P("SHD_THROWING_MASTERY", "Throwing Mastery", SkillTree.Shadowstep, 1, 1, WeaponReq.Thrown,
            "Thrown weapon damage +20%, range +1."));
        s.Add(Au("SHD_LETHAL_TEMPO", "Lethal Tempo", SkillTree.Shadowstep, 1, 1, WeaponReq.None,
            "After defeating enemy, RT resets to 0 (once per turn)."));
        s.Add(A("SHD_EXPOSE_WEAKNESS", "Expose Weakness", SkillTree.Shadowstep, 2,
            25, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 0, WeaponReq.AnyMelee,
            "Mark target: all attacks deal +20% for 2 turns."));
        s.Add(A("SHD_SHADOW_CLONE", "Shadow Clone", SkillTree.Shadowstep, 3,
            50, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 25, 0, WeaponReq.None,
            "Create decoy with 1 HP that draws attacks."));
        s.Add(A("SHD_ASSASSINATE", "Assassinate", SkillTree.Shadowstep, 3,
            60, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 30, 999, WeaponReq.Dagger,
            "If target below 20% HP, instantly defeat. Ignores anti one-shot cap."));

        // ═══════════════════════════════════════════════════════
        // 8. DREADNOUGHT — Fear Warrior (STR, MND)
        // ═══════════════════════════════════════════════════════
        s.Add(A("DRD_FRIGHTEN", "Frighten", SkillTree.Dreadnought, 1,
            20, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 70, WeaponReq.Greatsword,
            "Melee attack inflicting Fear (all stats -15%) for 2 turns."));
        s.Add(A("DRD_LAMENT", "Lament of the Dead", SkillTree.Dreadnought, 2,
            0, 35, ResourceType.Aether, 0, TargetType.Diamond, 1, 20, 0, WeaponReq.None,
            "Terrifying aura: all adjacent enemies gain Fear for 2 turns."));
        s.Add(A("DRD_DRAIN_HEART", "Drain Heart", SkillTree.Dreadnought, 1,
            20, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 70, WeaponReq.AnyMelee,
            "Melee attack healing attacker for 30% of damage dealt."));
        s.Add(A("DRD_DRAIN_AETHER", "Drain Aether", SkillTree.Dreadnought, 2,
            25, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 17, 60, WeaponReq.AnyMelee,
            "Melee attack stealing 15% of damage dealt as Aether."));
        s.Add(A("DRD_INTIMIDATE", "Intimidate", SkillTree.Dreadnought, 1,
            15, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Reduce target ATK by 20% for 3 turns."));
        s.Add(P("DRD_DARK_AURA", "Dark Aura", SkillTree.Dreadnought, 1, 2, WeaponReq.None,
            "Enemies in aura deal less damage.", "5%/10%"));
        s.Add(A("DRD_SANGUINE_ASSAULT", "Sanguine Assault", SkillTree.Dreadnought, 2,
            30, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 20, 120, WeaponReq.Greatsword,
            "Powerful melee. If it kills, heal to full HP."));
        s.Add(A("DRD_TERROR_STANCE", "Terror Stance", SkillTree.Dreadnought, 1,
            15, 0, ResourceType.Stamina, 0, TargetType.Self, 0, 10, 0, WeaponReq.None,
            "3 turns: melee attacks 25% chance to inflict Fear."));
        s.Add(A("DRD_WEAKEN", "Weaken", SkillTree.Dreadnought, 1,
            0, 15, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Reduce target DEF by 20% for 3 turns."));
        s.Add(A("DRD_WITHERING_GAZE", "Withering Gaze", SkillTree.Dreadnought, 2,
            0, 25, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.None,
            "Reduce target ATK and DEF by 15% for 2 turns."));
        s.Add(Au("DRD_IRON_MAIDEN", "Iron Maiden", SkillTree.Dreadnought, 1, 2, WeaponReq.None,
            "Adjacent enemies: chance Poison + Silence.", "20%/35%"));
        s.Add(A("DRD_DREAD_CHARGE", "Dread Charge", SkillTree.Dreadnought, 2,
            30, 0, ResourceType.Stamina, 1, TargetType.Line, 2, 25, 90, WeaponReq.Greatsword,
            "Rush 2 tiles, damage all in path. Inflicts Fear."));
        s.Add(P("DRD_BLOOD_PRICE", "Blood Price", SkillTree.Dreadnought, 1, 1, WeaponReq.None,
            "Spend 10% HP instead of Stamina/Aether for next action."));
        s.Add(Au("DRD_UNDYING_RAGE", "Undying Rage", SkillTree.Dreadnought, 1, 1, WeaponReq.None,
            "Below 25% HP: ATK +30%."));
        s.Add(A("DRD_ABYSSAL_STRIKE", "Abyssal Strike", SkillTree.Dreadnought, 3,
            55, 0, ResourceType.Stamina, 1, TargetType.Single, 0, 25, 130, WeaponReq.Greatsword,
            "Dark-element melee. Bonus damage = target's missing HP%.", Element.Dark));

        // ═══════════════════════════════════════════════════════
        // 9. WARSINGER — Battlefield Bard (MND, AGI)
        // ═══════════════════════════════════════════════════════
        s.Add(A("WAR_BATTLE_HYMN", "Battle Hymn", SkillTree.Warsinger, 1,
            0, 15, ResourceType.Aether, 0, TargetType.Diamond, 2, 15, 0, WeaponReq.Instrument,
            "All allies in area ATK +10% for 3 turns."));
        s.Add(A("WAR_SHIELD_CHANT", "Shield Chant", SkillTree.Warsinger, 1,
            0, 15, ResourceType.Aether, 0, TargetType.Diamond, 2, 15, 0, WeaponReq.Instrument,
            "All allies in area DEF +10% for 3 turns."));
        s.Add(A("WAR_DIRGE_DESPAIR", "Dirge of Despair", SkillTree.Warsinger, 2,
            0, 25, ResourceType.Aether, 0, TargetType.Diamond, 2, 17, 0, WeaponReq.Instrument,
            "All enemies in area: ATK -10% for 3 turns."));
        s.Add(A("WAR_TEMPO_SHIFT", "Tempo Shift", SkillTree.Warsinger, 2,
            0, 30, ResourceType.Aether, 0, TargetType.Diamond, 2, 20, 0, WeaponReq.Instrument,
            "All allies in area: RT -10 for 3 turns."));
        s.Add(A("WAR_LULLABY", "Lullaby", SkillTree.Warsinger, 2,
            0, 30, ResourceType.Aether, 3, TargetType.Diamond, 1, 20, 0, WeaponReq.Instrument,
            "30% chance to Sleep all targets in area for 2 turns."));
        s.Add(A("WAR_DISSONANCE", "Dissonance", SkillTree.Warsinger, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Single, 0, 17, 40, WeaponReq.Instrument,
            "Minor damage, 25% chance to Silence for 2 turns."));
        s.Add(A("WAR_RALLYING_CRY", "Rallying Cry", SkillTree.Warsinger, 1,
            0, 20, ResourceType.Aether, 0, TargetType.Diamond, 2, 15, 0, WeaponReq.Instrument,
            "Cleanse Fear from allies, grant +5% ATK for 2 turns."));
        s.Add(A("WAR_CADENCE_HASTE", "Cadence of Haste", SkillTree.Warsinger, 3,
            0, 40, ResourceType.Aether, 0, TargetType.Diamond, 2, 20, 0, WeaponReq.Instrument,
            "All allies gain Haste (RT -15) for 2 turns."));
        s.Add(A("WAR_SOOTHING_MELODY", "Soothing Melody", SkillTree.Warsinger, 1,
            0, 20, ResourceType.Aether, 0, TargetType.Diamond, 2, 15, 30, WeaponReq.Instrument,
            "Heal all allies in area (MND × 0.5 + 30)."));
        s.Add(P("WAR_WAR_DRUMS", "War Drums", SkillTree.Warsinger, 1, 1, WeaponReq.Instrument,
            "Allies within 2 tiles gain +5% CRIT."));
        s.Add(Au("WAR_ENCORE", "Encore", SkillTree.Warsinger, 1, 1, WeaponReq.Instrument,
            "20% chance song duration extends by 1 turn."));
        s.Add(P("WAR_HARMONIZE", "Harmonize", SkillTree.Warsinger, 1, 1, WeaponReq.Instrument,
            "If another Warsinger within 3 tiles, both songs +5% stronger."));
        s.Add(A("WAR_REQUIEM", "Requiem", SkillTree.Warsinger, 3,
            0, 50, ResourceType.Aether, 3, TargetType.Diamond, 1, 25, 100, WeaponReq.Instrument,
            "Heavy Light damage to undead. Heal living allies in area.", Element.Light));
        s.Add(A("WAR_POIGNANT_MELODY", "Poignant Melody", SkillTree.Warsinger, 2,
            0, 35, ResourceType.Aether, 3, TargetType.Single, 0, 20, 60, WeaponReq.Instrument,
            "Aether attack targeting MND. 25% chance Charm for 2 turns."));
        s.Add(A("WAR_SILENCE_SONG", "Silence Song", SkillTree.Warsinger, 2,
            0, 30, ResourceType.Aether, 3, TargetType.Diamond, 1, 20, 0, WeaponReq.Instrument,
            "25% chance to Silence all targets in area for 2 turns."));

        // ═══════════════════════════════════════════════════════
        // 10. TEMPLAR — Holy Knight (VIT, MND)
        // ═══════════════════════════════════════════════════════
        s.Add(A("TMP_SMITE", "Smite", SkillTree.Templar, 1,
            12, 8, ResourceType.Both, 1, TargetType.Single, 0, 17, 80, WeaponReq.Sword,
            "Light-element melee. +50% vs Dark.", Element.Light));
        s.Add(A("TMP_LAY_ON_HANDS", "Lay on Hands", SkillTree.Templar, 1,
            0, 15, ResourceType.Aether, 1, TargetType.Single, 0, 15, 80, WeaponReq.None,
            "Heal adjacent ally for MND×1.5 + 80."));
        s.Add(A("TMP_CONSECRATE", "Consecrate", SkillTree.Templar, 2,
            0, 30, ResourceType.Aether, 0, TargetType.Diamond, 1, 20, 0, WeaponReq.None,
            "Bless ground: allies gain HP regen (3%) for 3 turns."));
        s.Add(A("TMP_HOLY_SHIELD", "Holy Shield", SkillTree.Templar, 2,
            0, 25, ResourceType.Aether, 0, TargetType.Self, 0, 15, 0, WeaponReq.None,
            "Immunity to Dark-element damage for 2 turns."));
        s.Add(A("TMP_ABSOLUTION", "Absolution", SkillTree.Templar, 3,
            0, 40, ResourceType.Aether, 3, TargetType.Single, 0, 20, 0, WeaponReq.None,
            "Remove Condemn status, allowing resurrection."));
        s.Add(P("TMP_CRUSADERS_ZEAL", "Crusader's Zeal", SkillTree.Templar, 1, 1, WeaponReq.None,
            "After healing, gain ATK +10% for 1 turn."));
        s.Add(P("TMP_DIVINE_ARMOR", "Divine Armor", SkillTree.Templar, 1, 2, WeaponReq.None,
            "EDEF bonus.", "+10%/+20%"));
        s.Add(A("TMP_JUDGMENT", "Judgment", SkillTree.Templar, 2,
            0, 35, ResourceType.Aether, 3, TargetType.Single, 0, 20, 90, WeaponReq.Tome,
            "Light-element ranged. ETC-scaled. Ignores 20% EDEF.", Element.Light));
        s.Add(A("TMP_BLESSED_WEAPON", "Blessed Weapon", SkillTree.Templar, 1,
            8, 7, ResourceType.Both, 0, TargetType.Self, 0, 10, 0, WeaponReq.Sword,
            "Enchant with Light for 3 turns."));
        s.Add(A("TMP_SANCTIFIED_GROUND", "Sanctified Ground", SkillTree.Templar, 3,
            0, 50, ResourceType.Aether, 0, TargetType.Diamond, 2, 25, 0, WeaponReq.None,
            "Holy zone: allies -20% damage taken, undead cannot enter."));
        s.Add(A("TMP_OATH_PROTECTION", "Oath of Protection", SkillTree.Templar, 1,
            15, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Redirect 30% of ally's damage to yourself for 3 turns."));
        s.Add(Au("TMP_STALWART_FAITH", "Stalwart Faith", SkillTree.Templar, 1, 1, WeaponReq.None,
            "Immune to Fear and Charm."));
        s.Add(P("TMP_INQUISITORS_EYE", "Inquisitor's Eye", SkillTree.Templar, 1, 1, WeaponReq.None,
            "See stealthed/invisible units within 3 tiles."));
        s.Add(A("TMP_RADIANT_BURST", "Radiant Burst", SkillTree.Templar, 3,
            25, 30, ResourceType.Both, 0, TargetType.Ring, 2, 25, 110, WeaponReq.Sword,
            "Light AoE. Heavy damage, 30% Blind.", Element.Light));
        s.Add(A("TMP_RESURRECT", "Resurrect", SkillTree.Templar, 3,
            0, 70, ResourceType.Aether, 3, TargetType.Single, 0, 30, 0, WeaponReq.Tome,
            "Revive ally at 50% HP. Once per battle. Requires MND 20."));

        // ═══════════════════════════════════════════════════════
        // 11. HEXER — Dark Arts (ETC, MND)
        // ═══════════════════════════════════════════════════════
        s.Add(A("HEX_CURSE", "Curse", SkillTree.Hexer, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.Tome,
            "Healing received -50% for 3 turns."));
        s.Add(A("HEX_ENFEEBLE", "Enfeeble", SkillTree.Hexer, 1,
            0, 15, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.Tome,
            "Reduce target's highest stat by 20% for 3 turns."));
        s.Add(A("HEX_CONDEMN", "Condemn", SkillTree.Hexer, 2,
            0, 35, ResourceType.Aether, 3, TargetType.Single, 0, 20, 0, WeaponReq.Tome,
            "Target cannot be revived if defeated. Lasts 4 turns."));
        s.Add(A("HEX_WITHER", "Wither", SkillTree.Hexer, 1,
            0, 15, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.Tome,
            "HP regen zeroed, Bleed 3%/turn for 3 turns."));
        s.Add(A("HEX_HEX_WARD", "Hex Ward", SkillTree.Hexer, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Diamond, 1, 20, 0, WeaponReq.Tome,
            "EDEF -15% to all targets in area for 3 turns."));
        s.Add(A("HEX_SOUL_SIPHON", "Soul Siphon", SkillTree.Hexer, 2,
            0, 25, ResourceType.Aether, 3, TargetType.Single, 0, 17, 70, WeaponReq.Tome,
            "Dark damage. Heal self for 40% of damage dealt.", Element.Dark));
        s.Add(A("HEX_MASS_DEBILITATE", "Mass Debilitate", SkillTree.Hexer, 3,
            0, 50, ResourceType.Aether, 3, TargetType.Diamond, 2, 25, 0, WeaponReq.Tome,
            "ATK and DEF -15% to all enemies in area for 2 turns."));
        s.Add(A("HEX_LEECH_AETHER", "Leech Aether", SkillTree.Hexer, 1,
            0, 10, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Steal 25 Aether from target."));
        s.Add(A("HEX_BLIGHT", "Blight", SkillTree.Hexer, 2,
            0, 30, ResourceType.Aether, 3, TargetType.Diamond, 1, 20, 50, WeaponReq.Tome,
            "Dark AoE. 30% chance to Poison all targets.", Element.Dark));
        s.Add(A("HEX_PETRIFY", "Petrify", SkillTree.Hexer, 3,
            0, 45, ResourceType.Aether, 3, TargetType.Single, 0, 20, 0, WeaponReq.Tome,
            "25% chance Petrify for 2 turns (cannot act, +50% damage taken)."));
        s.Add(A("HEX_MIASMA", "Miasma", SkillTree.Hexer, 2,
            0, 30, ResourceType.Aether, 0, TargetType.Ring, 2, 20, 40, WeaponReq.None,
            "Toxic cloud: enemies take damage, 20% Poison."));
        s.Add(P("HEX_DARK_PACT", "Dark Pact", SkillTree.Hexer, 1, 1, WeaponReq.None,
            "Dark spells cost 15% less Aether."));
        s.Add(P("HEX_MALICE", "Malice", SkillTree.Hexer, 1, 1, WeaponReq.None,
            "Debuff duration +1 turn."));
        s.Add(Au("HEX_DREAD_HARVEST", "Dread Harvest", SkillTree.Hexer, 1, 1, WeaponReq.None,
            "Enemy dies within 3 tiles: recover 20 Aether."));
        s.Add(A("HEX_VOID_RUPTURE", "Void Rupture", SkillTree.Hexer, 3,
            0, 60, ResourceType.Aether, 3, TargetType.Cross, 2, 25, 100, WeaponReq.Tome,
            "Heavy Dark AoE. Debuffed targets take +30% bonus.", Element.Dark));

        // ═══════════════════════════════════════════════════════
        // 12. TACTICIAN — Battlefield Controller (AGI, MND)
        // ═══════════════════════════════════════════════════════
        s.Add(A("TAC_BARRICADE", "Barricade", SkillTree.Tactician, 1,
            15, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Place obstacle blocking movement for 3 turns."));
        s.Add(A("TAC_TRAP", "Trap", SkillTree.Tactician, 1,
            15, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 15, 40, WeaponReq.None,
            "Hidden trap: damage + Immobilize 1 turn."));
        s.Add(A("TAC_REPOSITION", "Reposition", SkillTree.Tactician, 1,
            15, 0, ResourceType.Stamina, 3, TargetType.Single, 0, 10, 0, WeaponReq.None,
            "Swap positions with target ally."));
        s.Add(P("TAC_FIELD_ALCHEMY", "Field Alchemy", SkillTree.Tactician, 1, 4, WeaponReq.None,
            "Items more effective.", "25%/50%/75%/100%"));
        s.Add(A("TAC_ANALYZE", "Analyze", SkillTree.Tactician, 1,
            10, 0, ResourceType.Stamina, 4, TargetType.Single, 0, 10, 0, WeaponReq.None,
            "Reveal target stats, element, HP, Aether, equipped abilities."));
        s.Add(P("TAC_EXPLOIT_OPENING", "Exploit Opening", SkillTree.Tactician, 1, 1, WeaponReq.None,
            "Attacks vs Stunned/Sleeping/Immobilized +25% damage."));
        s.Add(A("TAC_DELAY_TACTICS", "Delay Tactics", SkillTree.Tactician, 2,
            0, 25, ResourceType.Aether, 3, TargetType.Single, 0, 17, 0, WeaponReq.None,
            "Add +20 RT to target enemy."));
        s.Add(A("TAC_QUICKEN", "Quicken", SkillTree.Tactician, 1,
            0, 20, ResourceType.Aether, 3, TargetType.Single, 0, 15, 0, WeaponReq.None,
            "Reduce target ally's RT by 15."));
        s.Add(P("TAC_TERRAIN_MASTERY", "Terrain Mastery", SkillTree.Tactician, 1, 1, WeaponReq.None,
            "Ignore terrain movement penalties."));
        s.Add(P("TAC_WADE", "Wade", SkillTree.Tactician, 1, 2, WeaponReq.None,
            "Water/difficult terrain cost.", "normal/no cost"));
        s.Add(P("TAC_TREASURE_HUNT", "Treasure Hunt", SkillTree.Tactician, 1, 2, WeaponReq.None,
            "Bonus loot chance.", "10%/20%"));
        s.Add(P("TAC_TACTICIANS_EYE", "Tactician's Eye", SkillTree.Tactician, 1, 2, WeaponReq.None,
            "See turns ahead in turn order.", "1/2 turns"));
        s.Add(P("TAC_MAX_TP", "Max TP", SkillTree.Tactician, 1, 4, WeaponReq.None,
            "Tactical Points bonus.", "+1/+2/+3/+4"));
        s.Add(Au("TAC_REFLECT_DAMAGE", "Reflect Damage", SkillTree.Tactician, 1, 2, WeaponReq.None,
            "Return melee damage to attacker.", "5%/10%"));
        s.Add(Au("TAC_REFLECT_MAGIC", "Reflect Magic", SkillTree.Tactician, 1, 2, WeaponReq.None,
            "Return spell damage to caster.", "5%/10%"));

        return s;
    }
}
