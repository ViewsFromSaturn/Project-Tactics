"""
Character routes: CRUD, training (12 PM EST reset), RP sessions, name checking.
"""
from datetime import datetime, timezone

from flask import Blueprint, request, jsonify, g

from database import db
from models import Character
from routes.auth import require_auth
from daily_reset import (
    check_and_reset, award_rp_session_tp, seconds_until_next_reset,
    get_current_reset_period, calc_training_cost, get_lowest_stat
)

characters_bp = Blueprint("characters", __name__)

MAX_CHARACTERS = 3
VALID_CITIES = ["Lumere", "Praeven", "Caldris"]
VALID_RACES = [
    "Human", "Gorath", "Sythari", "Fenric", "Valdren",
    "Kaerath", "Nexari", "Ashborn", "Delvari", "Verskai"
]

# City → allowed races
CITY_RACES = {
    "Lumere":  ["Human", "Valdren", "Kaerath", "Delvari", "Ashborn", "Nexari"],
    "Praeven": ["Human", "Sythari", "Kaerath", "Delvari", "Ashborn", "Nexari"],
    "Caldris": ["Human", "Gorath", "Fenric", "Verskai", "Kaerath", "Delvari", "Ashborn", "Nexari"],
}

# Race → stat modifiers (mirrors RaceData.cs)
RACE_MODS = {
    "Human":   {"hp": 1.05, "sta": 1.05, "ae": 1.05, "atk": 1.05, "eatk": 1.05, "avd": 1.05, "regen": 1.05},
    "Gorath":  {"hp": 1.25, "sta": 1.25, "ae": 0.90, "atk": 1.20, "eatk": 0.90, "avd": 0.90, "regen": 1.00},
    "Sythari": {"hp": 1.00, "sta": 0.90, "ae": 1.10, "atk": 1.00, "eatk": 1.15, "avd": 1.10, "regen": 1.00},
    "Fenric":  {"hp": 1.10, "sta": 1.15, "ae": 0.95, "atk": 1.15, "eatk": 0.95, "avd": 1.10, "regen": 1.00},
    "Valdren": {"hp": 1.15, "sta": 1.10, "ae": 1.20, "atk": 1.00, "eatk": 1.00, "avd": 1.00, "regen": 1.25},
    "Kaerath": {"hp": 1.00, "sta": 1.15, "ae": 1.05, "atk": 1.10, "eatk": 1.00, "avd": 1.15, "regen": 1.10},
    "Nexari":  {"hp": 1.05, "sta": 0.95, "ae": 1.15, "atk": 1.00, "eatk": 1.05, "avd": 1.05, "regen": 1.15},
    "Ashborn": {"hp": 1.10, "sta": 1.05, "ae": 1.15, "atk": 1.00, "eatk": 1.15, "avd": 0.95, "regen": 1.00},
    "Delvari": {"hp": 1.00, "sta": 0.95, "ae": 1.10, "atk": 1.00, "eatk": 1.10, "avd": 1.05, "regen": 1.15},
    "Verskai": {"hp": 1.05, "sta": 1.10, "ae": 1.10, "atk": 1.10, "eatk": 1.05, "avd": 1.10, "regen": 1.00},
}


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
    race = data.get("race", "").strip()
    bio = data.get("bio", "").strip()
    slot = data.get("slot", 1)

    # ─── VALIDATION ──────────────────────────────────────
    errors = []

    if not name or len(name) < 2 or len(name) > 30:
        errors.append("Name must be 2-30 characters.")
    if city not in VALID_CITIES:
        errors.append(f"City must be one of: {', '.join(VALID_CITIES)}")
    if race not in VALID_RACES:
        errors.append(f"Race must be one of: {', '.join(VALID_RACES)}")
    elif city in CITY_RACES and race not in CITY_RACES[city]:
        errors.append(f"{race} cannot start in {city}.")
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
    mods = RACE_MODS.get(race, RACE_MODS["Human"])

    character = Character(
        account_id=g.current_account.id,
        slot=slot,
        name=name,
        city=city,
        bio=bio,
        race=race,
        allegiance="None",
        rp_rank="Aspirant",
        strength=1,
        vitality=1,
        agility=1,
        dexterity=1,
        mind=1,
        ether_control=1,
        training_points_bank=0,
        race_hp_mod=mods["hp"],
        race_stamina_mod=mods["sta"],
        race_aether_mod=mods["ae"],
        race_atk_mod=mods["atk"],
        race_eatk_mod=mods["eatk"],
        race_avd_mod=mods["avd"],
        race_regen_mod=mods["regen"],
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
    allowed_fields = ["bio", "current_hp", "current_stamina", "current_aether"]

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
#  TRAINING (SERVER-AUTHORITATIVE, 12:00 PM EST RESET)
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/<character_id>/train", methods=["POST"])
@require_auth
def train_stat(character_id):
    """
    Spend banked TP on a training stat. Server-authoritative with soft cap.
    TP bank carries over between days — 12 PM EST reset only affects RP counters.
    Soft cap: gap 10-19 = 2 TP per point, gap 20+ = 4 TP per point.
    """
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    # Always check reset on any training action
    check_and_reset(character)

    data = request.get_json()
    stat = data.get("stat", "").lower()
    points = data.get("points", 1)

    valid_stats = {
        "strength": "strength", "str": "strength",
        "vitality": "vitality", "vit": "vitality",
        "agility": "agility", "agi": "agility",
        "dexterity": "dexterity", "dex": "dexterity",
        "mind": "mind", "mnd": "mind",
        "ether_control": "ether_control", "etc": "ether_control",
    }

    if stat not in valid_stats:
        return jsonify({"error": f"Invalid stat: '{stat}'"}), 400

    stat_name = valid_stats[stat]

    if points < 1:
        return jsonify({"error": "Must allocate at least 1 point."}), 400

    if character.training_points_bank <= 0:
        return jsonify({
            "error": "No TP available. Earn TP through RP sessions.",
            "training_points_bank": 0,
        }), 400

    # ── SERVER-SIDE SOFT CAP ENFORCEMENT ──
    # Simulate allocation point-by-point to enforce correct TP costs
    lowest = get_lowest_stat(character)
    current_val = getattr(character, stat_name)
    tp_available = character.training_points_bank

    total_gained = 0
    total_spent = 0
    sim_val = current_val

    for _ in range(points):
        cost = calc_training_cost(sim_val, lowest)
        if total_spent + cost > tp_available:
            break
        total_spent += cost
        sim_val += 1
        total_gained += 1

    if total_gained == 0:
        cost_needed = calc_training_cost(current_val, lowest)
        return jsonify({
            "error": f"Need {cost_needed} TP for +1 {stat_name} (soft cap). Have {tp_available}.",
            "training_points_bank": tp_available,
        }), 400

    # Apply stat increase + deduct TP
    setattr(character, stat_name, current_val + total_gained)
    character.training_points_bank -= total_spent

    # Cap combat pools (don't exceed new max after stat change)
    character.current_hp = min(character.current_hp, character.max_hp)
    character.current_stamina = min(character.current_stamina, character.max_stamina)
    character.current_aether = min(character.current_aether, character.max_aether)

    db.session.commit()

    return jsonify({
        "message": f"+{total_gained} {stat_name} (spent {total_spent} TP)",
        "stat_gained": total_gained,
        "tp_spent": total_spent,
        "character": character.to_dict(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  RP SESSION → TP EARNING (SERVER-AUTHORITATIVE)
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/<character_id>/rp-session", methods=["POST"])
@require_auth
def complete_rp_session(character_id):
    """
    Called by the chat system when a valid RP session is completed.
    Server validates session count and awards TP per schedule:
      1st → +2 TP, 2nd → +2 TP, 3rd → +1 TP (capped by level)

    Chat system must verify before calling:
      - 10+ IC messages (Say, Emote, Yell verbs)
      - 30+ minutes active RP (gaps >5min excluded)
      - At least 1 other player
      - Anti-spam check passed (no copy-paste)
    """
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    tp_awarded, total_earned, at_cap = award_rp_session_tp(character)
    db.session.commit()

    if tp_awarded == 0 and at_cap:
        return jsonify({
            "message": "Daily TP cap reached. New TP available after reset.",
            "tp_awarded": 0,
            "daily_tp_earned": total_earned,
            "daily_tp_cap": character.daily_tp_cap,
            "training_points_bank": character.training_points_bank,
            "at_cap": True,
            "seconds_until_reset": seconds_until_next_reset(),
        }), 200

    return jsonify({
        "message": f"RP session complete! +{tp_awarded} TP earned.",
        "tp_awarded": tp_awarded,
        "daily_tp_earned": total_earned,
        "daily_tp_cap": character.daily_tp_cap,
        "daily_rp_sessions": character.daily_rp_sessions,
        "training_points_bank": character.training_points_bank,
        "at_cap": at_cap,
        "seconds_until_reset": seconds_until_next_reset(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  TRAINING STATUS (read-only, for UI countdown)
# ═══════════════════════════════════════════════════════════

@characters_bp.route("/<character_id>/training-status", methods=["GET"])
@require_auth
def training_status(character_id):
    """
    Current training period info for the UI timer and badge.
    Triggers reset check so client always sees fresh data.
    """
    character = Character.query.filter_by(
        id=character_id, account_id=g.current_account.id
    ).first()

    if not character:
        return jsonify({"error": "Character not found."}), 404

    check_and_reset(character)
    db.session.commit()

    return jsonify({
        "training_points_bank": character.training_points_bank,
        "daily_tp_earned": character.daily_tp_earned,
        "daily_tp_cap": character.daily_tp_cap,
        "daily_rp_sessions": character.daily_rp_sessions,
        "reset_period": get_current_reset_period(),
        "seconds_until_reset": seconds_until_next_reset(),
        "character_level": character.character_level,
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
