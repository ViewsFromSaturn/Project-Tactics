using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Core;

/// <summary>
/// Core player data class. Holds the 6 training stats, derives all combat stats,
/// and manages character identity (name, faction, city, rank).
/// Attach this as a resource or hold it in a PlayerController node.
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
    // ─── IDENTITY ───────────────────────────────────────────────
    [Export] public string CharacterName { get; set; } = "New Character";
    [Export] public string RaceName { get; set; } = "Human";
    [Export] public string City { get; set; } = "Lumere";
    [Export] public string Allegiance { get; set; } = "None";
    [Export] public string RpRank { get; set; } = "Aspirant"; // Pure RP, not stat-based

    // ─── BIO & PLAY-BY ─────────────────────────────────────────
    [Export] public string Bio { get; set; } = "";           // Character backstory (500 char max)
    [Export] public string PlayByPath { get; set; } = "";    // Path to portrait image

    // ─── TRAINING STATS (Player invests daily points here) ──────
    [Export] public int Strength { get; set; } = 1;
    [Export] public int Speed { get; set; } = 1;
    [Export] public int Agility { get; set; } = 1;
    [Export] public int Endurance { get; set; } = 1;
    [Export] public int Stamina { get; set; } = 1;
    [Export] public int EtherControl { get; set; } = 1;

    // ─── DAILY TRAINING TRACKING ────────────────────────────────
    [Export] public int DailyPointsRemaining { get; set; } = 5;
    [Export] public string LastTrainingDate { get; set; } = "";

    // ─── CURRENT COMBAT STATE ───────────────────────────────────
    [Export] public int CurrentHp { get; set; } = -1;   // -1 = uninitialized, set to max on first load
    [Export] public int CurrentEther { get; set; } = -1;

    // ─── FACTION PASSIVE MODIFIERS (set by RaceData) ─────────
    [Export] public float RaceHpModifier { get; set; } = 1.0f;
    [Export] public float RaceEtherModifier { get; set; } = 1.0f;
    [Export] public float RaceAtkModifier { get; set; } = 1.0f;
    [Export] public float RaceEatkModifier { get; set; } = 1.0f;
    [Export] public float RaceAvdModifier { get; set; } = 1.0f;
    [Export] public float RaceRegenModifier { get; set; } = 1.0f;

    // ═════════════════════════════════════════════════════════════
    //  CHARACTER LEVEL
    //  Average of all 6 training stats, rounded down.
    // ═════════════════════════════════════════════════════════════
    public int CharacterLevel =>
        (Strength + Speed + Agility + Endurance + Stamina + EtherControl) / 6;

    // ═════════════════════════════════════════════════════════════
    //  DERIVED COMBAT STATS
    //  Auto-calculated from training stats + race modifiers.
    // ═════════════════════════════════════════════════════════════

    // --- Health & Ether ---
    public int MaxHp => (int)((200 + (Endurance * 15) + (Stamina * 8)) * RaceHpModifier);
    public int MaxEther => (int)((100 + (EtherControl * 20) + (Stamina * 5)) * RaceEtherModifier);
    public int EtherRegen => (int)(EtherControl * 0.8f * RaceRegenModifier);

    // --- Offense ---
    public int Atk => (int)((Strength * 2.5f + Speed * 0.5f) * RaceAtkModifier);
    public int Eatk => (int)((EtherControl * 2.5f + Agility * 0.3f) * RaceEatkModifier);
    public int CritPercent => (int)(Speed * 0.3f + Agility * 0.2f);

    // --- Defense ---
    public int Def => (int)(Endurance * 2.0f + Stamina * 0.5f);
    public int Edef => (int)(EtherControl * 1.0f + Endurance * 1.0f);

    // --- Evasion & Accuracy ---
    public int Avd => (int)((Agility * 1.5f + Speed * 1.0f) * RaceAvdModifier);
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
    /// Calculate dodge chance vs an ether ability.
    /// </summary>
    public float CalcEtherDodge(int attackerInt, int terrainBonus = 0, bool isAoe = false)
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
    /// Calculate ether ability damage taken. Caps at 60% of max HP.
    /// </summary>
    public int CalcEtherDamage(int attackerEatk, int abilityPower = 0, float elementBonus = 1.0f)
    {
        int raw = (int)((attackerEatk * 1.5f + abilityPower) * elementBonus);
        int defense = (int)(Edef * 0.8f);
        int damage = Math.Max(raw - defense, 1);
        int maxDamage = (int)(MaxHp * 0.6f);
        return Math.Min(damage, maxDamage);
    }

    // ═════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Call once when a character is first loaded or created to set current HP/Ether.
    /// </summary>
    public void InitializeCombatState()
    {
        if (CurrentHp < 0) CurrentHp = MaxHp;
        if (CurrentEther < 0) CurrentEther = MaxEther;
    }

    /// <summary>
    /// Clamp current values to their maximums (call after stat changes).
    /// </summary>
    public void RefreshDerivedStats()
    {
        CurrentHp = Math.Min(CurrentHp, MaxHp);
        CurrentEther = Math.Min(CurrentEther, MaxEther);
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
        "ethercontrol" or "etc" => EtherControl,
        _ => 0
    };

    public int LowestTrainingStat =>
        new[] { Strength, Speed, Agility, Endurance, Stamina, EtherControl }.Min();

    public int HighestTrainingStat =>
        new[] { Strength, Speed, Agility, Endurance, Stamina, EtherControl }.Max();

    public Dictionary<string, int> GetAllTrainingStats() => new()
    {
        { "Strength", Strength },
        { "Speed", Speed },
        { "Agility", Agility },
        { "Endurance", Endurance },
        { "Stamina", Stamina },
        { "EtherControl", EtherControl }
    };

    public Dictionary<string, int> GetAllDerivedStats() => new()
    {
        { "HP", MaxHp },
        { "Ether", MaxEther },
        { "EtherRegen", EtherRegen },
        { "ATK", Atk },
        { "DEF", Def },
        { "EATK", Eatk },
        { "EDEF", Edef },
        { "AVD", Avd },
        { "ACC", Acc },
        { "CRIT%", CritPercent },
        { "MOVE", Move },
        { "JUMP", Jump },
        { "RT", BaseRt }
    };
}
