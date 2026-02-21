"""
Ability routes: learn skills/spells, RPP economy, loadout persistence.
RPP costs: T1=3, T2=8, T3=15, T4=25 (same for skills & spells)
Prereqs: 3×T1→T2, 2×T2→T3, 1×T3→T4 (per tree/element)
Daily RPP cap: 8/day, resets at 12:00 PM EST (17:00 UTC)
"""
from flask import Blueprint, request, jsonify, g

from database import db
from models import Character, LearnedAbility
from routes.auth import require_auth
from daily_reset import get_current_reset_period

abilities_bp = Blueprint("abilities", __name__)

# ─── RPP COSTS BY TIER ──────────────────────────────────────
RPP_COST = {1: 3, 2: 8, 3: 15, 4: 25}
DAILY_RPP_CAP = 8

# ─── TIER GATE: how many of previous tier needed ────────────
TIER_GATE = {2: 3, 3: 2, 4: 1}  # T1=free


# ═══════════════════════════════════════════════════════════
#  LEARN SKILL
# ═══════════════════════════════════════════════════════════

@abilities_bp.route("/<character_id>/learn-skill", methods=["POST"])
@require_auth
def learn_skill(character_id):
    """
    Learn a skill. Server validates:
    - Character ownership
    - Not already learned
    - Tier gate met (3×T1→T2, 2×T2→T3, 1×T3→T4)
    - Sufficient RPP
    """
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    ability_id = data.get("ability_id", "").strip()
    tier = data.get("tier", 1)
    tree = data.get("tree", "").strip()

    if not ability_id:
        return jsonify({"error": "ability_id required."}), 400
    if tier not in RPP_COST:
        return jsonify({"error": f"Invalid tier: {tier}"}), 400

    # Already learned?
    if char.has_ability(ability_id, "skill"):
        return jsonify({"error": "Skill already learned."}), 409

    # Tier gate check
    if tier > 1:
        prev_tier = tier - 1
        needed = TIER_GATE.get(tier, 0)
        owned_prev = sum(
            1 for a in char.learned_abilities
            if a.ability_type == "skill" and a.tree_or_element == tree and a.tier == prev_tier
        )
        if owned_prev < needed:
            return jsonify({
                "error": f"Need {needed}× Tier {prev_tier} {tree} skills (have {owned_prev}).",
                "needed": needed, "have": owned_prev,
            }), 400

    # RPP check
    cost = RPP_COST[tier]
    if char.rpp < cost:
        return jsonify({
            "error": f"Need {cost} RPP (have {char.rpp}).",
            "cost": cost, "rpp": char.rpp,
        }), 400

    # Learn it
    char.rpp -= cost
    ability = LearnedAbility(
        character_id=char.id, ability_type="skill",
        ability_id=ability_id, tier=tier,
        tree_or_element=tree, rpp_spent=cost,
    )
    db.session.add(ability)
    db.session.commit()

    return jsonify({
        "message": f"Learned {ability_id} (-{cost} RPP)",
        "rpp": char.rpp,
        "learned_skills": char.get_learned_skill_ids(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  LEARN SPELL
# ═══════════════════════════════════════════════════════════

@abilities_bp.route("/<character_id>/learn-spell", methods=["POST"])
@require_auth
def learn_spell(character_id):
    """Learn a spell. Same validation as skills but for spells/elements."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    data = request.get_json()
    ability_id = data.get("ability_id", "").strip()
    tier = data.get("tier", 1)
    element = data.get("element", "").strip()

    if not ability_id:
        return jsonify({"error": "ability_id required."}), 400
    if tier not in RPP_COST:
        return jsonify({"error": f"Invalid tier: {tier}"}), 400

    if char.has_ability(ability_id, "spell"):
        return jsonify({"error": "Spell already learned."}), 409

    # Tier gate
    if tier > 1:
        prev_tier = tier - 1
        needed = TIER_GATE.get(tier, 0)
        owned_prev = sum(
            1 for a in char.learned_abilities
            if a.ability_type == "spell" and a.tree_or_element == element and a.tier == prev_tier
        )
        if owned_prev < needed:
            return jsonify({
                "error": f"Need {needed}× Tier {prev_tier} {element} spells (have {owned_prev}).",
                "needed": needed, "have": owned_prev,
            }), 400

    cost = RPP_COST[tier]
    if char.rpp < cost:
        return jsonify({
            "error": f"Need {cost} RPP (have {char.rpp}).",
            "cost": cost, "rpp": char.rpp,
        }), 400

    char.rpp -= cost
    ability = LearnedAbility(
        character_id=char.id, ability_type="spell",
        ability_id=ability_id, tier=tier,
        tree_or_element=element, rpp_spent=cost,
    )
    db.session.add(ability)
    db.session.commit()

    return jsonify({
        "message": f"Learned {ability_id} (-{cost} RPP)",
        "rpp": char.rpp,
        "learned_spells": char.get_learned_spell_ids(),
    }), 200


# ═══════════════════════════════════════════════════════════
#  GET LEARNED ABILITIES
# ═══════════════════════════════════════════════════════════

@abilities_bp.route("/<character_id>/abilities", methods=["GET"])
@require_auth
def get_abilities(character_id):
    """Return all learned skills and spells for a character."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    return jsonify({
        "learned_skills": char.get_learned_skill_ids(),
        "learned_spells": char.get_learned_spell_ids(),
        "abilities": [a.to_dict() for a in char.learned_abilities],
        "rpp": char.rpp,
    }), 200


# ═══════════════════════════════════════════════════════════
#  RPP EARNING — called by chat/RP system
# ═══════════════════════════════════════════════════════════

@abilities_bp.route("/<character_id>/earn-rpp", methods=["POST"])
@require_auth
def earn_rpp(character_id):
    """
    Award RPP for RP participation. Called by chat system.
    Daily cap: 8 RPP. Resets at 12:00 PM EST.

    Expected JSON: {"amount": 1-3, "reason": "rp_session"}
    """
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    # Check/reset daily RPP counter
    current_period = get_current_reset_period()
    if char.last_rpp_reset_date != current_period:
        char.daily_rpp_earned = 0
        char.last_rpp_reset_date = current_period

    data = request.get_json()
    amount = data.get("amount", 1)
    reason = data.get("reason", "rp_session")

    if not isinstance(amount, int) or amount < 1:
        return jsonify({"error": "Amount must be a positive integer."}), 400

    # Enforce daily cap
    remaining = DAILY_RPP_CAP - char.daily_rpp_earned
    if remaining <= 0:
        return jsonify({
            "message": "Daily RPP cap reached.",
            "rpp_awarded": 0, "rpp": char.rpp,
            "daily_rpp_earned": char.daily_rpp_earned,
            "daily_rpp_cap": DAILY_RPP_CAP,
            "at_cap": True,
        }), 200

    awarded = min(amount, remaining)
    char.rpp += awarded
    char.daily_rpp_earned += awarded
    db.session.commit()

    return jsonify({
        "message": f"+{awarded} RPP ({reason})",
        "rpp_awarded": awarded, "rpp": char.rpp,
        "daily_rpp_earned": char.daily_rpp_earned,
        "daily_rpp_cap": DAILY_RPP_CAP,
        "at_cap": char.daily_rpp_earned >= DAILY_RPP_CAP,
    }), 200


# ═══════════════════════════════════════════════════════════
#  RPP STATUS (read-only)
# ═══════════════════════════════════════════════════════════

@abilities_bp.route("/<character_id>/rpp-status", methods=["GET"])
@require_auth
def rpp_status(character_id):
    """Current RPP balance and daily earning status."""
    char = Character.query.filter_by(id=character_id, account_id=g.current_account.id).first()
    if not char:
        return jsonify({"error": "Character not found."}), 404

    # Check/reset
    current_period = get_current_reset_period()
    if char.last_rpp_reset_date != current_period:
        char.daily_rpp_earned = 0
        char.last_rpp_reset_date = current_period
        db.session.commit()

    return jsonify({
        "rpp": char.rpp,
        "daily_rpp_earned": char.daily_rpp_earned,
        "daily_rpp_cap": DAILY_RPP_CAP,
        "remaining_today": max(0, DAILY_RPP_CAP - char.daily_rpp_earned),
    }), 200
