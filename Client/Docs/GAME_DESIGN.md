# Project Tactics — Game Design Reference

## Stat System Overview

### Tier 1: Training Stats (Player Controlled)

Players receive daily points to invest directly into these 6 stats.
1 point = +1 to that stat. No EXP bars.

| Stat          | Abbr | Purpose                              |
|---------------|------|--------------------------------------|
| Strength      | STR  | Physical power, melee damage         |
| Speed         | SPD  | Burst speed, turn order, movement    |
| Agility       | AGI  | Reflexes, dodge, accuracy            |
| Endurance     | END  | Toughness, HP, physical defense      |
| Stamina       | STA  | Sustain, HP/Ether pool depth         |
| Ether Control | ETC  | Ability power, ether pool, regen     |

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
| HP            | (200 + END×15 + STA×8) × RaceHP               | Health pool              |
| Ether         | (100 + ETC×20 + STA×5) × RaceEther            | Ability resource         |
| Ether Regen   | ETC × 0.8 × RaceRegen                         | Per turn in combat       |
| ATK           | (STR×2.5 + SPD×0.5) × RaceATK                 | Physical damage          |
| DEF           | END×2.0 + STA×0.5                                 | Physical damage reduction|
| EATK          | (ETC×2.5 + AGI×0.3) × RaceEATK                | Ether ability damage     |
| EDEF          | ETC×1.0 + END×1.0                                 | Ether damage reduction   |
| AVD           | (AGI×1.5 + SPD×1.0) × RaceAVD                 | Dodge chance base        |
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

### Ether Ability Dodge
```
Dodge% = (AVD × 0.3) + (AGI × 0.2) - (AttackerINT × 0.3) + Terrain
AOE: halved
Hard cap: 60%
```

### Mental Resist
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

### Ether Ability
```
Raw = (AttackerEATK × 1.5 + AbilityPower) × ElementBonus
Final = max(Raw - EDEF × 0.8, 1)
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
| Action           | Weight |
|------------------|--------|
| Wait / Defend    | 10     |
| Move Only        | 15     |
| Light Attack     | 20     |
| Medium Ability   | 30     |
| Heavy Ability    | 40     |
| Finishing Move   | 50     |

---

## Progression Systems (Future)

| System           | Source                | Spends On                |
|------------------|-----------------------|--------------------------|
| Daily Points     | Automatic (daily)     | Training stats (STR etc) |
| RPP              | RP participation      | Abilities, perks, upgrades |
| Rank             | Staff/events          | Story access, ability tier |
| Race Passives | In-game RP            | Stat modifiers (free)    |
| Bonus EXP        | TBD system            | Extra daily points       |

---

## Race Passives

| Race                 | HP    | Ether  | ATK   | EATK  | AVD   | Regen |
|----------------------|-------|--------|-------|-------|-------|-------|
| Ironblood Covenant   | +15%  | +20%   | —     | —     | —     | +25%  |
| Ashen Veil           | —     | +10%   | —     | +15%  | +10%  | —     |
| Pale Ward            | —     | +5%    | +10%  | —     | +15%  | +10%  |
| Duskwatch            | —     | +10%   | —     | +10%  | +5%   | +15%  |
| Stoneborne           | +25%  | -10%   | +20%  | -10%  | -10%  | —     |
| Thornmantle          | +5%   | +15%   | —     | +5%   | +5%   | +15%  |
| Redfang              | +10%  | -5%    | +15%  | -5%   | +10%  | —     |
| Sandborn             | +10%  | +15%   | —     | +15%  | -5%   | —     |
| Tidecaller           | +5%   | +10%   | +10%  | +5%   | +10%  | —     |
| Human                | +5%   | +5%    | +5%   | +5%   | +5%   | +5%   |

---

## Combat Turn Flow

```
1. RP NARRATION (optional, 60-120s timer)
   → Active player writes action narration

2. MECHANICAL ACTION (30s timer)
   → Move → Act (Ability/Attack/Item) → Face

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
| RaceData.cs        | Race definitions, passive modifiers |
| DailyTraining.cs      | Point allocation, soft cap, reset    |
| PlayerController.cs   | Overworld movement, input handling   |
| CameraController.cs   | Smooth follow cam, zoom, shake       |
| DebugOverlay.cs       | Testing HUD, stat display            |
