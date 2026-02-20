using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Core;

// ─── ENUMS ───────────────────────────────────────────────

public enum SocialTier { Noble, Common, Wild }

// ─── RACE DEFINITION ─────────────────────────────────────

public struct RaceDefinition
{
	public string Name;
	public string Description;
	public string PassiveDescription;
	public SocialTier Tier;
	public string[] HomeCities; // cities where this race can be selected

	// Stat modifiers (multiplied against derived stats)
	public float HpModifier;
	public float StaminaModifier;
	public float AetherModifier;
	public float AtkModifier;
	public float EatkModifier;
	public float AvdModifier;
	public float RegenModifier;

	// Elemental resists/weaknesses (flat % modifier on incoming damage)
	// Positive = resist (takes less), Negative = weakness (takes more)
	// e.g. { Fire, 0.20f } means 20% fire resist → takes 80% fire damage
	public Dictionary<Combat.Element, float> ElementResists;
}

// ─── RACE REGISTRY ───────────────────────────────────────

public partial class RaceData : Node
{
	private static readonly Dictionary<string, RaceDefinition> Races = new()
	{
		// ═══════════════════════════════════════════════════
		//  NOBLE RACES
		// ═══════════════════════════════════════════════════

		["Human"] = new RaceDefinition
		{
			Name = "Human",
			Description = "Builders of the empire. No innate gifts, no innate flaws — just relentless ambition.",
			PassiveDescription = "+5% to all stats. No elemental affinity.",
			Tier = SocialTier.Noble,
			HomeCities = new[] { "Lumere", "Praeven", "Caldris" },
			HpModifier = 1.05f,  StaminaModifier = 1.05f, AetherModifier = 1.05f,
			AtkModifier = 1.05f, EatkModifier = 1.05f,    AvdModifier = 1.05f,
			RegenModifier = 1.05f,
			ElementResists = new() // no resists, no weaknesses
		},

		["Valdren"] = new RaceDefinition
		{
			Name = "Valdren",
			Description = "Hardy and long-lived. Deep ether reserves flow through resilient bodies. Old blood of Lumere.",
			PassiveDescription = "+15% HP, +10% STA, +20% Aether, +25% Regen. Resist Ice/Water, weak to Lightning/Fire.",
			Tier = SocialTier.Noble,
			HomeCities = new[] { "Lumere" },
			HpModifier = 1.15f,  StaminaModifier = 1.10f, AetherModifier = 1.20f,
			AtkModifier = 1.0f,  EatkModifier = 1.0f,     AvdModifier = 1.0f,
			RegenModifier = 1.25f,
			ElementResists = new()
			{
				{ Combat.Element.Ice, 0.20f },
				{ Combat.Element.Water, 0.15f },
				{ Combat.Element.Lightning, -0.15f },
				{ Combat.Element.Fire, -0.10f },
			}
		},

		["Sythari"] = new RaceDefinition
		{
			Name = "Sythari",
			Description = "Sharp-sensed and ether-attuned. Praeven's scholars and seers, born to channel.",
			PassiveDescription = "+10% Aether, +15% EATK, +10% AVD, -10% STA. Resist Light/Dark, weak to Fire/Earth.",
			Tier = SocialTier.Noble,
			HomeCities = new[] { "Praeven" },
			HpModifier = 1.0f,   StaminaModifier = 0.90f, AetherModifier = 1.10f,
			AtkModifier = 1.0f,  EatkModifier = 1.15f,    AvdModifier = 1.10f,
			RegenModifier = 1.0f,
			ElementResists = new()
			{
				{ Combat.Element.Light, 0.20f },
				{ Combat.Element.Dark, 0.15f },
				{ Combat.Element.Fire, -0.15f },
				{ Combat.Element.Earth, -0.10f },
			}
		},

		// ═══════════════════════════════════════════════════
		//  COMMON RACES (available in all 3 cities)
		// ═══════════════════════════════════════════════════

		["Kaerath"] = new RaceDefinition
		{
			Name = "Kaerath",
			Description = "Disciplined and precise. Martial tradition honed over generations, respected in every city.",
			PassiveDescription = "+10% ATK, +15% AVD, +15% STA, +10% Regen. Resist Light/Wind, weak to Dark/Earth.",
			Tier = SocialTier.Common,
			HomeCities = new[] { "Lumere", "Praeven", "Caldris" },
			HpModifier = 1.0f,   StaminaModifier = 1.15f, AetherModifier = 1.05f,
			AtkModifier = 1.10f, EatkModifier = 1.0f,     AvdModifier = 1.15f,
			RegenModifier = 1.10f,
			ElementResists = new()
			{
				{ Combat.Element.Light, 0.15f },
				{ Combat.Element.Wind, 0.15f },
				{ Combat.Element.Dark, -0.15f },
				{ Combat.Element.Earth, -0.10f },
			}
		},

		["Delvari"] = new RaceDefinition
		{
			Name = "Delvari",
			Description = "Cunning tacticians with efficient ether circulation. Common-born, sharp-minded.",
			PassiveDescription = "+10% Aether/EATK, +15% Regen, -5% STA. Resist Lightning/Wind, weak to Earth/Dark.",
			Tier = SocialTier.Common,
			HomeCities = new[] { "Lumere", "Praeven", "Caldris" },
			HpModifier = 1.0f,   StaminaModifier = 0.95f, AetherModifier = 1.10f,
			AtkModifier = 1.0f,  EatkModifier = 1.10f,    AvdModifier = 1.05f,
			RegenModifier = 1.15f,
			ElementResists = new()
			{
				{ Combat.Element.Lightning, 0.15f },
				{ Combat.Element.Wind, 0.15f },
				{ Combat.Element.Earth, -0.15f },
				{ Combat.Element.Dark, -0.10f },
			}
		},

		["Ashborn"] = new RaceDefinition
		{
			Name = "Ashborn",
			Description = "Born from harsh, scorched lands. Strong ether burns through their veins. Respected but not trusted.",
			PassiveDescription = "+10% HP, +5% STA, +15% Aether/EATK. Resist Fire/Lightning, weak to Water/Ice.",
			Tier = SocialTier.Common,
			HomeCities = new[] { "Lumere", "Praeven", "Caldris" },
			HpModifier = 1.10f,  StaminaModifier = 1.05f, AetherModifier = 1.15f,
			AtkModifier = 1.0f,  EatkModifier = 1.15f,    AvdModifier = 0.95f,
			RegenModifier = 1.0f,
			ElementResists = new()
			{
				{ Combat.Element.Fire, 0.20f },
				{ Combat.Element.Lightning, 0.15f },
				{ Combat.Element.Water, -0.15f },
				{ Combat.Element.Ice, -0.10f },
			}
		},

		["Nexari"] = new RaceDefinition
		{
			Name = "Nexari",
			Description = "Symbiotic ether users who drain and sustain. Viewed with suspicion in imperial lands.",
			PassiveDescription = "+15% Aether/Regen, -5% STA, +5% HP/EATK/AVD. Resist Earth/Water, weak to Fire/Light.",
			Tier = SocialTier.Common,
			HomeCities = new[] { "Lumere", "Praeven", "Caldris" },
			HpModifier = 1.05f,  StaminaModifier = 0.95f, AetherModifier = 1.15f,
			AtkModifier = 1.0f,  EatkModifier = 1.05f,    AvdModifier = 1.05f,
			RegenModifier = 1.15f,
			ElementResists = new()
			{
				{ Combat.Element.Earth, 0.20f },
				{ Combat.Element.Water, 0.15f },
				{ Combat.Element.Fire, -0.15f },
				{ Combat.Element.Light, -0.10f },
			}
		},

		// ═══════════════════════════════════════════════════
		//  WILD / FEARED RACES (Caldris only)
		// ═══════════════════════════════════════════════════

		["Gorath"] = new RaceDefinition
		{
			Name = "Gorath",
			Description = "Massive and powerful. Built to endure and overwhelm. Too brutish for imperial courts.",
			PassiveDescription = "+25% HP/STA, +20% ATK, -10% Aether/EATK/AVD. Resist Fire/Earth, weak to Water/Ice.",
			Tier = SocialTier.Wild,
			HomeCities = new[] { "Caldris" },
			HpModifier = 1.25f,  StaminaModifier = 1.25f, AetherModifier = 0.90f,
			AtkModifier = 1.20f, EatkModifier = 0.90f,    AvdModifier = 0.90f,
			RegenModifier = 1.0f,
			ElementResists = new()
			{
				{ Combat.Element.Fire, 0.20f },
				{ Combat.Element.Earth, 0.15f },
				{ Combat.Element.Water, -0.15f },
				{ Combat.Element.Ice, -0.10f },
			}
		},

		["Fenric"] = new RaceDefinition
		{
			Name = "Fenric",
			Description = "Fast and ferocious. Predatory instincts and sharp reflexes. The empire fears what it cannot leash.",
			PassiveDescription = "+10% HP, +15% ATK/STA, +10% AVD, -5% Aether. Resist Lightning/Wind, weak to Ice/Fire.",
			Tier = SocialTier.Wild,
			HomeCities = new[] { "Caldris" },
			HpModifier = 1.10f,  StaminaModifier = 1.15f, AetherModifier = 0.95f,
			AtkModifier = 1.15f, EatkModifier = 0.95f,    AvdModifier = 1.10f,
			RegenModifier = 1.0f,
			ElementResists = new()
			{
				{ Combat.Element.Lightning, 0.20f },
				{ Combat.Element.Wind, 0.15f },
				{ Combat.Element.Ice, -0.15f },
				{ Combat.Element.Fire, -0.10f },
			}
		},

		["Verskai"] = new RaceDefinition
		{
			Name = "Verskai",
			Description = "Fluid and adaptive. Shifters of form and purpose. Nobody trusts what they cannot pin down.",
			PassiveDescription = "+10% ATK/AVD/Aether/STA, +5% HP/EATK. Resist Dark/Water, weak to Light/Lightning.",
			Tier = SocialTier.Wild,
			HomeCities = new[] { "Caldris" },
			HpModifier = 1.05f,  StaminaModifier = 1.10f, AetherModifier = 1.10f,
			AtkModifier = 1.10f, EatkModifier = 1.05f,    AvdModifier = 1.10f,
			RegenModifier = 1.0f,
			ElementResists = new()
			{
				{ Combat.Element.Dark, 0.15f },
				{ Combat.Element.Water, 0.15f },
				{ Combat.Element.Light, -0.15f },
				{ Combat.Element.Lightning, -0.10f },
			}
		},
	};

