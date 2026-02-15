using Godot;
using System.Collections.Generic;

namespace ProjectTactics.Core;

/// <summary>
/// Defines all available races and their passive bonuses.
/// Races are chosen at character creation and are permanent.
/// They modify derived stats through multipliers on PlayerData.
/// Factions (Sword, Faith, Nobility, etc.) are joined through RP and have no stat effect.
/// </summary>
public partial class RaceData : Node
{
    // ─── RACE REGISTRY ──────────────────────────────────────────
    // TODO: Replace placeholder races with final designs.
    private static readonly Dictionary<string, RaceDefinition> Races = new()
    {
        ["Valdren"] = new RaceDefinition
        {
            Name = "Valdren",
            Description = "Hardy and long-lived. Deep ether reserves and resilient bodies.",
            HpModifier = 1.15f,
            EtherModifier = 1.20f,
            AtkModifier = 1.0f,
            EatkModifier = 1.0f,
            AvdModifier = 1.0f,
            RegenModifier = 1.25f,
            PassiveDescription = "+15% HP, +20% Ether, +25% Ether Regen"
        },

        ["Sythari"] = new RaceDefinition
        {
            Name = "Sythari",
            Description = "Sharp-sensed and ether-attuned. Natural affinity for channeling.",
            HpModifier = 1.0f,
            EtherModifier = 1.10f,
            AtkModifier = 1.0f,
            EatkModifier = 1.15f,
            AvdModifier = 1.10f,
            RegenModifier = 1.0f,
            PassiveDescription = "+10% Ether, +15% EATK, +10% AVD"
        },

        ["Kaerath"] = new RaceDefinition
        {
            Name = "Kaerath",
            Description = "Disciplined and precise. Channel ether through martial technique.",
            HpModifier = 1.0f,
            EtherModifier = 1.05f,
            AtkModifier = 1.10f,
            EatkModifier = 1.0f,
            AvdModifier = 1.15f,
            RegenModifier = 1.10f,
            PassiveDescription = "+10% ATK, +15% AVD, +10% Ether Regen"
        },

        ["Delvari"] = new RaceDefinition
        {
            Name = "Delvari",
            Description = "Cunning tacticians with efficient ether circulation.",
            HpModifier = 1.0f,
            EtherModifier = 1.10f,
            AtkModifier = 1.0f,
            EatkModifier = 1.10f,
            AvdModifier = 1.05f,
            RegenModifier = 1.15f,
            PassiveDescription = "+10% Ether, +10% EATK, +15% Regen"
        },

        ["Gorath"] = new RaceDefinition
        {
            Name = "Gorath",
            Description = "Massive and powerful. Built to endure and overwhelm.",
            HpModifier = 1.25f,
            EtherModifier = 0.90f,
            AtkModifier = 1.20f,
            EatkModifier = 0.90f,
            AvdModifier = 0.90f,
            RegenModifier = 1.0f,
            PassiveDescription = "+25% HP, +20% ATK, -10% Ether/EATK/AVD"
        },

        ["Thornkin"] = new RaceDefinition
        {
            Name = "Thornkin",
            Description = "Symbiotic ether users. Drain energy and sustain themselves.",
            HpModifier = 1.05f,
            EtherModifier = 1.15f,
            AtkModifier = 1.0f,
            EatkModifier = 1.05f,
            AvdModifier = 1.05f,
            RegenModifier = 1.15f,
            PassiveDescription = "+15% Ether, +15% Regen, +5% across others"
        },

        ["Fenric"] = new RaceDefinition
        {
            Name = "Fenric",
            Description = "Fast and ferocious. Predatory instincts and sharp reflexes.",
            HpModifier = 1.10f,
            EtherModifier = 0.95f,
            AtkModifier = 1.15f,
            EatkModifier = 0.95f,
            AvdModifier = 1.10f,
            RegenModifier = 1.0f,
            PassiveDescription = "+10% HP, +15% ATK, +10% AVD"
        },

        ["Ashborn"] = new RaceDefinition
        {
            Name = "Ashborn",
            Description = "Born from harsh lands. Strong defensive ether techniques.",
            HpModifier = 1.10f,
            EtherModifier = 1.15f,
            AtkModifier = 1.0f,
            EatkModifier = 1.15f,
            AvdModifier = 0.95f,
            RegenModifier = 1.0f,
            PassiveDescription = "+10% HP, +15% Ether/EATK"
        },

        ["Mirefolk"] = new RaceDefinition
        {
            Name = "Mirefolk",
            Description = "Fluid and adaptive. Shift between forms to evade harm.",
            HpModifier = 1.05f,
            EtherModifier = 1.10f,
            AtkModifier = 1.10f,
            EatkModifier = 1.05f,
            AvdModifier = 1.10f,
            RegenModifier = 1.0f,
            PassiveDescription = "+10% ATK/AVD/Ether, +5% HP/EATK"
        },

        // ─── HUMAN (DEFAULT) ────────────────────────────────────
        ["Human"] = new RaceDefinition
        {
            Name = "Human",
            Description = "No innate advantages. Balanced baseline with no weaknesses.",
            HpModifier = 1.05f,
            EtherModifier = 1.05f,
            AtkModifier = 1.05f,
            EatkModifier = 1.05f,
            AvdModifier = 1.05f,
            RegenModifier = 1.05f,
            PassiveDescription = "+5% to all stats (jack of all trades)"
        }
    };

    // ═════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a race definition by name. Returns Human if not found.
    /// </summary>
    public static RaceDefinition GetRace(string raceName)
    {
        return Races.TryGetValue(raceName, out var race) ? race : Races["Human"];
    }

    /// <summary>
    /// Get all available race names.
    /// </summary>
    public static IEnumerable<string> GetAllRaceNames() => Races.Keys;

    /// <summary>
    /// Apply race passive modifiers to a PlayerData instance.
    /// Call this when a character is created or loaded.
    /// </summary>
    public static void ApplyRacePassives(PlayerData data)
    {
        var race = GetRace(data.RaceName);
        data.RaceHpModifier = race.HpModifier;
        data.RaceEtherModifier = race.EtherModifier;
        data.RaceAtkModifier = race.AtkModifier;
        data.RaceEatkModifier = race.EatkModifier;
        data.RaceAvdModifier = race.AvdModifier;
        data.RaceRegenModifier = race.RegenModifier;
        data.RefreshDerivedStats();
    }
}

// ─── RACE DEFINITION STRUCT ─────────────────────────────────────
public struct RaceDefinition
{
    public string Name;
    public string Description;
    public float HpModifier;
    public float EtherModifier;
    public float AtkModifier;
    public float EatkModifier;
    public float AvdModifier;
    public float RegenModifier;
    public string PassiveDescription;
}
