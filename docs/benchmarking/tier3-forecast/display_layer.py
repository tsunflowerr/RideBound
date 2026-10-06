"""Promise-layer ("display") forecast, zero-run replay (FORECAST-PLAN.md, Family A). Library, no side effects.

Every pickup/drop promise the Runner published at time t with ETA p (ledger entry; simulation ms, 0 = window start)
is shown to the rider as
    D = max(p, S(t, p - t)),   S = promise_stretch.stretch with forecast_weight(m, t),
i.e. the naive remaining time p - t is stretched along the forecast growth of the travel-time factor after the
current 15-min bin (one-sided: a promise is never shortened). m is the mean absolute measured factor profile of the
source days (preflight_world_v1_3.forecast_profile; held from bin 8 as the world holds it).

Source-day rules (input-only, fixed before any test outcome):
  causal-no15 (PRIMARY): prior same-kind days only, 15/11 (documented snowstorm) never a source, active only with
    >= 2 source days: 14/11 <- 12,13; 15/11 <- 12,13,14; 16/11 <- 12,13,14; 12/11, 13/11, 17/11, 18/11 inactive.
  loo-no15 (SENSITIVITY): all other same-kind days except 15/11 and the scenario day (weekday 12,13,14,16; weekend
    17 <- 18, 18 <- 17).
  causal-with15, causal-no12 (SENSITIVITIES): as causal-no15 but including 15/11, resp. excluding 12/11.
An inactive cell displays the naive promise (D = p).
"""
from __future__ import annotations

import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import promise_stretch as ps  # noqa: E402

WEEKDAYS = ("2018-11-12", "2018-11-13", "2018-11-14", "2018-11-15", "2018-11-16")
WEEKEND = ("2018-11-17", "2018-11-18")
INCIDENT = "2018-11-15"
WINDOW_STARTS = {"w07": 25200, "w08": 28800, "w14": 50400, "w15": 54000, "w17": 61200}
MIN_SOURCES = 2


def sources(day: str, rule: str) -> tuple[str, ...]:
    """Source days for a scenario day under a rule; () means inactive. The scenario day is never a source."""
    kind = WEEKDAYS if day in WEEKDAYS else WEEKEND if day in WEEKEND else None
    if kind is None:
        raise ValueError(f"unknown day {day}")
    if rule == "causal-no15":
        days = [d for d in kind if d < day and d != INCIDENT]
    elif rule == "causal-with15":
        days = [d for d in kind if d < day]
    elif rule == "causal-no12":
        days = [d for d in kind if d < day and d not in (INCIDENT, "2018-11-12")]
    elif rule == "loo-no15":
        days = [d for d in kind if d != day and d != INCIDENT]
    else:
        raise ValueError(f"unknown rule {rule}")
    assert day not in days
    if rule.startswith("causal") and len(days) < MIN_SOURCES:
        return ()
    return tuple(days) if days else ()


def profile(day: str, window: str, rule: str):
    days = sources(day, rule)
    return ps.v13.forecast_profile(list(days), WINDOW_STARTS[window]) if days else None


def display(t_ms: float, p_ms: float, prof) -> float:
    """Displayed time of a promise p published at t: max(p, stretched p); naive when prof is None."""
    if prof is None or p_ms <= t_ms:
        return float(p_ms)
    return max(float(p_ms), ps.stretch(t_ms, p_ms - t_ms, ps.forecast_weight(prof, t_ms)))
