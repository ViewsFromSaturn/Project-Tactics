using System.Collections.Generic;

namespace ProjectTactics.Combat;

// ═══════════════════════════════════════════════════════════════
//  ENUMS
// ═══════════════════════════════════════════════════════════════

public enum SkillSlotType { Active, Passive, Auto }
public enum ResourceType { None, Stamina, Aether, Both }
public enum Element { None, Fire, Ice, Lightning, Earth, Wind, Water, Light, Dark }
public enum TargetType { Self, Single, Diamond, Line, Ring, Cross }
public enum SpellCastType { Missile, Indirect, Healing, Transfer, Status, Utility }

public enum SkillTree
{
    Vanguard, Marksman, Evoker, Mender, Runeblade, Bulwark,
    Shadowstep, Dreadnought, Warsinger, Templar, Hexer, Tactician
}

public enum WeaponReq
{
    None, AnyMelee, AnyRanged, Any,
    Sword, Katana, Dagger, Greatsword, Axe, Hammer, Spear, Mace,
    Bow, Crossbow, Fusil, Thrown, Tome, Instrument,
    AnyPlusShield, AnyOnehanded
}

// ═══════════════════════════════════════════════════════════════
//  SKILL DEFINITION (Skill Tree abilities — 180 total)
// ═══════════════════════════════════════════════════════════════

public class SkillDefinition
{
    public string Id;            // e.g. "VAN_MIGHTY_IMPACT"
    public string Name;          // e.g. "Mighty Impact"
    public SkillTree Tree;       // Which tree it belongs to
    public SkillSlotType Slot;   // Active, Passive, Auto
    public int Tier;             // 1-4 (I-IV), passives may have ranks
    public int MaxRank;          // For scaling passives (1-4 ranks), 1 for non-scaling

    // Cost
    public int StaminaCost;
    public int AetherCost;
    public ResourceType Resource;

    // Targeting
    public int Range;            // 0 = self, 1 = adjacent, etc.
    public TargetType Target;
    public int AreaSize;         // 0 = single, 1+ = AoE radius

    // Combat
    public int RtCost;           // Added to RT
    public int Power;            // Base power (0 for non-damage)
    public Element Element;

    // Requirements
    public WeaponReq Weapon;

    // Text
    public string Description;
    public string ScalingNote;   // e.g. "5%/10%/15%/20%" for ranked passives

    public SkillDefinition() { Element = Element.None; Weapon = WeaponReq.None; MaxRank = 1; }
}

// ═══════════════════════════════════════════════════════════════
//  SPELL DEFINITION (Element spells — 105 total)
// ═══════════════════════════════════════════════════════════════

public class SpellDefinition
{
    public string Id;            // e.g. "FIRE_EMBERBOLT_1"
    public string Name;          // e.g. "Emberbolt I"
    public Element Element;
    public int Tier;             // 1-4

    // Cost
    public int AetherCost;

    // Targeting
    public int RangeMin;         // For missiles (e.g. 3)
    public int RangeMax;         // For missiles (e.g. 6), or just range for indirect
    public SpellCastType CastType;
    public TargetType Target;
    public int AreaSize;

    // Combat
    public int RtCost;
    public int Power;            // Base power scaling

    // Requirements
    public string StatReq;       // e.g. "ETC" or "MND"
    public int StatReqValue;     // e.g. 8 for Tier II

    // RPP
    public int RppCost;          // Cost to learn

    // Text
    public string Description;
    public string StatusEffect;  // e.g. "Fire Averse", "Poison", ""

    public SpellDefinition() { Element = Element.None; Target = TargetType.Single; }
}
