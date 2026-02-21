using Godot;
using System;

namespace ProjectTactics.Systems;

/// <summary>
/// Daily training system — 12:00 PM EST (17:00 UTC) global reset.
/// TP bank carries over between days. Only RP earning counters reset.
/// All actual allocation is server-authoritative (POST /train).
/// This handles client-side preview, soft cap display, and countdown.
/// </summary>
public partial class DailyTraining : Node
{
	[Signal] public delegate void PointsAllocatedEventHandler(string statName, int newValue);
	[Signal] public delegate void DailyPointsResetEventHandler(int totalPoints);
	[Signal] public delegate void SoftCapWarningEventHandler(string statName, float efficiency);

	// ─── CONFIG ─────────────────────────────────────────────
	private const int PointsLowLevel = 5;    // Level 1-9
	private const int PointsMidLevel = 3;    // Level 10-19
	private const int PointsHighLevel = 1;   // Level 20+

	private const int SoftCapTier1Gap = 10;
	private const float SoftCapTier1Mult = 0.5f;
	private const int SoftCapTier2Gap = 20;
	private const float SoftCapTier2Mult = 0.25f;

	// 12:00 PM EST = 17:00 UTC
	private const int ResetHourUtc = 17;

	// ═════════════════════════════════════════════════════════
	//  12:00 PM EST GLOBAL CLOCK
	// ═════════════════════════════════════════════════════════

	/// <summary>
	/// Current training period as ISO date. Flips at 17:00 UTC (12 PM EST).
	/// Before 17:00 UTC → yesterday's date. At/after → today's date.
	/// </summary>
	public static string GetCurrentResetPeriod()
	{
		var now = DateTime.UtcNow;
		var periodDate = now.Hour < ResetHourUtc
			? now.Date.AddDays(-1)
			: now.Date;
		return periodDate.ToString("yyyy-MM-dd");
	}

	/// <summary>Seconds until the next 17:00 UTC (12 PM EST) reset.</summary>
	public static int SecondsUntilReset()
	{
		var now = DateTime.UtcNow;
		var todayReset = new DateTime(now.Year, now.Month, now.Day, ResetHourUtc, 0, 0, DateTimeKind.Utc);
		var nextReset = now >= todayReset ? todayReset.AddDays(1) : todayReset;
		return (int)(nextReset - now).TotalSeconds;
	}

	/// <summary>Format seconds as "HH:MM:SS" for countdown display.</summary>
	public static string FormatCountdown(int totalSeconds)
	{
		if (totalSeconds <= 0) return "00:00:00";
		int h = totalSeconds / 3600;
		int m = (totalSeconds % 3600) / 60;
		int s = totalSeconds % 60;
		return $"{h:D2}:{m:D2}:{s:D2}";
	}

	/// <summary>
	/// Client-side reset check. Only resets RP earning counters.
	/// TP bank carries over — never zeroed client-side.
	/// </summary>
	public bool TryDailyReset(Core.PlayerData data)
	{
		string currentPeriod = GetCurrentResetPeriod();
		if (data.LastResetDate == currentPeriod)
			return false;

		// Only RP counters reset — bank persists
		data.DailyTpEarned = 0;
		data.DailyRpSessions = 0;
		data.LastResetDate = currentPeriod;

		EmitSignal(SignalName.DailyPointsReset, data.TrainingPointsBank);
		return true;
	}

	// ═════════════════════════════════════════════════════════
	//  DAILY POINTS CAP
	// ═════════════════════════════════════════════════════════

	public int GetDailyPointAllowance(int characterLevel)
	{
		return characterLevel switch
		{
			< 10 => PointsLowLevel,
			< 20 => PointsMidLevel,
			_    => PointsHighLevel
		};
	}

	// ═════════════════════════════════════════════════════════
	//  SOFT CAP
	// ═════════════════════════════════════════════════════════

	public float GetEfficiency(int statValue, int lowestStat)
	{
		int gap = statValue - lowestStat;
		if (gap >= SoftCapTier2Gap) return SoftCapTier2Mult;
		if (gap >= SoftCapTier1Gap) return SoftCapTier1Mult;
		return 1.0f;
	}

	/// <summary>TP cost for +1 stat point, accounting for soft cap tier.</summary>
	public int GetTpCostForPoint(int statValue, int lowestStat)
	{
		int gap = statValue - lowestStat;
		if (gap >= 20) return 4;  // 25% efficiency
		if (gap >= 10) return 2;  // 50% efficiency
		return 1;                  // 100% efficiency
	}

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

	// ═════════════════════════════════════════════════════════
	//  CLIENT-SIDE PREVIEW (actual spend goes through server)
	// ═════════════════════════════════════════════════════════

	/// <summary>
	/// Preview how many stat points N clicks would buy,
	/// simulating soft cap cost escalation point by point.
	/// </summary>
	public (int statGain, int tpCost) PreviewAllocation(
		Core.PlayerData data, string statName, int clickCount)
	{
		int statValue = data.GetTrainingStat(statName);
		int lowest = data.LowestTrainingStat;
		int tpAvailable = data.TrainingPointsBank;
		int gained = 0;
		int spent = 0;
		int simVal = statValue;

		for (int i = 0; i < clickCount; i++)
		{
			int cost = GetTpCostForPoint(simVal, lowest);
			if (spent + cost > tpAvailable) break;
			spent += cost;
			simVal++;
			gained++;
		}

		return (gained, spent);
	}

	// ═════════════════════════════════════════════════════════
	//  BONUS POINTS (events, mentoring — never expire)
	// ═════════════════════════════════════════════════════════

	public void GrantBonusPoints(Core.PlayerData data, int amount)
	{
		data.TrainingPointsBank += amount;
		GD.Print($"[DailyTraining] Granted {amount} bonus TP to {data.CharacterName}");
	}
}

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
		return $"{StatName}: {CurrentValue} ({Efficiency * 100}% eff, gap {Gap})";
	}
}
