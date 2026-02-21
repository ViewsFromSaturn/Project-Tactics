"""
Admin routes for staff moderation and management.
All routes require admin privileges.
"""
print("!!! NEW ADMIN.PY LOADED !!!")
from flask import Blueprint, request, jsonify, g
from datetime import datetime, timezone

from database import db
from models import Account, Character
from routes.auth import require_admin

admin_bp = Blueprint("admin", __name__)

VALID_RANKS = ["Aspirant", "Sworn", "Warden", "Banneret", "Justicar"]
VALID_STATS = ["strength", "vitality", "agility", "dexterity", "mind", "ether_control"]


@admin_bp.route("/accounts", methods=["GET"])
@require_admin
def list_accounts():
    """List all accounts."""
    accounts = Account.query.order_by(Account.created_at.desc()).all()
    return jsonify({"accounts": [a.to_dict() for a in accounts]}), 200


@admin_bp.route("/characters", methods=["GET"])
@require_admin
def list_all_characters():
    """List all characters across all accounts."""
    characters = Character.query.order_by(Character.created_at.desc()).all()
    return jsonify({"characters": [c.to_summary() for c in characters]}), 200


@admin_bp.route("/character/<character_id>/set-rank", methods=["POST"])
@require_admin
def set_rank(character_id):
    """Set a character's RP rank."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    rank = data.get("rank", "")

    if rank not in VALID_RANKS:
        return jsonify({"error": f"Invalid rank. Must be one of: {', '.join(VALID_RANKS)}"}), 400

    old_rank = character.rp_rank
    character.rp_rank = rank
    db.session.commit()

    return jsonify({
        "message": f"{character.name}: {old_rank} → {rank}",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/character/<character_id>/grant-stats", methods=["POST"])
@require_admin
def grant_stats(character_id):
    """Grant bonus stat points to a character."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    grants = data.get("grants", {})

    applied = {}
    for stat_name, amount in grants.items():
        if stat_name in VALID_STATS and isinstance(amount, int) and amount > 0:
            current = getattr(character, stat_name)
            setattr(character, stat_name, current + amount)
            applied[stat_name] = amount

    if not applied:
        return jsonify({"error": "No valid stat grants provided."}), 400

    db.session.commit()

    return jsonify({
        "message": f"Stats granted to {character.name}: {applied}",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/account/<account_id>/ban", methods=["POST"])
@require_admin
def ban_account(account_id):
    """Ban or unban an account."""
    account = Account.query.get(account_id)
    if not account:
        return jsonify({"error": "Account not found."}), 404

    if account.id == g.current_account.id:
        return jsonify({"error": "Cannot ban yourself."}), 400

    data = request.get_json()
    ban = data.get("ban", True)

    account.is_banned = ban
    db.session.commit()

    action = "banned" if ban else "unbanned"
    return jsonify({"message": f"Account '{account.username}' {action}."}), 200


@admin_bp.route("/character/<character_id>/set-rpp", methods=["POST"])
@require_admin
def set_rpp(character_id):
    """Grant, remove, or set RPP for a character."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    mode = data.get("mode", "set")  # "set", "grant", "remove"
    amount = data.get("amount", 0)

    if not isinstance(amount, int) or amount < 0:
        return jsonify({"error": "Amount must be a non-negative integer."}), 400

    old_rpp = character.rpp
    if mode == "set":
        character.rpp = amount
    elif mode == "grant":
        character.rpp += amount
    elif mode == "remove":
        character.rpp = max(0, character.rpp - amount)
    else:
        return jsonify({"error": "Mode must be 'set', 'grant', or 'remove'."}), 400

    db.session.commit()
    return jsonify({
        "message": f"{character.name}: RPP {old_rpp} → {character.rpp} ({mode} {amount})",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/character/<character_id>/set-tp", methods=["POST"])
@require_admin
def set_tp(character_id):
    """Grant, remove, or set Training Points for a character."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    mode = data.get("mode", "set")
    amount = data.get("amount", 0)

    if not isinstance(amount, int) or amount < 0:
        return jsonify({"error": "Amount must be a non-negative integer."}), 400

    old_tp = character.training_points_bank
    if mode == "set":
        character.training_points_bank = amount
    elif mode == "grant":
        character.training_points_bank += amount
    elif mode == "remove":
        character.training_points_bank = max(0, character.training_points_bank - amount)
    else:
        return jsonify({"error": "Mode must be 'set', 'grant', or 'remove'."}), 400

    db.session.commit()
    return jsonify({
        "message": f"{character.name}: TP {old_tp} → {character.training_points_bank} ({mode} {amount})",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/character/<character_id>/set-stats", methods=["POST"])
@require_admin
def set_stats(character_id):
    """Set individual stat values (overwrites, not adds)."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    stats = data.get("stats", {})

    applied = {}
    for stat_name, value in stats.items():
        if stat_name in VALID_STATS and isinstance(value, int) and value >= 0:
            old = getattr(character, stat_name)
            setattr(character, stat_name, value)
            applied[stat_name] = f"{old} → {value}"

    if not applied:
        return jsonify({"error": "No valid stat overrides provided."}), 400

    db.session.commit()
    return jsonify({
        "message": f"Stats set for {character.name}: {applied}",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/character/<character_id>/set-level", methods=["POST"])
@require_admin
def set_level(character_id):
    """Set character level by evenly distributing stat points."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    level = data.get("level")
    rank = data.get("rank")

    if level is not None:
        if not isinstance(level, int) or level < 1 or level > 100:
            return jsonify({"error": "Level must be 1-100."}), 400
        # Level = average of 6 stats, so set each stat to level
        for stat in VALID_STATS:
            setattr(character, stat, level)

    if rank is not None:
        if rank not in VALID_RANKS:
            return jsonify({"error": f"Invalid rank. Must be one of: {', '.join(VALID_RANKS)}"}), 400
        character.rp_rank = rank

    db.session.commit()
    return jsonify({
        "message": f"{character.name}: Level → {character.character_level}, Rank → {character.rp_rank}",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/character/<character_id>/reset-training", methods=["POST"])
@require_admin
def reset_training(character_id):
    """Force reset daily training cooldowns for a character."""
    character = Character.query.get(character_id)
    if not character:
        return jsonify({"error": "Character not found."}), 404

    character.daily_tp_earned = 0
    character.daily_rp_sessions = 0
    character.last_reset_date = ""
    db.session.commit()

    return jsonify({
        "message": f"Daily training reset for {character.name}.",
        "character": character.to_dict(),
    }), 200


@admin_bp.route("/announce", methods=["POST"])
@require_admin
def announce():
    """Server-wide broadcast message. Client polls or receives via websocket."""
    data = request.get_json()
    message = data.get("message", "").strip()

    if not message or len(message) > 500:
        return jsonify({"error": "Message required (max 500 chars)."}), 400

    # Store announcement — clients can poll /api/admin/latest-announcement
    # For now, just acknowledge. WebSocket push comes later.
    return jsonify({
        "message": f"Announced: {message}",
        "announcement": {
            "text": message,
            "admin": g.current_account.username,
            "timestamp": datetime.now(timezone.utc).isoformat(),
        }
    }), 200
