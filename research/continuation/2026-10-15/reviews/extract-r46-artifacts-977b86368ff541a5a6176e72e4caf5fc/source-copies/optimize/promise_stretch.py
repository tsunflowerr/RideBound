"""Promise-layer forecast core (2026-10-06): stretch a naive promise along a travel-time factor path.

A naive promise P0 published at t0 assumes the current bin's speed for the whole remaining time W = P0 - t0. If
the travel-time factor (a uniform citywide multiplier, as in the measured world) follows g(tau) relative to the
level at t0, a vehicle covers "naive seconds" at rate 1/g(tau), so the stretched arrival time t0 + D solves
    integral_{t0}^{t0+D} 1 / g(tau) dtau = W.
g is piecewise constant per 15-min bin of simulation time (0 = window start) and is held from bin HOLD_BIN on,
exactly as preflight_world.measured_base and the world wrappers do.
  * true path:  g(tau) = f(b(tau)) / f(b(t0)), f = the cell day's measured factor (an oracle; used to validate);
  * forecast:   g(tau) = 1 in the current bin, m(b(tau)) / m(b(t0)) after it, m = mean of the source days
                (anchored on the current measured level; only the growth shape comes from other days).
Times are integer milliseconds in, float milliseconds out.
"""
from __future__ import annotations

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "tang3" / "world"))
import preflight_world_v1_3 as v13  # noqa: E402  (absolute_profile, forecast_profile, HOLD_BIN)

BIN_MS = 900_000


def stretch(t0_ms: float, work_ms: float, weight) -> float:
    """Arrival time t0 + D with integral 1/weight(b) over [t0, t0+D] = work; weight(b) for bin b (> 0)."""
    if work_ms <= 0:
        return t0_ms + work_ms
    t, remaining = float(t0_ms), float(work_ms)
    for _ in range(10_000):
        b = int(t // BIN_MS)
        w = weight(b)
        segment = (b + 1) * BIN_MS - t                  # time left in this bin
        covered = segment / w                           # naive-ms covered in that time
        if covered >= remaining:
            return t + remaining * w
        remaining -= covered
        t = (b + 1) * BIN_MS
    raise RuntimeError("stretch did not converge")


def true_weight(day: str, window_start: int, t0_ms: float):
    f = v13.absolute_profile(day, window_start)
    b0 = int(t0_ms // BIN_MS)
    level = lambda b: f[min(b, v13.HOLD_BIN)]  # noqa: E731
    return lambda b: level(b) / level(b0)


def forecast_weight(profile: tuple[float, ...], t0_ms: float):
    b0 = int(t0_ms // BIN_MS)
    level = lambda b: profile[min(b, v13.HOLD_BIN)]  # noqa: E731
    return lambda b: 1.0 if b == b0 else level(b) / level(b0)
