namespace ProjectTactics.Combat;

/// <summary>
/// Weapon stat scaling from Combat v3.0.
/// Two-layer damage: StatOverhead (training) + EquipmentDelta (gear).
/// Melee = big stat overhead, small equip delta (training matters).
/// Ranged = HUGE equip delta (gear matters).
/// </summary>
public enum WeaponCategory { Melee, Ranged, Ether }

public class WeaponType
{
	public string Name;
	public WeaponCategory Category;

	// Attacker stat scaling
	public float AtkStr;   // STR weight for attacker
	public float AtkDex;   // DEX weight for attacker

	// Defender stat scaling
	public float DefStr;   // STR weight for defender
	public float DefVit;   // VIT weight for defender

	// Equipment multipliers
	public float AtkEquipMult;  // weapon ATK multiplier
	public float DefEquipMult;  // armor DEF multiplier

	// Weapon properties
	public int BaseRange;       // attack range in tiles
	public int BaseRtCost;      // RT cost for basic attack

	/// <summary>
	/// Calculate physical stat overhead for this weapon type.
	/// StatOverhead = (ATK_STR×WpnSTR + ATK_DEX×WpnDEX) - (DEF_STR×DefSTR + DEF_VIT×DefVIT)
	/// </summary>
	public int CalcStatOverhead(int atkStr, int atkDex, int defStr, int defVit, float affinity = 1.0f)
	{
		float attack = atkStr * AtkStr + atkDex * AtkDex;
		float defense = defStr * DefStr + defVit * DefVit;
		return (int)((attack - defense) * affinity);
	}

	/// <summary>
	/// Calculate equipment delta.
	/// EquipDelta = (WeaponATK × AtkEquipMult) - (ArmorDEF × DefEquipMult)
	/// </summary>
	public int CalcEquipDelta(int weaponAtk, int armorDef, float equipCompat = 1.0f)
	{
		return (int)(weaponAtk * AtkEquipMult * equipCompat - armorDef * DefEquipMult);
	}

	/// <summary>Full two-layer damage with anti one-shot cap.</summary>
	public int CalcDamage(int atkStr, int atkDex, int defStr, int defVit,
		int weaponAtk, int armorDef, int targetMaxHp,
		float affinity = 1.0f, float equipCompat = 1.0f)
	{
		int statOH = CalcStatOverhead(atkStr, atkDex, defStr, defVit, affinity);
		int equipD = CalcEquipDelta(weaponAtk, armorDef, equipCompat);
		int total = System.Math.Max(statOH + equipD, 1);
		int cap = (int)(targetMaxHp * 0.6f); // anti one-shot
		return System.Math.Min(total, cap);
	}
}

/// <summary>Ether ability scaling (spells).</summary>
public class EtherScaling
{
	public float AtkEtc;  // ETC weight
	public float AtkMnd;  // MND weight
	public float DefMnd;  // defender MND weight
	public float DefVit;  // defender VIT weight
	public float DefEquipMult; // armor EDEF multiplier

	public int CalcStatOverhead(int atkEtc, int atkMnd, int defMnd, int defVit, float affinity = 1.0f)
	{
		float attack = atkEtc * AtkEtc + atkMnd * AtkMnd;
		float defense = defMnd * DefMnd + defVit * DefVit;
		return (int)((attack - defense) * affinity);
	}

	public int CalcEquipDelta(int abilityPower, int armorEdef, float spellCompat = 1.0f)
	{
		return (int)(abilityPower * spellCompat - armorEdef * DefEquipMult);
	}

	public int CalcDamage(int atkEtc, int atkMnd, int defMnd, int defVit,
		int abilityPower, int armorEdef, int targetMaxHp,
		float affinity = 1.0f, float spellCompat = 1.0f)
	{
		int statOH = CalcStatOverhead(atkEtc, atkMnd, defMnd, defVit, affinity);
		int equipD = CalcEquipDelta(abilityPower, armorEdef, spellCompat);
		int total = System.Math.Max(statOH + equipD, 1);
		int cap = (int)(targetMaxHp * 0.6f);
		return System.Math.Min(total, cap);
	}
}

