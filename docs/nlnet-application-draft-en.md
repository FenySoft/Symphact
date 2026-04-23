# NLnet NGI Zero Commons Fund — Application Draft (Neuron OS)

> **Deadline:** TBD — next NGI Zero Commons Fund open call (projected Q3 2026)
> **Form:** https://nlnet.nl/propose/
> **Call:** NGI Zero Commons Fund
> **Status:** DRAFT — not yet submitted

> Magyar verzió: [nlnet-application-draft-hu.md](nlnet-application-draft-hu.md)

> Version: 1.0

---

## Thematic Call

NGI Zero Commons Fund

## Proposal Name

**Neuron OS: Capability-Based Actor Runtime for Secure .NET Computing**

## Website / Wiki

https://github.com/FenySoft/NeuronOS

## Abstract

Neuron OS is a **capability-based actor runtime for .NET**, built on the principle that *every stateful entity is an actor and communication happens exclusively through immutable messages*. The project has completed its foundational phase (v0.1 pre-alpha): **46 passing xUnit tests** covering the core primitives — `TMailbox` (FIFO mailbox), `TActorRef` (capability token), `TActor<TState>` (abstract actor with `Init()` + `Handle()`), `TActorContext` (handler context with `Send`), and `TActorSystem` (runtime with `Spawn`, `Send`, `DrainAsync`). All developed with strict TDD methodology on .NET 10, Apache-2.0 licensed. **This grant funds the transition from core primitives to a usable, secure actor operating system**: supervision, scheduling, persistence, capability-based security, and the first device actors.

**Why an actor OS, and why now?**

Current operating systems (Linux, Windows, macOS) carry architectural decisions made in the 1970s — shared memory, monolithic kernels, POSIX permissions, fork/exec. These designs are increasingly unfit for modern reality: 1000+ cores per chip, AI-driven threats, supply chain attacks (log4j, xz-utils), and the need for fault tolerance in safety-critical systems. Joe Armstrong (creator of Erlang) described this in his 2014 talk "The Mess We're In" — actor-oriented hardware was needed but did not exist. **That has now changed.** Open silicon (Tiny Tapeout, eFabless, IHP MPW), mature actor frameworks (Akka.NET, Orleans, Akka JVM), and the end of Dennard scaling make a clean-slate actor OS viable in 2026.

Neuron OS combines four proven ideas into one runtime, on a modern foundation:

- **Actor model** (Erlang/OTP, 40+ years in telecom/finance) — let-it-crash + supervision
- **Capability-based security** (seL4, CHERI) — unforgeable references, no global namespace
- **Type-safe runtime** (Singularity research prototype, 2003) — memory safety at the ISA level
- **Deterministic message passing** (QNX commercial demonstration) — formal verifiability

**The grant targets three concrete outcomes:**

1. **Supervision + Scheduler (M1):** Implement `ISupervisorStrategy` (OneForOne, AllForOne), lifecycle hooks (`PreStart`, `PostStop`, `PreRestart`, `PostRestart`), `IScheduler` + `TRoundRobinScheduler` + `TDedicatedThreadScheduler`. Deliverable: ~80+ new xUnit tests, let-it-crash + supervision tree working end-to-end.

2. **Kernel actors + capability security (M2):** `capability_registry` with HMAC-signed tokens, `router` with location transparency, persistence via event sourcing (state survives restart). Deliverable: capability-based message routing with revocation + delegation.

3. **First device actors + reference app (M3):** `uart_device`, `gpio_device`, `timer_device` actors running on .NET host against a simulated MMIO layer. First end-to-end demo: a distributed counter across 4 cores using the CLI-CPU reference simulator (via the upcoming `FenySoft.CilCpu.Sim` NuGet package). Deliverable: reproducible reference app with docs.

**Why this matters for the NGI ecosystem:**

