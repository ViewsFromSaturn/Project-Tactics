using Godot;
using System.Collections.Generic;

namespace NarutoRP.Core;

/// <summary>
/// Defines all available clans and their passive bonuses.
/// Clans modify derived stats through multipliers on PlayerData.
/// Extend this with clan-locked jutsu, perk trees, and abilities later.
/// </summary>
public partial class ClanData : Node
{
    // ─── CLAN REGISTRY ──────────────────────────────────────────
    private static readonly Dictionary<string, ClanDefinition> Clans = new()
    {
        ["Uzumaki"] = new ClanDefinition
        {
            Name = "Uzumaki",
            Description = "Known for massive chakra reserves and sealing jutsu.",
            Village = "Leaf",
            HpModifier = 1.15f,        // +15% HP
            ChakraModifier = 1.20f,    // +20% Chakra Pool
            AtkModifier = 1.0f,
            JatkModifier = 1.0f,
            AvdModifier = 1.0f,
            RegenModifier = 1.25f,     // +25% Chakra Regen
            PassiveDescription = "+15% HP, +20% Chakra, +25% Chakra Regen"
        },

        ["Uchiha"] = new ClanDefinition
        {
            Name = "Uchiha",
            Description = "Elite clan with the Sharingan dojutsu. Masters of fire and genjutsu.",
            Village = "Leaf",
            HpModifier = 1.0f,
            ChakraModifier = 1.10f,    // +10% Chakra
            AtkModifier = 1.0f,
            JatkModifier = 1.15f,      // +15% Jutsu ATK
            AvdModifier = 1.10f,       // +10% Avoidance (Sharingan prediction)
            RegenModifier = 1.0f,
            PassiveDescription = "+10% Chakra, +15% JATK, +10% AVD"
        },

        ["Hyuga"] = new ClanDefinition
        {
            Name = "Hyuga",
            Description = "Byakugan wielders. Masters of Gentle Fist taijutsu.",
            Village = "Leaf",
            HpModifier = 1.0f,
            ChakraModifier = 1.05f,
            AtkModifier = 1.10f,       // +10% Physical ATK
            JatkModifier = 1.0f,
            AvdModifier = 1.15f,       // +15% Avoidance (Byakugan sees all)
            RegenModifier = 1.10f,     // +10% Regen (chakra point mastery)
            PassiveDescription = "+10% ATK, +15% AVD, +10% Chakra Regen"
        },

        ["Nara"] = new ClanDefinition
        {
            Name = "Nara",
            Description = "Shadow manipulation specialists. Brilliant tacticians.",
            Village = "Leaf",
            HpModifier = 1.0f,
            ChakraModifier = 1.10f,
            AtkModifier = 1.0f,
            JatkModifier = 1.10f,      // +10% Jutsu ATK
            AvdModifier = 1.05f,
            RegenModifier = 1.15f,     // +15% Regen (efficient chakra use)
            PassiveDescription = "+10% Chakra, +10% JATK, +15% Regen"
        },

        ["Akimichi"] = new ClanDefinition
        {
            Name = "Akimichi",
            Description = "Body expansion specialists. Immense physical power.",
            Village = "Leaf",
            HpModifier = 1.25f,        // +25% HP (tanky)
            ChakraModifier = 0.90f,    // -10% Chakra (physical focus)
            AtkModifier = 1.20f,       // +20% Physical ATK
            JatkModifier = 0.90f,      // -10% Jutsu ATK
            AvdModifier = 0.90f,       // -10% Avoidance (large target)
            RegenModifier = 1.0f,
            PassiveDescription = "+25% HP, +20% ATK, -10% Chakra/JATK/AVD"
        },

        ["Aburame"] = new ClanDefinition
        {
            Name = "Aburame",
            Description = "Insect users. Drain chakra and track enemies.",
            Village = "Leaf",
            HpModifier = 1.05f,
            ChakraModifier = 1.15f,    // +15% Chakra (insect symbiosis)
            AtkModifier = 1.0f,
            JatkModifier = 1.05f,
            AvdModifier = 1.05f,
            RegenModifier = 1.15f,     // +15% Regen
            PassiveDescription = "+15% Chakra, +15% Regen, +5% across others"
        },

        ["Inuzuka"] = new ClanDefinition
        {
            Name = "Inuzuka",
            Description = "Beast companions. Fast and ferocious taijutsu fighters.",
            Village = "Leaf",
            HpModifier = 1.10f,
            ChakraModifier = 0.95f,
            AtkModifier = 1.15f,       // +15% ATK
            JatkModifier = 0.95f,
            AvdModifier = 1.10f,       // +10% AVD (animal reflexes)
            RegenModifier = 1.0f,
            PassiveDescription = "+10% HP, +15% ATK, +10% AVD"
        },

        // ─── SAND VILLAGE ───────────────────────────────────────
        ["Kazekage Clan"] = new ClanDefinition
        {
            Name = "Kazekage Clan",
            Description = "Sand manipulation bloodline. Strong defense.",
            Village = "Sand",
            HpModifier = 1.10f,
            ChakraModifier = 1.15f,
            AtkModifier = 1.0f,
            JatkModifier = 1.15f,
            AvdModifier = 0.95f,       // Sand shield does the work, less dodge
            RegenModifier = 1.0f,
            PassiveDescription = "+10% HP, +15% Chakra/JATK"
        },

        // ─── MIST VILLAGE ───────────────────────────────────────
        ["Hozuki"] = new ClanDefinition
        {
            Name = "Hozuki",
            Description = "Water body transformation. Hard to damage physically.",
            Village = "Mist",
            HpModifier = 1.05f,
            ChakraModifier = 1.10f,
            AtkModifier = 1.10f,
            JatkModifier = 1.05f,
            AvdModifier = 1.10f,       // Liquify dodge
            RegenModifier = 1.0f,
            PassiveDescription = "+10% ATK/AVD/Chakra, +5% HP/JATK"
        },

        // ─── CLANLESS ───────────────────────────────────────────
        ["Clanless"] = new ClanDefinition
        {
            Name = "Clanless",
            Description = "No bloodline. Balanced baseline with no weaknesses.",
            Village = "Any",
            HpModifier = 1.05f,
            ChakraModifier = 1.05f,
            AtkModifier = 1.05f,
            JatkModifier = 1.05f,
            AvdModifier = 1.05f,
            RegenModifier = 1.05f,
            PassiveDescription = "+5% to all stats (jack of all trades)"
        }
    };

