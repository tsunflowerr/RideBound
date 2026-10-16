"""T3.2 reference accounting: an independent oracle for the v/e/p decomposition (thesis ch. 3).

Input is one audit row per (request, coordinate, publication attempt): the promise in force before
the decision `v`, the kept-route projection on the reduced state `e`, and the published value `p`,
all in canonical integer milliseconds. Only COMMITTED rows count (a rejected proposal or an
unacknowledged retry never enters a rider's history, M07/M08). A coordinate stops accumulating once
closed (pickup at actual boarding, M11). The oracle never calls production code and never makes a
dispatch decision; it only recomputes quantities so they can be compared with a production ledger.

Definitions (per publication t, per rider and coordinate):
  x_t = e_t - v_t,  c_t = p_t - e_t,  z_t = p_t - v_t = x_t + c_t      (exact, signed)
  C = sum |c_t|,  X = sum |x_t|,  V = sum |z_t|,  E = |p_T - p_0|,  H = V - E
  M+ = max_t max(0, p_t - p_0),  M = max_t |p_t - p_0|
with p_0 the first committed publication (its own x, c, z are 0 by definition: v = e = p).
"""
from __future__ import annotations

from dataclasses import dataclass, field

MAX_CANONICAL = 2 ** 53 - 1


class AccountingError(ValueError):
    """A typed failure: bad input is rejected, never silently coerced (M14)."""


@dataclass(frozen=True)
class AuditRow:
    request_id: str
    coordinate: str          # "pickup" | "drop"
    sequence: int            # strictly increasing per (request, coordinate) among committed rows
    v: int
    e: int
    p: int
    committed: bool = True   # False: rejected proposal or unacknowledged attempt
    closed: bool = False     # True: the coordinate is closed at or before this row (e.g. boarded)
    initial: bool = False    # True only for the first committed publication (p_0)


@dataclass
class Totals:
    count: int = 0
    C: int = 0
    X: int = 0
    V: int = 0
    E: int = 0
    H: int = 0
    Mplus: int = 0
    M: int = 0
    p0: int | None = None
    last_p: int | None = None      # last published value while the coordinate was open (for E)
    chain_p: int | None = None     # last published value of any committed row (for continuity)
    steps: list = field(default_factory=list)   # (sequence, x, c, z)


def _check(value: int, name: str, row: AuditRow) -> None:
    if not isinstance(value, int) or isinstance(value, bool):
        raise AccountingError(f"{name} of {row.request_id}/{row.coordinate}#{row.sequence} is not an integer")
    if value < 0 or value > MAX_CANONICAL:
        raise AccountingError(f"{name} of {row.request_id}/{row.coordinate}#{row.sequence} outside canonical range")


def accumulate(rows: list[AuditRow]) -> dict[tuple[str, str], Totals]:
    """Totals per (request, coordinate) from committed, open rows in sequence order."""
    out: dict[tuple[str, str], Totals] = {}
    seen: dict[tuple[str, str], int] = {}
    for row in sorted(rows, key=lambda r: (r.request_id, r.coordinate, r.sequence)):
        for name in ("v", "e", "p"):
            _check(getattr(row, name), name, row)
        if not row.committed:
            continue
        key = (row.request_id, row.coordinate)
        if key in seen and row.sequence <= seen[key]:
            raise AccountingError(f"duplicate or out-of-order committed row {key}#{row.sequence}")
        seen[key] = row.sequence
        totals = out.setdefault(key, Totals())
        if totals.p0 is None:
            if not row.initial or not (row.v == row.e == row.p):
                raise AccountingError(f"first committed row of {key} must be the initial promise (v = e = p)")
            totals.p0 = totals.last_p = totals.chain_p = row.p
            totals.count = 1
            continue
        if row.initial:
            raise AccountingError(f"second initial promise for {key}#{row.sequence}")
        if row.v != totals.chain_p:
            # A continuous chain publishes against the promise in force (v_t = p_{t-1}), also
            # after the coordinate closed: the closing publication itself is still in force.
            raise AccountingError(f"{key}#{row.sequence}: v={row.v} is not the previous published p={totals.chain_p}")
        totals.chain_p = row.p
        if row.closed:
            continue
        x, c = row.e - row.v, row.p - row.e
        z = x + c
        assert z == row.p - row.v
        totals.C += abs(c)
        totals.X += abs(x)
        totals.V += abs(z)
        totals.count += 1
        totals.last_p = row.p
        totals.Mplus = max(totals.Mplus, max(0, row.p - totals.p0))
        totals.M = max(totals.M, abs(row.p - totals.p0))
        totals.steps.append((row.sequence, x, c, z))
    for totals in out.values():
        totals.E = abs(totals.last_p - totals.p0)
        totals.H = totals.V - totals.E
        # Invariants of the decomposition (thesis md:chuoi): must hold for every rider.
        if not (totals.V <= totals.C + totals.X and totals.E <= totals.M <= totals.V
                and totals.Mplus <= totals.M):
            raise AccountingError(f"invariant broken: {totals}")
    return out


def compare(production: dict[tuple[str, str], dict[str, int]],
            oracle: dict[tuple[str, str], Totals],
            fields: tuple[str, ...] = ("C", "V")) -> list[str]:
    """Diagnostics for every (request, coordinate, field) where a production ledger disagrees with
    the oracle, plus riders present on one side only. Empty list = agreement."""
    problems = []
    for key in sorted(set(production) | set(oracle)):
        if key not in oracle:
            problems.append(f"{key}: in production ledger but has no committed history")
            continue
        if key not in production:
            problems.append(f"{key}: committed history missing from production ledger")
            continue
        for name in fields:
            expected, actual = getattr(oracle[key], name), production[key].get(name)
            if actual != expected:
                problems.append(f"{key}: {name} production={actual} oracle={expected}")
    return problems
