"""
Admin routes for staff moderation and management.
All routes require admin privileges.
"""
from flask import Blueprint, request, jsonify, g

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
