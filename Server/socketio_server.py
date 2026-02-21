"""
Real-time messaging and position sync via Flask-SocketIO.
Handles proximity chat, global channels, admin whisper, and player positions.
"""
import math
import jwt
import os
from flask import request
from flask_socketio import SocketIO, emit, disconnect

from database import db
from models import Account, Character

socketio = SocketIO(cors_allowed_origins="*", async_mode="threading")

# ═══════════════════════════════════════════════════════════
#  CONNECTED PLAYER STATE
# ═══════════════════════════════════════════════════════════

# sid → player info
connected_players = {}

# character_id → sid (reverse lookup for admin whisper targeting)
char_to_sid = {}

# Chat ranges in pixels (tile = 32px)
TILE_PX = 32
CHAT_RANGES = {
    "say": 8 * TILE_PX,        # 256px
    "whisper": 2 * TILE_PX,    # 64px
    "yell": 12 * TILE_PX,      # 384px
    "emote": 10 * TILE_PX,     # 320px
    "story": 10 * TILE_PX,     # 320px
}

# Ranks that can SEND faction chat
FACTION_SEND_RANKS = {"Banneret", "Justicar"}


def distance(x1, y1, x2, y2):
    return math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)


def players_in_range(sender_sid, range_px):
    """Yield SIDs of players within range of sender."""
    sender = connected_players.get(sender_sid)
    if not sender:
        return
    sx, sy = sender["x"], sender["y"]
    for sid, p in connected_players.items():
        if sid == sender_sid:
            continue
        if distance(sx, sy, p["x"], p["y"]) <= range_px:
            yield sid


def all_other_sids(exclude_sid):
    for sid in connected_players:
        if sid != exclude_sid:
            yield sid


# ═══════════════════════════════════════════════════════════
#  AUTH HELPER
# ═══════════════════════════════════════════════════════════

def verify_token(token):
    """Verify JWT and return account or None."""
    try:
        secret = os.getenv("SECRET_KEY", "dev-secret-change-me")
        payload = jwt.decode(token, secret, algorithms=["HS256"])
        account_id = payload.get("account_id")
        if not account_id:
            return None
        return Account.query.get(account_id)
    except (jwt.ExpiredSignatureError, jwt.InvalidTokenError):
        return None


# ═══════════════════════════════════════════════════════════
#  SOCKET EVENTS
# ═══════════════════════════════════════════════════════════

@socketio.on("connect")
def handle_connect(auth=None):
    """Authenticate via JWT token passed in auth dict."""
    token = None
    if auth and isinstance(auth, dict):
        token = auth.get("token")

    if not token:
        disconnect()
        return

    account = verify_token(token)
    if not account:
        disconnect()
        return

    # Store minimal auth info — full data comes on join_world
    connected_players[request.sid] = {
        "account_id": account.id,
        "is_admin": account.is_admin,
        "character_id": None,
        "name": None,
        "rank": None,
        "allegiance": None,
        "x": 0.0,
        "y": 0.0,
    }
    print(f"[Socket] Connected: {account.username} (sid={request.sid})")


@socketio.on("disconnect")
def handle_disconnect():
    """Clean up player state on disconnect."""
    player = connected_players.pop(request.sid, None)
    if player and player.get("character_id"):
        char_to_sid.pop(player["character_id"], None)
        # Notify others
        for sid in list(connected_players.keys()):
            emit("player_left", {"id": player["character_id"]}, to=sid)
        print(f"[Socket] Disconnected: {player.get('name', '?')}")


@socketio.on("join_world")
def handle_join_world(data):
    """Player enters overworld with a character."""
    sid = request.sid
    player = connected_players.get(sid)
    if not player:
        return

    character_id = data.get("character_id")
    if not character_id:
        return

    character = Character.query.get(character_id)
    if not character or character.account_id != player["account_id"]:
        return

    # Update player state
    player["character_id"] = character.id
    player["name"] = character.name
    player["rank"] = character.rp_rank or "Aspirant"
    player["allegiance"] = character.allegiance or "None"
    player["x"] = float(data.get("x", 0))
    player["y"] = float(data.get("y", 0))

    char_to_sid[character.id] = sid

    # Tell this player about all other players already in world
    for other_sid, other in connected_players.items():
        if other_sid == sid or not other.get("character_id"):
            continue
        emit("player_joined", {
            "id": other["character_id"],
            "name": other["name"],
            "rank": other["rank"],
            "allegiance": other["allegiance"],
            "x": other["x"],
            "y": other["y"],
        }, to=sid)

    # Tell all other players about this player
    join_data = {
        "id": character.id,
        "name": character.name,
        "rank": player["rank"],
        "allegiance": player["allegiance"],
        "x": player["x"],
        "y": player["y"],
    }
    for other_sid in all_other_sids(sid):
        if connected_players[other_sid].get("character_id"):
            emit("player_joined", join_data, to=other_sid)

    print(f"[Socket] {character.name} joined world at ({player['x']:.0f}, {player['y']:.0f})")