- **Apache-2.0, fully libre:** Permissive license compatible with the broader .NET ecosystem — enterprise adoption path is clear.
- **8+ million .NET developers can target this runtime using familiar tools:** C#, F#, VB.NET all compile to CIL. The runtime runs on any .NET host today (Windows, Linux, macOS) — silicon independence means no hardware blocking.
- **Capability-based security at the framework level:** Unlike POSIX permissions, Neuron OS has no global namespace. An actor can only send a message to another if it holds a capability — there is no ambient authority. This is the seL4/CHERI security model in userland .NET.
- **Co-designed with open silicon:** While Neuron OS runs on any CIL host today, it is co-designed with the Cognitive Fabric Processing Unit (CFPU) project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). OS requirements feed back into hardware design via the `osreq-to-cfpu` issue template — a bidirectional loop similar to Apple's M-series OS/hardware integration, but fully open.
- **Formally verifiable:** The actor model's mathematical foundations (CSP, pi-calculus, Erlang's proven runtime) make Neuron OS a natural target for formal verification — unlike the ~40M lines of Linux kernel C code.
- **European sovereignty:** An open, auditable .NET runtime free of US/Asian IP dependencies — Apache-2.0 license permits any European entity to fork, modify, and certify.

**Why now?** Three converging forces make this moment critical:

1. **Security pressure:** Supply chain attacks (SolarWinds, log4j, xz-utils), ransomware, and AI-generated exploits demand stronger isolation than POSIX can provide. Capability-based security eliminates entire classes of vulnerabilities by construction.
2. **Hardware trend:** 100+ core chips are mainstream (AWS Graviton4, Ampere AmpereOne, Apple M-series), and shared-memory scaling is at a wall. Actor runtimes scale linearly.
3. **Regulatory environment:** EU CRA (Cyber Resilience Act, 2024), IEC 62443, ISO 21434 are pushing toward provable safety/security properties. Actor + capability model is natively certifiable; Linux is not.

## Have you been involved with projects or organisations relevant to this topic before?

The applicant has 35+ years of professional software and hardware experience:

- **National-scale production systems (1990s–2026):** As part of a 3-person team, developed "Atlasz" — a railway dispatch control system for MÁV (Hungarian National Railways), used in national traffic management until 2026. Also Visual Restaurant / Visual Hotel & Restaurant suites (Delphi, later .NET), widely deployed in the Hungarian hospitality industry.
- **.NET ecosystem (20+ years):** Professional C#/.NET development including mandatory government data reporting integrations (NAV tax authority, NTAK tourism), Akka.NET actor systems (QCassa/JokerQ: 55+ actors in production), Avalonia UI cross-platform applications, and Android/iOS deployment.
- **Hungarian Tax Control Unit (Adóügyi Ellenőrző Egység):** Sole developer of the software for the original AEE project (regulation 48/2013 NGM). Current successor **QCassa/JokerQ** is a PQC-secured modern replacement (55+ Akka.NET actors in supervision hierarchy) developed solo under regulation 8/2025 NGM. This project provides deep, hands-on experience with production actor-model architecture that directly informs Neuron OS design decisions.
- **Hardware co-design experience:** Parallel development of the **CLI-CPU / CFPU** open silicon project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — 250+ xUnit tests on the reference simulator, 48 CIL-T0 opcodes, working Roslyn-based linker, preliminary Verilog RTL (ALU module: 41/41 cocotb tests). This hardware project is pursuing a separate NLnet grant (submitted 2026-06-01) and provides the bidirectional OS↔HW design loop.

The Neuron OS project itself began in April 2026, and in ~14 hours of focused TDD work delivered v0.1 (M0.1 + M0.2): 46 xUnit tests, ~580 lines of runtime code, working `TActorSystem` with inter-actor messaging. **This velocity is documented in the roadmap** ([`docs/roadmap.md`](roadmap.md)) and forms the basis for realistic milestone estimates in this proposal.

## Requested Amount

**€30,000**

## Explain what the requested budget will be used for

