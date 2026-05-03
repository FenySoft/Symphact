# NLnet NGI Zero Commons Fund — Application Draft (Symphact)

> **Deadline:** 2026-06-01, 12:00 CEST
> **Form:** https://nlnet.nl/propose/
> **Call:** NGI Zero Commons Fund — 13th open call (same call as the parallel CLI-CPU submission filed 2026-04-14)
> **Status:** DRAFT — preparing submission for 2026-06-01

> Magyar verzió: [nlnet-application-draft-hu.md](nlnet-application-draft-hu.md)

> Version: 1.1

---

## Thematic Call

NGI Zero Commons Fund

## Proposal Name

**Symphact: Capability-Based Actor Runtime for Secure .NET Computing**

## Website / Wiki

https://github.com/FenySoft/Symphact

## Abstract

Symphact is a **capability-based actor runtime for .NET**, built on the principle that *every stateful entity is an actor and communication happens exclusively through immutable messages*. The project has completed milestones M0.1 through M0.4: **142 passing xUnit tests** covering the core primitives — `TMailbox` (FIFO mailbox), `TActorRef` (capability token), `TActor<TState>` (abstract actor), `TActorContext` (handler context), `TActorSystem` (runtime), supervision (M0.3 — `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hooks, actor hierarchy), and the scheduler API with per-actor parallelism (M0.4 — `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, CFPU dedicated-core-per-actor simulation). All developed with strict TDD methodology on .NET 10, Apache-2.0 licensed. **M0.3 supervision and M0.4 scheduler shipped between v0.1 (April 2026) and submission and are explicitly out of scope of this grant.** **This grant funds the transition from a working actor core to a usable, secure actor operating system**: persistence, location transparency, capability-based security with delegation/revocation, and the first device actors.

**Why an actor OS, and why now?**

Current operating systems (Linux, Windows, macOS) carry architectural decisions made in the 1970s — shared memory, monolithic kernels, POSIX permissions, fork/exec. These designs are increasingly unfit for modern reality: 1000+ cores per chip, AI-driven threats, supply chain attacks (log4j, xz-utils), and the need for fault tolerance in safety-critical systems. Joe Armstrong (creator of Erlang) described this in his 2014 talk "The Mess We're In" — actor-oriented hardware was needed but did not exist. **That has now changed.** Open silicon (Tiny Tapeout, eFabless, IHP MPW), mature actor frameworks (Akka.NET, Orleans, Akka JVM), and the end of Dennard scaling make a clean-slate actor OS viable in 2026.

Symphact combines four proven ideas into one runtime, on a modern foundation:

- **Actor model** (Erlang/OTP, 40+ years in telecom/finance) — let-it-crash + supervision
- **Capability-based security** (seL4, CHERI) — unforgeable references, no global namespace
- **Type-safe runtime** (Singularity research prototype, 2003) — memory safety at the ISA level
- **Deterministic message passing** (QNX commercial demonstration) — formal verifiability

**The grant targets three concrete outcomes (built on top of the already-delivered M0.1–M0.4 foundation):**

1. **Persistence + Location transparency (M1–M2):** Event-sourcing journal (`IPersistenceProvider`, `TInMemoryJournal`, `TSqliteJournal`), snapshot + replay integrated with the M0.3 supervision lifecycle so actor state survives restart. TCP-based remoting transport (`ITransport` / `TTcpTransport`) with location-transparent `Send` routing. Deliverable: ~80+ new xUnit tests, persistence + remoting working end-to-end.

2. **Capability registry + Kernel actors (M2–M3):** `TCapabilityRegistry` with CST-based capability tokens (HW-managed on CFPU, software-managed on .NET hosts), `TRouter` actor for location-transparent address resolution, `TRootSupervisor` boot sequence. Deliverable: capability-based message routing with revocation + delegation, ~70+ new tests.

