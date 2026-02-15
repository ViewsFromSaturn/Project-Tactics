# Naruto RP — Game Design Reference

## Stat System Overview

### Tier 1: Training Stats (Player Controlled)

Players receive daily points to invest directly into these 6 stats.
1 point = +1 to that stat. No EXP bars.

| Stat           | Abbr | Purpose                              |
|----------------|------|--------------------------------------|
| Strength       | STR  | Physical power, taijutsu damage      |
| Speed          | SPD  | Burst speed, turn order, movement    |
| Agility        | AGI  | Reflexes, dodge, accuracy            |
| Endurance      | END  | Toughness, HP, physical defense      |
| Stamina        | STA  | Sustain, HP/Chakra pool depth        |
| Chakra Control | CKC  | Jutsu power, chakra pool, regen      |

### Daily Points

| Character Level | Daily Points |
|-----------------|-------------|
| 1 - 9           | 5           |
| 10 - 19         | 3           |
| 20+             | 1           |

Character Level = average of all 6 stats (rounded down)

### Soft Cap (Diminishing Returns)

| Gap Above Lowest Stat | Efficiency |
|------------------------|-----------|
| 0 - 9                  | 100%      |
| 10 - 19                | 50%       |
| 20+                    | 25%       |

---

### Tier 2: Derived Combat Stats (Auto-Calculated)

| Stat          | Formula                                           | Notes                    |
|---------------|---------------------------------------------------|--------------------------|
| HP            | (200 + END×15 + STA×8) × ClanHP                  | Health pool              |
| Chakra        | (100 + CKC×20 + STA×5) × ClanChakra              | Jutsu resource           |
| Chakra Regen  | CKC × 0.8 × ClanRegen                            | Per turn in combat       |
| ATK           | (STR×2.5 + SPD×0.5) × ClanATK                    | Physical damage          |
| DEF           | END×2.0 + STA×0.5                                 | Physical damage reduction|
| JATK          | (CKC×2.5 + AGI×0.3) × ClanJATK                   | Jutsu damage             |
| JDEF          | CKC×1.0 + END×1.0                                 | Jutsu damage reduction   |
| AVD           | (AGI×1.5 + SPD×1.0) × ClanAVD                    | Dodge chance base        |
| ACC           | AGI×1.0 + SPD×0.5                                 | Hit chance base          |
| CRIT%         | SPD×0.3 + AGI×0.2                                 | Critical hit chance      |
| MOVE          | 4 + floor(SPD/15), max 7                          | Tiles per turn           |
| JUMP          | 2 + floor(STR/20), max 5                          | Height levels            |
| RT            | clamp(100 - SPD/5 + ActionWeight, 80, 150)        | Turn cooldown            |

---

## Dodge Formulas

### Physical Dodge
```
Dodge% = (AVD × 0.4) + (AGI × 0.2) - (AttackerDEX × 0.3) + Terrain + Facing
Hard cap: 75%
```

### Jutsu Dodge
```
Dodge% = (AVD × 0.3) + (AGI × 0.2) - (AttackerINT × 0.3) + Terrain
AOE: halved
Hard cap: 60%
```

### Genjutsu Resist
```
Resist% = (MND × 0.4) + (SEN × 0.3) - (AttackerINT × 0.3)
Hard cap: 70%
```

---

## Damage Formulas

### Physical
```
Raw = (AttackerATK × 1.5) + SkillModifier
Final = max(Raw - DEF × 0.8, 1)
Cap: 60% of target MaxHP per hit
```

### Jutsu
```
Raw = (AttackerJATK × 1.5 + JutsuPower) × ElementBonus
Final = max(Raw - JDEF × 0.8, 1)
Cap: 60% of target MaxHP per hit
```

---

## Recovery Time (RT) System

- All units start at RT 0
- Lowest RT acts first
- After acting: RT = BaseRT + ActionWeight
- BaseRT = clamp(100 - SPD/5, 80, 150)
- RT Floor: 80 (speed stacking cap)
- RT Ceiling: 150 (slow builds protected)

### Action Weights
| Action         | Weight |
|----------------|--------|
| Wait / Defend  | 10     |
| Move Only      | 15     |
| Light Attack   | 20     |
| Medium Jutsu   | 30     |
| Heavy Jutsu    | 40     |
| Finishing Move  | 50     |

---

## Progression Systems (Future)

| System         | Source                | Spends On               |
|----------------|-----------------------|--------------------------|
| Daily Points   | Automatic (daily)     | Training stats (STR etc) |
| RPP            | RP participation      | Jutsu, perks, upgrades   |
| Rank           | Staff/events          | Story access, jutsu tier |
| Clan Passives  | Character creation    | Stat modifiers (free)    |
| Bonus EXP      | TBD system            | Extra daily points       |

---

## Clan Passives

| Clan         | HP    | Chakra | ATK   | JATK  | AVD   | Regen |
|--------------|-------|--------|-------|-------|-------|-------|
| Uzumaki      | +15%  | +20%   | —     | —     | —     | +25%  |
| Uchiha       | —     | +10%   | —     | +15%  | +10%  | —     |
| Hyuga        | —     | +5%    | +10%  | —     | +15%  | +10%  |
| Nara         | —     | +10%   | —     | +10%  | +5%   | +15%  |
| Akimichi     | +25%  | -10%   | +20%  | -10%  | -10%  | —     |
| Aburame      | +5%   | +15%   | —     | +5%   | +5%   | +15%  |
| Inuzuka      | +10%  | -5%    | +15%  | -5%   | +10%  | —     |
| Kazekage     | +10%  | +15%   | —     | +15%  | -5%   | —     |
| Hozuki       | +5%   | +10%   | +10%  | +5%   | +10%  | —     |
| Clanless     | +5%   | +5%    | +5%   | +5%   | +5%   | +5%   |

---

## Combat Turn Flow

```
1. RP NARRATION (optional, 60-120s timer)
   → Active player writes action narration

2. MECHANICAL ACTION (30s timer)
   → Move → Act (Jutsu/Attack/Item) → Face

3. REACTION POST (optional, 60s timer)
   → Defender narrates response

4. RESOLUTION (automatic)
   → Damage, dodge, status effects resolved
   → Combat log updates
   → Next unit (lowest RT)
```

---

## File Reference

| File                  | Purpose                              |
|-----------------------|--------------------------------------|
| GameManager.cs        | Singleton, game state, save/load     |
| PlayerData.cs         | Stat system, derived stats, formulas |
| ClanData.cs           | Clan definitions, passive modifiers  |
| DailyTraining.cs      | Point allocation, soft cap, reset    |
| PlayerController.cs   | Overworld movement, input handling   |
| CameraController.cs   | Smooth follow cam, zoom, shake       |
| DebugOverlay.cs       | Testing HUD, stat display            |
