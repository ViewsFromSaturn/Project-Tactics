using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Core;

/// <summary>
/// Core player data — Combat System v3.0.
/// 6 training stats: STR, VIT, DEX, AGI, ETC, MND
/// 3 resource pools: HP, Stamina, Aether
/// Fires DataChanged event whenever a stat or identity property is set.
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
	public event Action DataChanged;
	private void NotifyChanged() => DataChanged?.Invoke();

	// ─── IDENTITY ───────────────────────────────────────────────
	private string _characterName = "New Character";
	private string _raceName = "Human";
	private string _city = "Lumere";
	private string _allegiance = "None";
	private string _rpRank = "Aspirant";
	private string _bio = "";
	private string _playByPath = "";

	[Export] public string CharacterName { get => _characterName; set { _characterName = value; NotifyChanged(); } }
	[Export] public string RaceName { get => _raceName; set { _raceName = value; NotifyChanged(); } }
	[Export] public string City { get => _city; set { _city = value; NotifyChanged(); } }
	[Export] public string Allegiance { get => _allegiance; set { _allegiance = value; NotifyChanged(); } }
	[Export] public string RpRank { get => _rpRank; set { _rpRank = value; NotifyChanged(); } }
	[Export] public string Bio { get => _bio; set { _bio = value; NotifyChanged(); } }
	[Export] public string PlayByPath { get => _playByPath; set { _playByPath = value; NotifyChanged(); } }

	// ─── IC PROFILE FIELDS ──────────────────────────────────────
	private string _tagline = "";
	private string _rpStatus = "Open to RP";
	private string _personalityTraits = "";
	private string _rumors = "";

	[Export] public string Tagline { get => _tagline; set { _tagline = value; NotifyChanged(); } }
	[Export] public string RpStatus { get => _rpStatus; set { _rpStatus = value; NotifyChanged(); } }
	[Export] public string PersonalityTraits { get => _personalityTraits; set { _personalityTraits = value; NotifyChanged(); } }
	[Export] public string Rumors { get => _rumors; set { _rumors = value; NotifyChanged(); } }

	public string[] GetTraitsList() =>
		string.IsNullOrEmpty(PersonalityTraits) ? Array.Empty<string>()
		: PersonalityTraits.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	public string[] GetRumorsList() =>
		string.IsNullOrEmpty(Rumors) ? Array.Empty<string>()
		: Rumors.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	// ─── TRAINING STATS (v3.0) ───────────────────────────────────
	// STR, VIT, DEX, AGI, ETC, MND — all start at 1
	private int _strength = 1, _vitality = 1, _dexterity = 1;
	private int _agility = 1, _etherControl = 1, _mind = 1;

	[Export] public int Strength     { get => _strength;     set { _strength = value; NotifyChanged(); } }
	[Export] public int Vitality     { get => _vitality;     set { _vitality = value; NotifyChanged(); } }
	[Export] public int Dexterity    { get => _dexterity;    set { _dexterity = value; NotifyChanged(); } }
	[Export] public int Agility      { get => _agility;      set { _agility = value; NotifyChanged(); } }
	[Export] public int EtherControl { get => _etherControl; set { _etherControl = value; NotifyChanged(); } }
	[Export] public int Mind         { get => _mind;         set { _mind = value; NotifyChanged(); } }

	// ─── DAILY TRAINING (RP-earned, banked TP) ───────────────────
	private int _trainingPointsBank = 0;      // Banked TP — persists until spent
	private int _dailyTpEarned = 0;           // TP earned today via RP (resets at 12 EST)
	private int _dailyRpSessions = 0;         // Valid RP sessions completed today
	private string _lastResetDate = "";        // ISO date of last 12 EST reset

	[Export] public int TrainingPointsBank  { get => _trainingPointsBank;  set { _trainingPointsBank = value; NotifyChanged(); } }
	[Export] public int DailyTpEarned       { get => _dailyTpEarned;       set { _dailyTpEarned = value; NotifyChanged(); } }
	[Export] public int DailyRpSessions     { get => _dailyRpSessions;     set { _dailyRpSessions = value; NotifyChanged(); } }
	[Export] public string LastResetDate    { get => _lastResetDate;        set { _lastResetDate = value; NotifyChanged(); } }

	/// <summary>Max TP earnable per day based on character level.</summary>
	public int DailyTpCap => CharacterLevel switch
	{
		< 10 => 5,   // Lv 1-9:  5 TP/day
		< 20 => 3,   // Lv 10-19: 3 TP/day
		_    => 1,   // Lv 20+:  1 TP/day
	};

	/// <summary>TP still earnable today.</summary>
	public int DailyTpRemaining => Math.Max(0, DailyTpCap - DailyTpEarned);

	// ─── CURRENT COMBAT STATE (3 pools) ──────────────────────────
	private int _currentHp = -1, _currentStamina = -1, _currentAether = -1;

	[Export] public int CurrentHp      { get => _currentHp;      set { _currentHp = value; NotifyChanged(); } }
	[Export] public int CurrentStamina { get => _currentStamina; set { _currentStamina = value; NotifyChanged(); } }
	[Export] public int CurrentAether  { get => _currentAether;  set { _currentAether = value; NotifyChanged(); } }

	// ─── RACE PASSIVE MODIFIERS ──────────────────────────────────
	[Export] public float RaceHpModifier    { get; set; } = 1.0f;
	[Export] public float RaceStaminaModifier { get; set; } = 1.0f;
	[Export] public float RaceAetherModifier { get; set; } = 1.0f;
	[Export] public float RaceAtkModifier   { get; set; } = 1.0f;
	[Export] public float RaceEatkModifier  { get; set; } = 1.0f;
	[Export] public float RaceAvdModifier   { get; set; } = 1.0f;
	[Export] public float RaceRegenModifier { get; set; } = 1.0f;

	// ═══════════════════════════════════════════════════════════
	//  CHARACTER LEVEL = average of 6 stats (rounded down)
	// ═══════════════════════════════════════════════════════════

	public int CharacterLevel =>
		(Strength + Vitality + Dexterity + Agility + EtherControl + Mind) / 6;

	// ═══════════════════════════════════════════════════════════
	//  RESOURCE POOLS (v3.0 — HP / Stamina / Aether)
	// ═══════════════════════════════════════════════════════════

	// HP = 200 + VIT×15 + MND×8    (floor 223 at creation)
	public int MaxHp => (int)((200 + Vitality * 15 + Mind * 8) * RaceHpModifier);

	// Stamina = 100 + STR×12 + VIT×8   (floor 120 at creation)
	public int MaxStamina => (int)((100 + Strength * 12 + Vitality * 8) * RaceStaminaModifier);

	// Aether = 100 + ETC×20 + MND×5    (floor 125 at creation)
	public int MaxAether => (int)((100 + EtherControl * 20 + Mind * 5) * RaceAetherModifier);

	// Regen per combat turn
	public int HpRegen      => (int)(Mind * 0.4f);
	public int StaminaRegen => (int)(Vitality * 0.3f);
	public int AetherRegen  => (int)(EtherControl * 0.8f * RaceRegenModifier);

	// ═══════════════════════════════════════════════════════════
	//  DERIVED COMBAT STATS (v3.0)
	// ═══════════════════════════════════════════════════════════

	// Offense
	public int Atk  => (int)(Strength * 2.5f * RaceAtkModifier);           // STR only
	public int Eatk => (int)((EtherControl * 2.5f + Mind * 0.3f) * RaceEatkModifier);
	public int CritPercent => (int)(Dexterity * 0.4f + Agility * 0.1f);    // DEX is crit king
	public int HealPower   => (int)(Mind * 1.5f + EtherControl * 0.5f);

	// Defense
	public int Def  => (int)(Vitality * 2.0f + Mind * 0.5f);               // VIT primary, MND minor
	public int Edef => (int)(EtherControl * 1.0f + Vitality * 0.8f + Mind * 0.5f); // split 3 stats
	public int Avd  => (int)((Agility * 1.5f + Dexterity * 0.5f) * RaceAvdModifier);
	public int Acc  => (int)(Agility * 1.2f + Dexterity * 0.3f);           // AGI primary accuracy
	public int StatusResist => (int)(Mind * 0.5f + Vitality * 0.2f);

	// Utility
	public int Move => Math.Min(3 + (Agility / 12), 7);                    // 3 + AGI/12
	public int Jump => Math.Min(2 + (Strength / 20), 5);

	// RT: DEX drives turn order (v3.0)
	public int BaseRt => Math.Clamp(100 - (Dexterity / 5), 80, 150);

	public int CalculateRt(int actionWeight) =>
		Math.Clamp(BaseRt + actionWeight, 80, 150);

	// ═══════════════════════════════════════════════════════════
	//  DODGE / DAMAGE / RESIST FORMULAS (v3.0)
	// ═══════════════════════════════════════════════════════════

	/// <summary>Physical dodge: AVD×0.4 + AGI×0.2 - AttackerACC×0.3 + terrain + facing. Cap 75%.</summary>
	public float CalcPhysicalDodge(int attackerAcc, int terrainBonus = 0, int facingBonus = 0)
	{
		float dodge = (Avd * 0.4f) + (Agility * 0.2f) - (attackerAcc * 0.3f)
					  + terrainBonus + facingBonus;
		return Math.Clamp(dodge, 0f, 75f);
	}

	/// <summary>Ether dodge: AVD×0.3 + AGI×0.2 - AttackerACC×0.3 + terrain. AoE halved. Cap 60%.</summary>
	public float CalcAetherDodge(int attackerAcc, int terrainBonus = 0, bool isAoe = false)
	{
		float dodge = (Avd * 0.3f) + (Agility * 0.2f) - (attackerAcc * 0.3f)
					  + terrainBonus;
		if (isAoe) dodge *= 0.5f;
		return Math.Clamp(dodge, 0f, 60f);
	}

	/// <summary>Status resist: StatusResist×0.4 + MND×0.2 - AttackerETC×0.2. Cap 70%.</summary>
	public float CalcStatusResist(int attackerEtc)
	{
		float resist = (StatusResist * 0.4f) + (Mind * 0.2f) - (attackerEtc * 0.2f);
		return Math.Clamp(resist, 0f, 70f);
	}

	public int CalcPhysicalDamage(int attackerAtk, int skillModifier = 0)
	{
		int raw = (int)(attackerAtk * 1.5f) + skillModifier;
		int defense = (int)(Def * 0.8f);
		int damage = Math.Max(raw - defense, 1);
		int maxDamage = (int)(MaxHp * 0.6f);   // Anti one-shot cap
		return Math.Min(damage, maxDamage);
	}

	public int CalcAetherDamage(int attackerEatk, int abilityPower = 0, float elementBonus = 1.0f)
	{
		int raw = (int)((attackerEatk * 1.5f + abilityPower) * elementBonus);
		int defense = (int)(Edef * 0.8f);
		int damage = Math.Max(raw - defense, 1);
		int maxDamage = (int)(MaxHp * 0.6f);
		return Math.Min(damage, maxDamage);
	}

	// ═══════════════════════════════════════════════════════════
	//  INITIALIZATION
	// ═══════════════════════════════════════════════════════════

	public void InitializeCombatState()
	{
		if (CurrentHp < 0)      _currentHp = MaxHp;
		if (CurrentStamina < 0) _currentStamina = MaxStamina;
		if (CurrentAether < 0)  _currentAether = MaxAether;
	}

	public void RefreshDerivedStats()
	{
		_currentHp = Math.Min(_currentHp, MaxHp);
		_currentStamina = Math.Min(_currentStamina, MaxStamina);
		_currentAether = Math.Min(_currentAether, MaxAether);
		NotifyChanged();
	}

	// ═══════════════════════════════════════════════════════════
	//  TRAINING POINT HELPERS
	// ═══════════════════════════════════════════════════════════

	/// <summary>Soft-cap efficiency based on gap above lowest stat.</summary>
	public float GetTrainingEfficiency(string statName)
	{
		int statVal = GetTrainingStat(statName);
		int gap = statVal - LowestTrainingStat;
		return gap switch
		{
			< 10 => 1.0f,    // 0-9 gap: 100%
			< 20 => 0.5f,    // 10-19 gap: 50%
			_    => 0.25f,   // 20+ gap: 25%
		};
	}

	/// <summary>Attempt to spend banked TP on a stat. Returns true if successful.</summary>
	public bool SpendTrainingPoint(string statName)
	{
		if (TrainingPointsBank <= 0) return false;
		float eff = GetTrainingEfficiency(statName);
		if (eff <= 0f) return false;

		// For now, always costs 1 TP. Efficiency affects how much stat you gain.
		// At 100% = +1 stat. At 50% = +1 stat but costs 2 TP effectively (future: fractional).
		// Simple v1: always +1 stat, 1 TP, soft cap is informational for now.
		TrainingPointsBank -= 1;

		switch (statName.ToLower())
		{
			case "strength" or "str": Strength += 1; break;
			case "vitality" or "vit": Vitality += 1; break;
			case "dexterity" or "dex": Dexterity += 1; break;
			case "agility" or "agi": Agility += 1; break;
			case "ethercontrol" or "etc": EtherControl += 1; break;
			case "mind" or "mnd": Mind += 1; break;
			default: TrainingPointsBank += 1; return false; // refund
		}
		return true;
	}

	// ═══════════════════════════════════════════════════════════
	//  STAT HELPERS
	// ═══════════════════════════════════════════════════════════

	public int GetTrainingStat(string statName) => statName.ToLower() switch
	{
		"strength" or "str"       => Strength,
		"vitality" or "vit"       => Vitality,
		"dexterity" or "dex"      => Dexterity,
		"agility" or "agi"        => Agility,
		"ethercontrol" or "etc"   => EtherControl,
		"mind" or "mnd"           => Mind,
		_ => 0
	};

	public int LowestTrainingStat =>
		new[] { Strength, Vitality, Dexterity, Agility, EtherControl, Mind }.Min();

	public int HighestTrainingStat =>
		new[] { Strength, Vitality, Dexterity, Agility, EtherControl, Mind }.Max();

	public Dictionary<string, int> GetAllTrainingStats() => new()
	{
		{ "Strength", Strength },
		{ "Vitality", Vitality },
		{ "Dexterity", Dexterity },
		{ "Agility", Agility },
		{ "EtherControl", EtherControl },
		{ "Mind", Mind }
	};

	public Dictionary<string, int> GetAllDerivedStats() => new()
	{
		{ "HP", MaxHp },
		{ "Stamina", MaxStamina },
		{ "Aether", MaxAether },
		{ "HP Regen", HpRegen },
		{ "STA Regen", StaminaRegen },
		{ "AE Regen", AetherRegen },
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