/// <summary>All 14 weapon types + ether scaling presets.</summary>
public static class WeaponTable
{
	// ─── MELEE ───────────────────────────────────────────
	public static readonly WeaponType Fist       = new() { Name="Fist",       Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.4f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=15 };
	public static readonly WeaponType Dagger     = new() { Name="Dagger",     Category=WeaponCategory.Melee,  AtkStr=1.5f, AtkDex=1.6f, DefStr=1.0f, DefVit=1.0f, AtkEquipMult=1.2f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=15 };
	public static readonly WeaponType Sword      = new() { Name="Sword",      Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.3f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=20 };
	public static readonly WeaponType Greatsword = new() { Name="Greatsword", Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.4f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=30 };
	public static readonly WeaponType Axe        = new() { Name="Axe",        Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.3f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=25 };
	public static readonly WeaponType Spear      = new() { Name="Spear",      Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.4f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=2, BaseRtCost=20 };
	public static readonly WeaponType Hammer     = new() { Name="Hammer",     Category=WeaponCategory.Melee,  AtkStr=1.8f, AtkDex=1.3f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=30 };
	public static readonly WeaponType Katana     = new() { Name="Katana",     Category=WeaponCategory.Melee,  AtkStr=1.5f, AtkDex=1.6f, DefStr=1.0f, DefVit=1.0f, AtkEquipMult=1.2f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=20 };
	public static readonly WeaponType Mace       = new() { Name="Mace",       Category=WeaponCategory.Melee,  AtkStr=1.0f, AtkDex=0.6f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=1.0f, DefEquipMult=1.0f, BaseRange=1, BaseRtCost=25 };

	// ─── RANGED ──────────────────────────────────────────
	public static readonly WeaponType Bow      = new() { Name="Bow",      Category=WeaponCategory.Ranged, AtkStr=1.3f, AtkDex=1.7f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=2.5f, DefEquipMult=2.5f, BaseRange=5, BaseRtCost=20 };
	public static readonly WeaponType Crossbow = new() { Name="Crossbow", Category=WeaponCategory.Ranged, AtkStr=1.3f, AtkDex=1.7f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=2.5f, DefEquipMult=2.5f, BaseRange=5, BaseRtCost=25 };
	public static readonly WeaponType Fusil    = new() { Name="Fusil",    Category=WeaponCategory.Ranged, AtkStr=1.3f, AtkDex=1.7f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=2.5f, DefEquipMult=2.5f, BaseRange=6, BaseRtCost=30 };
	public static readonly WeaponType Thrown   = new() { Name="Thrown",   Category=WeaponCategory.Ranged, AtkStr=1.3f, AtkDex=1.7f, DefStr=0.7f, DefVit=1.0f, AtkEquipMult=2.5f, DefEquipMult=2.5f, BaseRange=4, BaseRtCost=15 };

	// ─── ETHER SCALING PRESETS ───────────────────────────
	public static readonly EtherScaling DirectSpell = new() { AtkEtc=1.5f, AtkMnd=1.1f, DefMnd=0.7f, DefVit=1.0f, DefEquipMult=0.5f };
	public static readonly EtherScaling AoeSpell    = new() { AtkEtc=1.3f, AtkMnd=1.0f, DefMnd=0.7f, DefVit=1.0f, DefEquipMult=0.5f };
	public static readonly EtherScaling Healing     = new() { AtkEtc=1.3f, AtkMnd=1.0f, DefMnd=0.0f, DefVit=0.0f, DefEquipMult=0.0f };

	/// <summary>Lookup weapon type by name string.</summary>
	public static WeaponType Get(string name)
	{
		return name?.ToLower() switch
		{
			"fist"       => Fist,
			"dagger"     => Dagger,
			"sword"      => Sword,
			"greatsword" => Greatsword,
			"axe"        => Axe,
			"spear"      => Spear,
			"hammer"     => Hammer,
			"katana"     => Katana,
			"mace"       => Mace,
			"bow"        => Bow,
			"crossbow"   => Crossbow,
			"fusil"      => Fusil,
			"thrown"     => Thrown,
			_            => Fist, // default
		};
	}
}
