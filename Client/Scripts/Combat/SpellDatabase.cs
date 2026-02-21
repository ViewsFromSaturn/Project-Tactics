using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Combat;

/// <summary>All 105 spells. 6 standard elements × 10 + Light 23 + Dark 22.</summary>
public static class SpellDatabase
{
    private static List<SpellDefinition> _all;
    public static List<SpellDefinition> All => _all ??= Build();
    public static List<SpellDefinition> GetElement(Element el) => All.Where(s => s.Element == el).ToList();
    public static List<SpellDefinition> GetTier(int tier) => All.Where(s => s.Tier == tier).ToList();
    public static SpellDefinition Get(string id) => All.FirstOrDefault(s => s.Id == id);

    // RPP costs per tier
    public static int GetRppCost(int tier) => tier switch { 1 => 5, 2 => 15, 3 => 30, 4 => 50, _ => 0 };

    // Stat gate per tier
    public static int GetStatReq(int tier) => tier switch { 1 => 1, 2 => 8, 3 => 16, 4 => 25, _ => 0 };

    // ── Helpers ──────────────────────────────────────────────
    static SpellDefinition Missile(string id, string name, Element el, int tier,
        int cost, int rMin, int rMax, int rt, int power, string stat, string desc, string status = "") =>
        new() { Id = id, Name = name, Element = el, Tier = tier, AetherCost = cost,
            RangeMin = rMin, RangeMax = rMax, CastType = SpellCastType.Missile,
            Target = TargetType.Single, AreaSize = 0, RtCost = rt, Power = power,
            StatReq = stat, StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = status };

    static SpellDefinition Indirect(string id, string name, Element el, int tier,
        int cost, int range, int area, int rt, int power, string stat, string desc, string status = "") =>
        new() { Id = id, Name = name, Element = el, Tier = tier, AetherCost = cost,
            RangeMin = range, RangeMax = range, CastType = SpellCastType.Indirect,
            Target = area > 0 ? TargetType.Diamond : TargetType.Single, AreaSize = area,
            RtCost = rt, Power = power,
            StatReq = stat, StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = status };

    static SpellDefinition Heal(string id, string name, int tier,
        int cost, int range, TargetType tgt, int area, int rt, int power, string desc) =>
        new() { Id = id, Name = name, Element = Element.Light, Tier = tier, AetherCost = cost,
            RangeMin = range, RangeMax = range, CastType = SpellCastType.Healing,
            Target = tgt, AreaSize = area, RtCost = rt, Power = power,
            StatReq = "MND", StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = "" };

    static SpellDefinition Transfer(string id, string name, int tier,
        int cost, int range, int rt, int power, string desc) =>
        new() { Id = id, Name = name, Element = Element.Dark, Tier = tier, AetherCost = cost,
            RangeMin = range, RangeMax = range, CastType = SpellCastType.Transfer,
            Target = TargetType.Single, AreaSize = 0, RtCost = rt, Power = power,
            StatReq = "ETC", StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = "" };

    static SpellDefinition Status(string id, string name, int tier,
        int cost, int range, TargetType tgt, int area, int rt, string desc, string status) =>
        new() { Id = id, Name = name, Element = Element.Dark, Tier = tier, AetherCost = cost,
            RangeMin = range, RangeMax = range, CastType = SpellCastType.Status,
            Target = tgt, AreaSize = area, RtCost = rt, Power = 0,
            StatReq = "ETC", StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = status };

    static SpellDefinition Utility(string id, string name, Element el, int tier,
        int cost, int range, TargetType tgt, int area, int rt, string stat, string desc) =>
        new() { Id = id, Name = name, Element = el, Tier = tier, AetherCost = cost,
            RangeMin = range, RangeMax = range, CastType = SpellCastType.Utility,
            Target = tgt, AreaSize = area, RtCost = rt, Power = 0,
            StatReq = stat, StatReqValue = GetStatReq(tier), RppCost = GetRppCost(tier),
            Description = desc, StatusEffect = "" };

