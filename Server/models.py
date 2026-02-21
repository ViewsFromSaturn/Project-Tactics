"""
Database models for Project Tactics — Combat System v3.0.
Training stats: STR, VIT, DEX, AGI, ETC, MND
Resource pools: HP, Stamina, Aether
Daily training: RP-earned TP with 12:00 EST global reset
RPP economy: RP-earned points for purchasing abilities
Abilities: Learned skills + spells persisted per character
Equipment: Item definitions + character gear slots + inventory
"""
import uuid
from datetime import datetime, timezone

from werkzeug.security import generate_password_hash, check_password_hash

from database import db


class Account(db.Model):
    __tablename__ = "accounts"

    id = db.Column(db.String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    username = db.Column(db.String(30), unique=True, nullable=False, index=True)
    email = db.Column(db.String(120), unique=True, nullable=False, index=True)
    password_hash = db.Column(db.String(256), nullable=False)
    is_admin = db.Column(db.Boolean, default=False)
    is_banned = db.Column(db.Boolean, default=False)
    created_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))
    last_login = db.Column(db.DateTime, nullable=True)

    characters = db.relationship("Character", backref="account", lazy=True, cascade="all, delete-orphan")

    def set_password(self, pw):
        self.password_hash = generate_password_hash(pw)

    def check_password(self, pw):
        return check_password_hash(self.password_hash, pw)

    def to_dict(self):
        return {
            "id": self.id, "username": self.username, "email": self.email,
            "is_admin": self.is_admin,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "character_count": len(self.characters),
        }


