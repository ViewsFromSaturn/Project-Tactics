using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Core;

/// <summary>
/// Core player data class. Holds the 6 training stats, derives all combat stats,
/// and manages character identity (name, faction, city, rank).
/// Fires DataChanged event whenever a stat or identity property is set.
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
	/// <summary>Fired whenever any mutable property changes. UI panels subscribe to refresh.</summary>
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
	private string _personalityTraits = "";  // Comma-separated: "Stoic,Cunning,Loyal"
	private string _rumors = "";             // Pipe-separated: "Seen near archives|Carries a journal"

	[Export] public string Tagline { get => _tagline; set { _tagline = value; NotifyChanged(); } }
	[Export] public string RpStatus { get => _rpStatus; set { _rpStatus = value; NotifyChanged(); } }
	[Export] public string PersonalityTraits { get => _personalityTraits; set { _personalityTraits = value; NotifyChanged(); } }
	[Export] public string Rumors { get => _rumors; set { _rumors = value; NotifyChanged(); } }

	public string[] GetTraitsList() =>
		string.IsNullOrEmpty(PersonalityTraits) ? System.Array.Empty<string>()
		: PersonalityTraits.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

	public string[] GetRumorsList() =>
		string.IsNullOrEmpty(Rumors) ? System.Array.Empty<string>()
		: Rumors.Split('|', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

	// ─── TRAINING STATS ──────────────────────────────────────────
	private int _strength = 1, _speed = 1, _agility = 1;
	private int _endurance = 1, _stamina = 1, _etherControl = 1;

	[Export] public int Strength { get => _strength; set { _strength = value; NotifyChanged(); } }
	[Export] public int Speed { get => _speed; set { _speed = value; NotifyChanged(); } }
	[Export] public int Agility { get => _agility; set { _agility = value; NotifyChanged(); } }
	[Export] public int Endurance { get => _endurance; set { _endurance = value; NotifyChanged(); } }
	[Export] public int Stamina { get => _stamina; set { _stamina = value; NotifyChanged(); } }
	[Export] public int EtherControl { get => _etherControl; set { _etherControl = value; NotifyChanged(); } }

	// ─── DAILY TRAINING TRACKING ────────────────────────────────
	private int _dailyPointsRemaining = 5;
	private string _lastTrainingDate = "";

	[Export] public int DailyPointsRemaining { get => _dailyPointsRemaining; set { _dailyPointsRemaining = value; NotifyChanged(); } }
	[Export] public string LastTrainingDate { get => _lastTrainingDate; set { _lastTrainingDate = value; NotifyChanged(); } }

	// ─── CURRENT COMBAT STATE ───────────────────────────────────
	private int _currentHp = -1, _currentEther = -1;

	[Export] public int CurrentHp { get => _currentHp; set { _currentHp = value; NotifyChanged(); } }
	[Export] public int CurrentEther { get => _currentEther; set { _currentEther = value; NotifyChanged(); } }

	// ─── FACTION PASSIVE MODIFIERS ─────────────────────────────
	[Export] public float RaceHpModifier { get; set; } = 1.0f;
	[Export] public float RaceEtherModifier { get; set; } = 1.0f;
	[Export] public float RaceAtkModifier { get; set; } = 1.0f;
	[Export] public float RaceEatkModifier { get; set; } = 1.0f;
	[Export] public float RaceAvdModifier { get; set; } = 1.0f;
	[Export] public float RaceRegenModifier { get; set; } = 1.0f;

	// ═══ CHARACTER LEVEL ═══
	public int CharacterLevel =>
		(Strength + Speed + Agility + Endurance + Stamina + EtherControl) / 6;

	// ═══ DERIVED COMBAT STATS ═══
	public int MaxHp => (int)((200 + (Endurance * 15) + (Stamina * 8)) * RaceHpModifier);
	public int MaxEther => (int)((100 + (EtherControl * 20) + (Stamina * 5)) * RaceEtherModifier);
	public int EtherRegen => (int)(EtherControl * 0.8f * RaceRegenModifier);

	public int Atk => (int)((Strength * 2.5f + Speed * 0.5f) * RaceAtkModifier);
	public int Eatk => (int)((EtherControl * 2.5f + Agility * 0.3f) * RaceEatkModifier);
	public int CritPercent => (int)(Speed * 0.3f + Agility * 0.2f);

	public int Def => (int)(Endurance * 2.0f + Stamina * 0.5f);
	public int Edef => (int)(EtherControl * 1.0f + Endurance * 1.0f);

	public int Avd => (int)((Agility * 1.5f + Speed * 1.0f) * RaceAvdModifier);
	public int Acc => (int)(Agility * 1.0f + Speed * 0.5f);

	public int Move => Math.Min(4 + (Speed / 15), 7);
	public int Jump => Math.Min(2 + (Strength / 20), 5);

	public int BaseRt => Math.Clamp(100 - (Speed / 5), 80, 150);

	public int CalculateRt(int actionWeight) =>
		Math.Clamp(BaseRt + actionWeight, 80, 150);

	// ═══ DODGE / DAMAGE FORMULAS ═══

	public float CalcPhysicalDodge(int attackerDex, int terrainBonus = 0, int facingBonus = 0)
	{
		float dodge = (Avd * 0.4f) + (Agility * 0.2f) - (attackerDex * 0.3f)
					  + terrainBonus + facingBonus;
		return Math.Clamp(dodge, 0f, 75f);
	}

	public float CalcEtherDodge(int attackerInt, int terrainBonus = 0, bool isAoe = false)
	{
		float dodge = (Avd * 0.3f) + (Agility * 0.2f) - (attackerInt * 0.3f)
					  + terrainBonus;
		if (isAoe) dodge *= 0.5f;
		return Math.Clamp(dodge, 0f, 60f);
	}

	public int CalcPhysicalDamage(int attackerAtk, int skillModifier = 0)
	{
		int raw = (int)(attackerAtk * 1.5f) + skillModifier;
		int defense = (int)(Def * 0.8f);
		int damage = Math.Max(raw - defense, 1);
		int maxDamage = (int)(MaxHp * 0.6f);
		return Math.Min(damage, maxDamage);
	}

	public int CalcEtherDamage(int attackerEatk, int abilityPower = 0, float elementBonus = 1.0f)
	{
		int raw = (int)((attackerEatk * 1.5f + abilityPower) * elementBonus);
		int defense = (int)(Edef * 0.8f);
		int damage = Math.Max(raw - defense, 1);
		int maxDamage = (int)(MaxHp * 0.6f);
		return Math.Min(damage, maxDamage);
	}

	// ═══ INITIALIZATION ═══

	public void InitializeCombatState()
	{
		if (CurrentHp < 0) _currentHp = MaxHp;
		if (CurrentEther < 0) _currentEther = MaxEther;
	}

	public void RefreshDerivedStats()
	{
		_currentHp = Math.Min(_currentHp, MaxHp);
		_currentEther = Math.Min(_currentEther, MaxEther);
		NotifyChanged();
	}

	// ═══ STAT HELPERS ═══

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
