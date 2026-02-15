using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarutoRP.Core;

/// <summary>
/// Core player data class. Holds the 6 training stats, derives all combat stats,
/// and manages character identity (name, clan, village, rank).
/// Attach this as a resource or hold it in a PlayerController node.
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
    // ─── IDENTITY ───────────────────────────────────────────────
    [Export] public string CharacterName { get; set; } = "New Ninja";
    [Export] public string ClanName { get; set; } = "Clanless";
    [Export] public string Village { get; set; } = "Leaf";
    [Export] public string Allegiance { get; set; } = "None";
    [Export] public string RpRank { get; set; } = "Academy Student"; // Pure RP, not stat-based

    // ─── BIO & PLAY-BY ─────────────────────────────────────────
    [Export] public string Bio { get; set; } = "";           // Character backstory (500 char max)
    [Export] public string PlayByPath { get; set; } = "";    // Path to portrait image

    // ─── TRAINING STATS (Player invests daily points here) ──────
    [Export] public int Strength { get; set; } = 1;
    [Export] public int Speed { get; set; } = 1;
    [Export] public int Agility { get; set; } = 1;
    [Export] public int Endurance { get; set; } = 1;
    [Export] public int Stamina { get; set; } = 1;
    [Export] public int ChakraControl { get; set; } = 1;

    // ─── DAILY TRAINING TRACKING ────────────────────────────────
    [Export] public int DailyPointsRemaining { get; set; } = 5;
    [Export] public string LastTrainingDate { get; set; } = "";

    // ─── CURRENT COMBAT STATE ───────────────────────────────────
    [Export] public int CurrentHp { get; set; } = -1;   // -1 = uninitialized, set to max on first load
    [Export] public int CurrentChakra { get; set; } = -1;

    // ─── CLAN PASSIVE MODIFIERS (set by ClanData) ───────────────
    [Export] public float ClanHpModifier { get; set; } = 1.0f;
    [Export] public float ClanChakraModifier { get; set; } = 1.0f;
    [Export] public float ClanAtkModifier { get; set; } = 1.0f;
    [Export] public float ClanJatkModifier { get; set; } = 1.0f;
    [Export] public float ClanAvdModifier { get; set; } = 1.0f;
    [Export] public float ClanRegenModifier { get; set; } = 1.0f;

    // ═════════════════════════════════════════════════════════════
    //  CHARACTER LEVEL
    //  Average of all 6 training stats, rounded down.
    // ═════════════════════════════════════════════════════════════
    public int CharacterLevel =>
        (Strength + Speed + Agility + Endurance + Stamina + ChakraControl) / 6;

    // ═════════════════════════════════════════════════════════════
    //  DERIVED COMBAT STATS
    //  Auto-calculated from training stats + clan modifiers.
    // ═════════════════════════════════════════════════════════════

    // --- Health & Chakra ---
    public int MaxHp => (int)((200 + (Endurance * 15) + (Stamina * 8)) * ClanHpModifier);
    public int MaxChakra => (int)((100 + (ChakraControl * 20) + (Stamina * 5)) * ClanChakraModifier);
    public int ChakraRegen => (int)(ChakraControl * 0.8f * ClanRegenModifier);

    // --- Offense ---
    public int Atk => (int)((Strength * 2.5f + Speed * 0.5f) * ClanAtkModifier);
    public int Jatk => (int)((ChakraControl * 2.5f + Agility * 0.3f) * ClanJatkModifier);
    public int CritPercent => (int)(Speed * 0.3f + Agility * 0.2f);

    // --- Defense ---
    public int Def => (int)(Endurance * 2.0f + Stamina * 0.5f);
    public int Jdef => (int)(ChakraControl * 1.0f + Endurance * 1.0f);

    // --- Evasion & Accuracy ---
    public int Avd => (int)((Agility * 1.5f + Speed * 1.0f) * ClanAvdModifier);
    public int Acc => (int)(Agility * 1.0f + Speed * 0.5f);

    // --- Movement ---
    public int Move => Math.Min(4 + (Speed / 15), 7);
    public int Jump => Math.Min(2 + (Strength / 20), 5);

    // --- Recovery Time (turn speed in combat) ---
    public int BaseRt => Math.Clamp(100 - (Speed / 5), 80, 150);

    public int CalculateRt(int actionWeight)
    {
        return Math.Clamp(BaseRt + actionWeight, 80, 150);
    }

    // ═════════════════════════════════════════════════════════════
    //  DODGE FORMULAS
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate dodge chance vs a physical attack.
    /// </summary>
    public float CalcPhysicalDodge(int attackerDex, int terrainBonus = 0, int facingBonus = 0)
    {
        float dodge = (Avd * 0.4f) + (Agility * 0.2f) - (attackerDex * 0.3f)
                      + terrainBonus + facingBonus;
        return Math.Clamp(dodge, 0f, 75f); // Hard cap 75%
    }

    /// <summary>
    /// Calculate dodge chance vs a jutsu attack.
    /// </summary>
    public float CalcJutsuDodge(int attackerInt, int terrainBonus = 0, bool isAoe = false)
    {
        float dodge = (Avd * 0.3f) + (Agility * 0.2f) - (attackerInt * 0.3f)
                      + terrainBonus;
        if (isAoe) dodge *= 0.5f; // AOE halves dodge chance
        return Math.Clamp(dodge, 0f, 60f); // Hard cap 60%
    }

    // ═════════════════════════════════════════════════════════════
    //  DAMAGE FORMULAS (anti one-shot built in)
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate physical damage taken. Caps at 60% of max HP.
    /// </summary>
    public int CalcPhysicalDamage(int attackerAtk, int skillModifier = 0)
    {
        int raw = (int)(attackerAtk * 1.5f) + skillModifier;
        int defense = (int)(Def * 0.8f);
        int damage = Math.Max(raw - defense, 1); // Minimum 1 damage
        int maxDamage = (int)(MaxHp * 0.6f);     // 60% max HP cap
        return Math.Min(damage, maxDamage);
    }

    /// <summary>
    /// Calculate jutsu damage taken. Caps at 60% of max HP.
    /// </summary>
    public int CalcJutsuDamage(int attackerJatk, int jutsuPower = 0, float elementBonus = 1.0f)
    {
        int raw = (int)((attackerJatk * 1.5f + jutsuPower) * elementBonus);
        int defense = (int)(Jdef * 0.8f);
        int damage = Math.Max(raw - defense, 1);
        int maxDamage = (int)(MaxHp * 0.6f);
        return Math.Min(damage, maxDamage);
    }

    // ═════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Call once when a character is first loaded or created to set current HP/Chakra.
    /// </summary>
    public void InitializeCombatState()
    {
        if (CurrentHp < 0) CurrentHp = MaxHp;
        if (CurrentChakra < 0) CurrentChakra = MaxChakra;
    }

    /// <summary>
    /// Clamp current values to their maximums (call after stat changes).
    /// </summary>
    public void RefreshDerivedStats()
    {
        CurrentHp = Math.Min(CurrentHp, MaxHp);
        CurrentChakra = Math.Min(CurrentChakra, MaxChakra);
    }

    // ═════════════════════════════════════════════════════════════
    //  STAT HELPERS
    // ═════════════════════════════════════════════════════════════

    public int GetTrainingStat(string statName) => statName.ToLower() switch
    {
        "strength" or "str" => Strength,
        "speed" or "spd"    => Speed,
        "agility" or "agi"  => Agility,
        "endurance" or "end" => Endurance,
        "stamina" or "sta"  => Stamina,
        "chakracontrol" or "ckc" => ChakraControl,
        _ => 0
    };

    public int LowestTrainingStat =>
        new[] { Strength, Speed, Agility, Endurance, Stamina, ChakraControl }.Min();

    public int HighestTrainingStat =>
        new[] { Strength, Speed, Agility, Endurance, Stamina, ChakraControl }.Max();

    public Dictionary<string, int> GetAllTrainingStats() => new()
    {
        { "Strength", Strength },
        { "Speed", Speed },
        { "Agility", Agility },
        { "Endurance", Endurance },
        { "Stamina", Stamina },
        { "ChakraControl", ChakraControl }
    };

    public Dictionary<string, int> GetAllDerivedStats() => new()
    {
        { "HP", MaxHp },
        { "Chakra", MaxChakra },
        { "ChakraRegen", ChakraRegen },
        { "ATK", Atk },
        { "DEF", Def },
        { "JATK", Jatk },
        { "JDEF", Jdef },
        { "AVD", Avd },
        { "ACC", Acc },
        { "CRIT%", CritPercent },
        { "MOVE", Move },
        { "JUMP", Jump },
        { "RT", BaseRt }
    };
}
