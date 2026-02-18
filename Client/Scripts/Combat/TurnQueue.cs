using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Combat;

/// <summary>
/// RT-based turn queue (Tactics Ogre style).
/// 
/// All units start at RT = their BaseWt (heavier/slower units wait longer initially).
/// Tick down together. Lowest RT acts next (0 = your turn).
/// After acting: RT = WT*modifier + moveRT + actionRT.
/// 
/// Key difference from simple systems: 
/// - Moving far costs more RT (per tile)
/// - Doing less on your turn means you come back faster
/// - Equipment weight directly affects turn speed
/// </summary>
public class TurnQueue
{
	private readonly List<BattleUnit> _units = new();

	public IReadOnlyList<BattleUnit> AllUnits => _units;

	public void AddUnit(BattleUnit unit) => _units.Add(unit);
	public void RemoveUnit(BattleUnit unit) => _units.Remove(unit);

	/// <summary>Initialize all units with starting RT based on their WT.
	/// Faster units (lower BaseWt) get to act sooner.</summary>
	public void InitializeTurnOrder()
	{
		foreach (var u in _units)
			u.CurrentRt = u.BaseWt;
		AdvanceTime();
	}

	/// <summary>Get the next unit to act (lowest RT among alive units).</summary>
	public BattleUnit GetActiveUnit()
	{
		BattleUnit best = null;
		foreach (var u in _units)
		{
			if (!u.IsAlive) continue;
			if (best == null || u.CurrentRt < best.CurrentRt)
				best = u;
			// Tiebreaker: higher AGI goes first
			else if (u.CurrentRt == best.CurrentRt && u.Agility > best.Agility)
				best = u;
		}
		return best;
	}

	/// <summary>Subtract the minimum RT from all alive units so next actor hits 0.</summary>
	public void AdvanceTime()
	{
		int minRt = int.MaxValue;
		foreach (var u in _units)
			if (u.IsAlive && u.CurrentRt < minRt)
				minRt = u.CurrentRt;

		if (minRt <= 0 || minRt == int.MaxValue) return;

		foreach (var u in _units)
			if (u.IsAlive)
				u.TickRt(minRt);
	}

	/// <summary>
	/// End the active unit's turn. Calculates total RT from what they did.
	/// Then advances time so the next unit is ready.
	/// </summary>
	public void EndTurn(BattleUnit unit, int actionRt)
	{
		unit.EndTurn(actionRt);
		AdvanceTime();
	}

	/// <summary>
	/// Preview: what would the unit's RT be if they took this action?
	/// Useful for showing "if you attack with this weapon, you'll act again in X ticks".
	/// </summary>
	public int PreviewTurnRt(BattleUnit unit, int tilesMoved, int actionRt, bool willMove, bool willAct)
	{
		return unit.CalculateTurnRt(tilesMoved, actionRt, willMove, willAct);
	}

	/// <summary>Get turn order preview (next N units to act).</summary>
	public List<(BattleUnit unit, int rt)> GetTurnOrder(int count = 12)
	{
		// Simulate forward without mutating actual RT
		var simRt = new Dictionary<BattleUnit, int>();
		foreach (var u in _units)
			if (u.IsAlive)
				simRt[u] = u.CurrentRt;

		var order = new List<(BattleUnit, int)>();

		for (int i = 0; i < count && simRt.Count > 0; i++)
		{
			// Tick down to next actor
			int minRt = simRt.Values.Min();
			if (minRt > 0)
				foreach (var key in simRt.Keys.ToList())
					simRt[key] -= minRt;

			// Pick unit at RT 0 (tiebreak: higher AGI)
			BattleUnit next = null;
			foreach (var (u, rt) in simRt)
			{
				if (rt != 0) continue;
				if (next == null || u.Agility > next.Agility)
					next = u;
			}

			if (next == null) break;

			order.Add((next, next.CurrentRt));

			// Simulate: assume they do a medium action with 2 tiles of movement
			// This gives a rough "typical turn" preview
			int simTurnRt = next.CalculateTurnRt(2, ActionRt.MediumAttack, true, true);
			simRt[next] = simTurnRt;
		}

		return order;
	}

	/// <summary>Check if either team is wiped.</summary>
	public UnitTeam? GetWinningTeam()
	{
		bool teamAAlive = false, teamBAlive = false;
		foreach (var u in _units)
		{
			if (!u.IsAlive) continue;
			if (u.Team == UnitTeam.TeamA) teamAAlive = true;
			else teamBAlive = true;
		}

		if (!teamAAlive) return UnitTeam.TeamB;
		if (!teamBAlive) return UnitTeam.TeamA;
		return null;
	}
}
