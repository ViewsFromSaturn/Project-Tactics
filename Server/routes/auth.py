"""
Authentication routes: register, login, logout, session validation.
Uses JWT tokens stored client-side (Godot will send in Authorization header).
"""
import os
from datetime import datetime, timedelta, timezone
from functools import wraps

import jwt
from flask import Blueprint, request, jsonify, g

from database import db
from models import Account, Character

auth_bp = Blueprint("auth", __name__)

SECRET_KEY = os.getenv("SECRET_KEY", "dev-secret-change-me")
TOKEN_EXPIRY_HOURS = 72  # 3 days


# ═════════════════════════════════════════════════════════════
#  JWT HELPERS
# ═════════════════════════════════════════════════════════════

def create_token(account):
    payload = {
        "sub": account.id,
        "username": account.username,
        "is_admin": account.is_admin,
        "exp": datetime.now(timezone.utc) + timedelta(hours=TOKEN_EXPIRY_HOURS),
        "iat": datetime.now(timezone.utc),
    }
    return jwt.encode(payload, SECRET_KEY, algorithm="HS256")


def require_auth(f):
    """Decorator to require valid JWT token on a route."""
    @wraps(f)
    def decorated(*args, **kwargs):
        token = None
        auth_header = request.headers.get("Authorization", "")

        if auth_header.startswith("Bearer "):
            token = auth_header[7:]

        if not token:
            return jsonify({"error": "No token provided"}), 401

        try:
            payload = jwt.decode(token, SECRET_KEY, algorithms=["HS256"])
            account = Account.query.get(payload["sub"])

            if not account:
                return jsonify({"error": "Account not found"}), 401
            if account.is_banned:
                return jsonify({"error": "Account is banned"}), 403

            g.current_account = account
        except jwt.ExpiredSignatureError:
            return jsonify({"error": "Token expired"}), 401
        except jwt.InvalidTokenError:
            return jsonify({"error": "Invalid token"}), 401

        return f(*args, **kwargs)

    return decorated


def require_admin(f):
    """Decorator to require admin privileges."""
    @wraps(f)
    @require_auth
    def decorated(*args, **kwargs):
        if not g.current_account.is_admin:
            return jsonify({"error": "Admin access required"}), 403
        return f(*args, **kwargs)

    return decorated


# ═════════════════════════════════════════════════════════════
#  ROUTES
# ═════════════════════════════════════════════════════════════

@auth_bp.route("/register", methods=["POST"])
def register():
    """Create a new account."""
    data = request.get_json()

    if not data:
        return jsonify({"error": "No data provided"}), 400

    username = data.get("username", "").strip()
    email = data.get("email", "").strip().lower()
    password = data.get("password", "")

    # ─── VALIDATION ──────────────────────────────────────────
    errors = []

    if not username or len(username) < 3 or len(username) > 30:
        errors.append("Username must be 3-30 characters.")
    if not email or "@" not in email:
        errors.append("Valid email required.")
    if not password or len(password) < 6:
        errors.append("Password must be at least 6 characters.")

    if errors:
        return jsonify({"error": errors}), 400

    # Check uniqueness
    if Account.query.filter_by(username=username).first():
        return jsonify({"error": "Username already taken."}), 409
    if Account.query.filter_by(email=email).first():
        return jsonify({"error": "Email already registered."}), 409

    # ─── CREATE ACCOUNT ──────────────────────────────────────
    account = Account(username=username, email=email)
    account.set_password(password)

    db.session.add(account)
    db.session.commit()

    token = create_token(account)

    return jsonify({
        "message": "Account created.",
        "token": token,
        "account": account.to_dict(),
    }), 201


@auth_bp.route("/login", methods=["POST"])
def login():
    """Log in with username/email and password."""
    data = request.get_json()

    if not data:
        return jsonify({"error": "No data provided"}), 400

    identifier = data.get("username", "").strip()  # Can be username or email
    password = data.get("password", "")

    # Find by username or email
    account = Account.query.filter(
        (Account.username == identifier) | (Account.email == identifier.lower())
    ).first()

    if not account or not account.check_password(password):
        return jsonify({"error": "Invalid username or password."}), 401

    if account.is_banned:
        return jsonify({"error": "Account is banned."}), 403

    # Update last login
    account.last_login = datetime.now(timezone.utc)
    db.session.commit()

    token = create_token(account)

    return jsonify({
        "message": "Login successful.",
        "token": token,
        "account": account.to_dict(),
    }), 200


@auth_bp.route("/me", methods=["GET"])
@require_auth
def me():
    """Get current account info (validates token)."""
    return jsonify({"account": g.current_account.to_dict()}), 200


@auth_bp.route("/resume", methods=["GET"])
@require_auth
def resume():
    """Validate token and return account + all character data in one call.
    Replaces: /auth/me + /characters/ + /characters/{id} for returning players."""
    account = g.current_account
    characters = Character.query.filter_by(account_id=account.id).all()

    slots = {1: None, 2: None, 3: None}
    full_characters = {}
    for char in characters:
        slots[char.slot] = char.to_summary()
        full_characters[char.id] = char.to_dict()

    return jsonify({
        "account": account.to_dict(),
        "slots": slots,
        "characters": full_characters,
    }), 200


@auth_bp.route("/change-password", methods=["POST"])
@require_auth
def change_password():
    """Change account password."""
    data = request.get_json()
    old_password = data.get("old_password", "")
    new_password = data.get("new_password", "")

    if not g.current_account.check_password(old_password):
        return jsonify({"error": "Current password is incorrect."}), 401

    if len(new_password) < 6:
        return jsonify({"error": "New password must be at least 6 characters."}), 400

    g.current_account.set_password(new_password)
    db.session.commit()

    return jsonify({"message": "Password changed."}), 200