| Milestone | Description | Hours | Budget | Timeline |
|-----------|-------------|-------|--------|----------|
| **M1: Supervision + Scheduler** (M0.3-M0.4) | `ISupervisorStrategy` (OneForOne/AllForOne), lifecycle hooks, actor hierarchy, `IScheduler` with round-robin + dedicated-thread variants. ~80+ new xUnit tests. | ~65h | €6,000 | Month 1-4 |
| **M2: Persistence + Location Transparency** (M0.5-M0.7) | Event-sourcing persistence, capability registry with HMAC tokens, router with revocation + delegation. ~60+ new tests. | ~85h | €7,000 | Month 3-8 |
| **M3: Kernel actors + Device actors** (M2.1-M3.2) | `memory_manager`, `hot_code_loader` foundation, first device actors (`uart_device`, `gpio_device`, `timer_device`) on simulated MMIO. ~50+ new tests. | ~60h | €5,500 | Month 6-11 |
| **M4: CFPU integration demo** | End-to-end demo: distributed counter actor across 4 simulated CFPU cores via `FenySoft.CilCpu.Sim` NuGet. Discover 3-5 concrete HW requirements, file as `osreq-to-cfpu` issues. | ~35h | €3,500 | Month 9-12 |
| **M5: Developer experience + docs** | NuGet package publication (NeuronOS.Core), CLI tool (`neuronos-cli`), English architecture docs, contribution guide, 3 blog posts, lightning talk at a .NET conference. | ~70h | €5,500 | Ongoing |
| **M6: Security audit + formal groundwork** | External security review of the capability mechanism (independent reviewer, ~€2,000 subcontract). Initial formal specification of `send/receive` semantics in TLA+ or Dafny. | ~25h | €2,500 | Month 10-12 |
| **Total** | | **~340h** | **€30,000** | **12 months** |

**Cost structure:**
- Personnel: ~340 hours part-time × €36/hour = €27,500 (consistent with the parallel CLI-CPU proposal rate)
- Security audit subcontract: €2,000 (external reviewer for capability mechanism)
- Conference travel (1 lightning talk, EU-based): €500
- **No hardware costs** — Neuron OS runs entirely on software hosts (Windows/Linux/macOS/CI)

## Describe existing funding sources

The project is currently self-funded by the applicant. No external funding has been received. There are no pending applications to other funding bodies **for this work (Neuron OS runtime)**.

**Related but scope-separated:** A parallel NLnet NGI Zero Commons Fund application was submitted (2026-04) for the **CLI-CPU / CFPU hardware project** ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). The two projects are **deliberately non-overlapping in scope**:

| Dimension | CLI-CPU / CFPU | Neuron OS |
|-----------|---------------|-----------|
| **Deliverable** | Hardware ISA, RTL, silicon tape-out, FPGA | Software runtime, OS services |
| **Target** | Verilog synthesis, Sky130 PDK | .NET 10 library (runs on Windows/Linux/macOS) |
| **License** | CERN-OHL-S-2.0 (reciprocal hardware) | Apache-2.0 (permissive software) |
| **Repository** | `FenySoft/CLI-CPU` | `FenySoft/NeuronOS` |
| **Milestones funded** | F2 RTL, F3 Tiny Tapeout, F4 FPGA multi-core | M0.3-M3.2 actor runtime + kernel actors |
| **Dependencies** | None on Neuron OS | None on CLI-CPU silicon (simulator-ready) |

**Sustainability plan:**

