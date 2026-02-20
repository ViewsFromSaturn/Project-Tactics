"""
Database models for Project Tactics — Combat System v3.0.
Training stats: STR, VIT, DEX, AGI, ETC, MND
Resource pools: HP, Stamina, Aether
Daily training: RP-earned TP with 12:00 EST global reset
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

    characters = db.relationship(
        "Character", backref="account", lazy=True, cascade="all, delete-orphan"
    )

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
    Player character — Combat System v3.0.
    6 training stats, 3 resource pools, RP-earned daily training.
    Max 3 characters per account.
    """

    __tablename__ = "characters"

    id = db.Column(db.String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    account_id = db.Column(
        db.String(36), db.ForeignKey("accounts.id"), nullable=False
    )
    slot = db.Column(db.Integer, nullable=False)  # 1, 2, or 3

    # ─── IDENTITY ────────────────────────────────────────────
    name = db.Column(db.String(30), unique=True, nullable=False, index=True)
    race = db.Column(db.String(30), default="Human")
    city = db.Column(db.String(30), nullable=False)
    allegiance = db.Column(db.String(30), default="None")
    rp_rank = db.Column(db.String(30), default="Aspirant")
    bio = db.Column(db.String(500), default="")
    play_by_path = db.Column(db.String(256), default="")

    # ─── TRAINING STATS (v3.0) ───────────────────────────────
    strength = db.Column(db.Integer, default=1)       # STR
    vitality = db.Column(db.Integer, default=1)        # VIT (was Endurance)
    dexterity = db.Column(db.Integer, default=1)       # DEX (new — was Stamina stat)
    agility = db.Column(db.Integer, default=1)         # AGI
    ether_control = db.Column(db.Integer, default=1)   # ETC
    mind = db.Column(db.Integer, default=1)            # MND (new)

    # ─── DAILY TRAINING (RP-earned, banked TP) ───────────────
    training_points_bank = db.Column(db.Integer, default=0)   # Banked TP — persists until spent
    daily_tp_earned = db.Column(db.Integer, default=0)        # TP earned today via RP
    daily_rp_sessions = db.Column(db.Integer, default=0)      # Valid RP sessions today
    last_reset_date = db.Column(db.String(20), default="")    # ISO date of last 12 EST reset

    # ─── CURRENT COMBAT STATE (3 pools) ──────────────────────
    current_hp = db.Column(db.Integer, default=-1)
    current_stamina = db.Column(db.Integer, default=-1)
    current_aether = db.Column(db.Integer, default=-1)

    # ─── RACE MODIFIERS ──────────────────────────────────────
    race_hp_mod = db.Column(db.Float, default=1.0)
    race_stamina_mod = db.Column(db.Float, default=1.0)
    race_aether_mod = db.Column(db.Float, default=1.0)
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

    __table_args__ = (
        db.UniqueConstraint("account_id", "slot", name="uq_account_slot"),
    )

    # ═════════════════════════════════════════════════════════
    #  DERIVED STATS — mirrors PlayerData.cs formulas exactly
    # ═════════════════════════════════════════════════════════

    @property
    def character_level(self):
        return (
            self.strength + self.vitality + self.dexterity
            + self.agility + self.ether_control + self.mind
        ) // 6

    # ─── Resource Pools ──────────────────────────────────────

    @property
    def max_hp(self):
        """HP = 200 + VIT×15 + MND×8  (floor 223 at creation)"""
        return int((200 + self.vitality * 15 + self.mind * 8) * self.race_hp_mod)

    @property
    def max_stamina(self):
        """Stamina = 100 + STR×12 + VIT×8  (floor 120 at creation)"""
        return int(
            (100 + self.strength * 12 + self.vitality * 8) * self.race_stamina_mod
        )

    @property
    def max_aether(self):
        """Aether = 100 + ETC×20 + MND×5  (floor 125 at creation)"""
        return int(
            (100 + self.ether_control * 20 + self.mind * 5) * self.race_aether_mod
        )

    # ─── Regen per combat turn ───────────────────────────────

    @property
    def hp_regen(self):
        return int(self.mind * 0.4)

    @property
    def stamina_regen(self):
        return int(self.vitality * 0.3)

    @property
    def aether_regen(self):
        return int(self.ether_control * 0.8 * self.race_regen_mod)

    # ─── Combat Stats ────────────────────────────────────────

    @property
    def atk(self):
        return int((self.strength * 2.0 + self.dexterity * 0.5) * self.race_atk_mod)

    @property
    def eatk(self):
        return int(
            (self.ether_control * 2.5 + self.mind * 0.5) * self.race_eatk_mod
        )

    @property
    def defense(self):
        return int(self.vitality * 2.0 + self.strength * 0.5)

    @property
    def edef(self):
        return int(self.mind * 1.5 + self.vitality * 0.5)

    @property
    def avd(self):
        return int((self.agility * 1.5 + self.dexterity * 0.5) * self.race_avd_mod)

    @property
    def acc(self):
        return int(self.dexterity * 1.2 + self.agility * 0.3)

    @property
    def crit_percent(self):
        return int(self.dexterity * 0.3 + self.agility * 0.2)

    @property
    def move(self):
        return min(4 + self.agility // 15, 7)

    @property
    def jump(self):
        return min(2 + self.strength // 20, 5)

    @property
    def base_rt(self):
        return max(80, min(100 - self.agility // 5, 150))

    # ─── Daily TP cap ────────────────────────────────────────

    @property
    def daily_tp_cap(self):
        """Max TP earnable per day based on character level."""
        lvl = self.character_level
        if lvl < 10:
            return 5
        elif lvl < 20:
            return 3
        else:
            return 1

    @property
    def daily_tp_remaining(self):
        return max(0, self.daily_tp_cap - self.daily_tp_earned)

    # ═════════════════════════════════════════════════════════
    #  SERIALIZATION
    # ═════════════════════════════════════════════════════════

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
            # Training stats (v3.0)
            "strength": self.strength,
            "vitality": self.vitality,
            "dexterity": self.dexterity,
            "agility": self.agility,
            "ether_control": self.ether_control,
            "mind": self.mind,
            # Daily training (RP-earned)
            "training_points_bank": self.training_points_bank,
            "daily_tp_earned": self.daily_tp_earned,
            "daily_rp_sessions": self.daily_rp_sessions,
            "last_reset_date": self.last_reset_date,
            "daily_tp_cap": self.daily_tp_cap,
            "daily_tp_remaining": self.daily_tp_remaining,
            # Combat state (3 pools)
            "current_hp": self.current_hp,
            "current_stamina": self.current_stamina,
            "current_aether": self.current_aether,
            # Race modifiers
            "race_hp_mod": self.race_hp_mod,
            "race_stamina_mod": self.race_stamina_mod,
            "race_aether_mod": self.race_aether_mod,
            "race_atk_mod": self.race_atk_mod,
            "race_eatk_mod": self.race_eatk_mod,
            "race_avd_mod": self.race_avd_mod,
            "race_regen_mod": self.race_regen_mod,
            # Derived stats
            "character_level": self.character_level,
            "max_hp": self.max_hp,
            "max_stamina": self.max_stamina,
            "max_aether": self.max_aether,
            "hp_regen": self.hp_regen,
            "stamina_regen": self.stamina_regen,
            "aether_regen": self.aether_regen,
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
            "vitality": self.vitality,
            "dexterity": self.dexterity,
            "agility": self.agility,
            "ether_control": self.ether_control,
            "mind": self.mind,
        }
