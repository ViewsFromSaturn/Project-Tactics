"""
Equipment routes: equip/unequip gear, inventory management.
Equipment slots: main_hand, off_hand, head, body, legs, feet, ring, amulet
"""
from flask import Blueprint, request, jsonify, g

from database import db
from models import Character, CharacterEquipment, CharacterInventory, ItemDefinition
from routes.auth import require_auth

equipment_bp = Blueprint("equipment", __name__)

VALID_SLOTS = ["main_hand", "off_hand", "head", "body", "legs", "feet", "ring", "amulet"]


# ═══════════════════════════════════════════════════════════
#  GET EQUIPMENT + INVENTORY
# ═══════════════════════════════════════════════════════════

@equipment_bp.route("/<character_id>/equipment", methods=["GET"])
@require_auth
def get_equipment(character_id):
    """Return equipped items and full inventory."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    return jsonify({
        "equipment": char.get_equipped_items(),
        "inventory": char.get_inventory_list(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  EQUIP ITEM
# ═══════════════════════════════════════════════════════════

@equipment_bp.route("/<character_id>/equip", methods=["POST"])
@require_auth
def equip_item(character_id):
    """
    Equip an item from inventory to a slot.
    If slot is occupied, swap (old item goes back to inventory).
    Validates: ownership, item exists, correct slot, requirements met.
    """
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    item_id = data.get("item_id", "").strip()
    target_slot = data.get("slot", "").strip()

    if not item_id:
        return jsonify({"error": "item_id required."}), 400

    # Verify item definition exists
    item_def = ItemDefinition.query.get(item_id)
    if not item_def:
        return jsonify({"error": f"Item '{item_id}' not found."}), 404

    # Verify character owns the item
    inv_entry = CharacterInventory.query.filter_by(
        character_id=char.id, item_id=item_id
    ).first()
    if not inv_entry or inv_entry.quantity < 1:
        return jsonify({"error": "You don't own this item."}), 400

    # Determine slot
    slot = target_slot or item_def.slot
    if slot not in VALID_SLOTS:
        return jsonify({"error": f"Invalid slot: '{slot}'"}), 400
    if item_def.slot and item_def.slot != slot:
        return jsonify({"error": f"This item goes in '{item_def.slot}', not '{slot}'."}), 400

    # Check requirements
    if item_def.required_level > 0 and char.character_level < item_def.required_level:
        return jsonify({"error": f"Requires level {item_def.required_level} (you're {char.character_level})."}), 400

    if item_def.required_stat and item_def.required_stat_value > 0:
        stat_val = getattr(char, item_def.required_stat, 0)
        if stat_val < item_def.required_stat_value:
            return jsonify({
                "error": f"Requires {item_def.required_stat} {item_def.required_stat_value} (you have {stat_val})."
            }), 400

    # Unequip current item in that slot (if any)
    current_equip = CharacterEquipment.query.filter_by(
        character_id=char.id, slot=slot
    ).first()

    if current_equip:
        # Return old item to inventory
        _add_to_inventory(char.id, current_equip.item_id, 1)
        db.session.delete(current_equip)

    # Remove from inventory
    inv_entry.quantity -= 1
    if inv_entry.quantity <= 0:
        db.session.delete(inv_entry)

    # Equip new item
    equip = CharacterEquipment(
        character_id=char.id, slot=slot, item_id=item_id
    )
    db.session.add(equip)
    db.session.commit()

    return jsonify({
        "message": f"Equipped {item_def.name} → {slot}",
        "equipment": char.get_equipped_items(),
        "inventory": char.get_inventory_list(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  UNEQUIP ITEM
# ═══════════════════════════════════════════════════════════

@equipment_bp.route("/<character_id>/unequip", methods=["POST"])
@require_auth
def unequip_item(character_id):
    """Unequip an item from a slot, return to inventory."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    slot = data.get("slot", "").strip()

    if slot not in VALID_SLOTS:
        return jsonify({"error": f"Invalid slot: '{slot}'"}), 400

    equip = CharacterEquipment.query.filter_by(
        character_id=char.id, slot=slot
    ).first()

    if not equip:
        return jsonify({"error": f"Nothing equipped in {slot}."}), 400

    # Return to inventory
    _add_to_inventory(char.id, equip.item_id, 1)
    db.session.delete(equip)
    db.session.commit()

    return jsonify({
        "message": f"Unequipped {slot}",
        "equipment": char.get_equipped_items(),
        "inventory": char.get_inventory_list(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  INVENTORY MANAGEMENT
# ═══════════════════════════════════════════════════════════

@equipment_bp.route("/<character_id>/inventory/add", methods=["POST"])
@require_auth
def add_to_inventory(character_id):
    """Add items to inventory (admin/loot/quest reward)."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    item_id = data.get("item_id", "").strip()
    quantity = data.get("quantity", 1)

    if not item_id:
        return jsonify({"error": "item_id required."}), 400

    item_def = ItemDefinition.query.get(item_id)
    if not item_def:
        return jsonify({"error": f"Item '{item_id}' not found in catalog."}), 404

    if not isinstance(quantity, int) or quantity < 1:
        return jsonify({"error": "Quantity must be positive."}), 400

    _add_to_inventory(char.id, item_id, quantity)
    db.session.commit()

    return jsonify({
        "message": f"+{quantity}× {item_def.name}",
        "inventory": char.get_inventory_list(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  ITEM CATALOG (read-only)
# ═══════════════════════════════════════════════════════════

@equipment_bp.route("/catalog", methods=["GET"])
@require_auth
def get_catalog():
    """Return full item catalog, optionally filtered."""
    item_type = request.args.get("type")
    tier = request.args.get("tier", type=int)
    slot = request.args.get("slot")

    query = ItemDefinition.query
    if item_type:
        query = query.filter_by(item_type=item_type)
    if tier:
        query = query.filter_by(tier=tier)
    if slot:
        query = query.filter_by(slot=slot)

    items = query.order_by(ItemDefinition.tier, ItemDefinition.name).all()
    return jsonify({"items": [i.to_dict() for i in items]}), 200


# ═══════════════════════════════════════════════════════════
#  HELPERS
# ═══════════════════════════════════════════════════════════

def _add_to_inventory(character_id, item_id, quantity):
    """Add items to character inventory, stacking if already owned."""
    entry = CharacterInventory.query.filter_by(
        character_id=character_id, item_id=item_id
    ).first()

    if entry:
        entry.quantity += quantity
    else:
        entry = CharacterInventory(
            character_id=character_id, item_id=item_id, quantity=quantity
        )
        db.session.add(entry)
