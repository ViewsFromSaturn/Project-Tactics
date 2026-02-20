using Godot;
using System;

namespace ProjectTactics.Systems;

/// <summary>
/// Manages daily stat point allocation.
/// Handles point calculation based on character level,
/// soft cap diminishing returns, and daily reset.
/// </summary>
public partial class DailyTraining : Node
{
	// ─── SIGNALS ────────────────────────────────────────────────
	[Signal] public delegate void PointsAllocatedEventHandler(string statName, int newValue);
	[Signal] public delegate void DailyPointsResetEventHandler(int totalPoints);
	[Signal] public delegate void SoftCapWarningEventHandler(string statName, float efficiency);

	// ─── CONFIG ─────────────────────────────────────────────────
	// Daily points per character level bracket
	private const int PointsLowLevel = 5;    // Level 1-9
	private const int PointsMidLevel = 3;    // Level 10-19
	private const int PointsHighLevel = 1;   // Level 20+

	// Soft cap thresholds (gap between a stat and lowest stat)
	private const int SoftCapTier1Gap = 10;       // 50% efficiency
	private const float SoftCapTier1Mult = 0.5f;
	private const int SoftCapTier2Gap = 20;       // 25% efficiency
	private const float SoftCapTier2Mult = 0.25f;

	// ─── STATE ──────────────────────────────────────────────────
	// Track partial points from diminished returns
	// When soft cap reduces a point to 0.5, we bank it until it reaches 1.0
	private float _strengthBank = 0f;
	private float _speedBank = 0f;
	private float _agilityBank = 0f;
	private float _enduranceBank = 0f;
	private float _staminaBank = 0f;
	private float _etherControlBank = 0f;

	// ═════════════════════════════════════════════════════════════
	//  DAILY POINTS CALCULATION
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Get how many daily points this character should receive.
	/// </summary>
	public int GetDailyPointAllowance(int characterLevel)
	{
		return characterLevel switch
		{
			< 10 => PointsLowLevel,
			< 20 => PointsMidLevel,
			_    => PointsHighLevel
		};
	}

	// ═════════════════════════════════════════════════════════════
	//  SOFT CAP — DIMINISHING RETURNS
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Get the efficiency multiplier for investing in a stat.
	/// Returns 1.0 (full), 0.5 (tier 1 soft cap), or 0.25 (tier 2 soft cap).
	/// </summary>
	public float GetEfficiency(int statValue, int lowestStat)
	{
		int gap = statValue - lowestStat;

		if (gap >= SoftCapTier2Gap)
			return SoftCapTier2Mult;
		if (gap >= SoftCapTier1Gap)
			return SoftCapTier1Mult;

		return 1.0f;
	}

	/// <summary>
	/// Check if allocating to this stat would trigger soft cap.
	/// Use this for UI warnings before the player confirms.
	/// </summary>
	public SoftCapInfo CheckSoftCap(Core.PlayerData data, string statName)
	{
		int statValue = data.GetTrainingStat(statName);
		int lowest = data.LowestTrainingStat;
		float efficiency = GetEfficiency(statValue, lowest);

		return new SoftCapInfo
		{
			StatName = statName,
			CurrentValue = statValue,
			LowestStat = lowest,
			Gap = statValue - lowest,
			Efficiency = efficiency,
			IsCapped = efficiency < 1.0f
		};
	}

