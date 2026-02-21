"""
Daily Training Reset — 12:00 PM EST (17:00 UTC) Global Clock

Rules (per design doc + user clarification):
  - Reset at 12:00 PM EST (17:00 UTC) daily
  - Unspent TP CARRIES OVER between days
  - Daily RP earning counters (sessions, earned-today) reset to 0
  - TP earned via valid RP sessions, not given automatically
  - Server-authoritative — client cannot grant itself TP
"""

from datetime import datetime, timezone, timedelta

# 12:00 PM EST = 17:00 UTC
RESET_HOUR_UTC = 17

# TP award schedule per RP session
TP_SCHEDULE = [2, 2, 1]  # 1st→+2, 2nd→+2, 3rd→+1 = max 5 for Lv 1-9


def get_current_reset_period():
    """
    ISO date string for the current training period.
    The "day" flips at 17:00 UTC (12:00 PM EST).
    Before 17:00 UTC → yesterday's period. At/after → today's period.
    """
    now = datetime.now(timezone.utc)
    if now.hour < RESET_HOUR_UTC:
        period_date = now.date() - timedelta(days=1)
    else:
        period_date = now.date()
    return period_date.isoformat()


def seconds_until_next_reset():
    """Seconds until the next 17:00 UTC reset."""
    now = datetime.now(timezone.utc)
    today_reset = now.replace(hour=RESET_HOUR_UTC, minute=0, second=0, microsecond=0)
    if now >= today_reset:
        next_reset = today_reset + timedelta(days=1)
    else:
        next_reset = today_reset
    return int((next_reset - now).total_seconds())


def check_and_reset(character):
    """
    Check if this character's daily RP counters need resetting.
    TP bank CARRIES OVER — only daily earning counters reset.
    Returns True if a reset occurred.
    """
    current_period = get_current_reset_period()

    if character.last_reset_date == current_period:
        return False

    # Reset daily earning counters only — bank persists
    character.daily_tp_earned = 0
    character.daily_rp_sessions = 0
    character.last_reset_date = current_period
    return True


def award_rp_session_tp(character):
    """
    Award TP for completing a valid RP session.

    Schedule:
      1st valid session → +2 TP
      2nd valid session → +2 TP
      3rd valid session → +1 TP
      Total possible: 5 TP (Lv 1-9), 3 TP (Lv 10-19), 1 TP (Lv 20+)

    Returns: (tp_awarded, total_earned_today, at_cap)
    """
    check_and_reset(character)

    cap = character.daily_tp_cap
    already_earned = character.daily_tp_earned

    if already_earned >= cap:
        return (0, already_earned, True)

    session_idx = character.daily_rp_sessions
    if session_idx < len(TP_SCHEDULE):
        award = TP_SCHEDULE[session_idx]
    else:
        award = 0

    # Clamp to daily cap
    award = min(award, cap - already_earned)

    if award <= 0:
        return (0, already_earned, True)

    character.training_points_bank += award
    character.daily_tp_earned += award
    character.daily_rp_sessions += 1

    at_cap = character.daily_tp_earned >= cap
    return (award, character.daily_tp_earned, at_cap)


# ─── SOFT CAP (server-side validation) ─────────────────────

def calc_training_cost(stat_value, lowest_stat):
    """TP cost for +1 to a stat, accounting for soft cap."""
    gap = stat_value - lowest_stat
    if gap >= 20:
        return 4   # 25% efficiency
    elif gap >= 10:
        return 2   # 50% efficiency
    return 1       # 100% efficiency


def get_lowest_stat(character):
    """Return the lowest of the 6 training stats."""
    return min(
        character.strength, character.vitality, character.agility,
        character.dexterity, character.mind, character.ether_control
    )
