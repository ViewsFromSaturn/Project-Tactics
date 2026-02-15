"""
Database models for Project Tactics.
"""
import uuid
from datetime import datetime, timezone

from werkzeug.security import generate_password_hash, check_password_hash

from database import db


class Account(db.Model):
    """Player account - one account can have multiple characters."""

    __tablename__ = "accounts"

    id = db.Column(db.String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    username = db.Column(db.String(30), unique=True, nullable=False, index=True)
    email = db.Column(db.String(120), unique=True, nullable=False, index=True)
    password_hash = db.Column(db.String(256), nullable=False)
    is_admin = db.Column(db.Boolean, default=False)
    is_banned = db.Column(db.Boolean, default=False)
    created_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))
    last_login = db.Column(db.DateTime, nullable=True)

    # Relationships
    characters = db.relationship("Character", backref="account", lazy=True, cascade="all, delete-orphan")

    def set_password(self, password):
        self.password_hash = generate_password_hash(password)

    def check_password(self, password):
        return check_password_hash(self.password_hash, password)

    def to_dict(self):
        return {
            "id": self.id,
            "username": self.username,
            "email": self.email,
            "is_admin": self.is_admin,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "character_count": len(self.characters),
        }


class Character(db.Model):
    """
    Player character with training stats, derived stats, and RP data.
    Max 3 characters per account.
    """

    __tablename__ = "characters"

    id = db.Column(db.String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    account_id = db.Column(db.String(36), db.ForeignKey("accounts.id"), nullable=False)
    slot = db.Column(db.Integer, nullable=False)  # 1, 2, or 3

    # ─── IDENTITY ────────────────────────────────────────────
    name = db.Column(db.String(30), unique=True, nullable=False, index=True)
    race = db.Column(db.String(30), default="Human")
    city = db.Column(db.String(30), nullable=False)
    allegiance = db.Column(db.String(30), default="None")
    rp_rank = db.Column(db.String(30), default="Aspirant")
    bio = db.Column(db.String(500), default="")
    play_by_path = db.Column(db.String(256), default="")

    # ─── TRAINING STATS ──────────────────────────────────────
    strength = db.Column(db.Integer, default=1)
    speed = db.Column(db.Integer, default=1)
    agility = db.Column(db.Integer, default=1)
    endurance = db.Column(db.Integer, default=1)
    stamina = db.Column(db.Integer, default=1)
    ether_control = db.Column(db.Integer, default=1)

    # ─── DAILY TRAINING ──────────────────────────────────────
    daily_points_remaining = db.Column(db.Integer, default=5)
    last_training_date = db.Column(db.String(20), default="")

    # ─── CURRENT COMBAT STATE ────────────────────────────────
    current_hp = db.Column(db.Integer, default=-1)
    current_ether = db.Column(db.Integer, default=-1)

    # ─── RACE MODIFIERS ──────────────────────────────────────
    race_hp_mod = db.Column(db.Float, default=1.0)
    race_ether_mod = db.Column(db.Float, default=1.0)
    race_atk_mod = db.Column(db.Float, default=1.0)
    race_eatk_mod = db.Column(db.Float, default=1.0)
    race_avd_mod = db.Column(db.Float, default=1.0)
    race_regen_mod = db.Column(db.Float, default=1.0)

    # ─── TIMESTAMPS ──────────────────────────────────────────
    created_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))
    updated_at = db.Column(
        db.DateTime,
        default=lambda: datetime.now(timezone.utc),
        onupdate=lambda: datetime.now(timezone.utc),
    )

    # ─── UNIQUE CONSTRAINT: one character per slot per account ─
    __table_args__ = (
        db.UniqueConstraint("account_id", "slot", name="uq_account_slot"),
    )

    # ═════════════════════════════════════════════════════════
    #  DERIVED STATS (mirrors PlayerData.cs formulas)
    # ═════════════════════════════════════════════════════════

    @property
    def character_level(self):
        return (self.strength + self.speed + self.agility +
                self.endurance + self.stamina + self.ether_control) // 6

    @property
    def max_hp(self):
        return int((200 + self.endurance * 15 + self.stamina * 8) * self.race_hp_mod)

    @property
    def max_ether(self):
        return int((100 + self.ether_control * 20 + self.stamina * 5) * self.race_ether_mod)

    @property
    def ether_regen(self):
        return int(self.ether_control * 0.8 * self.race_regen_mod)

    @property
    def atk(self):
        return int((self.strength * 2.5 + self.speed * 0.5) * self.race_atk_mod)

    @property
    def defense(self):
        return int(self.endurance * 2.0 + self.stamina * 0.5)

    @property
    def eatk(self):
        return int((self.ether_control * 2.5 + self.agility * 0.3) * self.race_eatk_mod)

    @property
    def edef(self):
        return int(self.ether_control * 1.0 + self.endurance * 1.0)

    @property
    def avd(self):
        return int((self.agility * 1.5 + self.speed * 1.0) * self.race_avd_mod)

    @property
    def acc(self):
        return int(self.agility * 1.0 + self.speed * 0.5)

    @property
    def crit_percent(self):
        return int(self.speed * 0.3 + self.agility * 0.2)

    @property
    def move(self):
        return min(4 + self.speed // 15, 7)

    @property
    def jump(self):
        return min(2 + self.strength // 20, 5)

    @property
    def base_rt(self):
        return max(80, min(100 - self.speed // 5, 150))

    def to_dict(self):
        """Full character data for the Godot client."""
        return {
            "id": self.id,
            "account_id": self.account_id,
            "slot": self.slot,
            # Identity
            "name": self.name,
            "race": self.race,
            "city": self.city,
            "allegiance": self.allegiance,
            "rp_rank": self.rp_rank,
            "bio": self.bio,
            "play_by_path": self.play_by_path,
            # Training stats
            "strength": self.strength,
            "speed": self.speed,
            "agility": self.agility,
            "endurance": self.endurance,
            "stamina": self.stamina,
            "ether_control": self.ether_control,
            # Daily training
            "daily_points_remaining": self.daily_points_remaining,
            "last_training_date": self.last_training_date,
            # Combat state
            "current_hp": self.current_hp,
            "current_ether": self.current_ether,
            # Race modifiers
            "race_hp_mod": self.race_hp_mod,
            "race_ether_mod": self.race_ether_mod,
            "race_atk_mod": self.race_atk_mod,
            "race_eatk_mod": self.race_eatk_mod,
            "race_avd_mod": self.race_avd_mod,
            "race_regen_mod": self.race_regen_mod,
            # Derived stats
            "character_level": self.character_level,
            "max_hp": self.max_hp,
            "max_ether": self.max_ether,
            "ether_regen": self.ether_regen,
            "atk": self.atk,
            "defense": self.defense,
            "eatk": self.eatk,
            "edef": self.edef,
            "avd": self.avd,
            "acc": self.acc,
            "crit_percent": self.crit_percent,
            "move": self.move,
            "jump": self.jump,
            "base_rt": self.base_rt,
            # Timestamps
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }

    def to_summary(self):
        """Lightweight version for character select screen."""
        return {
            "id": self.id,
            "slot": self.slot,
            "name": self.name,
            "race": self.race,
            "city": self.city,
            "rp_rank": self.rp_rank,
            "character_level": self.character_level,
            "strength": self.strength,
            "speed": self.speed,
            "agility": self.agility,
            "endurance": self.endurance,
            "stamina": self.stamina,
            "ether_control": self.ether_control,
        }