	// ═══════════════════════════════════════════════════════
	//  PUBLIC API
	// ═══════════════════════════════════════════════════════

	public static RaceDefinition GetRace(string raceName)
		=> Races.TryGetValue(raceName, out var race) ? race : Races["Human"];

	public static IEnumerable<string> GetAllRaceNames()
		=> Races.Keys;

	/// <summary>Get race names available for a specific city.</summary>
	public static IEnumerable<string> GetRacesForCity(string city)
		=> Races.Where(r => r.Value.HomeCities.Contains(city)).Select(r => r.Key);

	/// <summary>Get elemental damage modifier for incoming damage.
	/// Returns multiplier: 0.80 = 20% resist, 1.15 = 15% weakness.</summary>
	public static float GetElementDamageMod(string raceName, Combat.Element incomingElement)
	{
		var race = GetRace(raceName);
		if (incomingElement == Combat.Element.None) return 1.0f;
		if (race.ElementResists != null && race.ElementResists.TryGetValue(incomingElement, out float resist))
			return 1.0f - resist; // positive resist → less damage, negative → more
		return 1.0f;
	}

	/// <summary>Apply race passive modifiers to a PlayerData instance.</summary>
	public static void ApplyRacePassives(PlayerData data)
	{
		var race = GetRace(data.RaceName);
		data.RaceHpModifier      = race.HpModifier;
		data.RaceStaminaModifier = race.StaminaModifier;
		data.RaceAetherModifier  = race.AetherModifier;
		data.RaceAtkModifier     = race.AtkModifier;
		data.RaceEatkModifier    = race.EatkModifier;
		data.RaceAvdModifier     = race.AvdModifier;
		data.RaceRegenModifier   = race.RegenModifier;
		data.RefreshDerivedStats();
	}
}