    // ═════════════════════════════════════════════════════════
    static List<SpellDefinition> Build()
    {
        var s = new List<SpellDefinition>(105);

        // ─── FIRE (10) ──────────────────────────────────────
        s.Add(Missile("FIRE_BOLT_1", "Emberbolt I", Element.Fire, 1, 15, 3, 6, 13, 40, "ETC",
            "A missile of ether-forged flame piercing a single target.", "Fire Averse"));
        s.Add(Missile("FIRE_BOLT_2", "Emberbolt II", Element.Fire, 2, 30, 3, 6, 16, 75, "ETC",
            "Intensified flame missile. Greater damage.", "Fire Averse"));
        s.Add(Missile("FIRE_BOLT_3", "Emberbolt III", Element.Fire, 3, 45, 3, 6, 19, 110, "ETC",
            "Searing flame missile. Heavy damage.", "Fire Averse"));
        s.Add(Missile("FIRE_BOLT_4", "Emberbolt IV", Element.Fire, 4, 60, 3, 6, 22, 150, "ETC",
            "White-hot flame missile. Devastating single-target damage.", "Fire Averse"));
        s.Add(Indirect("FIRE_STORM_1", "Firestorm I", Element.Fire, 1, 22, 5, 0, 14, 35, "ETC",
            "Conjures flame at a distant point, scorching a single target.", "Fire Averse"));
        s.Add(Indirect("FIRE_STORM_2", "Firestorm II", Element.Fire, 2, 46, 5, 2, 19, 55, "ETC",
            "A roaring blaze engulfs multiple targets in a diamond.", "Fire Averse"));
        s.Add(Indirect("FIRE_STORM_3", "Firestorm III", Element.Fire, 3, 70, 5, 3, 24, 80, "ETC",
            "A conflagration consuming a wide area. Heavy fire damage.", "Fire Averse"));
        s.Add(Indirect("FIRE_STORM_4", "Firestorm IV", Element.Fire, 4, 94, 5, 3, 28, 110, "ETC",
            "An inferno that devours all within range. Devastating AoE.", "Fire Averse"));
        s.Add(Utility("FIRE_INSTILL", "Instill Fire", Element.Fire, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Fire-Touched to a single target for 3 turns."));
        s.Add(Utility("FIRE_GUARD", "Flameguard", Element.Fire, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Fire Attuned to multiple targets for 3 turns."));

        // ─── ICE (10) ───────────────────────────────────────
        s.Add(Missile("ICE_BOLT_1", "Frostlance I", Element.Ice, 1, 15, 3, 6, 13, 40, "ETC",
            "A shard of crystallized ether impales a single target.", "Ice Averse"));
        s.Add(Missile("ICE_BOLT_2", "Frostlance II", Element.Ice, 2, 30, 3, 6, 16, 75, "ETC",
            "A larger ice shard. Greater damage.", "Ice Averse"));
        s.Add(Missile("ICE_BOLT_3", "Frostlance III", Element.Ice, 3, 45, 3, 6, 19, 110, "ETC",
            "A glacial lance. Heavy single-target damage.", "Ice Averse"));
        s.Add(Missile("ICE_BOLT_4", "Frostlance IV", Element.Ice, 4, 60, 3, 6, 22, 150, "ETC",
            "An absolute zero spike. Devastating single-target damage.", "Ice Averse"));
        s.Add(Indirect("ICE_STORM_1", "Blizzard I", Element.Ice, 1, 22, 5, 0, 14, 35, "ETC",
            "Flash-freezes an area around a single target.", "Ice Averse"));
        s.Add(Indirect("ICE_STORM_2", "Blizzard II", Element.Ice, 2, 46, 5, 2, 19, 55, "ETC",
            "A freezing storm engulfs multiple targets.", "Ice Averse"));
        s.Add(Indirect("ICE_STORM_3", "Blizzard III", Element.Ice, 3, 70, 5, 3, 24, 80, "ETC",
            "An arctic tempest covering a wide area. Heavy ice damage.", "Ice Averse"));
        s.Add(Indirect("ICE_STORM_4", "Blizzard IV", Element.Ice, 4, 94, 5, 3, 28, 110, "ETC",
            "A catastrophic glacier erupts. Devastating AoE.", "Ice Averse"));
        s.Add(Utility("ICE_INSTILL", "Instill Frost", Element.Ice, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Ice-Touched to a single target for 3 turns."));
        s.Add(Utility("ICE_GUARD", "Frostguard", Element.Ice, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Ice Attuned to multiple targets for 3 turns."));

        // ─── LIGHTNING (10) ─────────────────────────────────
        s.Add(Missile("LTN_BOLT_1", "Sparkbolt I", Element.Lightning, 1, 15, 3, 6, 13, 40, "ETC",
            "An arc of lightning strikes a single target.", "Lightning Averse"));
        s.Add(Missile("LTN_BOLT_2", "Sparkbolt II", Element.Lightning, 2, 30, 3, 6, 16, 75, "ETC",
            "A powerful lightning arc. Greater damage.", "Lightning Averse"));
        s.Add(Missile("LTN_BOLT_3", "Sparkbolt III", Element.Lightning, 3, 45, 3, 6, 19, 110, "ETC",
            "A thunderbolt. Heavy single-target damage.", "Lightning Averse"));
        s.Add(Missile("LTN_BOLT_4", "Sparkbolt IV", Element.Lightning, 4, 60, 3, 6, 22, 150, "ETC",
            "Divine lightning. Devastating single-target damage.", "Lightning Averse"));
        s.Add(Indirect("LTN_STORM_1", "Tempest I", Element.Lightning, 1, 22, 5, 0, 14, 35, "ETC",
            "A storm crackles at a point, shocking a single target.", "Lightning Averse"));
        s.Add(Indirect("LTN_STORM_2", "Tempest II", Element.Lightning, 2, 46, 5, 2, 19, 55, "ETC",
            "A thunderstorm strikes multiple targets.", "Lightning Averse"));
        s.Add(Indirect("LTN_STORM_3", "Tempest III", Element.Lightning, 3, 70, 5, 3, 24, 80, "ETC",
            "A raging electrical storm. Heavy lightning damage.", "Lightning Averse"));
        s.Add(Indirect("LTN_STORM_4", "Tempest IV", Element.Lightning, 4, 94, 5, 3, 28, 110, "ETC",
            "A cataclysmic supercell. Devastating AoE.", "Lightning Averse"));
        s.Add(Utility("LTN_INSTILL", "Instill Storm", Element.Lightning, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Lightning-Touched to a single target for 3 turns."));
        s.Add(Utility("LTN_GUARD", "Stormguard", Element.Lightning, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Lightning Attuned to multiple targets for 3 turns."));

        // ─── EARTH (10) ─────────────────────────────────────
        s.Add(Missile("ERT_BOLT_1", "Stonebolt I", Element.Earth, 1, 15, 3, 6, 13, 40, "ETC",
            "Hurls a spike of compressed stone.", "Earth Averse"));
        s.Add(Missile("ERT_BOLT_2", "Stonebolt II", Element.Earth, 2, 30, 3, 6, 16, 75, "ETC",
            "A heavier stone spike. Greater damage.", "Earth Averse"));
        s.Add(Missile("ERT_BOLT_3", "Stonebolt III", Element.Earth, 3, 45, 3, 6, 19, 110, "ETC",
            "A massive boulder. Heavy single-target damage.", "Earth Averse"));
        s.Add(Missile("ERT_BOLT_4", "Stonebolt IV", Element.Earth, 4, 60, 3, 6, 22, 150, "ETC",
            "A meteoric impact. Devastating single-target damage.", "Earth Averse"));
        s.Add(Indirect("ERT_STORM_1", "Quake I", Element.Earth, 1, 22, 5, 0, 14, 35, "ETC",
            "The earth splits beneath a single target.", "Earth Averse"));
        s.Add(Indirect("ERT_STORM_2", "Quake II", Element.Earth, 2, 46, 5, 2, 19, 55, "ETC",
            "A tremor shakes multiple targets.", "Stagger"));
        s.Add(Indirect("ERT_STORM_3", "Quake III", Element.Earth, 3, 70, 5, 3, 24, 80, "ETC",
            "A violent earthquake. Heavy earth damage.", "Stagger"));
        s.Add(Indirect("ERT_STORM_4", "Quake IV", Element.Earth, 4, 94, 5, 3, 28, 110, "ETC",
            "The ground shatters. Devastating AoE.", "Stagger"));
        s.Add(Utility("ERT_INSTILL", "Instill Stone", Element.Earth, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Earth-Touched to a single target for 3 turns."));
        s.Add(Utility("ERT_GUARD", "Stoneguard", Element.Earth, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Earth Attuned to multiple targets for 3 turns."));

        // ─── WIND (10) ──────────────────────────────────────
        s.Add(Missile("WND_BOLT_1", "Galebolt I", Element.Wind, 1, 15, 3, 6, 13, 40, "ETC",
            "A blade of compressed air slices a single target.", "Wind Averse"));
        s.Add(Missile("WND_BOLT_2", "Galebolt II", Element.Wind, 2, 30, 3, 6, 16, 75, "ETC",
            "A sharper wind blade. Greater damage.", "Wind Averse"));
        s.Add(Missile("WND_BOLT_3", "Galebolt III", Element.Wind, 3, 45, 3, 6, 19, 110, "ETC",
            "A razor cyclone. Heavy single-target damage.", "Wind Averse"));
        s.Add(Missile("WND_BOLT_4", "Galebolt IV", Element.Wind, 4, 60, 3, 6, 22, 150, "ETC",
            "A vacuum blade. Devastating single-target damage.", "Wind Averse"));
        s.Add(Indirect("WND_STORM_1", "Cyclone I", Element.Wind, 1, 22, 5, 0, 14, 35, "ETC",
            "A violent gust tears at a single target.", "Wind Averse"));
        s.Add(Indirect("WND_STORM_2", "Cyclone II", Element.Wind, 2, 46, 5, 2, 19, 55, "ETC",
            "A whirlwind engulfs multiple targets.", "Misstep"));
        s.Add(Indirect("WND_STORM_3", "Cyclone III", Element.Wind, 3, 70, 5, 3, 24, 80, "ETC",
            "A hurricane-force storm. Heavy wind damage.", "Misstep"));
        s.Add(Indirect("WND_STORM_4", "Cyclone IV", Element.Wind, 4, 94, 5, 3, 28, 110, "ETC",
            "An apocalyptic tornado. Devastating AoE.", "Misstep"));
        s.Add(Utility("WND_INSTILL", "Instill Wind", Element.Wind, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Wind-Touched to a single target for 3 turns."));
        s.Add(Utility("WND_GUARD", "Windguard", Element.Wind, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Wind Attuned to multiple targets for 3 turns."));

        // ─── WATER (10) ─────────────────────────────────────
        s.Add(Missile("WTR_BOLT_1", "Tidebolt I", Element.Water, 1, 15, 3, 6, 13, 40, "ETC",
            "A pressurized jet of water strikes a single target.", "Water Averse"));
        s.Add(Missile("WTR_BOLT_2", "Tidebolt II", Element.Water, 2, 30, 3, 6, 16, 75, "ETC",
            "A heavier water jet. Greater damage.", "Water Averse"));
        s.Add(Missile("WTR_BOLT_3", "Tidebolt III", Element.Water, 3, 45, 3, 6, 19, 110, "ETC",
            "A crushing torrent. Heavy single-target damage.", "Water Averse"));
        s.Add(Missile("WTR_BOLT_4", "Tidebolt IV", Element.Water, 4, 60, 3, 6, 22, 150, "ETC",
            "A pressurized maelstrom. Devastating single-target damage.", "Water Averse"));
        s.Add(Indirect("WTR_STORM_1", "Deluge I", Element.Water, 1, 22, 5, 0, 14, 35, "ETC",
            "Water surges beneath a single target.", "Water Averse"));
        s.Add(Indirect("WTR_STORM_2", "Deluge II", Element.Water, 2, 46, 5, 2, 19, 55, "ETC",
            "A flood engulfs multiple targets.", "Breach"));
        s.Add(Indirect("WTR_STORM_3", "Deluge III", Element.Water, 3, 70, 5, 3, 24, 80, "ETC",
            "A tidal wave crashes. Heavy water damage.", "Breach"));
        s.Add(Indirect("WTR_STORM_4", "Deluge IV", Element.Water, 4, 94, 5, 3, 28, 110, "ETC",
            "A catastrophic tsunami. Devastating AoE.", "Breach"));
        s.Add(Utility("WTR_INSTILL", "Instill Tide", Element.Water, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Water-Touched to a single target for 3 turns."));
        s.Add(Utility("WTR_GUARD", "Tideguard", Element.Water, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Water Attuned to multiple targets for 3 turns."));

        // ─── LIGHT / DIVINE (23) ────────────────────────────
        // Offensive
        s.Add(Missile("LGT_BOLT_1", "Radiance I", Element.Light, 1, 15, 3, 6, 13, 40, "MND",
            "A bolt of holy light pierces a single target.", "Light Averse"));
        s.Add(Missile("LGT_BOLT_2", "Radiance II", Element.Light, 2, 30, 3, 6, 16, 75, "MND",
            "Intensified holy light. Greater damage.", "Light Averse"));
        s.Add(Missile("LGT_BOLT_3", "Radiance III", Element.Light, 3, 45, 3, 6, 19, 110, "MND",
            "A searing divine lance. Heavy damage.", "Light Averse"));
        s.Add(Missile("LGT_BOLT_4", "Radiance IV", Element.Light, 4, 60, 3, 6, 22, 150, "MND",
            "A pillar of pure light. Devastating single-target damage.", "Light Averse"));
        s.Add(Indirect("LGT_STORM_1", "Judgment I", Element.Light, 1, 22, 5, 0, 14, 35, "MND",
            "Holy energy descends upon a single target.", "Light Averse"));
        s.Add(Indirect("LGT_STORM_2", "Judgment II", Element.Light, 2, 46, 5, 2, 19, 55, "MND",
            "A rain of light engulfs multiple targets.", "Light Averse"));
        s.Add(Indirect("LGT_STORM_3", "Judgment III", Element.Light, 3, 70, 5, 3, 24, 80, "MND",
            "A divine reckoning across a wide area. Heavy light damage.", "Light Averse"));
        s.Add(Indirect("LGT_STORM_4", "Judgment IV", Element.Light, 4, 94, 5, 3, 28, 110, "MND",
            "The wrath of heaven itself. Devastating AoE.", "Light Averse"));
        // Healing
        s.Add(Heal("LGT_HEAL_1", "Heal I", 1, 15, 5, TargetType.Single, 0, 13, 50,
            "Restores a small amount of HP. Deals damage to undead."));
        s.Add(Heal("LGT_HEAL_2", "Heal II", 2, 30, 5, TargetType.Single, 0, 16, 90,
            "Restores a moderate amount of HP."));
        s.Add(Heal("LGT_HEAL_3", "Heal III", 3, 45, 5, TargetType.Single, 0, 19, 140,
            "Restores a large amount of HP."));
        s.Add(Heal("LGT_HEAL_4", "Heal IV", 4, 60, 5, TargetType.Single, 0, 22, 200,
            "Fully restores HP to a single ally."));
        s.Add(Heal("LGT_MHEAL_1", "Major Heal I", 2, 35, 5, TargetType.Diamond, 2, 19, 50,
            "Restores HP to multiple allies. Lower potency than single-target."));
        s.Add(Heal("LGT_MHEAL_2", "Major Heal II", 3, 55, 5, TargetType.Diamond, 2, 24, 80,
            "Restores moderate HP to multiple allies."));
        s.Add(Heal("LGT_MHEAL_3", "Major Heal III", 4, 75, 5, TargetType.Diamond, 3, 28, 120,
            "Restores significant HP to all allies in a wide area."));
        s.Add(Heal("LGT_RESURRECT", "Resurrection", 4, 80, 3, TargetType.Single, 0, 30, 0,
            "Revive a defeated ally at 30% HP. Once per battle per caster."));
        // Utility
        s.Add(Utility("LGT_EASE", "Ease", Element.Light, 1, 15, 5, TargetType.Single, 0, 13, "MND",
            "Remove 1 debuff from a single ally."));
        s.Add(Utility("LGT_CLEANSE", "Cleanse", Element.Light, 2, 30, 5, TargetType.Diamond, 2, 17, "MND",
            "Remove all debuffs from multiple allies."));
        s.Add(Utility("LGT_EXORCISM_1", "Exorcism I", Element.Light, 1, 20, 4, TargetType.Single, 0, 15, "MND",
            "Heavy light damage to undead/corrupted. Minor heal to living."));
        s.Add(Utility("LGT_EXORCISM_2", "Exorcism II", Element.Light, 3, 40, 4, TargetType.Diamond, 2, 20, "MND",
            "Banish multiple undead/corrupted targets in area."));
        s.Add(Utility("LGT_HASTE", "Boon of Swiftness", Element.Light, 1, 20, 5, TargetType.Single, 0, 15, "MND",
            "Grant Haste to a single ally. RT costs reduced by 15 for 3 turns."));
        s.Add(Utility("LGT_INSTILL", "Instill Light", Element.Light, 1, 15, 5, TargetType.Single, 0, 13, "MND",
            "Grants Light-Touched to a single target for 3 turns."));
        s.Add(Utility("LGT_GUARD", "Lightguard", Element.Light, 1, 10, 5, TargetType.Diamond, 3, 12, "MND",
            "Grants Light Attuned to multiple targets for 3 turns."));

        // ─── DARK (22) ──────────────────────────────────────
        // Offensive
        s.Add(Missile("DRK_BOLT_1", "Shadowbolt I", Element.Dark, 1, 15, 3, 6, 13, 40, "ETC",
            "A lance of dark ether pierces a single target.", "Dark Averse"));
        s.Add(Missile("DRK_BOLT_2", "Shadowbolt II", Element.Dark, 2, 30, 3, 6, 16, 75, "ETC",
            "A heavier dark lance. Greater damage.", "Dark Averse"));
        s.Add(Missile("DRK_BOLT_3", "Shadowbolt III", Element.Dark, 3, 45, 3, 6, 19, 110, "ETC",
            "A dark rift. Heavy damage.", "Dark Averse"));
        s.Add(Missile("DRK_BOLT_4", "Shadowbolt IV", Element.Dark, 4, 60, 3, 6, 22, 150, "ETC",
            "A void detonation. Devastating single-target damage.", "Dark Averse"));
        s.Add(Indirect("DRK_STORM_1", "Abyss I", Element.Dark, 1, 22, 5, 0, 14, 35, "ETC",
            "Dark energy erupts beneath a single target.", "Dark Averse"));
        s.Add(Indirect("DRK_STORM_2", "Abyss II", Element.Dark, 2, 46, 5, 2, 19, 55, "ETC",
            "A rift of darkness engulfs multiple targets.", "Enfeeble"));
        s.Add(Indirect("DRK_STORM_3", "Abyss III", Element.Dark, 3, 70, 5, 3, 24, 80, "ETC",
            "A chasm of void energy. Heavy dark damage.", "Enfeeble"));
        s.Add(Indirect("DRK_STORM_4", "Abyss IV", Element.Dark, 4, 94, 5, 3, 28, 110, "ETC",
            "The abyss consumes all. Devastating AoE.", "Enfeeble"));
        // Draining
        s.Add(Transfer("DRK_DRAIN_HP_1", "Drain Heart I", 1, 20, 3, 17, 50,
            "Dark damage. Caster heals for 30% of damage dealt."));
        s.Add(Transfer("DRK_DRAIN_HP_2", "Drain Heart II", 3, 40, 3, 20, 90,
            "Greater dark damage. Caster heals for 40% of damage dealt."));
        s.Add(Transfer("DRK_DRAIN_AE_1", "Drain Aether I", 1, 10, 3, 15, 0,
            "Steal 20 Aether from a single target."));
        s.Add(Transfer("DRK_DRAIN_AE_2", "Drain Aether II", 3, 20, 3, 17, 0,
            "Steal 40 Aether from a single target."));
        // Status
        s.Add(Status("DRK_POISON_1", "Poison Cloud I", 1, 18, 4, TargetType.Diamond, 1, 15,
            "A toxic cloud. 40% chance to Poison for 4 turns.", "Poison"));
        s.Add(Status("DRK_POISON_2", "Poison Cloud II", 2, 35, 4, TargetType.Diamond, 2, 20,
            "A larger toxic cloud. 50% chance to Poison for 4 turns.", "Poison"));
        s.Add(Status("DRK_PARALYZE", "Paralytic Wave", 2, 30, 3, TargetType.Diamond, 2, 20,
            "A numbing pulse. 30% chance to Paralyze for 2 turns.", "Paralyze"));
        s.Add(Status("DRK_CHARM", "Charm", 2, 30, 3, TargetType.Single, 0, 20,
            "Beguile a single target. 25% chance to Charm for 2 turns.", "Charm"));
        s.Add(Status("DRK_SLEEP", "Sleep", 1, 20, 4, TargetType.Single, 0, 17,
            "Lull a single target to Sleep for 2 turns. Broken by damage.", "Sleep"));
        s.Add(Status("DRK_PETRIFY", "Petrify", 3, 45, 3, TargetType.Single, 0, 20,
            "25% chance to Petrify for 2 turns. Cannot act, +50% damage taken.", "Petrify"));
        s.Add(Status("DRK_CONDEMN", "Condemn", 3, 35, 3, TargetType.Single, 0, 20,
            "Target cannot be revived if defeated. Lasts 4 turns.", "Condemn"));
        s.Add(Status("DRK_LAMENT", "Lament of the Dead", 2, 35, 0, TargetType.Diamond, 1, 20,
            "All adjacent enemies gain Fear (all stats -15%) for 2 turns.", "Fear"));
        // Utility
        s.Add(Utility("DRK_INSTILL", "Instill Dark", Element.Dark, 1, 15, 5, TargetType.Single, 0, 13, "ETC",
            "Grants Dark-Touched to a single target for 3 turns."));
        s.Add(Utility("DRK_GUARD", "Darkguard", Element.Dark, 1, 10, 5, TargetType.Diamond, 3, 12, "ETC",
            "Grants Dark Attuned to multiple targets for 3 turns."));

        return s;
    }
}
