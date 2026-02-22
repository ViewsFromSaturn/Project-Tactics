using System;
using System.Collections.Generic;
using System.Linq;
using ProjectTactics.Combat;
using ProjectTactics.UI;

namespace ProjectTactics.Core;

// ═══════════════════════════════════════════════════════════════
//  EQUIPMENT ENUMS
// ═══════════════════════════════════════════════════════════════

public enum EquipSlot { Helmet, MainHand, OffHand, Chest, Arms, Legs, Accessory }
public enum ItemCategory { Equipment, Consumable, Material, KeyItem }

// ═══════════════════════════════════════════════════════════════
//  EQUIPMENT ITEM
// ═══════════════════════════════════════════════════════════════

public class EquipmentItem
{
	public string Id;
	public string Name;
	public string Icon;
	public string Description;
	public EquipSlot Slot;
	public int Rarity;  // 1-5
	public Dictionary<string, int> StatBonuses = new();
	public int Weight;
	public Element Element = Element.None;
	public string WeaponType;

	public string RarityStars => new string('★', Rarity) + new string('☆', 5 - Rarity);
	public string BonusSummary =>
		string.Join(", ", StatBonuses.Select(kv => $"{kv.Key} {(kv.Value >= 0 ? "+" : "")}{kv.Value}"));

	public static readonly string[] SlotIcons = { "🪖", "⚔", "🛡", "🧥", "🧤", "👢", "💍" };
	public static readonly string[] SlotNames = { "Helmet", "Main Hand", "Off Hand", "Chest", "Arms", "Legs", "Accessory" };

	public static string GetSlotIcon(EquipSlot slot) => SlotIcons[(int)slot];
	public static string GetSlotName(EquipSlot slot) => SlotNames[(int)slot];

	public static Godot.Color GetRarityColor(int rarity) => rarity switch
	{
		1 => UITheme.RarityCommon,
		2 => UITheme.RarityUncommon,
		3 => UITheme.RarityRare,
		4 => UITheme.RarityEpic,
		5 => UITheme.RarityLegendary,
		_ => UITheme.Text
	};
}

// ═══════════════════════════════════════════════════════════════
//  INVENTORY ITEM
// ═══════════════════════════════════════════════════════════════

public class InventoryItem
{
	public string Id;
	public string Name;
	public string Icon;
	public string Description;
	public ItemCategory Category;
	public int Quantity;
	public int MaxStack;
	public EquipmentItem EquipData;

	// Consumable combat fields
	public int RtCost;          // RT added when used in battle
	public string TargetType;   // "Self", "Single", etc.
	public int HealAmount;      // HP restored (0 if not a heal)
	public int StaminaRestore;  // STA restored
	public int AetherRestore;   // AE restored

	public bool IsEquipment => Category == ItemCategory.Equipment && EquipData != null;
	public bool IsConsumable => Category == ItemCategory.Consumable;
	public bool IsStackable => MaxStack > 1;
}

// ═══════════════════════════════════════════════════════════════
//  CHARACTER LOADOUT — shared state on GameManager
// ═══════════════════════════════════════════════════════════════

public class CharacterLoadout
{
	public event Action LoadoutChanged;
	private void Notify() => LoadoutChanged?.Invoke();

	// Equipment — 7 slots
	public Dictionary<EquipSlot, EquipmentItem> Equipment = new()
	{
		[EquipSlot.Helmet]    = null, [EquipSlot.MainHand]  = null,
		[EquipSlot.OffHand]   = null, [EquipSlot.Chest]     = null,
		[EquipSlot.Arms]      = null, [EquipSlot.Legs]      = null,
		[EquipSlot.Accessory] = null,
	};

	// Skill loadout: 5 Active + 2 Passive + 1 Auto
	public SkillDefinition[] ActiveSkills  = new SkillDefinition[5];
	public SkillDefinition[] PassiveSkills = new SkillDefinition[2];
	public SkillDefinition AutoSkill;

	// Spell loadout: 4 slots
	public SpellDefinition[] EquippedSpells = new SpellDefinition[4];

	// Learned pools (populated by AbilityShopPanel purchases)
	public HashSet<string> LearnedSkillIds = new();
	public HashSet<string> LearnedSpellIds = new();

	// RPP currency
	public int Rpp = 0;  // Loaded from server via CharacterSelect

	// Inventory: max 20
	public List<InventoryItem> Inventory = new();
	public const int MaxInventorySlots = 20;

	public int TotalEquipWeight =>
		Equipment.Values.Where(e => e != null).Sum(e => e.Weight);

	public Dictionary<string, int> TotalEquipBonuses()
	{
		var totals = new Dictionary<string, int>();
		foreach (var item in Equipment.Values)
		{
			if (item == null) continue;
			foreach (var kv in item.StatBonuses)
			{
				if (!totals.ContainsKey(kv.Key)) totals[kv.Key] = 0;
				totals[kv.Key] += kv.Value;
			}
		}
		return totals;
	}

	public List<SkillDefinition> GetLearnedSkills() =>
		LearnedSkillIds.Select(id => SkillDatabase.Get(id)).Where(s => s != null).ToList();

	public List<SpellDefinition> GetLearnedSpells() =>
		LearnedSpellIds.Select(id => SpellDatabase.Get(id)).Where(s => s != null).ToList();

	// ─── MUTATORS (fire event) ───

	public bool LearnSkill(string skillId, int rppCost)
	{
		if (Rpp < rppCost || LearnedSkillIds.Contains(skillId)) return false;
		Rpp -= rppCost;
		LearnedSkillIds.Add(skillId);
		Notify();
		return true;
	}

	public bool LearnSpell(string spellId, int rppCost)
	{
		if (Rpp < rppCost || LearnedSpellIds.Contains(spellId)) return false;
		Rpp -= rppCost;
		LearnedSpellIds.Add(spellId);
		Notify();
		return true;
	}

	public void EquipSkill(SkillSlotType type, int index, SkillDefinition skill)
	{
		switch (type)
		{
			case SkillSlotType.Active:  if (index < 5) ActiveSkills[index] = skill; break;
			case SkillSlotType.Passive: if (index < 2) PassiveSkills[index] = skill; break;
			case SkillSlotType.Auto:    AutoSkill = skill; break;
		}
		Notify();
	}

	public void EquipSpell(int index, SpellDefinition spell)
	{
		if (index >= 0 && index < 4) { EquippedSpells[index] = spell; Notify(); }
	}

	public void SetEquipment(EquipSlot slot, EquipmentItem item)
	{
		Equipment[slot] = item; Notify();
	}
}
