using System;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

public enum Element { None, Fire, Water, Ice, Lightning, Earth, Wind, Light, Dark }

public enum Affinity { Strong, Neutral, Weak }

/// <summary>
/// 8-element rock-paper-scissors system from Combat v3.0.
/// Modifiers:
///   StatOverhead affinity: Strong ×1.3, Neutral ×1.0, Weak ×0.9
///   EquipCompat:           Match  ×1.1, Neutral ×1.0, Opposed ×0.7
///   SpellCompat:           Match  ×1.4, Neutral ×1.0, Opposed ×0.5
/// </summary>
public static class ElementSystem
{
	// ─── STRENGTH TABLE ──────────────────────────────────
	// Key = attacker element, Value = set of elements it's strong against
	static readonly Dictionary<Element, HashSet<Element>> _strengths = new()
	{
		{ Element.Fire,      new() { Element.Ice, Element.Wind } },
		{ Element.Water,     new() { Element.Fire, Element.Earth } },
		{ Element.Ice,       new() { Element.Wind, Element.Water } },
		{ Element.Lightning, new() { Element.Water, Element.Ice } },
		{ Element.Earth,     new() { Element.Lightning, Element.Fire } },
		{ Element.Wind,      new() { Element.Earth, Element.Ice } },
		{ Element.Light,     new() { Element.Dark } },
		{ Element.Dark,      new() { Element.Light } },
	};

	static readonly Dictionary<Element, HashSet<Element>> _weaknesses = new()
	{
		{ Element.Fire,      new() { Element.Water, Element.Earth } },
		{ Element.Water,     new() { Element.Ice, Element.Lightning } },
		{ Element.Ice,       new() { Element.Fire, Element.Lightning } },
		{ Element.Lightning, new() { Element.Earth, Element.Wind } },
		{ Element.Earth,     new() { Element.Water, Element.Wind } },
		{ Element.Wind,      new() { Element.Fire, Element.Lightning } },
		{ Element.Light,     new() { Element.Dark } },
		{ Element.Dark,      new() { Element.Light } },
	};

	// ─── AFFINITY CHECK ──────────────────────────────────

	public static Affinity GetAffinity(Element attacker, Element defender)
	{
		if (attacker == Element.None || defender == Element.None)
			return Affinity.Neutral;

		if (_strengths.TryGetValue(attacker, out var strong) && strong.Contains(defender))
			return Affinity.Strong;

		if (_weaknesses.TryGetValue(attacker, out var weak) && weak.Contains(defender))
			return Affinity.Weak;

		return Affinity.Neutral;
	}

	// ─── MODIFIERS ───────────────────────────────────────

	/// <summary>Stat overhead affinity modifier (training stats vs training stats)</summary>
	public static float AffinityModifier(Element attacker, Element defender)
	{
		return GetAffinity(attacker, defender) switch
		{
			Affinity.Strong  => 1.3f,
			Affinity.Weak    => 0.9f,
			_                => 1.0f,
		};
	}

	/// <summary>Equipment compatibility modifier (weapon element vs armor element)</summary>
	public static float EquipCompatModifier(Element weapon, Element armor)
	{
		return GetAffinity(weapon, armor) switch
		{
			Affinity.Strong  => 1.1f,
			Affinity.Weak    => 0.7f,
			_                => 1.0f,
		};
	}

	/// <summary>Spell compatibility modifier (spell element vs target element)</summary>
	public static float SpellCompatModifier(Element spell, Element target)
	{
		return GetAffinity(spell, target) switch
		{
			Affinity.Strong  => 1.4f,
			Affinity.Weak    => 0.5f,
			_                => 1.0f,
		};
	}

	// ─── HIDDEN ELEMENT ROLL ─────────────────────────────

	/// <summary>Race-weighted element tables. Human = equal, others weighted.</summary>
	static readonly Dictionary<string, float[]> _raceWeights = new()
	{
		//                        Fire  Water  Ice   Lght  Earth  Wind  Light  Dark
		{ "Human",   new float[]{ 12.5f,12.5f,12.5f,12.5f,12.5f,12.5f,12.5f,12.5f } },
		{ "Gorath",  new float[]{ 30f,  5f,   5f,  10f,  30f,  10f,   5f,   5f   } },
		{ "Sythari", new float[]{ 5f,  15f,  15f,   5f,   5f,  15f,  20f,  20f   } },
		{ "Fenric",  new float[]{ 15f,  5f,   5f,  25f,   5f,  25f,  10f,  10f   } },
		{ "Valdren", new float[]{ 10f, 20f,  20f,  10f,  10f,  10f,  10f,  10f   } },
		{ "Kaerath", new float[]{ 10f, 10f,  10f,  10f,  10f,  10f,  20f,  20f   } },
		{ "Nexari", new float[]{ 5f,  20f,   5f,   5f,  25f,  20f,  10f,  10f   } },
		{ "Ashborn", new float[]{ 30f,  5f,   5f,  15f,  25f,   5f,   5f,  10f   } },
		{ "Delvari", new float[]{ 10f, 10f,  10f,  20f,  10f,  20f,  10f,  10f   } },
		{ "Verskai", new float[]{ 10f, 20f,  10f,  10f,  10f,  10f,  15f,  15f   } },
	};

	static readonly Element[] _elementOrder =
	{
		Element.Fire, Element.Water, Element.Ice, Element.Lightning,
		Element.Earth, Element.Wind, Element.Light, Element.Dark
	};

	/// <summary>Roll a hidden primary element based on race weights.</summary>
	public static Element RollElement(string race, Random rng = null)
	{
		rng ??= new Random();

		if (!_raceWeights.TryGetValue(race, out var weights))
			weights = _raceWeights["Human"];

		float roll = (float)(rng.NextDouble() * 100.0);
		float cumulative = 0;

		for (int i = 0; i < weights.Length; i++)
		{
			cumulative += weights[i];
			if (roll < cumulative)
				return _elementOrder[i];
		}

		return _elementOrder[^1]; // fallback
	}
}
