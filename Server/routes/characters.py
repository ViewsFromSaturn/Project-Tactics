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
VALID_CITIES = ["Lumere", "Praeven", "Caldris"]


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
    city = data.get("city", "").strip()
    bio = data.get("bio", "").strip()
    slot = data.get("slot", 1)

    # ─── VALIDATION ──────────────────────────────────────
    errors = []

    if not name or len(name) < 2 or len(name) > 30:
        errors.append("Name must be 2-30 characters.")
    if city not in VALID_CITIES:
        errors.append(f"City must be one of: {', '.join(VALID_CITIES)}")
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
        city=city,
        bio=bio,
        race="Human",
        allegiance="None",
        rp_rank="Aspirant",
        strength=1,
        vitality=1,
        agility=1,
        dexterity=1,
        mind=1,
        ether_control=1,
        training_points_bank=0,
        race_hp_mod=1.0,
        race_stamina_mod=1.0,
        race_aether_mod=1.0,
        race_atk_mod=1.0,
        race_eatk_mod=1.0,
        race_avd_mod=1.0,
        race_regen_mod=1.0,
    )

    # Initialize HP/Ether to max
    character.current_hp = character.max_hp
    character.current_stamina = character.max_stamina
    character.current_aether = character.max_aether

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
    allowed_fields = ["bio", "current_hp", "current_stamina", "current_aether",
                      "training_points_bank", "last_reset_date"]

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
        "vitality": "vitality",
        "vit": "vitality",
        "agility": "agility",
        "agi": "agility",
        "dexterity": "dexterity",
        "dex": "dexterity",
        "mind": "mind",
        "mnd": "mind",
        "ether_control": "ether_control",
        "etc": "ether_control",
    }

    if stat not in valid_stats:
        return jsonify({"error": f"Invalid stat. Use: {list(valid_stats.keys())}"}), 400

    stat_name = valid_stats[stat]

    # Check daily reset — add TP to bank (v3.0: TP persists, daily cap on earning)
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    if character.last_reset_date != today:
        level = character.character_level
        if level < 10:
            daily_tp = 5
        elif level < 20:
            daily_tp = 3
        else:
            daily_tp = 1
        character.training_points_bank += daily_tp  # Add to bank, not replace
        character.daily_tp_earned = 0
        character.daily_rp_sessions = 0
        character.last_reset_date = today

    if points < 1:
        return jsonify({"error": "Must allocate at least 1 point."}), 400

    if character.training_points_bank < points:
        return jsonify({
            "error": f"Not enough points. Remaining: {character.training_points_bank}"
        }), 400

    # ─── SOFT CAP (DIMINISHING RETURNS) ──────────────────
    current_value = getattr(character, stat_name)
    stats = [character.strength, character.vitality, character.agility,
             character.dexterity, character.mind, character.ether_control]
    lowest = min(stats)
    gap = current_value - lowest

    # Calculate effective points
    actual_gain = 0
    points_spent = 0

    for _ in range(points):
        if points_spent >= character.training_points_bank:
            break

        if gap < 10:
            cost = 1   # 100% efficiency
        elif gap < 20:
            cost = 2   # 50% efficiency
        else:
            cost = 4   # 25% efficiency

        if points_spent + cost > character.training_points_bank:
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
    character.training_points_bank -= points_spent

    # Recalc current HP/ether (cap at new max)
    character.current_hp = min(character.current_hp, character.max_hp)
    character.current_aether = min(character.current_aether, character.max_aether)

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