1. **.NET Foundation submission (Month 9):** Apply for .NET Foundation project membership — provides code signing, CLA management, legal/IP support, Azure infrastructure hosting (no direct funding, but community legitimacy).
2. **Follow-up NLnet proposal (Month 12-14):** M0.5-M1.0 + distributed actor model (Phase 3-4 of the roadmap).
3. **GitHub Sponsors / Open Collective (Month 6+):** Set up community funding channels for ongoing maintenance. Target: €500-1500/month steady state by end of year 2.
4. **Commercial consulting (Year 2+):** FenySoft Kft. offers integration consulting services for regulated industries (healthcare, finance, critical infrastructure) that need actor-model architectures. This provides cross-subsidy for core runtime maintenance without compromising the open-source core.
5. **Dual licensing for specialized components (optional, Year 3+):** Core runtime stays Apache-2.0; enterprise support, formally verified components, and industry-specific extensions may carry commercial licenses — only if demand materializes.

The project has a **path to self-sustainability by Year 3** without reliance on continuous grant funding.

## Comparison with existing efforts

| Project | Approach | Limitation | Neuron OS difference |
|---------|----------|------------|---------------------|
| **Akka.NET** | Actor framework on .NET | Runs on Linux/Windows with global GC, POSIX permissions, no capability security | Capability-based security, per-core GC model, co-designed with actor-native HW |
| **Microsoft Orleans** | Virtual actor framework | Cloud-scale, stateless actor focus, not OS-level | OS-level actors including device drivers and kernel services |
| **Erlang/OTP** | Actor VM with supervision | Dynamically typed, not .NET ecosystem, own language | .NET ecosystem (C#/F#/VB.NET), statically typed, CIL compilation target |
| **seL4** | Formally verified microkernel | C only, no .NET support, small developer community | .NET runtime, capability security as OS primitive, developer ergonomics |
| **CHERI / Morello** | Capability architecture extension | ARM/RISC-V specific, still academic | Software-first, capability in runtime — works on any CIL host today |
| **Singularity (Microsoft Research, 2003)** | Type-safe OS in C# | Research only, abandoned, no community | Production-grade actor runtime, co-designed with open HW |
| **Redox OS** | Microkernel in Rust | Rust only, no .NET, not actor-based | Actor-based, .NET ecosystem |
| **Tock** | Embedded OS in Rust | Embedded focus, not actor-based | Actor model, general-purpose |

**No existing project combines:** capability-based security + actor runtime + .NET ecosystem + open-source + co-designed with open silicon. Neuron OS is a new position in this space.

## What are significant technical challenges you expect to solve during the project?

1. **Capability token forgery resistance:** The `TActorRef` struct carries an `HMAC-CapabilityTag`. The challenge: generating, verifying, and revoking capability tokens without a trusted execution environment (the CLI-CPU silicon will eventually provide this, but the first year targets software hosts). Mitigation: HMAC-SHA256 with a runtime-generated key kept in an opaque registry actor; formal specification of the threat model.

2. **Supervisor restart semantics on shared-memory hosts:** Erlang's "restart" assumes actor state is isolated (separate heap). On a .NET shared-GC host, restarting an actor requires careful state teardown to prevent cross-actor leaks. Challenge: clean restart boundaries without compromising performance. Mitigation: per-actor arena allocators (ArrayPool-backed) + explicit state serialization in M0.5.

3. **Deterministic scheduler for formal verification:** TLA+ specifications for the actor model require deterministic scheduling to be useful. Challenge: reconciling determinism with multi-thread execution. Mitigation: `TDedicatedThreadScheduler` provides per-actor thread isolation; `TRoundRobinScheduler` provides deterministic single-threaded execution for verification runs.

4. **Hot code loading without runtime reflection penalties:** .NET's reflection-based code loading is slow. Challenge: CIL-level verification + loading without `System.Reflection.Emit` overhead. Mitigation: precompile new actor versions as `AssemblyLoadContext` with unloadable semantics; verify opcodes against whitelist.

5. **OS-to-HW requirement discovery discipline:** The `osreq-to-cfpu` loop must produce concrete, actionable HW requirements (not wish-list items). Challenge: separating "nice-to-have" from "required for correctness". Mitigation: every `osreq` issue must include a benchmark measurement from the simulator showing the need — no requirement without data.

## Describe the ecosystem of the project

**Upstream dependencies (all open source):**
- **.NET 10 SDK** (Microsoft, MIT license) — runtime, compiler, test framework
- **xUnit 2.9.3** — testing framework
- **CLI-CPU reference simulator** (CERN-OHL-S-2.0) — via upcoming `FenySoft.CilCpu.Sim` NuGet for CFPU integration demos
- **Visual Studio Code / Code - OSS** (MIT) — primary development environment

**Downstream users and stakeholders:**
- **.NET developer community (~8M+ developers):** Any C#/F# codebase can adopt Neuron OS as an actor runtime. Akka.NET users are natural early adopters (API similarity is intentional).
- **Embedded / IoT developers:** The device actor model + capability security provide a safer alternative to RTOSes (FreeRTOS, Zephyr) for regulated domains.
- **Security-conscious sectors:** Healthcare, critical infrastructure, automotive (ISO 26262), medical devices (IEC 62304), industrial control (IEC 61508) — where Linux certification takes 10+ years.
- **CLI-CPU / CFPU hardware project:** Neuron OS is the primary software target for the open silicon work, producing concrete HW requirements via `osreq-to-cfpu`.
- **Formal methods research community:** TLA+ / Dafny specifications make Neuron OS a teaching and research target.
- **European digital sovereignty initiative:** Apache-2.0 licensed, fully auditable, no US/Asian IP dependencies.

**Community building plan:**
- **GitHub repository with CI/CD:** Every commit runs all tests; green badge visible on README.
- **NuGet package publication:** `NeuronOS.Core` published to nuget.org by Month 4.
- **Documentation website:** GitHub Pages site with tutorials, API reference, design rationale.
- **Blog posts:** 3 technical milestone posts + 1 year-end retrospective.
- **Conference outreach:** Lightning talk at .NET Conf, Norbit / Update Conf / or equivalent EU-based .NET event.
- **`osreq-to-cfpu` issue template:** Enables HW/OS co-design contribution.
- **Monthly progress reports** on the project website.

**Non-goals (explicit scope boundaries):**
- **NOT a replacement for Linux in this grant period.** The long-term vision mentions a 10-20 year transition; this grant targets *a working actor runtime with capability security*, not desktop/server replacement.
- **NOT a kernel for bare-metal hardware in this grant period.** Runs on .NET host OS; CFPU-native execution is a follow-up phase.
- **NOT a distributed system in this grant period.** Location transparency primitives land in M2, but multi-node distribution is Phase 3 (follow-up grant).

## .NET independence and standards alignment

The CIL specification (ECMA-335) is an international standard ratified by ISO/IEC 23271. Neuron OS targets the bytecode format, not any proprietary Microsoft runtime. Alternative CIL implementations exist (Mono, legacy .NET Framework compilers, various Roslyn-independent front-ends). The runtime design operates at the CIL level and is independent of any upstream runtime changes.

**Apache-2.0 licensing** provides permissive use in any downstream project, including commercial use — consistent with the .NET ecosystem's norms and .NET Foundation project expectations.

---

## Attachments plan

PDF attachments (~15-20 pages total):
1. **Architecture overview** — excerpt from `docs/vision-en.md` (capability model, actor hierarchy, message routing)
2. **Roadmap** — `docs/roadmap.md` Phase 1-7 with hour estimates
3. **Current status snapshot** — 46 xUnit test output, code metrics, repo screenshot
4. **CLI-CPU ↔ Neuron OS interaction diagram** — OS requirements feedback loop
5. **Threat model summary** — capability forgery, supervisor escape, hot-load tampering
6. **1-page executive summary** — problem, approach, deliverables, budget, sustainability

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.0 | 2026-04-23 | Initial draft. Scope: M0.3-M3.2 + CFPU demo + security audit. Budget €30,000 / 12 months / ~340h. Scope-separated from CLI-CPU proposal (hardware vs software). |