class Character(db.Model):
    __tablename__ = "characters"

    id = db.Column(db.String(36), primary_key=True, default=lambda: str(uuid.uuid4()))
    account_id = db.Column(db.String(36), db.ForeignKey("accounts.id"), nullable=False)
    slot = db.Column(db.Integer, nullable=False)

    # ─── IDENTITY ────────────────────────────────────────────
    name = db.Column(db.String(30), unique=True, nullable=False, index=True)
    race = db.Column(db.String(30), default="Human")
    city = db.Column(db.String(30), nullable=False)
    allegiance = db.Column(db.String(30), default="None")
    rp_rank = db.Column(db.String(30), default="Aspirant")
    bio = db.Column(db.String(500), default="")
    play_by_path = db.Column(db.String(256), default="")

    # ─── TRAINING STATS (v3.0) ───────────────────────────────
    strength = db.Column(db.Integer, default=1)
    vitality = db.Column(db.Integer, default=1)
    dexterity = db.Column(db.Integer, default=1)
    agility = db.Column(db.Integer, default=1)
    ether_control = db.Column(db.Integer, default=1)
    mind = db.Column(db.Integer, default=1)

    # ─── DAILY TRAINING (RP-earned, banked TP) ───────────────
    training_points_bank = db.Column(db.Integer, default=0)
    daily_tp_earned = db.Column(db.Integer, default=0)
    daily_rp_sessions = db.Column(db.Integer, default=0)
    last_reset_date = db.Column(db.String(20), default="")

    # ─── CURRENT COMBAT STATE ────────────────────────────────
    current_hp = db.Column(db.Integer, default=-1)
    current_stamina = db.Column(db.Integer, default=-1)
    current_aether = db.Column(db.Integer, default=-1)

    # ─── RPP (Roleplay Points) ───────────────────────────────
    rpp = db.Column(db.Integer, default=0)
    daily_rpp_earned = db.Column(db.Integer, default=0)
    last_rpp_reset_date = db.Column(db.String(20), default="")

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
    updated_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc),
                           onupdate=lambda: datetime.now(timezone.utc))

    # ─── RELATIONSHIPS ───────────────────────────────────────
    learned_abilities = db.relationship("LearnedAbility", backref="character", lazy=True, cascade="all, delete-orphan")
    equipment = db.relationship("CharacterEquipment", backref="character", lazy=True, cascade="all, delete-orphan")
    inventory = db.relationship("CharacterInventory", backref="character", lazy=True, cascade="all, delete-orphan")

    __table_args__ = (db.UniqueConstraint("account_id", "slot", name="uq_account_slot"),)

    # ═════════════════════════════════════════════════════════
    #  DERIVED STATS — mirrors PlayerData.cs
    # ═════════════════════════════════════════════════════════

    @property
    def character_level(self):
        return (self.strength + self.vitality + self.dexterity + self.agility + self.ether_control + self.mind) // 6

    @property
    def max_hp(self):
        return int((200 + self.vitality * 15 + self.mind * 8) * self.race_hp_mod)

    @property
    def max_stamina(self):
        return int((100 + self.strength * 12 + self.vitality * 8) * self.race_stamina_mod)

    @property
    def max_aether(self):
        return int((100 + self.ether_control * 20 + self.mind * 5) * self.race_aether_mod)

    @property
    def hp_regen(self):
        return int(self.mind * 0.4)

    @property
    def stamina_regen(self):
        return int(self.vitality * 0.3)

    @property
    def aether_regen(self):
        return int(self.ether_control * 0.8 * self.race_regen_mod)

    @property
    def atk(self):
        return int((self.strength * 2.0 + self.dexterity * 0.5) * self.race_atk_mod)

    @property
    def eatk(self):
        return int((self.ether_control * 2.5 + self.mind * 0.5) * self.race_eatk_mod)

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

    @property
    def daily_tp_cap(self):
        lvl = self.character_level
        if lvl < 10: return 5
        elif lvl < 20: return 3
        else: return 1

    @property
    def daily_tp_remaining(self):
        return max(0, self.daily_tp_cap - self.daily_tp_earned)

    # ─── ABILITY HELPERS ─────────────────────────────────────

    def get_learned_skill_ids(self):
        return [a.ability_id for a in self.learned_abilities if a.ability_type == "skill"]

    def get_learned_spell_ids(self):
        return [a.ability_id for a in self.learned_abilities if a.ability_type == "spell"]

    def has_ability(self, ability_id, ability_type):
        return any(a.ability_id == ability_id and a.ability_type == ability_type for a in self.learned_abilities)

    def get_equipped_items(self):
        return {e.slot: e.to_dict() for e in self.equipment}

    def get_inventory_list(self):
        return [i.to_dict() for i in self.inventory]

    # ═════════════════════════════════════════════════════════
    #  SERIALIZATION
    # ═════════════════════════════════════════════════════════

    def to_dict(self):
        return {
            "id": self.id, "account_id": self.account_id, "slot": self.slot,
            "name": self.name, "race": self.race, "city": self.city,
            "allegiance": self.allegiance, "rp_rank": self.rp_rank,
            "bio": self.bio, "play_by_path": self.play_by_path,
            "strength": self.strength, "vitality": self.vitality,
            "dexterity": self.dexterity, "agility": self.agility,
            "ether_control": self.ether_control, "mind": self.mind,
            "training_points_bank": self.training_points_bank,
            "daily_tp_earned": self.daily_tp_earned,
            "daily_rp_sessions": self.daily_rp_sessions,
            "last_reset_date": self.last_reset_date,
            "daily_tp_cap": self.daily_tp_cap,
            "daily_tp_remaining": self.daily_tp_remaining,
            "current_hp": self.current_hp,
            "current_stamina": self.current_stamina,
            "current_aether": self.current_aether,
            "rpp": self.rpp, "daily_rpp_earned": self.daily_rpp_earned,
            "race_hp_mod": self.race_hp_mod, "race_stamina_mod": self.race_stamina_mod,
            "race_aether_mod": self.race_aether_mod, "race_atk_mod": self.race_atk_mod,
            "race_eatk_mod": self.race_eatk_mod, "race_avd_mod": self.race_avd_mod,
            "race_regen_mod": self.race_regen_mod,
            "character_level": self.character_level,
            "max_hp": self.max_hp, "max_stamina": self.max_stamina,
            "max_aether": self.max_aether,
            "hp_regen": self.hp_regen, "stamina_regen": self.stamina_regen,
            "aether_regen": self.aether_regen,
            "atk": self.atk, "defense": self.defense,
            "eatk": self.eatk, "edef": self.edef,
            "avd": self.avd, "acc": self.acc,
            "crit_percent": self.crit_percent,
            "move": self.move, "jump": self.jump, "base_rt": self.base_rt,
            "learned_skills": self.get_learned_skill_ids(),
            "learned_spells": self.get_learned_spell_ids(),
            "equipment": self.get_equipped_items(),
            "inventory": self.get_inventory_list(),
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }

    def to_summary(self):
        return {
            "id": self.id, "slot": self.slot, "name": self.name,
            "race": self.race, "city": self.city, "rp_rank": self.rp_rank,
            "character_level": self.character_level,
            "strength": self.strength, "vitality": self.vitality,
            "dexterity": self.dexterity, "agility": self.agility,
            "ether_control": self.ether_control, "mind": self.mind,
        }


# ═════════════════════════════════════════════════════════════
#  LEARNED ABILITIES
# ═════════════════════════════════════════════════════════════

class LearnedAbility(db.Model):
    __tablename__ = "learned_abilities"

    id = db.Column(db.Integer, primary_key=True, autoincrement=True)
    character_id = db.Column(db.String(36), db.ForeignKey("characters.id"), nullable=False, index=True)
    ability_type = db.Column(db.String(10), nullable=False)  # 'skill' or 'spell'
    ability_id = db.Column(db.String(60), nullable=False)
    tier = db.Column(db.Integer, default=1)
    tree_or_element = db.Column(db.String(30), default="")  # e.g. "Vanguard" or "Fire"
    rpp_spent = db.Column(db.Integer, default=0)
    learned_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))

    __table_args__ = (db.UniqueConstraint("character_id", "ability_type", "ability_id", name="uq_char_ability"),)

    def to_dict(self):
        return {
            "ability_type": self.ability_type, "ability_id": self.ability_id,
            "tier": self.tier, "tree_or_element": self.tree_or_element,
            "rpp_spent": self.rpp_spent,
            "learned_at": self.learned_at.isoformat() if self.learned_at else None,
        }


