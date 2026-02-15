"""
Character routes: CRUD, training, name checking.
"""
from datetime import datetime, timezone

from flask import Blueprint, request, jsonify, g

from database import db
from models import Character
from routes.auth import require_auth

characters_bp = Blueprint("characters", __name__)

MAX_CHARACTERS = 3
VALID_VILLAGES = ["Konohagakure", "Sunagakure", "Kirigakure"]


# ═══════════════════════════════════════════════════════════
#  CHARACTER CRUD
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/", methods=["GET"])
@require_auth
def get_characters():
    """Get all character slots for the logged-in account."""
    characters = Character.query.filter_by(
        account_id=g.current_account.id
    ).all()

    slots = {1: None, 2: None, 3: None}
    for char in characters:
        slots[char.slot] = char.to_summary()

    return jsonify({"slots": slots}), 200


@characters_bp.route("/", methods=["POST"])
@require_auth
def create_character():
    """Create a new character in a slot."""
    data = request.get_json()

    if not data:
        return jsonify({"error": "No data provided"}), 400

    name = data.get("name", "").strip()
    village = data.get("village", "").strip()
    bio = data.get("bio", "").strip()
    slot = data.get("slot", 1)

    # ─── VALIDATION ──────────────────────────────────────
    errors = []

    if not name or len(name) < 2 or len(name) > 30:
        errors.append("Name must be 2-30 characters.")
    if village not in VALID_VILLAGES:
        errors.append(f"Village must be one of: {', '.join(VALID_VILLAGES)}")
    if slot not in [1, 2, 3]:
        errors.append("Slot must be 1, 2, or 3.")
    if len(bio) > 500:
        errors.append("Bio cannot exceed 500 characters.")

    if errors:
        return jsonify({"error": errors}), 400

    # Check slot availability
    existing = Character.query.filter_by(
        account_id=g.current_account.id, slot=slot
    ).first()
    if existing:
        return jsonify({"error": f"Slot {slot} is already occupied."}), 409

    # Check character count
    count = Character.query.filter_by(account_id=g.current_account.id).count()
    if count >= MAX_CHARACTERS:
        return jsonify({"error": f"Maximum {MAX_CHARACTERS} characters allowed."}), 409

    # Check name uniqueness
    if Character.query.filter_by(name=name).first():
        return jsonify({"error": "Character name already taken."}), 409

    # ─── CREATE ──────────────────────────────────────────
    character = Character(
        account_id=g.current_account.id,
        slot=slot,
        name=name,
        village=village,
        bio=bio,
        clan="Clanless",
        allegiance="None",
        rp_rank="Academy Student",
        strength=1,
        speed=1,
        agility=1,
        endurance=1,
        stamina=1,
        chakra_control=1,
        daily_points_remaining=5,
        clan_hp_mod=1.0,
        clan_chakra_mod=1.0,
        clan_atk_mod=1.0,
        clan_jatk_mod=1.0,
        clan_avd_mod=1.0,
        clan_regen_mod=1.0,
    )

    # Initialize HP/Chakra to max
    character.current_hp = character.max_hp
    character.current_chakra = character.max_chakra

    db.session.add(character)
    db.session.commit()

    return jsonify({
        "message": f"Character '{name}' created.",
        "character": character.to_dict(),
    }), 201


@characters_bp.route("/<character_id>", methods=["GET"])
@require_auth
def get_character(character_id):
    """Get full character data by ID."""
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    return jsonify({"character": character.to_dict()}), 200


@characters_bp.route("/<character_id>", methods=["PUT"])
@require_auth
def update_character(character_id):
    """Update character data (client save)."""
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()

    # Only allow updating specific fields from client
    allowed_fields = ["bio", "current_hp", "current_chakra",
                      "daily_points_remaining", "last_training_date"]

    for field in allowed_fields:
        if field in data:
            setattr(character, field, data[field])

    db.session.commit()

    return jsonify({
        "message": "Character updated.",
        "character": character.to_dict(),
    }), 200


@characters_bp.route("/<character_id>", methods=["DELETE"])
@require_auth
def delete_character(character_id):
    """Delete a character."""
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    name = character.name
    db.session.delete(character)
    db.session.commit()

    return jsonify({"message": f"Character '{name}' deleted."}), 200


# ═══════════════════════════════════════════════════════════
#  TRAINING (SERVER-AUTHORITATIVE)
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/<character_id>/train", methods=["POST"])
@require_auth
def train_stat(character_id):
    """Allocate training points to a stat. Server-authoritative with soft cap."""
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    stat = data.get("stat", "").lower()
    points = data.get("points", 1)

    valid_stats = {
        "strength": "strength",
        "str": "strength",
        "speed": "speed",
        "spd": "speed",
        "agility": "agility",
        "agi": "agility",
        "endurance": "endurance",
        "end": "endurance",
        "stamina": "stamina",
        "sta": "stamina",
        "chakra_control": "chakra_control",
        "ckc": "chakra_control",
    }

    if stat not in valid_stats:
        return jsonify({"error": f"Invalid stat. Use: {list(valid_stats.keys())}"}), 400

    stat_name = valid_stats[stat]

    # Check daily reset
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    if character.last_training_date != today:
        # Calculate daily points based on character level
        level = character.character_level
        if level < 10:
            character.daily_points_remaining = 5
        elif level < 20:
            character.daily_points_remaining = 3
        else:
            character.daily_points_remaining = 1
        character.last_training_date = today

    if points < 1:
        return jsonify({"error": "Must allocate at least 1 point."}), 400

    if character.daily_points_remaining < points:
        return jsonify({
            "error": f"Not enough points. Remaining: {character.daily_points_remaining}"
        }), 400

    # ─── SOFT CAP (DIMINISHING RETURNS) ──────────────────
    current_value = getattr(character, stat_name)
    stats = [character.strength, character.speed, character.agility,
             character.endurance, character.stamina, character.chakra_control]
    lowest = min(stats)
    gap = current_value - lowest

    # Calculate effective points
    actual_gain = 0
    points_spent = 0

    for _ in range(points):
        if points_spent >= character.daily_points_remaining:
            break

        if gap < 10:
            cost = 1   # 100% efficiency
        elif gap < 20:
            cost = 2   # 50% efficiency
        else:
            cost = 4   # 25% efficiency

        if points_spent + cost > character.daily_points_remaining:
            break

        actual_gain += 1
        points_spent += cost
        gap += 1

    if actual_gain == 0:
        return jsonify({
            "error": "Not enough points for soft cap cost.",
            "gap": gap,
            "cost_per_point": 4 if gap >= 20 else (2 if gap >= 10 else 1),
        }), 400

    # Apply
    setattr(character, stat_name, current_value + actual_gain)
    character.daily_points_remaining -= points_spent

    # Recalc current HP/chakra (cap at new max)
    character.current_hp = min(character.current_hp, character.max_hp)
    character.current_chakra = min(character.current_chakra, character.max_chakra)

    db.session.commit()

    return jsonify({
        "message": f"+{actual_gain} {stat_name} ({points_spent} points spent)",
        "character": character.to_dict(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  NAME CHECK
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/check-name", methods=["GET"])
@require_auth
def check_name():
    """Check if a character name is available."""
    name = request.args.get("name", "").strip()

    if not name or len(name) < 2:
        return jsonify({"available": False, "reason": "Name too short."}), 200

    existing = Character.query.filter_by(name=name).first()

    return jsonify({
        "available": existing is None,
        "name": name,
    }), 200