3. **First device actors + CFPU reference demo (M3–M4):** `uart_device`, `gpio_device`, `timer_device` actors running on .NET host against a simulated MMIO layer. First end-to-end demo: a distributed counter across 4 simulated cores using the CLI-CPU reference simulator (via the upcoming `FenySoft.CilCpu.Sim` NuGet package). Deliverable: reproducible reference app with docs, ~60+ new tests, 3–5 concrete HW requirements filed as `osreq-to-cfpu` issues. (Hot code loading is **explicitly deferred to a follow-up grant** — its complexity exceeds what fits in 12 months alongside the other deliverables.)

**Why this matters for the NGI ecosystem:**

- **Apache-2.0, fully libre:** Permissive license compatible with the broader .NET ecosystem — enterprise adoption path is clear.
- **A large existing .NET developer base can target this runtime using familiar tools:** C#, F#, VB.NET all compile to CIL. Microsoft's annual developer surveys and Akka.NET / Orleans production deployments demonstrate the runtime's entrenched position in enterprise software — particularly in regulated industries (financial services, government, healthcare) where the Symphact security model is most valuable. The runtime runs on any .NET host today (Windows, Linux, macOS) — silicon independence means no hardware blocking.
- **Capability-based security at the framework level:** Unlike POSIX permissions, Symphact has no global namespace. An actor can only send a message to another if it holds a capability — there is no ambient authority. This is the seL4/CHERI security model in userland .NET. Software-only security measures are increasingly insufficient against supply chain attacks (log4j, xz-utils), AI-generated exploits, and state-level threats — capability security eliminates entire vulnerability classes by construction.
- **Co-designed with open silicon:** While Symphact runs on any CIL host today, it is co-designed with the Cognitive Fabric Processing Unit (CFPU) project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). OS requirements feed back into hardware design via the `osreq-to-cfpu` issue template — a bidirectional loop similar to Apple's M-series OS/hardware integration, but fully open.
- **Formally verifiable:** The actor model's mathematical foundations (CSP, pi-calculus, Erlang's proven runtime) make Symphact a natural target for formal verification — unlike the ~40M lines of Linux kernel C code.
- **European sovereignty:** An open, auditable .NET runtime free of US/Asian IP dependencies — Apache-2.0 license permits any European entity to fork, modify, and certify.

**Why now?** Three converging forces make this moment critical:

1. **Hardware trend:** 100+ core chips are mainstream (AWS Graviton4, Ampere AmpereOne, Apple M-series), and shared-memory scaling is at a wall. Actor runtimes scale linearly with core count.
2. **Open silicon maturity:** Tiny Tapeout, eFabless, IHP MPW make custom actor-native silicon viable for the first time — the parallel CLI-CPU project demonstrates this with a fully tested CIL-T0 reference simulator and preliminary RTL. Symphact provides the software target.
3. **Regulatory environment:** EU CRA (Cyber Resilience Act, 2024), IEC 62443, ISO 21434 are pushing toward provable safety/security properties. Actor + capability model is natively certifiable; Linux (~40M lines of C) is not.

## Have you been involved with projects or organisations relevant to this topic before?

The applicant has 35+ years of professional software and hardware experience:

- **National-scale production systems (1990s–2026):** As part of a 3-person team, developed "Atlasz" — a railway dispatch control system for MÁV (Hungarian National Railways), used in national traffic management until 2026. Also Visual Restaurant / Visual Hotel & Restaurant suites (Delphi, later .NET), widely deployed in the Hungarian hospitality industry.
- **.NET ecosystem (20+ years):** Professional C#/.NET development including mandatory government data reporting integrations (NAV tax authority, NTAK tourism), Akka.NET actor systems (QCassa/JokerQ: 55+ actors in production), Avalonia UI cross-platform applications, and Android/iOS deployment.
- **Hungarian Tax Control Unit (Adóügyi Ellenőrző Egység):** Sole developer of the software for the original AEE project (regulation 48/2013 NGM). Current successor **QCassa/JokerQ** is a modern replacement secured with **LMS hash-based signatures (NIST SP 800-208 stateful hash-based signature scheme — quantum-resistant via SHA-256 collision resistance)**, with 55+ Akka.NET actors in supervision hierarchy, developed solo under regulation 8/2025 NGM. This project provides deep, hands-on experience with production actor-model architecture and hash-based signature integration that directly informs Symphact's AuthCode design (signer + bytecode blacklist using the same LMS family).
- **Hardware co-design experience:** Parallel development of the **CLI-CPU / CFPU** open silicon project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — 250+ xUnit tests on the reference simulator, 48 CIL-T0 opcodes, working Roslyn-based linker, preliminary Verilog RTL (ALU module: 41/41 cocotb tests). This hardware project is pursuing a separate NLnet grant (submitted 2026-06-01) and provides the bidirectional OS↔HW design loop.

