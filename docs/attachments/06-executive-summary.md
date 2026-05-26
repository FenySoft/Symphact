# Attachment 6 — Executive Summary

> **Symphact — actor-based operating system co-designed with the CFPU.** NLnet NGI Zero Commons Fund, 13th open call, deadline 2026-06-01. Requested: **€30 000**, 12 months, Apache-2.0. Repo: <https://github.com/FenySoft/Symphact>.

**Problem.** The current computing stack (Linux + GPU + cache coherence) operates at the edge of physical limits. Dennard scaling stalled in the mid-2000s; coherent shared memory scales poorly past a few hundred cores ("dark silicon"); AI training energy keeps growing. The next 15–20 years likely require a share-nothing, message-passing paradigm with one homogeneous primitive on a dedicated-core-per-actor grid — the only route around the physical limits.

**Approach.** Symphact is the first openly documented prototype of this paradigm. A single primitive `TActor<TState>` serves four roles on a homogeneous core grid: classical OS process, hardware driver (MMIO), AI agent (LLM-driven), and LIF / Izhikevich "smart neuron". Co-designed with CFPU open silicon ([`FenySoft/CLI-CPU`](https://github.com/FenySoft/CLI-CPU), CERN-OHL-S-2.0); feedback runs through the public `osreq-to-cfpu` template. **At submission (2026-05-23): 186 green xUnit tests** cover M0.1–M0.4 + M0.5 BCL-only slices (actor core, supervision with let-it-crash + lifecycle hooks, per-actor parallel scheduler simulating CFPU dedicated-core-per-actor, BCL-only journal + snapshot store). ~65 hours self-funded TDD; out of grant scope.

**Grant deliverables (12 months, €30 000):**

| # | Milestone | Hours | € |
|---|---|---|---|
| **M1** | Persistence — content-addressed `TCasJournal` + `TCasSnapshotStore` (SHA-256, BCL-only), supervision lifecycle integration | ~80 | 2 900 |
| **M2** | Remoting + Capability Registry — `ITransport`/`TTcpTransport`, `TCapabilityRegistry` (CST) with delegation + revocation | ~160 | 5 800 |
| **M3** | Kernel + Device actors — `TRootSupervisor`, `TRouter`, device framework with `uart_device`, `gpio_device`, `timer_device` | ~180 | 6 500 |
| **M4** | CFPU integration demo — end-to-end on `FenySoft.CilCpu.Sim`, surfacing 3–7 concrete `osreq-to-cfpu` issues | ~80 | 2 900 |
| **M5** | Dev experience + docs + outreach — NuGet packages, bilingual docs, 3 blog posts, 2-3 demo videos, GitHub Pages, monthly research log | ~180 | 6 500 |
| **M6** | Formal-verification foundations — TLA+/Dafny spec of `send`/`receive` semantics, capability + supervision invariants | ~80 | 2 900 |
| | Engineering hours total + outreach/docs (travel-free, video production) | **~760** | **27 500 + 2 500 = 30&nbsp;000** |

Hot code loading and paid contributor onboarding **explicitly deferred** to a follow-up grant (the latter requires the physical-HW evidence the parallel CLI-CPU grant produces in F3/F4, 2027–2028).

**Why NGI.** *Apache-2.0, fully libre* — ~8M existing .NET developers can target it with familiar tools. *Capability-based security at framework level* — no global namespace, no ambient authority; seL4/CHERI model in .NET userland, HW-enforced on CFPU. *Open OS→HW co-design loop* — bidirectional, public, with documented precedents (Transputer + Occam 1984; Symbolics Lisp Machine 1985; TPU + TensorFlow 2017). *Multi-agent AI infrastructure* — current Python function-calling stacks (Anthropic Agent SDK, OpenAI Agents, LangGraph) are literally actor systems with poor implementations; Symphact offers structured, capability-restricted, auditable runtime relevant to EU AI Act high-risk. *European paradigm sovereignty* — free of US/Asian IP dependencies, NGI-mission aligned.

**Sustainability.** (a) Follow-up NLnet grant 12–14 months out — central deliverable shifts to cohort building once CLI-CPU F3-F4 delivers silicon + FPGA. (b) **FenySoft Kft. co-funding** — applicant's company has live revenue from regulated NGM/8/2025 cash-register software (QCassa/JokerQ, 55+ Akka.NET actors in production); concrete commitment **~5h/month** post-grant for ≥12 months — keeps CI green, NuGet current, research log running. (c) Possible-but-not-promised: .NET Foundation, GitHub Sponsors, commercial Akka.NET→Symphact migration consulting. Honest capacity disclosure: 12 months sit at the upper edge alongside the parallel CLI-CPU grant; deliverables conservatively scoped to actually ship.

**Applicant.** 35+ years professional software/hardware experience. National-scale production systems: "Atlasz" MÁV railway dispatch (26 years uninterrupted live operation since 2000). Sole developer of original AEE (Hungarian Tax Control Unit, regulation 48/2013 NGM); successor QCassa/JokerQ secured with LMS hash-based signatures (NIST SP 800-208), 55+ Akka.NET actors in supervision hierarchy. Parallel hardware project: CLI-CPU / CFPU (250+ simulator tests, 48 CIL-T0 opcodes, working Roslyn linker, preliminary Verilog RTL with 41/41 cocotb tests) — separate NLnet grant submitted 2026-04-14, under review.

**This is the first engineering stone of a paradigm-shift path, with documented evidence that the paradigm is physically realisable.**
