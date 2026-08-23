#!/usr/bin/env python3
from __future__ import annotations

import argparse
import html
import pathlib
from typing import Iterable

from reportlab.graphics.shapes import Drawing, Line, Rect, String
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    KeepTogether,
    LongTable,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


NAVY = colors.HexColor("#17324D")
BLUE = colors.HexColor("#246B9B")
PALE_BLUE = colors.HexColor("#EAF3F8")
RED = colors.HexColor("#A33A3A")
PALE_RED = colors.HexColor("#F7EAEA")
AMBER = colors.HexColor("#A66616")
PALE_AMBER = colors.HexColor("#FFF4DD")
GREEN = colors.HexColor("#2E6F55")
PALE_GREEN = colors.HexColor("#E8F4EE")
INK = colors.HexColor("#1E2933")
MUTED = colors.HexColor("#586873")
GRID = colors.HexColor("#B8C4CC")


def register_fonts() -> tuple[str, str]:
    regular = pathlib.Path(r"C:\Windows\Fonts\arial.ttf")
    bold = pathlib.Path(r"C:\Windows\Fonts\arialbd.ttf")
    if regular.exists() and bold.exists():
        pdfmetrics.registerFont(TTFont("RideBoundSans", str(regular)))
        pdfmetrics.registerFont(TTFont("RideBoundSans-Bold", str(bold)))
        return "RideBoundSans", "RideBoundSans-Bold"
    return "Helvetica", "Helvetica-Bold"


FONT, FONT_BOLD = register_fonts()
PAGE_WIDTH, PAGE_HEIGHT = A4


def escaped(text: object) -> str:
    return html.escape(str(text)).replace("\n", "<br/>")


def paragraph(text: object, style: ParagraphStyle) -> Paragraph:
    return Paragraph(escaped(text), style)


def rich(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text, style)


def styled_table(
    rows: Iterable[Iterable[object]],
    widths: list[float],
    styles: dict[str, ParagraphStyle],
    header: bool = True,
    font_size: float = 7.4,
) -> LongTable:
    converted = []
    for row_index, row in enumerate(rows):
        cell_style = styles["table_header"] if header and row_index == 0 else styles["table"]
        converted.append(
            [
                value
                if isinstance(value, (Paragraph, Drawing))
                else paragraph(value, cell_style)
                for value in row
            ]
        )
    table = LongTable(converted, colWidths=widths, repeatRows=1 if header else 0)
    commands = [
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.35, GRID),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("FONTNAME", (0, 0), (-1, -1), FONT),
        ("FONTSIZE", (0, 0), (-1, -1), font_size),
    ]
    if header:
        commands.extend(
            [
                ("BACKGROUND", (0, 0), (-1, 0), NAVY),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("FONTNAME", (0, 0), (-1, 0), FONT_BOLD),
            ]
        )
        for row_index in range(1, len(converted)):
            if row_index % 2 == 0:
                commands.append(("BACKGROUND", (0, row_index), (-1, row_index), colors.HexColor("#F5F7F8")))
    table.setStyle(TableStyle(commands))
    return table


def callout(
    title: str,
    body: str,
    styles: dict[str, ParagraphStyle],
    background: colors.Color,
    accent: colors.Color,
) -> Table:
    data = [[rich(f"<b>{html.escape(title)}</b><br/>{html.escape(body)}", styles["callout"])]]
    table = Table(data, colWidths=[PAGE_WIDTH - 34 * mm])
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), background),
                ("BOX", (0, 0), (-1, -1), 0.8, accent),
                ("LINEBEFORE", (0, 0), (0, -1), 4, accent),
                ("LEFTPADDING", (0, 0), (-1, -1), 10),
                ("RIGHTPADDING", (0, 0), (-1, -1), 10),
                ("TOPPADDING", (0, 0), (-1, -1), 8),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
            ]
        )
    )
    return table


