---
status: archived
---

# NLnet Grant Application — Post-Submission Corrections

> **Purpose:** This document records internal review corrections identified **AFTER** the Symphact NLnet NGI Zero Commons Fund application was submitted. The submitted file ([`nlnet-application-draft-en.md`](nlnet-application-draft-en.md) resp. `-hu.md`, and the [`attachments/`](attachments/) documents) is preserved **unchanged** as the authoritative record of what was received by NLnet.
>
> **How to use this:**
> - If NLnet reviewers ask for clarification, use the refined text below in your response.
> - The follow-up grant application will incorporate these corrections from the start.
> - The sister CLI-CPU grant application ([`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU)) has a similarly formatted corrections document: [`nlnet-corrections-en.md`](https://github.com/FenySoft/CLI-CPU/blob/main/docs/nlnet-corrections-en.md).

> Magyar verzió: [nlnet-corrections-hu.md](nlnet-corrections-hu.md)

> Version: 1.0 — 2026-07-18

---

## Correction 1 — AllForOne strategy overstated at submission time

**Submitted text (draft, lines 37 and 98; similarly `attachments/01-architecture-overview.md` line 34):**
> "The project has completed milestones M0.1–M0.4 and is halfway through M0.5: **186 green xUnit tests** cover the core primitives — … supervision (M0.3 — `ISupervisorStrategy`, **OneForOne / AllForOne**, lifecycle hooks, actor hierarchy) … The M0.3 supervision, M0.4 scheduler, and M0.5 persistence BCL-only reference slices shipped between v0.1 (April 2026) and the submission…"

**Problem:** The submitted text claims that M0.3 supervision — including `TAllForOneStrategy` — was fully complete **before** submission. Git history shows this is not accurate: the `TAllForOneStrategy` implementation actually shipped in commit **`d076f5a`, 2026-06-30** ("feat: TAllForOneStrategy implementation + 12 new tests (#25)") — **after** the submission deadline (2026-06-01). At submission time only `TOneForOneStrategy` was complete; both the submitted "186 tests" figure and the "OneForOne / AllForOne" description reflect the post-submission state, not the state at submission. The `03-status-snapshot.md` attachment's own source-file inventory implicitly supports this (it lists only the OneForOne file).

**Refined text:**
> At submission time (2026-06-01), `TOneForOneStrategy` (child-level restart) was complete, with 174 green xUnit tests. The sibling strategy, `TAllForOneStrategy` (group-level restart, +12 tests), was completed after submission, on **2026-06-30**, as self-funded work — also outside grant scope, but not yet delivered at submission time. Current state (2026-07-18): both strategies complete, 198 green xUnit tests total.

---

## Correction 2 — Test count is stale + the companion README's milestone table does not add up

**Submitted text (`attachments/03-status-snapshot.md`, "Test suite" section):**
> "## 2. Test suite — 186 green xUnit tests … | `Symphact.Core.Tests` | **120** | … | `Symphact.Platform.DotNet.Tests` | **22** | … | `Symphact.Persistence.Tests` | **44** | … | **Total** | **186** |"

**Problem:** The attachment's internal arithmetic (120+22+44=186) is correct and was accurate at submission time. Two nuances apply:
1. The figure is **now stale**: today's (2026-07-18) actual `dotnet test` result is **198** (132 Core + 22 Platform.DotNet + 44 Persistence) — the Core count grew 120→132 with AllForOne (see Correction 1).
2. The repository's public `README.md`/`README-hu.md` (not part of the submitted material, but reachable by anyone following the GitHub link from the submitted text) reported, at submission time, a **per-milestone** breakdown (30+16+30+86+44=206) that did **not** match the project-level 186 — a careful reviewer could have noticed this in the repo. This README has since (2026-07-18) been **corrected**: the headline figure now reads 198, with a note flagging that the per-milestone breakdown is under review.

**Refined text:**
> At submission time (2026-06-01), 174 green xUnit tests were complete (120 Core + 22 Platform.DotNet + 44 Persistence — the Core count in the attachment already included OneForOne but not AllForOne). Current state (2026-07-18): **198 green xUnit tests** (132 Core + 22 Platform.DotNet + 44 Persistence).

---

## Correction 3 — Hour double-bookkeeping between the roadmap estimate and the grant table

**Submitted text (`attachments/02-roadmap.md`, Grant-funded scope table):**
> "| **M1: Persistence** | M0.5 (content-addressed) | **~80h** | €2,900 | Months 1-3 |"

**At the same time, the project's own roadmap estimate for the same milestone (`docs/roadmap-en.md`, M0.5 section, as of submission time):**
> "**Est. hours:** ~16-24" (for the entire M0.5 milestone, BCL-only + content-addressed combined)

**Problem:** For the same deliverable (M0.5 content-addressed tier), the submitted attachment allocates **~80 hours** in the grant table, while the project's own roadmap estimate for the same period states **~16-24 hours** for the ENTIRE M0.5 milestone (including the BCL-only part, which had already shipped). This is roughly a 3.5-5× discrepancy, without a reconciling explanation in the submitted material. A smaller-magnitude version of the same pattern appears at other grant milestones (e.g., M2 Remoting+Capability Registry ~160h grant vs. roadmap Phase 2 total ~120-160h — though Phase 2 also covers four additional items beyond M2.5).

**Refined text:**
> The hour figures in the grant table cover **more than coding work alone** — they also include associated documentation, `osreq-to-cfpu` issue preparation, formal-specification writing, blog/video content, and grant administration — this breakdown will be itemized in the follow-up report (research log). The technical roadmap estimate (`docs/roadmap-en.md`) estimates runtime-coding work only, which is why it is systematically lower than the grant table's full-scope hours.

---

## Correction 4 — TLA+ vs. "TLA+ or Dafny" inconsistency within the submitted package

**Submitted text (`attachments/02-roadmap.md`, Phase 5 table, M5.4 row):**
> "| M5.4 Formal Verification Foundations | ~32–48 | **TLA+ / Dafny** spec of `send`/`receive` semantics, capability invariants |"

**Versus the submitted draft's M6 grant milestone (same submission package, line 121):**
> "Initial formal specification of `send` / `receive` semantics **in TLA+** (chosen **over Dafny**: protocol-level spec, documented industry precedent at AWS/Cosmos DB/Azure, closer to Symphact's messaging invariants than Dafny's code-level refinement proofs)."

**Problem:** Within the submitted package, two documents reflect two different states: the draft's M6 line records an **explicit, justified decision** (TLA+, not Dafny), while the `02-roadmap.md` attachment's Phase 5 table still leaves both open as options.

**Refined text:**
> The formal-verification tool choice is a **closed decision: TLA+** (not Dafny) — rationale: a protocol-level specification fits messaging invariants better than Dafny's code-level refinement-proof model, and there is documented industry precedent (AWS, Cosmos DB, Azure). The `02-roadmap.md` attachment's M5.4 row reflects the pre-decision, more open phrasing — the binding direction is the one recorded in the draft's M6 line.

---

## Correction 5 — F-phase labels swapped in the draft's M5 line

**Submitted text (draft, line 120, M5 milestone description):**
> "…before the physical silicon demo (**CLI-CPU F4 FPGA + F5 Tiny Tapeout**, 2027–2028) exists…"

**Versus the rest of the submitted package (draft lines 65 and 152, and the `attachments/04-cli-cpu-symphact-interaction.md` attachment's "Funded milestones" row), consistently:**
> "…the actor-native silicon output is produced by **CFPU phases F3-F4** (expected 2027–2028)…" / "…alongside CLI-CPU's **F3-F4 phases** (**Tiny Tapeout silicon + FPGA multi-core**, expected 2027–2028)…" / "**Funded milestones** … F2 RTL, **F3 Tiny Tapeout, F4 FPGA multi-core**"

**Problem:** In the draft's M5 line (line 120), the two phase labels are swapped — correctly F3=Tiny Tapeout, F4=FPGA multi-core, but the line states the reverse ("F4 FPGA + F5 Tiny Tapeout"). This is an isolated typo; the other five locations in the submitted package consistently use the correct ordering.

**Refined text:**
> "…before the physical silicon demo (**CLI-CPU F3 Tiny Tapeout + F4 FPGA multi-core**, 2027–2028) exists…"

---

## Correction 6 — Stale Sky130 PDK target in the attachment

**Submitted text (`attachments/04-cli-cpu-symphact-interaction.md`, comparison table):**
> "| **Target** | .NET 10 library (Windows / Linux / macOS) | Verilog synthesis, **Sky130 PDK** |"

**Problem:** The sister CLI-CPU project has pivoted since submission: the F3 first-silicon target is **IHP SG13G2** (2026-06-01 unified on-chip SRAM ADR), with the longer-term F6.7 targeting **22FDX**. Sky130 (SkyWater, US) as the sole stated target was only partially accurate even at submission time (see the CLI-CPU project's own `nlnet-corrections-en.md` corrections #2 and #13, which address the same question on the hardware side), and has become further stale since.

**Refined text:**
> "| **Target** | .NET 10 library (Windows / Linux / macOS) | Verilog synthesis, **IHP SG13G2** (F3), Sky130 (Tiny Tapeout entry point), **22FDX** (F6.7+) |"
>
> See corrections #2 and #13 in the sister CLI-CPU grant application's corrections document for the full rationale.

---

## Correction 7 — Draft status field is stale

**Submitted text (draft header):**
> "**Status:** DRAFT — preparing for the 2026-06-01 deadline"

**Problem:** Git history shows the submission actually occurred by the 2026-06-01 deadline (see the preceding "pre-submission NLnet refinements"-type commits and the 2026-06-13 public funding-surface cleanup, which presupposes a post-submission state). The "DRAFT — preparing for submission" header has been inaccurate since the submission.

**Refined text:**
> "**Status:** SUBMITTED 2026-06-01, round 13 — under review."

---

## What to do if NLnet reviewers ask for clarification

1. **Acknowledge that the submitted text reflects the 2026-06-01 state**, and offer this corrections document as clarification.
2. **Lead with the most substantive corrections** (#1 AllForOne overstatement, #3 hour double-bookkeeping) — these bear on deliverable credibility and budget justification.
3. **Reference the sister CLI-CPU application's similar corrections document** (relevant to #6 as well), so the two applications present a mutually reinforcing, transparent picture.
4. **Always cite the latest `dotnet test` result for the test count (#2)**, not the submission-time snapshot.

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-07-18 | Initial post-submission corrections document. 7 correction items catalogued from internal review, following the sister CLI-CPU project's precedent format. The submitted draft and attachments preserved unchanged. |
