using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Ability info for display in the battle HUD.
/// Actual ability logic will be data-driven later.
/// </summary>
public class AbilityInfo
{
	public string Name;
	public string Icon;        // emoji/text icon
	public string Category;    // "ether", "phys", "heal", "buff"
	public string Description;
	public int EtherCost;
	public int Power;
	public int Range;
	public int RtCost;
	public string TargetType;  // "Single", "AOE Diamond", "AOE Line", "Self"
	public bool IsUsable;      // enough ether, not silenced, etc.

	public static List<AbilityInfo> GetTestAbilities() => new()
	{
		new() { Name = "Ether Bolt",     Icon = "⚡", Category = "ether", Description = "Fires a bolt of raw ether at an enemy.",              EtherCost = 18, Power = 42, Range = 4, RtCost = 25, TargetType = "Single",      IsUsable = true },
		new() { Name = "Mend",           Icon = "✚", Category = "heal",  Description = "Restores moderate HP to an ally.",                     EtherCost = 25, Power = 35, Range = 3, RtCost = 20, TargetType = "Single",      IsUsable = true },
		new() { Name = "Flame Strike",   Icon = "🔥", Category = "phys",  Description = "Channels ether into a blazing melee attack.",          EtherCost = 35, Power = 60, Range = 1, RtCost = 30, TargetType = "Single",      IsUsable = true },
		new() { Name = "Haste",          Icon = "↑", Category = "buff",  Description = "Reduces target's RT by 30% for 3 turns.",              EtherCost = 20, Power = 0,  Range = 3, RtCost = 20, TargetType = "Single",      IsUsable = true },
		new() { Name = "Void Pulse",     Icon = "◎", Category = "ether", Description = "Releases a shockwave of void energy in a diamond.",    EtherCost = 45, Power = 55, Range = 3, RtCost = 35, TargetType = "AOE Diamond", IsUsable = true },
		new() { Name = "Healing Region", Icon = "♥", Category = "heal",  Description = "Creates a zone that heals allies within over 3 turns.", EtherCost = 50, Power = 25, Range = 4, RtCost = 30, TargetType = "AOE Diamond", IsUsable = false },
		new() { Name = "Ward",           Icon = "🛡", Category = "buff",  Description = "Grants a shield absorbing damage for 2 turns.",        EtherCost = 15, Power = 0,  Range = 3, RtCost = 15, TargetType = "Single",      IsUsable = true },
	};
}

/// <summary>
/// Item info for display in the battle HUD.
/// </summary>
public class ItemInfo
{
	public string Name;
	public string Icon;
	public string Description;
	public int Quantity;
	public int RtCost;
	public string TargetType; // "Self", "Single"
	public bool IsUsable;

	public static List<ItemInfo> GetTestItems() => new()
	{
		new() { Name = "Health Tonic", Icon = "♥", Description = "Restores 80 HP.",          Quantity = 3, RtCost = 20, TargetType = "Single", IsUsable = true },
		new() { Name = "Ether Vial",   Icon = "⚡", Description = "Restores 50 Ether.",       Quantity = 2, RtCost = 20, TargetType = "Self",   IsUsable = true },
		new() { Name = "Antidote",     Icon = "✚", Description = "Cures poison.",             Quantity = 1, RtCost = 15, TargetType = "Single", IsUsable = true },
		new() { Name = "Fire Bomb",    Icon = "💣", Description = "Deals 40 fire damage AOE.", Quantity = 2, RtCost = 25, TargetType = "Single", IsUsable = true },
	};
}