def service_chart() -> Drawing:
    drawing = Drawing(480, 155)
    drawing.add(String(0, 137, "Service-rate delta versus preregistered −1.00 pp margin", fontName=FONT_BOLD, fontSize=10, fillColor=NAVY))
    left = 112
    width = 330
    scale = width / 8.0
    for index, (label, value, color) in enumerate(
        [("Panel A · 8 vehicles", 7.1296, RED), ("Panel B · 4 vehicles", 4.9074, AMBER)]
    ):
        y = 94 - index * 52
        drawing.add(String(0, y + 7, label, fontName=FONT, fontSize=8.5, fillColor=INK))
        drawing.add(Rect(left, y, width, 22, fillColor=colors.HexColor("#EEF1F3"), strokeColor=None))
        drawing.add(Rect(left, y, value * scale, 22, fillColor=color, strokeColor=None))
        drawing.add(Line(left + scale, y - 5, left + scale, y + 27, strokeColor=NAVY, strokeWidth=1.2))
        drawing.add(String(left + value * scale + 5, y + 6, f"−{value:.2f} pp", fontName=FONT_BOLD, fontSize=8.5, fillColor=INK))
    drawing.add(String(left + scale - 20, 2, "margin", fontName=FONT, fontSize=7, fillColor=NAVY))
    return drawing


def allocation_chart() -> Drawing:
    drawing = Drawing(480, 170)
    drawing.add(String(0, 151, "Cache-key allocation per 250,000 constructions", fontName=FONT_BOLD, fontSize=10, fillColor=NAVY))
    values = [("Baseline", 40.000072, MUTED), ("Optimized", 28.000072, BLUE)]
    x0 = 92
    bar_width = 100
    gap = 90
    # Leave a clear label band below the chart title. The allocation values are
    # deliberately not scaled to fill the entire drawing because their labels
    # otherwise collide with the title at A4 rendering size.
    scale = 2.05
    drawing.add(Line(55, 35, 420, 35, strokeColor=GRID, strokeWidth=0.7))
    for index, (label, value, color) in enumerate(values):
        x = x0 + index * (bar_width + gap)
        height = value * scale
        drawing.add(Rect(x, 35, bar_width, height, fillColor=color, strokeColor=None))
        drawing.add(
            String(
                x + bar_width / 2,
                18,
                label,
                fontName=FONT,
                fontSize=8.5,
                fillColor=INK,
                textAnchor="middle",
            )
        )
        drawing.add(String(x + 21, 42 + height, f"{value:.1f} MB", fontName=FONT_BOLD, fontSize=9, fillColor=INK))
    drawing.add(String(327, 128, "−30.0%", fontName=FONT_BOLD, fontSize=12, fillColor=GREEN))
    return drawing


def page_footer(canvas, document) -> None:
    canvas.saveState()
    canvas.setStrokeColor(colors.HexColor("#D5DDE2"))
    canvas.line(17 * mm, 14 * mm, PAGE_WIDTH - 17 * mm, 14 * mm)
    canvas.setFont(FONT, 7.3)
    canvas.setFillColor(MUTED)
    canvas.drawString(17 * mm, 9.5 * mm, "RideBound · Final WP1–WP10 review · 2026-08-23")
    canvas.drawRightString(PAGE_WIDTH - 17 * mm, 9.5 * mm, f"Page {document.page}")
    canvas.restoreState()