@socketio.on("position")
def handle_position(data):
    """Update player position. Broadcast to nearby players."""
    sid = request.sid
    player = connected_players.get(sid)
    if not player or not player.get("character_id"):
        return

    player["x"] = float(data.get("x", player["x"]))
    player["y"] = float(data.get("y", player["y"]))

    move_data = {
        "id": player["character_id"],
        "x": player["x"],
        "y": player["y"],
    }

    # Broadcast to all other players (client can cull by distance)
    for other_sid in all_other_sids(sid):
        if connected_players[other_sid].get("character_id"):
            emit("player_moved", move_data, to=other_sid)


@socketio.on("chat")
def handle_chat(data):
    """Process a chat message. Route based on type."""
    sid = request.sid
    player = connected_players.get(sid)
    if not player or not player.get("character_id"):
        return

    msg_type = data.get("type", "say").lower()
    text = data.get("text", "").strip()
    target = data.get("target")  # for admin whisper
    color = data.get("color")    # sender's IC color

    if not text or len(text) > 2000:
        return

    chat_payload = {
        "sender": player["name"],
        "sender_id": player["character_id"],
        "text": text,
        "type": msg_type,
    }
    if color:
        chat_payload["color"] = color

    # ── PROXIMITY CHAT ──
    if msg_type in CHAT_RANGES:
        range_px = CHAT_RANGES[msg_type]
        for target_sid in players_in_range(sid, range_px):
            emit("chat", chat_payload, to=target_sid)
        return

    # ── OOC (global) ──
    if msg_type == "ooc":
        for target_sid in all_other_sids(sid):
            if connected_players[target_sid].get("character_id"):
                emit("chat", chat_payload, to=target_sid)
        return

    # ── FACTION (same allegiance, rank-gated for sending) ──
    if msg_type == "faction":
        # Only Banneret+ can send
        if player["rank"] not in FACTION_SEND_RANKS:
            emit("chat_error", {"error": "Only Banneret and above can use Faction chat."}, to=sid)
            return

        sender_allegiance = player["allegiance"]
        if not sender_allegiance or sender_allegiance == "None":
            emit("chat_error", {"error": "You have no faction allegiance."}, to=sid)
            return

        for target_sid, target_player in connected_players.items():
            if target_sid == sid:
                continue
            if not target_player.get("character_id"):
                continue
            # Same allegiance can READ faction chat (any rank)
            if target_player.get("allegiance") == sender_allegiance:
                emit("chat", chat_payload, to=target_sid)
        return

    # ── ADMIN WHISPER (direct to target character) ──
    if msg_type == "admin_whisper":
        if not player.get("is_admin"):
            return
        if not target:
            return

        # Find target by character name
        target_sid = None
        for check_sid, check_player in connected_players.items():
            if check_player.get("name") == target:
                target_sid = check_sid
                break

        if target_sid:
            chat_payload["target"] = target
            emit("chat", chat_payload, to=target_sid)
        else:
            emit("chat_error", {"error": f"Player '{target}' not online."}, to=sid)
        return

    # ── ANNOUNCE (admin broadcast) ──
    if msg_type == "announce":
        if not player.get("is_admin"):
            return
        for target_sid in all_other_sids(sid):
            if connected_players[target_sid].get("character_id"):
                emit("chat", chat_payload, to=target_sid)
        return


# ═══════════════════════════════════════════════════════════
#  UTILITY: Send announcement from HTTP route
# ═══════════════════════════════════════════════════════════

def broadcast_announcement(text, admin_name):
    """Called from admin HTTP route to push announcement via socket."""
    payload = {
        "sender": "SERVER",
        "sender_id": "",
        "text": text,
        "type": "announce",
    }
    for sid, player in connected_players.items():
        if player.get("character_id"):
            socketio.emit("chat", payload, to=sid)
