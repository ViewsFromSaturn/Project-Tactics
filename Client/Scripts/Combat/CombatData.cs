using System.Collections.Generic;

namespace ProjectTactics.Combat;

// ─── RESOURCE & SLOT ENUMS ───────────────────────────────

public enum ResourceType { None, Stamina, Aether, Both }
public enum SkillSlotType { Active, Passive, Auto }
public enum SkillCategory { Physical, Ether, Hybrid, Heal, Buff, Debuff }

/// <summary>What this ability actually DOES — derived from skill/spell data, not just resource type.</summary>
public enum AbilityIntent { DamageEnemy, HealAlly, BuffSelf, BuffAlly, DebuffEnemy, Utility }

// ─── ABILITY INFO ────────────────────────────────────────

public class AbilityInfo
{
	public string Name;
	public string Icon;
	public string SourceId;       // Original skill/spell ID for effect registry lookup
	public SkillCategory Category;
	public AbilityIntent Intent;   // What the ability actually does
	public SkillSlotType SlotType;
	public string Description;

	// Cost
	public ResourceType ResourceType;
	public int StaminaCost;
	public int AetherCost;

	// Combat
	public int Power;
	public int Range;
	public int RtCost;
	public string TargetType; // "Single", "AOE Diamond", "Self", "Line"

	// Element
	public Element Element;

	// Requirements
	public string RequiredWeapon; // null = any, "Sword", "Bow", etc.
	public string SkillTree;      // which tree it belongs to

	public bool IsUsable;

	/// <summary>Check if unit can afford this ability.</summary>
	public bool CanAfford(int currentStamina, int currentAether)
	{
		return ResourceType switch
		{
			ResourceType.Stamina => currentStamina >= StaminaCost,
			ResourceType.Aether  => currentAether >= AetherCost,
			ResourceType.Both    => currentStamina >= StaminaCost && currentAether >= AetherCost,
			_                    => true,
		};
	}

	/// <summary>Get cost display string.</summary>
	public string CostString()
	{
		return ResourceType switch
		{
			ResourceType.Stamina => $"{StaminaCost} STA",
			ResourceType.Aether  => $"{AetherCost} AE",
			ResourceType.Both    => $"{StaminaCost} STA + {AetherCost} AE",
			_                    => "Free",
		};
	}

	// ─── TEST DATA ───────────────────────────────────────

	public static List<AbilityInfo> GetTestAbilities() => new()
	{
		// Physical (Stamina)
		new() {
			Name="Double Strike", Icon="⚔", Category=SkillCategory.Physical, SlotType=SkillSlotType.Active,
			Description="Two rapid strikes against a single target.",
			ResourceType=ResourceType.Stamina, StaminaCost=18, AetherCost=0,
			Power=35, Range=1, RtCost=20, TargetType="Single", Element=Element.None,
			RequiredWeapon=null, SkillTree="Vanguard", IsUsable=true
		},
		new() {
			Name="Overpower", Icon="💪", Category=SkillCategory.Physical, SlotType=SkillSlotType.Active,
			Description="A devastating blow that ignores a portion of DEF.",
			ResourceType=ResourceType.Stamina, StaminaCost=30, AetherCost=0,
			Power=55, Range=1, RtCost=30, TargetType="Single", Element=Element.None,
			RequiredWeapon=null, SkillTree="Dreadnought", IsUsable=true
		},
		new() {
			Name="Shield Bash", Icon="🛡", Category=SkillCategory.Physical, SlotType=SkillSlotType.Active,
			Description="Strikes with shield, chance to stun.",
			ResourceType=ResourceType.Stamina, StaminaCost=15, AetherCost=0,
			Power=25, Range=1, RtCost=20, TargetType="Single", Element=Element.None,
			RequiredWeapon=null, SkillTree="Bulwark", IsUsable=true
		},

		// Ether (Aether)
		new() {
			Name="Ether Bolt", Icon="⚡", Category=SkillCategory.Ether, SlotType=SkillSlotType.Active,
			Description="Fires a bolt of raw ether at an enemy.",
			ResourceType=ResourceType.Aether, StaminaCost=0, AetherCost=18,
			Power=42, Range=4, RtCost=25, TargetType="Single", Element=Element.Lightning,
			RequiredWeapon=null, SkillTree="Evoker", IsUsable=true
		},
		new() {
			Name="Void Pulse", Icon="◎", Category=SkillCategory.Ether, SlotType=SkillSlotType.Active,
			Description="Releases a shockwave of void energy in a diamond.",
			ResourceType=ResourceType.Aether, StaminaCost=0, AetherCost=45,
			Power=55, Range=3, RtCost=35, TargetType="AOE Diamond", Element=Element.Dark,
			RequiredWeapon=null, SkillTree="Hexer", IsUsable=true
		},

		// Heal (Aether)
		new() {
			Name="Mend", Icon="✚", Category=SkillCategory.Heal, SlotType=SkillSlotType.Active,
			Description="Restores moderate HP to an ally.",
			ResourceType=ResourceType.Aether, StaminaCost=0, AetherCost=25,
			Power=35, Range=3, RtCost=20, TargetType="Single", Element=Element.Light,
			RequiredWeapon=null, SkillTree="Mender", IsUsable=true
		},

		// Hybrid (Both)
		new() {
			Name="Aether Edge", Icon="🗡", Category=SkillCategory.Hybrid, SlotType=SkillSlotType.Active,
			Description="Channels ether into blade for a devastating hybrid strike.",
			ResourceType=ResourceType.Both, StaminaCost=20, AetherCost=15,
			Power=60, Range=1, RtCost=30, TargetType="Single", Element=Element.None,
			RequiredWeapon="Sword", SkillTree="Runeblade", IsUsable=true
		},
	};
}

// ─── ITEM INFO ───────────────────────────────────────────

public class ItemInfo
{
	public string Name;
	public string Icon;
	public string Description;
	public int Quantity, RtCost;
	public string TargetType;
	public bool IsUsable;

	public static List<ItemInfo> GetTestItems() => new()
	{
		new() { Name="Health Tonic",  Icon="♥", Description="Restores 80 HP.",       Quantity=3, RtCost=20, TargetType="Single", IsUsable=true },
		new() { Name="Stamina Draft", Icon="▲", Description="Restores 50 Stamina.",  Quantity=2, RtCost=20, TargetType="Self",   IsUsable=true },
		new() { Name="Aether Vial",   Icon="⚡", Description="Restores 50 Aether.",   Quantity=2, RtCost=20, TargetType="Self",   IsUsable=true },
		new() { Name="Antidote",      Icon="✚", Description="Cures poison.",          Quantity=1, RtCost=15, TargetType="Single", IsUsable=true },
		new() { Name="Fire Bomb",     Icon="💣", Description="Deals 40 fire damage.", Quantity=2, RtCost=25, TargetType="Single", IsUsable=true },
	};
}