	// ═════════════════════════════════════════════════════════════
	//  POINT ALLOCATION
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Attempt to allocate 1 daily point to a training stat.
	/// Returns true if successful, false if no points remaining.
	/// Handles soft cap fractional banking automatically.
	/// </summary>
	public bool AllocatePoint(Core.PlayerData data, string statName)
	{
		if (data.TrainingPointsBank <= 0)
			return false;

		int statValue = data.GetTrainingStat(statName);
		int lowest = data.LowestTrainingStat;
		float efficiency = GetEfficiency(statValue, lowest);

		// Warn about soft cap
		if (efficiency < 1.0f)
		{
			EmitSignal(SignalName.SoftCapWarning, statName, efficiency);
		}

		// Calculate actual gain (with fractional banking)
		float gain = 1.0f * efficiency;
		ref float bank = ref GetBank(statName);
		bank += gain;

		// Only apply whole number gains, bank the rest
		int wholeGain = (int)bank;
		bank -= wholeGain;

		// Apply the stat increase
		if (wholeGain > 0)
		{
			ApplyStatIncrease(data, statName, wholeGain);
		}

		// Always consume the daily point even if banked
		data.TrainingPointsBank--;

		// Refresh derived stats after any change
		data.RefreshDerivedStats();

		EmitSignal(SignalName.PointsAllocated, statName, data.GetTrainingStat(statName));
		return true;
	}

	/// <summary>
	/// Allocate multiple points at once to a single stat.
	/// Convenience method for UI batch allocation.
	/// </summary>
	public int AllocateMultiple(Core.PlayerData data, string statName, int count)
	{
		int allocated = 0;
		for (int i = 0; i < count; i++)
		{
			if (AllocatePoint(data, statName))
				allocated++;
			else
				break;
		}
		return allocated;
	}

	// ═════════════════════════════════════════════════════════════
	//  DAILY RESET
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Check if daily points should reset (new day).
	/// Call this on login and periodically.
	/// </summary>
	public bool TryDailyReset(Core.PlayerData data)
	{
		string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

		if (data.LastResetDate == today)
			return false; // Already trained today

		int points = GetDailyPointAllowance(data.CharacterLevel);
		data.TrainingPointsBank = points;
		data.LastResetDate = today;

		EmitSignal(SignalName.DailyPointsReset, points);
		return true;
	}

	// ═════════════════════════════════════════════════════════════
	//  BONUS POINTS (for future bonus EXP system)
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Grant bonus training points (from RP rewards, events, etc).
	/// These are added on top of daily points.
	/// </summary>
	public void GrantBonusPoints(Core.PlayerData data, int amount)
	{
		data.TrainingPointsBank += amount;
		GD.Print($"[DailyTraining] Granted {amount} bonus points to {data.CharacterName}");
	}

	// ═════════════════════════════════════════════════════════════
	//  INTERNAL HELPERS
	// ═════════════════════════════════════════════════════════════

	private void ApplyStatIncrease(Core.PlayerData data, string statName, int amount)
	{
		switch (statName.ToLower())
		{
			case "strength" or "str":       data.Strength += amount; break;
			case "vitality" or "vit":      data.Vitality += amount; break;
			case "agility" or "agi":        data.Agility += amount; break;
			case "dexterity" or "dex":      data.Dexterity += amount; break;
			case "mind" or "mnd":           data.Mind += amount; break;
			case "ethercontrol" or "etc":   data.EtherControl += amount; break;
		}
	}

	private ref float GetBank(string statName)
	{
		switch (statName.ToLower())
		{
			case "strength" or "str":       return ref _strengthBank;
			case "speed" or "spd":          return ref _speedBank;
			case "agility" or "agi":        return ref _agilityBank;
			case "endurance" or "end":      return ref _enduranceBank;
			case "stamina" or "sta":        return ref _staminaBank;
			case "ethercontrol" or "etc":   return ref _etherControlBank;
			default:                        return ref _strengthBank;
		}
	}
}

// ─── DATA STRUCT ────────────────────────────────────────────────
public struct SoftCapInfo
{
	public string StatName;
	public int CurrentValue;
	public int LowestStat;
	public int Gap;
	public float Efficiency;
	public bool IsCapped;

	public override readonly string ToString()
	{
		if (!IsCapped) return $"{StatName}: {CurrentValue} (Full efficiency)";
		return $"{StatName}: {CurrentValue} ({Efficiency * 100}% efficiency, {Gap} above lowest)";
	}
}
