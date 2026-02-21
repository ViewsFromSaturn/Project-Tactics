using Godot;
using System.Linq;
using ProjectTactics.Core;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Inventory panel — reads from GameManager.ActiveLoadout.
/// Shows equipment slots and item inventory. Hotkey: I
/// </summary>
public partial class InventoryPanel : WindowPanel
{
	CharacterLoadout _loadout;
	VBoxContainer _content;

	public InventoryPanel()
	{
		WindowTitle = "Inventory";
		DefaultSize = new Vector2(360, 480);
		DefaultPosition = new Vector2(200, 100);
	}

	public override void OnOpen()
	{
		base.OnOpen();
		_loadout = GameManager.Instance?.ActiveLoadout;
		if (_loadout != null) _loadout.LoadoutChanged += OnLoadoutChanged;
		RebuildContent();
	}

	public override void OnClose()
	{
		base.OnClose();
		if (_loadout != null) _loadout.LoadoutChanged -= OnLoadoutChanged;
	}

	private void OnLoadoutChanged() => CallDeferred(nameof(RebuildContent));

	protected override void BuildContent(VBoxContainer content)
	{
		_content = content;
		_loadout = GameManager.Instance?.ActiveLoadout;
		RebuildContent();
	}

	private void RebuildContent()
	{
		if (_content == null) return;
		foreach (var c in _content.GetChildren()) c.QueueFree();

		if (_loadout == null)
		{
			_content.AddChild(PlaceholderText("No loadout data."));
			return;
		}

		// ── EQUIPMENT ──
		_content.AddChild(SectionHeader("Equipment"));

		foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
		{
			var item = _loadout.Equipment[slot];
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			row.CustomMinimumSize = new Vector2(0, 28);

			var slotLabel = UITheme.CreateDim($"{EquipmentItem.GetSlotIcon(slot)} {EquipmentItem.GetSlotName(slot)}", 10);
			slotLabel.CustomMinimumSize = new Vector2(100, 0);
			row.AddChild(slotLabel);

			if (item != null)
			{
				var nameLabel = UITheme.CreateBody(item.Name, 11, EquipmentItem.GetRarityColor(item.Rarity));
				nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(nameLabel);

				if (item.StatBonuses.Count > 0)
				{
					var bonusLabel = UITheme.CreateDim(item.BonusSummary, 9);
					row.AddChild(bonusLabel);
				}
			}
			else
			{
				var emptyLabel = UITheme.CreateDim("— Empty", 10);
				emptyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(emptyLabel);
			}

			_content.AddChild(row);
		}

		// Weight
		_content.AddChild(UITheme.CreateSpacer(4));
		var weightRow = new HBoxContainer(); weightRow.AddThemeConstantOverride("separation", 4);
		weightRow.AddChild(UITheme.CreateDim("Equip Weight:", 9));
		weightRow.AddChild(UITheme.CreateBody($"{_loadout.TotalEquipWeight}", 10, UITheme.TextSecondary));
		_content.AddChild(weightRow);

		_content.AddChild(UITheme.CreateSpacer(8));
		_content.AddChild(ThinSeparator());

		// ── ITEMS ──
		_content.AddChild(SectionHeader("Items"));

		if (_loadout.Inventory.Count == 0)
		{
			_content.AddChild(PlaceholderText("No items in inventory."));
		}
		else
		{
			foreach (var item in _loadout.Inventory)
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 8);
				row.CustomMinimumSize = new Vector2(0, 26);

				var icon = UITheme.CreateBody(item.Icon, 12, UITheme.Text);
				icon.CustomMinimumSize = new Vector2(24, 0);
				row.AddChild(icon);

				var nameLabel = UITheme.CreateBody(item.Name, 11, UITheme.Text);
				nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(nameLabel);

				if (item.IsStackable)
				{
					var qty = UITheme.CreateDim($"x{item.Quantity}", 10);
					row.AddChild(qty);
				}

				_content.AddChild(row);
			}
		}

		var countLabel = UITheme.CreateDim(
			$"{_loadout.Inventory.Count}/{CharacterLoadout.MaxInventorySlots} slots used", 9);
		countLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_content.AddChild(countLabel);
	}
}