    // ═════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a clan definition by name. Returns Clanless if not found.
    /// </summary>
    public static ClanDefinition GetClan(string clanName)
    {
        return Clans.TryGetValue(clanName, out var clan) ? clan : Clans["Clanless"];
    }

    /// <summary>
    /// Get all available clan names.
    /// </summary>
    public static IEnumerable<string> GetAllClanNames() => Clans.Keys;

    /// <summary>
    /// Get all clans for a specific village.
    /// </summary>
    public static List<ClanDefinition> GetClansForVillage(string village)
    {
        var result = new List<ClanDefinition>();
        foreach (var clan in Clans.Values)
        {
            if (clan.Village == village || clan.Village == "Any")
                result.Add(clan);
        }
        return result;
    }

    /// <summary>
    /// Apply clan passive modifiers to a PlayerData instance.
    /// Call this when a character selects or changes their clan.
    /// </summary>
    public static void ApplyClanPassives(PlayerData data)
    {
        var clan = GetClan(data.ClanName);
        data.ClanHpModifier = clan.HpModifier;
        data.ClanChakraModifier = clan.ChakraModifier;
        data.ClanAtkModifier = clan.AtkModifier;
        data.ClanJatkModifier = clan.JatkModifier;
        data.ClanAvdModifier = clan.AvdModifier;
        data.ClanRegenModifier = clan.RegenModifier;
        data.RefreshDerivedStats();
    }
}

// ─── CLAN DEFINITION STRUCT ─────────────────────────────────────
public struct ClanDefinition
{
    public string Name;
    public string Description;
    public string Village;
    public float HpModifier;
    public float ChakraModifier;
    public float AtkModifier;
    public float JatkModifier;
    public float AvdModifier;
    public float RegenModifier;
    public string PassiveDescription;

    // Future: List<string> ClanLockedJutsu
    // Future: List<string> ClanLockedPerks
    // Future: string KeykeiGenkai (bloodline limit name)
}