def build_styles() -> dict[str, ParagraphStyle]:
    samples = getSampleStyleSheet()
    return {
        "cover_title": ParagraphStyle(
            "CoverTitle",
            parent=samples["Title"],
            fontName=FONT_BOLD,
            fontSize=25,
            leading=30,
            textColor=NAVY,
            alignment=TA_LEFT,
            spaceAfter=14,
        ),
        "cover_subtitle": ParagraphStyle(
            "CoverSubtitle",
            parent=samples["Normal"],
            fontName=FONT,
            fontSize=13,
            leading=18,
            textColor=BLUE,
            spaceAfter=14,
        ),
        "h1": ParagraphStyle(
            "Heading1",
            parent=samples["Heading1"],
            fontName=FONT_BOLD,
            fontSize=17,
            leading=21,
            textColor=NAVY,
            spaceBefore=4,
            spaceAfter=9,
        ),
        "h2": ParagraphStyle(
            "Heading2",
            parent=samples["Heading2"],
            fontName=FONT_BOLD,
            fontSize=12,
            leading=15,
            textColor=BLUE,
            spaceBefore=8,
            spaceAfter=5,
        ),
        "body": ParagraphStyle(
            "Body",
            parent=samples["BodyText"],
            fontName=FONT,
            fontSize=9.2,
            leading=13.2,
            textColor=INK,
            spaceAfter=6,
        ),
        "small": ParagraphStyle(
            "Small",
            parent=samples["BodyText"],
            fontName=FONT,
            fontSize=7.5,
            leading=10.3,
            textColor=MUTED,
            spaceAfter=4,
        ),
        "table": ParagraphStyle(
            "Table",
            parent=samples["BodyText"],
            fontName=FONT,
            fontSize=7.4,
            leading=9.5,
            textColor=INK,
        ),
        "table_header": ParagraphStyle(
            "TableHeader",
            parent=samples["BodyText"],
            fontName=FONT_BOLD,
            fontSize=7.3,
            leading=9.2,
            textColor=colors.white,
        ),
        "callout": ParagraphStyle(
            "Callout",
            parent=samples["BodyText"],
            fontName=FONT,
            fontSize=9.2,
            leading=13,
            textColor=INK,
        ),
        "center": ParagraphStyle(
            "Center",
            parent=samples["BodyText"],
            fontName=FONT,
            fontSize=8.5,
            leading=11,
            alignment=TA_CENTER,
            textColor=MUTED,
        ),
    }