The Symphact project itself began in April 2026. As of submission (May 2026), **142 passing xUnit tests** cover M0.1–M0.4: actor core, inter-actor messaging, supervision (let-it-crash with OneForOne / AllForOne strategies, lifecycle hooks, hierarchy), and the scheduler API with per-actor parallelism (`TInlineScheduler` + `TDedicatedThreadScheduler` simulating CFPU dedicated-core-per-actor). Approximately ~50 hours of focused TDD work delivered this baseline — **this velocity is documented in the roadmap** ([`docs/roadmap-en.md`](roadmap-en.md)) and forms the basis for the milestone estimates in this proposal.

## Requested Amount

**€30,000**

## Explain what the requested budget will be used for

> **Note on scope:** M0.3 supervision and M0.4 scheduler are already delivered (May 2026, ~50h self-funded) and are **not** funded by this grant. The milestones below cover the work remaining to reach a usable, secure, persistent actor runtime with kernel actors, device actors, and a CFPU integration demo.

| Milestone | Description | Hours | Budget | Timeline |
|-----------|-------------|-------|--------|----------|
| **M1: Persistence** (roadmap M0.5) | Event-sourcing journal (`IPersistenceProvider`, `TInMemoryJournal`, `TSqliteJournal`), snapshot + replay integrated with M0.3 supervision lifecycle. ~30+ new xUnit tests. | ~80h | €2,900 | Month 1-3 |
| **M2: Remoting + Capability Registry** (roadmap M0.6 + M2.5) | TCP transport (`ITransport` / `TTcpTransport`), serialization, location-transparent `Send` routing, CST-based capability registry with delegation + revocation. ~80+ new tests. | ~160h | €5,800 | Month 2-7 |
| **M3: Kernel + Device actors** (roadmap M2.1, M2.3, M3.1-M3.4) | `TRootSupervisor` boot sequence, `TRouter` actor, device actor framework + `uart_device`, `gpio_device`, `timer_device` on simulated MMIO. (Hot code loading explicitly deferred to follow-up grant.) ~80+ new tests. | ~180h | €6,500 | Month 5-10 |
| **M4: CFPU integration demo** (roadmap M0.7 partial) | End-to-end demo: distributed counter actor across simulated cores via `FenySoft.CilCpu.Sim` NuGet (CLI-CPU C# reference simulator). Discover 3-5 concrete HW requirements, file as `osreq-to-cfpu` issues. | ~80h | €2,900 | Month 8-11 |
| **M5: Developer experience + docs + outreach** | NuGet package publication (`Symphact.Core`, `Symphact.Persistence`, `Symphact.Remoting`, `Symphact.Security`, `Symphact.Devices`), `symphact` CLI tool, English architecture docs, contribution guide, 3 blog posts, lightning talk at a .NET conference. **Contributor growth commitment:** target 3+ external contributors by Month 12, tracked in `CONTRIBUTORS.md`. | ~180h | €6,500 | Ongoing |
| **M6: Security audit + formal groundwork** (roadmap M5.4 partial) | External security review of the capability mechanism (independent reviewer, €2,000 subcontract). Initial formal specification of `send` / `receive` semantics in TLA+ or Dafny. | ~80h | €5,400 | Month 9-12 |
| **Total** | | **~760h** | **€30,000** | **12 months** |

**Cost structure:**
- Personnel: ~760 hours part-time × €36/hour ≈ €27,500 (consistent with the parallel CLI-CPU proposal rate; ~63h/month part-time, in line with the M0.1–M0.4 actual velocity)
- Security audit subcontract: €2,000 (external reviewer for capability mechanism)
- Conference travel (1 lightning talk, EU-based): €500
- **No hardware costs** — Symphact runs entirely on software hosts (Windows/Linux/macOS/CI)

## Describe existing funding sources

The project is currently self-funded by the applicant. No external funding has been received. There are no pending applications to other funding bodies **for this work (Symphact runtime)**.

**Related but scope-separated:** A parallel NLnet NGI Zero Commons Fund application was submitted on 2026-04-14 for the **CLI-CPU / CFPU hardware project** ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — **same 13th open call** as this Symphact submission. Full transparency on parallel funding is intentional. The two projects are **deliberately non-overlapping in scope**:

| Dimension | CLI-CPU / CFPU | Symphact |
|-----------|---------------|-----------|
| **Deliverable** | Hardware ISA, RTL, silicon tape-out, FPGA | Software runtime, OS services |
| **Target** | Verilog synthesis, Sky130 PDK | .NET 10 library (runs on Windows/Linux/macOS) |
| **License** | CERN-OHL-S-2.0 (reciprocal hardware) | Apache-2.0 (permissive software) |
| **Repository** | `FenySoft/CLI-CPU` | `FenySoft/Symphact` |
| **Milestones funded** | F2 RTL, F3 Tiny Tapeout, F4 FPGA multi-core | M0.3-M3.2 actor runtime + kernel actors |
| **Dependencies** | None on Symphact | None on CLI-CPU silicon (simulator-ready) |

**Sustainability plan:**

1. **.NET Foundation submission (Month 9):** Apply for .NET Foundation project membership — provides code signing, CLA management, legal/IP support, Azure infrastructure hosting (no direct funding, but community legitimacy).
2. **Follow-up NLnet proposal (Month 12-14):** M0.5-M1.0 + distributed actor model (Phase 3-4 of the roadmap).
3. **GitHub Sponsors / Open Collective (Month 6+):** Set up community funding channels for ongoing maintenance. Target: €500-1500/month steady state by end of year 2.
4. **Commercial consulting (Year 2+):** FenySoft Kft. offers integration consulting services for regulated industries (healthcare, finance, critical infrastructure) that need actor-model architectures. This provides cross-subsidy for core runtime maintenance without compromising the open-source core.
5. **Dual licensing for specialized components (optional, Year 3+):** Core runtime stays Apache-2.0; enterprise support, formally verified components, and industry-specific extensions may carry commercial licenses — only if demand materializes.

The project has a **path to self-sustainability by Year 3** without reliance on continuous grant funding.

## Comparison with existing efforts

| Project | Approach | Limitation | Symphact difference |
|---------|----------|------------|---------------------|
| **Akka.NET** | Actor framework on .NET | Runs on Linux/Windows with global GC, POSIX permissions, no capability security | Capability-based security, per-core GC model, co-designed with actor-native HW |
| **Microsoft Orleans** | Virtual actor framework | Cloud-scale, stateless actor focus, not OS-level | OS-level actors including device drivers and kernel services |
| **Erlang/OTP** | Actor VM with supervision | Dynamically typed, not .NET ecosystem, own language | .NET ecosystem (C#/F#/VB.NET), statically typed, CIL compilation target |
| **seL4** | Formally verified microkernel | C only, no .NET support, small developer community | .NET runtime, capability security as OS primitive, developer ergonomics |
| **CHERI / Morello** | Capability architecture extension | ARM/RISC-V specific, still academic | Software-first, capability in runtime — works on any CIL host today |
| **Singularity (Microsoft Research, 2003)** | Type-safe OS in C# | Research only, abandoned, no community | Production-grade actor runtime, co-designed with open HW |
| **Redox OS** | Microkernel in Rust | Rust only, no .NET, not actor-based | Actor-based, .NET ecosystem |
| **Tock** | Embedded OS in Rust | Embedded focus, not actor-based | Actor model, general-purpose |

**No existing project combines:** capability-based security + actor runtime + .NET ecosystem + open-source + co-designed with open silicon. Symphact is a new position in this space.

## What are significant technical challenges you expect to solve during the project?

1. **Capability token forgery resistance:** The `TActorRef` struct is an opaque CST (Capability Slot Table) index. On CFPU hardware, capabilities are HW-managed in QRAM (Quench-RAM); on .NET hosts, the challenge is generating, verifying, and revoking capability tokens without a trusted execution environment. Mitigation: CST-based capability registry actor with runtime-managed slot allocation; formal specification of the threat model.

2. **Supervisor restart semantics on shared-memory hosts:** Erlang's "restart" assumes actor state is isolated (separate heap). On a .NET shared-GC host, restarting an actor requires careful state teardown to prevent cross-actor leaks. Challenge: clean restart boundaries without compromising performance. Mitigation: per-actor arena allocators (ArrayPool-backed) + explicit state serialization in M0.5.

3. **Deterministic scheduler for formal verification:** TLA+ specifications for the actor model require deterministic scheduling to be useful. Challenge: reconciling determinism with multi-thread execution. Mitigation: `TDedicatedThreadScheduler` provides per-actor thread isolation; `TRoundRobinScheduler` provides deterministic single-threaded execution for verification runs.

4. **Persistence + supervisor restart interaction:** Event sourcing must integrate cleanly with `PreRestart` / `PostRestart` lifecycle hooks — replay must complete before the actor accepts new messages, otherwise restart races with live traffic. Challenge: defining a clean "recovering" sub-state without breaking the FIFO invariant. Mitigation: stash messages during replay; explicit `RecoveryCompleted` signal in M0.5 spec.

5. **OS-to-HW requirement discovery discipline:** The `osreq-to-cfpu` loop must produce concrete, actionable HW requirements (not wish-list items). Challenge: separating "nice-to-have" from "required for correctness". Mitigation: every `osreq` issue must include a benchmark measurement from the simulator showing the need — no requirement without data.

## Describe the ecosystem of the project

**Upstream dependencies (all open source):**
- **.NET 10 SDK** (Microsoft, MIT license) — runtime, compiler, test framework
- **xUnit 2.9.3** — testing framework
- **CLI-CPU reference simulator** (CERN-OHL-S-2.0) — via upcoming `FenySoft.CilCpu.Sim` NuGet for CFPU integration demos
- **Visual Studio Code / Code - OSS** (MIT) — primary development environment

**Downstream users and stakeholders:**
- **.NET developer community (~8M+ developers):** Any C#/F# codebase can adopt Symphact as an actor runtime. Akka.NET users are natural early adopters (API similarity is intentional).
- **Embedded / IoT developers:** The device actor model + capability security provide a safer alternative to RTOSes (FreeRTOS, Zephyr) for regulated domains.
- **Security-conscious sectors:** Healthcare, critical infrastructure, automotive (ISO 26262), medical devices (IEC 62304), industrial control (IEC 61508) — where Linux certification takes 10+ years.
- **CLI-CPU / CFPU hardware project:** Symphact is the primary software target for the open silicon work, producing concrete HW requirements via `osreq-to-cfpu`.
- **Formal methods research community:** TLA+ / Dafny specifications make Symphact a teaching and research target.
- **European digital sovereignty initiative:** Apache-2.0 licensed, fully auditable, no US/Asian IP dependencies.

**Community building plan:**
- **GitHub repository with CI/CD:** Every commit runs all tests; green badge visible on README.
- **NuGet package publication:** `Symphact.Core` published to nuget.org by Month 4.
- **Documentation website:** GitHub Pages site with tutorials, API reference, design rationale.
- **Blog posts:** 3 technical milestone posts + 1 year-end retrospective.
- **Conference outreach:** Lightning talk at .NET Conf, Update Conf, or equivalent EU-based .NET event.
- **`osreq-to-cfpu` issue template:** Enables HW/OS co-design contribution.
- **Monthly progress reports** on the project website.

**Contributor growth commitment (single-applicant risk mitigation):** Symphact is currently a single-developer project — a documented risk factor for NLnet. To address this, a `CONTRIBUTORS.md` file is maintained in the repository listing all substantive technical contributors (runtime authors, test authors, documentation maintainers — not merely typo-fix PRs). The goal is **3+ substantive external contributors by Month 12**. Onboarding-focused tasks are explicitly labeled `good-first-issue` and `help-wanted` in the issue tracker.

**Supporting letters:** For the follow-up proposal, we plan to attach one academic letter (BME Department of Electron Devices or SZTAKI) and one industry letter (.NET Foundation member, Akka.NET maintainer, or Tiny Tapeout mentor) documenting institutional backing.

**Non-goals (explicit scope boundaries):**
- **NOT a replacement for Linux in this grant period.** The long-term vision mentions a 10-20 year transition; this grant targets *a working actor runtime with capability security*, not desktop/server replacement.
- **NOT a kernel for bare-metal hardware in this grant period.** Runs on .NET host OS; CFPU-native execution is a follow-up phase.
- **NOT a distributed system in this grant period.** Location transparency primitives land in M2, but multi-node distribution is Phase 3 (follow-up grant).

## .NET independence and standards alignment

The CIL specification (ECMA-335) is an international standard ratified by ISO/IEC 23271. Symphact targets the bytecode format, not any proprietary Microsoft runtime. Alternative CIL implementations exist (Mono, legacy .NET Framework compilers, various Roslyn-independent front-ends). The runtime design operates at the CIL level and is independent of any upstream runtime changes.

**Apache-2.0 licensing** provides permissive use in any downstream project, including commercial use — consistent with the .NET ecosystem's norms and .NET Foundation project expectations.

---

## Attachments plan

PDF attachments (~15-20 pages total):
1. **Architecture overview** — excerpt from `docs/vision-en.md` (capability model, actor hierarchy, message routing)
2. **Roadmap** — `docs/roadmap-en.md` Phase 1-7 with hour estimates
3. **Current status snapshot** — 142 xUnit test output (M0.1–M0.4 ✅), code metrics, repo screenshot
4. **CLI-CPU ↔ Symphact interaction diagram** — OS requirements feedback loop
5. **Threat model summary** — capability forgery, supervisor escape, hot-load tampering
6. **1-page executive summary** — problem, approach, deliverables, budget, sustainability

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.1 | 2026-05-01 | Submission-ready revision for the 13th open call (deadline 2026-06-01, same call as the parallel CLI-CPU submission). M0.3 supervision and M0.4 scheduler acknowledged as already delivered (out of grant scope). New milestone structure: M1 Persistence, M2 Remoting + Capability Registry, M3 Kernel + Device actors (hot code loading deferred to follow-up grant), M4 CFPU integration demo, M5 DX + outreach + contributor growth, M6 Security audit + formal groundwork. Budget arithmetic fixed: 760h × €36 ≈ €27,500 personnel + €2,000 audit + €500 conference = €30,000. CLI-CPU corrections #5 (developer-base grounding) and #9 (single-applicant team risk + supporting letters) integrated. "Why now?" deduplicated. Test count updated 46 → 142. |
| 1.0 | 2026-04-23 | Initial draft. Scope: M0.3-M3.2 + CFPU demo + security audit. Budget €30,000 / 12 months / ~340h. Scope-separated from CLI-CPU proposal (hardware vs software). |