# ═════════════════════════════════════════════════════════════
#  ITEM DEFINITIONS
# ═════════════════════════════════════════════════════════════

class ItemDefinition(db.Model):
    __tablename__ = "item_definitions"

    id = db.Column(db.String(60), primary_key=True)
    name = db.Column(db.String(60), nullable=False)
    description = db.Column(db.String(300), default="")
    item_type = db.Column(db.String(20), nullable=False)  # weapon, armor, accessory, consumable, material
    slot = db.Column(db.String(20), default="")  # main_hand, off_hand, head, body, legs, feet, ring, amulet

    weapon_type = db.Column(db.String(20), default="")
    atk_bonus = db.Column(db.Integer, default=0)
    eatk_bonus = db.Column(db.Integer, default=0)
    accuracy_bonus = db.Column(db.Integer, default=0)
    crit_bonus = db.Column(db.Integer, default=0)
    def_bonus = db.Column(db.Integer, default=0)
    edef_bonus = db.Column(db.Integer, default=0)
    avd_bonus = db.Column(db.Integer, default=0)
    hp_bonus = db.Column(db.Integer, default=0)
    stamina_bonus = db.Column(db.Integer, default=0)
    aether_bonus = db.Column(db.Integer, default=0)

    required_level = db.Column(db.Integer, default=0)
    required_stat = db.Column(db.String(20), default="")
    required_stat_value = db.Column(db.Integer, default=0)

    buy_price = db.Column(db.Integer, default=0)
    sell_price = db.Column(db.Integer, default=0)
    tier = db.Column(db.Integer, default=1)
    rarity = db.Column(db.String(15), default="common")

    craftable = db.Column(db.Boolean, default=False)
    craft_profession = db.Column(db.String(20), default="")
    craft_level = db.Column(db.Integer, default=0)

    def to_dict(self):
        return {
            "id": self.id, "name": self.name, "description": self.description,
            "item_type": self.item_type, "slot": self.slot,
            "weapon_type": self.weapon_type,
            "atk_bonus": self.atk_bonus, "eatk_bonus": self.eatk_bonus,
            "accuracy_bonus": self.accuracy_bonus, "crit_bonus": self.crit_bonus,
            "def_bonus": self.def_bonus, "edef_bonus": self.edef_bonus,
            "avd_bonus": self.avd_bonus,
            "hp_bonus": self.hp_bonus, "stamina_bonus": self.stamina_bonus,
            "aether_bonus": self.aether_bonus,
            "required_level": self.required_level,
            "required_stat": self.required_stat,
            "required_stat_value": self.required_stat_value,
            "buy_price": self.buy_price, "sell_price": self.sell_price,
            "tier": self.tier, "rarity": self.rarity,
            "craftable": self.craftable, "craft_profession": self.craft_profession,
            "craft_level": self.craft_level,
        }


# ═════════════════════════════════════════════════════════════
#  CHARACTER EQUIPMENT — equipped gear
# ═════════════════════════════════════════════════════════════

class CharacterEquipment(db.Model):
    __tablename__ = "character_equipment"

    id = db.Column(db.Integer, primary_key=True, autoincrement=True)
    character_id = db.Column(db.String(36), db.ForeignKey("characters.id"), nullable=False, index=True)
    slot = db.Column(db.String(20), nullable=False)
    item_id = db.Column(db.String(60), db.ForeignKey("item_definitions.id"), nullable=False)
    equipped_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))

    item = db.relationship("ItemDefinition", lazy="joined")

    __table_args__ = (db.UniqueConstraint("character_id", "slot", name="uq_char_slot"),)

    def to_dict(self):
        return {
            "slot": self.slot, "item_id": self.item_id,
            "item": self.item.to_dict() if self.item else None,
            "equipped_at": self.equipped_at.isoformat() if self.equipped_at else None,
        }


# ═════════════════════════════════════════════════════════════
#  CHARACTER INVENTORY — owned items
# ═════════════════════════════════════════════════════════════

class CharacterInventory(db.Model):
    __tablename__ = "character_inventory"

    id = db.Column(db.Integer, primary_key=True, autoincrement=True)
    character_id = db.Column(db.String(36), db.ForeignKey("characters.id"), nullable=False, index=True)
    item_id = db.Column(db.String(60), db.ForeignKey("item_definitions.id"), nullable=False)
    quantity = db.Column(db.Integer, default=1)
    obtained_at = db.Column(db.DateTime, default=lambda: datetime.now(timezone.utc))

    item = db.relationship("ItemDefinition", lazy="joined")

    __table_args__ = (db.UniqueConstraint("character_id", "item_id", name="uq_char_item"),)

    def to_dict(self):
        return {
            "item_id": self.item_id, "quantity": self.quantity,
            "item": self.item.to_dict() if self.item else None,
        }