def build_story(styles: dict[str, ParagraphStyle]) -> list[object]:
    story: list[object] = []
    story.extend(
        [
            Spacer(1, 29 * mm),
            rich("RIDEBOUND", styles["cover_subtitle"]),
            rich("Final source, logic,<br/>benchmark and evidence review", styles["cover_title"]),
            rich("Work packages WP1–WP10 · post-confirmatory closure", styles["cover_subtitle"]),
            Spacer(1, 12 * mm),
            callout(
                "Outcome",
                "Repository assurance gates pass, but the scientific results stay negative: WP9 fails the service gate at both capacity strata, and WP10 fails closed on an explicit RidePy position capability.",
                styles,
                PALE_RED,
                RED,
            ),
            Spacer(1, 9 * mm),
            paragraph("Review date: 23 August 2026", styles["body"]),
            paragraph("Evidence authority: repository docs/18, docs/19, ADR-048/051/052 and immutable external receipts", styles["body"]),
            paragraph("Claim class: finite-panel empirical result + mechanical correctness/reproducibility. No population, SLA, satisfaction or novelty claim.", styles["small"]),
            PageBreak(),
        ]
    )

    story.extend(
        [
            rich("1. Executive verdict", styles["h1"]),
            rich(
                "No unresolved correctness or evidence-integrity blocker was found in the reviewed RideBound repository. This is not a formal proof: it is the result of a full-tree machine pass, manual review of outcome-bearing paths, differential/mutation tests, exact artifact binding and actual simulator execution.",
                styles["body"],
            ),
            callout(
                "WP9 · confirmatory result",
                "FAIL. Panel A (8 vehicles): 1735 → 1581 completed, −154 = −7.1296 pp. Panel B (4 vehicles): 966 → 860, −106 = −4.9074 pp. Both exceed the preregistered −1.00 pp margin.",
                styles,
                PALE_RED,
                RED,
            ),
            Spacer(1, 5 * mm),
            callout(
                "WP10 · cross-system result",
                "NEGATIVE CAPABILITY. Canonical RidePy execution passes. The representative subset fails closed because nodeOnly cannot represent concurrent mid-edge vehicle progress. Layer 3 is not established.",
                styles,
                PALE_AMBER,
                AMBER,
            ),
            Spacer(1, 5 * mm),
            callout(
                "Engineering closure",
                "855/855 .NET, 95/95 FleetPy and 23/23 RidePy tests pass; Release and format are clean; no known vulnerable NuGet package. ADR-052 removes exact redundant cache-key allocation without changing any search counter.",
                styles,
                PALE_GREEN,
                GREEN,
            ),
            rich("What this report does not claim", styles["h2"]),
            rich(
                "It does not convert near-zero revision burden into user benefit, infer a population effect from five travel days, rescue failed cells, claim production latency, or claim dynamic insertion / ETA stability / least commitment / satisfaction as novel.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    story.extend(
        [
            rich("2. Review scope and method", styles["h1"]),
            rich(
                "The pre-report inventory opened 1,226 reviewable files (302,565,761 bytes; 145,082 text lines). The benchmark tool and later patches were read directly. The final scanner covered 1,232 reviewable files, 1,221 text files and 146,534 text lines.",
                styles["body"],
            ),
            styled_table(
                [
                    ["Review layer", "What was checked", "Why it matters"],
                    ["Whole tree", "UTF-8, JSON, Python syntax, Markdown links/fences, imports, stubs, nondeterminism, float and dependency scans", "Finds defects beyond selected unit-test paths"],
                    ["Manual high-risk read", "Protocol/hash/retry; reducer/physical; ledger/budget/locks; candidate/solver; bundles/oracles; FleetPy/RidePy; WP9/WP10 analyzers", "Targets outcome-bearing and fail-open boundaries"],
                    ["Independent evidence", "Exact-small oracles, process oracle, mutation tests, frozen receipts, terminal inventories", "Avoids trusting a producer's self-reported success"],
                    ["Actual systems", "Pinned FleetPy 1.0.2, RidePy 2.10.1 container and read-only BeGo baseline", "Separates adapter mocks from native lifecycle behavior"],
                    ["Claim audit", "Finite-panel unit, service gate, locked/earned burden, prior-art boundary", "Prevents a mechanically correct artifact from supporting an invalid claim"],
                ],
                [32 * mm, 84 * mm, 61 * mm],
                styles,
            ),
            Spacer(1, 5 * mm),
            rich("Architecture result", styles["h2"]),
            rich(
                "Domain and Application contain no EF Core, ASP.NET, map-provider, OR-Tools, FleetPy, RidePy, OptiGo or Npgsql dependency. OR-Tools remains isolated in its solver adapter. Python adapters perform mapping and lifecycle orchestration and call the same versioned Runner; static scan found no alternate solver, budget or hard-lock implementation.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    wp_rows = [
        ["WP", "Boundary revalidated", "Current verdict"],
        ["1", "Strict protocol, canonical JSON/hash, retry/session", "PASS — no ambiguity or replay advance found"],
        ["2", "Immutable online state, route/frozen prefix, physical validator, B1", "PASS — conservation and no-reassignment enforced"],
        ["3", "Promises, three-way delta, 10D ledger/budget, locks, certificates", "PASS — monotone/no-refund; exact state binding"],
        ["4", "Bounded candidates, portfolio, CP-SAT/fallback/evidence", "PASS within declared bounds; no global-optimality overclaim"],
        ["5", "Independent BeGo durable adapter and recorded DB/process evidence", "PASS for mechanical integration; no effectiveness upgrade"],
        ["6", "Dataset/plan/process/metric/oracle/bundle verifier", "PASS — verifier recomputes outcome-bearing fields"],
        ["7", "FleetPy mapping/clock/plan/Runner client", "PASS — actual pinned Layer 2 mechanics"],
        ["8", "Experimental unit, pairing, endpoint, panel, prereg/freeze", "PASS for finite-panel design; solver seed not a replicate"],
        ["9", "H6 panels, burden decomposition, robustness, reproducibility", "COMPLETE — negative confirmatory result"],
        ["10", "RidePy source/image/native lifecycle/subset/failure retention", "COMPLETE — negative capability result"],
    ]
    story.extend(
        [
            rich("3. Work-package verdicts", styles["h1"]),
            styled_table(wp_rows, [13 * mm, 91 * mm, 73 * mm], styles, font_size=7.1),
            Spacer(1, 4 * mm),
            rich(
                "A completed work package means its declared gate was evaluated and its evidence retained. It does not mean the treatment won. WP9 and WP10 are correctly complete with negative results.",
                styles["small"],
            ),
            PageBreak(),
        ]
    )

    story.extend(
        [
            rich("4. WP9 confirmatory result", styles["h1"]),
            service_chart(),
            styled_table(
                [
                    ["Panel", "Arrivals / arm", "B1 completed", "C1 completed", "Delta", "Gate"],
                    ["A · 8 vehicles", "2,160", "1,735", "1,581", "−154 · −7.1296 pp", "FAIL"],
                    ["B · 4 vehicles", "2,160", "966", "860", "−106 · −4.9074 pp", "FAIL"],
                ],
                [37 * mm, 31 * mm, 31 * mm, 31 * mm, 31 * mm, 16 * mm],
                styles,
            ),
            rich("Why the burden gate does not rescue service", styles["h2"]),
            rich(
                "In Panel A, C1 burden is 0.17% of B1 and exactly zero in 12/20 cells. Pickup-ETA burden is definitional under the lock. Much of the remaining reduction occurs because C1 declines work rather than serving it with fewer revisions. Robustness attributes approximately half the service loss to lock/ranking and half to the 30-second budget; C2 recovers almost nothing.",
                styles["body"],
            ),
            rich(
                "All 20 Panel A cells are negative. Solver seed 19 minus seed 7 is exactly zero on completed service, burden and disruptive count in both arms; seeds do not create independent units. The conclusion is conditional on the fixed panels and five travel realizations, with achieved precision about 1.40 pp against the 1.00 pp margin.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    story.extend(
        [
            rich("5. WP10 RidePy result", styles["h1"]),
            callout(
                "Canonical gate · PASS",
                "Both B1 and C1 complete 5/5 requests, reconcile 5 native pickups + 5 native drops and emit 22 Runner decisions. The published Runner tree remains byte-identical.",
                styles,
                PALE_GREEN,
                GREEN,
            ),
            Spacer(1, 5 * mm),
            callout(
                "Representative subset · FAIL CLOSED",
                "22 arm jobs pass. B1 travel-update-stress-r3 fails at epoch 17; its paired C1 arm is not run. Native pickup occurs at 116,000 ms while the last nodeOnly Runner ETA remains 178,000 ms: a 62-second observability gap.",
                styles,
                PALE_AMBER,
                AMBER,
            ),
            rich("Valid-pair descriptive summary", styles["h2"]),
            styled_table(
                [
                    ["Stratum", "B1", "C1", "Delta service"],
                    ["Uncongested", "—", "—", "0.00 pp"],
                    ["Insertion", "—", "—", "−7.14 pp"],
                    ["Travel stress", "—", "—", "−16.67 pp"],
                    ["All 11 valid pairs", "54 / 62", "49 / 62", "−8.06 pp"],
                ],
                [48 * mm, 38 * mm, 38 * mm, 53 * mm],
                styles,
            ),
            rich(
                "These 11 pairs are not the planned complete subset estimand. No missing-job denominator is laundered, no confidence interval is reported, and the result is not pooled with H6. The named limitation is RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED; inferring progress from clock would invent simulator state.",
                styles["body"],
            ),
            rich("Runtime recoverability", styles["h2"]),
            rich(
                "The exact executed image is archived at 695,427,072 bytes with SHA-256 4783c541c256d1551677684eb5182cc43a8845d6bb3c5dc34778aadc9fc9a872 and reloads to image ID sha256:5468b9cba13c…e573. This proves exact restore, not future byte-identical rebuilding from evolving apt/pip indexes.",
                styles["small"],
            ),
            PageBreak(),
        ]
    )

    pdf_rows = [
        ["Full PDF", "Pages", "SHA-256", "Applied / rejected conclusion"],
        ["Alonso-Mora et al. 2017 · main", "6", "edbb62156e36479b…64c12ea", "Keep request–trip–vehicle separation and bounded feasible sets; do not import reassignment"],
        ["Alonso-Mora et al. 2017 · supplement", "32", "0d7e37aba541035b…760ddd5", "Reuse requires exact state identity"],
        ["Gschwind & Drexl 2019", "39", "16b82b489c6ae925…ad18e61", "Exact temporal preprocessing is relevant; full constant-time test not transplanted"],
        ["Simonetto et al. 2019", "30", "9f5e31a8d69a63b1…ae3868", "Sparse/batched LAP changes the pool; no pre-confirmatory import"],
        ["Engelhardt et al. 2020", "11", "5b3d20b26e701da7…fd01ed", "Direction/distance/random filters trade quality for time; rejected without loss bound"],
        ["Zalesak et al. 2025", "23", "744193567e9033de…8ba086", "Route stability and assignment mechanisms are prior art and distinct"],
        ["Schulz & Pfeiffer 2026", "46", "9a4d4997cdecc724…5c8d7", "Preprocess/reuse needs explicit invalidation; paper horizons are not defaults"],
    ]
    story.extend(
        [
            rich("6. Full-PDF research audit", styles["h1"]),
            rich(
                "The optimization was selected after extracting and reading every page of the retained PDFs, not from titles or abstracts. Hashes below bind the exact files. Full untruncated hashes and extraction hashes are recorded in docs/21.",
                styles["body"],
            ),
            styled_table(pdf_rows, [48 * mm, 13 * mm, 49 * mm, 67 * mm], styles, font_size=6.8),
            Spacer(1, 5 * mm),
            callout(
                "Research boundary",
                "Literature is evidence for a mechanism and its assumptions, not a source of RideBound defaults. No random/direction/sparse prune, reassignment, new batching interval, promise horizon, service margin or novelty claim was imported.",
                styles,
                PALE_BLUE,
                BLUE,
            ),
            PageBreak(),
        ]
    )

    story.extend(
        [
            rich("7. ADR-052 exact-reuse optimization", styles["h1"]),
            rich(
                "ForwardSlackCacheKey allocated a textual position fingerprint on every lookup even though the same key already held the immutable VehicleState reference. Terminal evaluation also constructed a second key only to verify the prefetched lookup created by that exact node. The patch removes the redundant string and compares the existing key against exact run, vehicle, route, time, travel and allowance inputs.",
                styles["body"],
            ),
            allocation_chart(),
            styled_table(
                [
                    ["Route stops", "Baseline µs / 250k", "Optimized", "Time", "Allocation"],
                    ["4", "48,170", "43,955", "−8.8%", "40,000,072 → 28,000,072 · −30%"],
                    ["8", "58,469", "54,894", "−6.1%", "40,000,072 → 28,000,072 · −30%"],
                    ["16", "80,861", "77,082", "−4.7%", "40,000,072 → 28,000,072 · −30%"],
                ],
                [27 * mm, 39 * mm, 31 * mm, 26 * mm, 54 * mm],
                styles,
            ),
            rich("End-to-end generator", styles["h2"]),
            rich(
                "Allocation falls consistently by 0.79–1.30%. Timing is mixed (−4.9%, +2.8%, −1.9%), so this report makes no generator speed claim. All six work-profile counters remain identical in every baseline and optimized process; the full physical validator still runs.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    verification_rows = [
        ["Gate", "Observed result"],
        ["RideBound required suite", "855 pass · 0 fail · 0 skip"],
        ["Release /warnaserror", "0 warnings · 0 errors"],
        ["Format + diff", "verify-only format PASS; git diff --check PASS"],
        ["NuGet vulnerability", "0 known vulnerable direct/transitive package in all solution projects"],
        ["FleetPy pinned suite", "95/95 · 0 skip"],
        ["RidePy pinned-container suite", "23/23"],
        ["BeGo read-only current baseline", "backend 149 pass + 5 explicit opt-in skip; frontend 9/9"],
        ["Final tree scan", "1,232 files; 1,221 text; 146,534 lines; 0 UTF-8/JSON/Python/Markdown error"],
        ["Architecture/static", "0 forbidden core dependency; 0 decision RNG/wall clock/float; 0 Python decision reimplementation"],
        ["WP10 analyzer", "exact terminal inventory + freeze/full Runner/seed; 7 mutation classes"],
        ["Docker archive/load", "SHA-256 exact; reload returns same image ID"],
    ]
    story.extend(
        [
            rich("8. Verification and review findings", styles["h1"]),
            styled_table(verification_rows, [63 * mm, 114 * mm], styles),
            rich("Defects found by review, not test-count worship", styles["h2"]),
            rich(
                "The strengthened WP10 analyzer now rejects an omitted valid pair, any extra/unplanned arm or failure artifact, seed drift and Runner-receipt drift. Format review corrected line-ending/import drift. The image archive closes exact restore risk. None of these changes modifies WP9 H6 or the WP10 terminal failure.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    risks = [
        ["Residual risk", "Current control / honest limit"],
        ["Finite-panel inference", "Report only the 20-cell/five-travel-realization result; no population CI or universal claim"],
        ["Bounded candidate generation", "Loss diagnostics are explicit, but no general solution-quality bound"],
        ["RidePy mid-edge observability", "Named fail-closed capability; do not infer hidden state"],
        ["Container rebuild", "Exact image archive restores; Dockerfile rebuild is not claimed byte-identical"],
        ["Dirty worktree ownership", "Preserved user artifacts/config/corpora/__pycache__; no cleanup/reset"],
        ["Review strength", "Whole-tree + manual + mutation + native simulators is strong assurance, not formal verification"],
    ]
    story.extend(
        [
            rich("9. Residual risks and conclusion", styles["h1"]),
            styled_table(risks, [57 * mm, 120 * mm], styles),
            Spacer(1, 6 * mm),
            callout(
                "Final conclusion",
                "WP1–WP10 are implementation/evidence complete at their declared gates. The repository baseline is green and the final review found no unresolved blocker. The treatment nevertheless fails the WP9 service criterion, and RidePy does not establish Layer 3. These negative results are the correct terminal findings—not defects to hide or tune away.",
                styles,
                PALE_BLUE,
                BLUE,
            ),
            rich("Next decision", styles["h2"]),
            rich(
                "Do not silently proceed to a new effectiveness experiment. WP11 Product UX or WP12 manuscript/release requires a new refinement decision that preserves H6, exposes limitations and treats intermediate commitment levels as exploratory rather than retroactive rescue.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )

    evidence_rows = [
        ["Evidence", "Identity / location"],
        ["WP9 H6 freeze", "84f6eff31addbdd12349a19201d79c872fbd05aaf5e0aa45dd73aee6d5c3dee2"],
        ["WP10 source/runtime receipt", "2b43106207b142e7ccde39482f73d551678881b869f0954cdc638ec9e7840775"],
        ["WP10 subset freeze v3", "18a74fa34f94a35ff92fbdc4ea2611e982527682760c52432419e0feef206672"],
        ["WP10 strengthened analysis v2", "be3e90771ae0216e891a8284b5457b1f635db214aa52681ce8d468b4007dcca3"],
        ["WP10 failure transcript", "0ee5e3ec…a85 · retained under E:\\RideBoundData\\wp10\\subset-results-v3"],
        ["Exact RidePy image archive", "4783c541c256d1551677684eb5182cc43a8845d6bb3c5dc34778aadc9fc9a872"],
        ["Optimization raw JSON", "six SHA-256 values in docs/benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md"],
        ["Repository review", "docs/reviews/wp1-wp10-final"],
        ["Live status / traceability", "docs/18-status-and-decision-log.md · docs/19-requirement-traceability.md"],
    ]
    story.extend(
        [
            rich("Appendix A. Evidence identities", styles["h1"]),
            styled_table(evidence_rows, [57 * mm, 120 * mm], styles, font_size=7.1),
            Spacer(1, 7 * mm),
            rich("Interpretation guard", styles["h2"]),
            rich(
                "Hashes establish exact artifact identity, not correctness by themselves. Correctness support comes from independent recomputation, mutation/differential tests, explicit failure retention, source review and actual simulator execution. All evidence should be read together with the claim boundary in docs/03.",
                styles["body"],
            ),
            PageBreak(),
        ]
    )
    full_pdf_hashes = [
        ["Retained PDF", "Pages", "Exact SHA-256"],
        ["Alonso-Mora et al. 2017 · main", "6", "edbb62156e36479b742a1a7381e59206\n73a4b6a3130bba39aed74cb8364c12ea"],
        ["Alonso-Mora et al. 2017 · supplement", "32", "0d7e37aba541035bdbc60da0eb35e81a\n63859eb96850a28d9a8fba116760ddd5"],
        ["Gschwind & Drexl 2019", "39", "16b82b489c6ae925581bebd00223d4aa8\nbce7541f1b561b2bd2320529ad18e61"],
        ["Simonetto et al. 2019", "30", "9f5e31a8d69a63b1286fbe55577d07a\n8449585356aa6802df9fb734d75ae3868"],
        ["Engelhardt et al. 2020", "11", "5b3d20b26e701da7837a149eb2953e1a\n828d0bf4fab780dbe5a50eb6defd01ed"],
        ["Zalesak et al. 2025", "23", "744193567e9033de631bc636045302399\n55d70575ddb8614b7d75fcf078ba086"],
        ["Schulz & Pfeiffer 2026", "46", "9a4d4997cdecc7242521ff733f8d474b\n6b57dc1856ce261734b7923b25a8c8d7"],
    ]
    story.extend(
        [
            Table(
                [[rich("Appendix B. Full-PDF provenance", styles["h1"])]],
                colWidths=[PAGE_WIDTH - 34 * mm],
                style=TableStyle(
                    [
                        ("LEFTPADDING", (0, 0), (-1, -1), 0),
                        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
                        ("TOPPADDING", (0, 0), (-1, -1), 0),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
                    ]
                ),
            ),
            rich(
                "Original files are retained outside the repository at E:\\RideBoundData\\research\\pdf-20260820. Every page was extracted and inspected; the hashes below are complete rather than display abbreviations.",
                styles["body"],
            ),
            styled_table(full_pdf_hashes, [69 * mm, 18 * mm, 90 * mm], styles, font_size=7.1),
            Spacer(1, 7 * mm),
            rich(
                "The paper evidence constrains what may be reused; it does not turn a cited mechanism into RideBound novelty or an empirical result on the RideBound panels.",
                styles["small"],
            ),
            Spacer(1, 12 * mm),
            rich("End of report", styles["center"]),
        ]
    )
    return story


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=pathlib.Path, required=True)
    arguments = parser.parse_args()
    output = arguments.output.resolve()
    if output.suffix.lower() != ".pdf":
        raise SystemExit("output must be a PDF")
    output.parent.mkdir(parents=True, exist_ok=True)
    styles = build_styles()
    document = SimpleDocTemplate(
        str(output),
        pagesize=A4,
        rightMargin=17 * mm,
        leftMargin=17 * mm,
        topMargin=16 * mm,
        bottomMargin=19 * mm,
        title="RideBound final WP1-WP10 review",
        author="RideBound project",
        subject="Source, logic, benchmark and evidence review",
    )
    document.build(
        build_story(styles),
        onFirstPage=page_footer,
        onLaterPages=page_footer,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
