# NLnet NGI Zero Commons Fund — Application Draft (Symphact)

> **Deadline:** 2026-06-01, 12:00 CEST
> **Form:** https://nlnet.nl/propose/
> **Call:** NGI Zero Commons Fund — 13th open call (same call as the parallel CLI-CPU submission filed 2026-04-14)
> **Status:** DRAFT — preparing submission for 2026-06-01

> Magyar verzió: [nlnet-application-draft-hu.md](nlnet-application-draft-hu.md)

> Version: 1.3

---

## Thematic Call

NGI Zero Commons Fund

## Proposal Name

**Symphact: An Actor Substrate for the Post-Dennard Era — one primitive for classical compute, AI-agent workloads, and hardware control**

## Website / Wiki

https://github.com/FenySoft/Symphact

## Abstract

**The current computing stack — Linux + GPU + accelerators + cache coherence — operates at the edge of physical limits.** Dennard scaling stalled in the mid-2000s (Bohr, IEEE 2007; Esmaeilzadeh et al., ISCA 2011), coherent shared memory scales increasingly poorly past a few hundred cores (MOESI/MESIF overhead, "dark silicon"), AI training energy remains high ($100M+ for a single frontier model, even as efficiency trends partially offset growth), and the heterogeneous CPU+GPU+NPU+TPU stack grows more entangled with every generation. **Over the next 15–20 years the computing paradigm will likely change:** share-nothing, message-passing, a single homogeneous primitive, dedicated-core-per-actor — the only path that routes around the physical limits (no coherence, no shared bus, linear scaling with core count).

Symphact + CFPU is the **first openly documented, working architectural prototype** of this new paradigm: a single primitive (`TActor`) serving as classical OS process, AI-agent runtime, and hardware driver **on the same homogeneous core grid**. Existing efforts each target a **single segment** (Intel Loihi = SNN only; Akka.NET = userland only; seL4 = security only; Linux = classical OS only) — **homogeneous integration is Symphact's own territory**.

The project has completed milestones M0.1 through M0.4 and is mid-way through M0.5: **186 passing xUnit tests** covering the core primitives — `TMailbox` (FIFO mailbox), `TActorRef` (capability token), `TActor<TState>` (abstract actor), `TActorContext` (handler context), `TActorSystem` (runtime), supervision (M0.3 — `ISupervisorStrategy`, OneForOne / AllForOne, lifecycle hooks, actor hierarchy), the scheduler API with per-actor parallelism (M0.4 — `IScheduler`, `TInlineScheduler`, `TDedicatedThreadScheduler`, CFPU dedicated-core-per-actor simulation), and the first two slices of the persistence layer (M0.5 — `IJournal` + `TInMemoryJournal`, `ISnapshotStore` + `TInMemorySnapshotStore`, BCL-only reference implementations). All developed with strict TDD methodology on .NET 10, Apache-2.0 licensed. **M0.3 supervision, M0.4 scheduler and the BCL-only reference slices of M0.5 persistence shipped between v0.1 (April 2026) and submission and are explicitly out of scope of this grant — the grant funds the content-addressed production-grade journal, integration with the supervision lifecycle, and the rest of M0.5.**

**The grant is engineering, research, and paradigmatic in character.** Persistence, remoting, and device-actor layers are concrete engineering deliverables. Alongside them, 9 open research questions begin to be answered — for which **no production-grade precedent exists**, because dedicated-core-per-actor hardware did not exist before. **And** the 12 months lay down **the first engineering layer of the next computing paradigm**: not the final optimum, but the foundation on which the next 20–30 years' cohort can build. **This is not a 12-month race — it is the first engineering stone of a paradigm-shift path, with documented evidence that the paradigm is physically realisable.**

**Deliberate separation between vision and deliverables:** The "15–30 year path of the post-Dennard paradigm" and the "unified primitive thesis" are the project's **long-term direction and research frame**. The **12-month part of this grant** delivers the **first engineering foundation** of that vision — concretely: content-addressed persistence (M1), location-transparent remoting + capability registry (M2), 3 device actors + 1 LLM-driven agent actor + 1 LIF-neuron actor on the CFPU simulator (M3–M4), audit + formal groundwork (M6), bilingual documentation and outreach (M5). The **vision is the context**, the **deliverables are the measurable foundation**. The mismatch (visionary ambition vs. 12-month scope) is **deliberate**: this is the **first step**, not the last.

**Why a paradigm shift, and why now?**

Current operating systems (Linux, Windows, macOS) carry architectural decisions made in the 1970s — shared memory, monolithic kernels, POSIX permissions, fork/exec, cache coherence as an invisible base assumption. These designs are **hitting physical walls**: coherence overhead becomes significant past a few hundred cores per chip ("dark silicon" phenomenon), frequency scaling has stalled since the mid-2000s, single-model AI training has reached $100M+ (efficiency trends — DeepSeek V3, MoE, distillation — partially offset but do not reverse the underlying trend), and the heterogeneous CPU+GPU+NPU+TPU+accelerator stack grows more entangled with every generation. Joe Armstrong (creator of Erlang) expressed in several talks and writings that the actor model would ideally run on dedicated hardware — that hardware did not yet exist. **That has now changed.** Open silicon (Tiny Tapeout, eFabless, IHP MPW), mature actor frameworks (Akka.NET, Orleans, Akka JVM, SpiNNaker), the mass arrival of multi-agent AI systems (Anthropic Agent SDK, OpenAI Agents, LangGraph), and the end of Dennard scaling together **force** the paradigm shift — the question is no longer whether, but who owns the next paradigm.

Symphact combines five proven ideas into **one homogeneous primitive**, on a modern foundation:

- **Actor model** (Erlang/OTP, 40+ years in telecom/finance) — let-it-crash + supervision
- **Capability-based security** (seL4, CHERI) — unforgeable references, no global namespace
- **Type-safe runtime** (Singularity research prototype, 2003) — memory safety at the ISA level
- **Deterministic message passing** (QNX commercial demonstration) — formal verifiability
- **Neuromorphic-adjacent compute** (Intel Loihi, IBM TrueNorth, SpiNNaker / EBRAINS) — asynchronous, local state, event-driven; same philosophy, only at typed-message level instead of spike level

**The unified primitive thesis:** the same `TActor<TState>` primitive serves as a classical OS process, hardware driver (via MMIO), AI agent (LLM-driven), and "smart neuron" (asynchronous learning entity) — on the same homogeneous core grid. **Nobody builds this openly:** Loihi is SNN-only, Akka.NET is userland-only, seL4 is security-only, Linux is classical OS only. Homogeneous integration is **Symphact's own territory and this grant's central thesis**.

**The grant targets three concrete outcomes (built on top of the already-delivered M0.1–M0.4 + first M0.5 slice foundation):**

1. **Persistent, share-nothing foundation (M1–M2):** Content-addressed event-sourcing journal (`IPersistenceProvider`, `TCasJournal`, `TCasSnapshotStore` — Git/IPFS-style content-addressable storage where filename = SHA-256 hash of the blob, append-only blobs with automatic deduplication, BCL-only — no external dependency), snapshot + replay integrated with the M0.3 supervision lifecycle. TCP-based remoting transport (`ITransport` / `TTcpTransport`) with location-transparent `Send` routing — the **first demonstration of the share-nothing message-passing foundation** over real network hops. Deliverable: ~80+ new xUnit tests.

2. **Demonstrating the homogeneous primitive (M2–M3):** `TCapabilityRegistry` with CST-based capability tokens (HW-managed on CFPU, software-managed on .NET hosts), `TRouter` actor for location-transparent address resolution, `TRootSupervisor` boot sequence, and **`TDeviceActor` (classical role), `TAgentActor` (AI-agent role), `TNeuronActor` (neuromorphic-adjacent role)** — all three using **the same `TActor<TState>` primitive**, differing only in configuration and handler logic. This is the first measurable demonstration of the unified primitive thesis. Deliverable: ~70+ new tests plus a documented benchmark showing that the three roles run on the same runtime with consistent semantics.

3. **First end-to-end multi-role demo on the CFPU simulator (M3–M4):** A reproducible application that, **within one Symphact system**, runs: (a) classical computation (distributed counter on 4 simulated cores), (b) hardware control (`uart_device`, `gpio_device`, `timer_device` over MMIO), (c) a multi-agent AI pipeline (LLM-driven `TAgentActor`s with capability-restricted interactions), and (d) a neuromorphic-adjacent experiment (`TNeuronActor` async learning pattern). All over the CLI-CPU reference simulator via the `FenySoft.CilCpu.Sim` NuGet. Deliverable: ~60+ new tests, 3–7 concrete HW requirements filed as `osreq-to-cfpu` issues, a publishable blog-post series. (Hot code loading is **explicitly deferred to a follow-up grant**.)

Alongside these three engineering deliverables, M5 carries an **outreach and documentation commitment** — NuGet publication, bilingual architecture docs, 3 blog posts, 2-3 online technical demo videos, GitHub Pages, monthly research log. The **actual cohort-building** (academic LoIs, industry partners) will be the **central deliverable of the follow-up grant**, once the parallel CLI-CPU project has delivered the first physical HW evidence (Tiny Tapeout F3, FPGA F4) — until then, the NLnet-compliant "contributor growth commitment" specifically applies to this foundation phase: passive interest and a contact funnel, not paid or active contributors.

**Why this matters for the NGI ecosystem:**

- **Apache-2.0, fully libre:** Permissive license compatible with the broader .NET ecosystem — enterprise adoption path is clear.
- **A large existing .NET developer base can target this runtime using familiar tools:** C#, F#, VB.NET all compile to CIL. Microsoft's annual developer surveys and Akka.NET / Orleans production deployments demonstrate the runtime's entrenched position in enterprise software — particularly in regulated industries (financial services, government, healthcare) where the Symphact security model is most valuable. The runtime runs on any .NET host today (Windows, Linux, macOS) — silicon independence means no hardware blocking.
- **Capability-based security at the framework level:** Unlike POSIX permissions, Symphact has no global namespace. An actor can only send a message to another if it holds a capability — there is no ambient authority. This is the seL4/CHERI security model in userland .NET. Software-only security measures are increasingly insufficient against supply chain attacks (log4j, xz-utils), AI-generated exploits, and state-level threats — capability security eliminates entire vulnerability classes by construction.
- **OS→HW feedback as a primary deliverable:** While Symphact runs on any CIL host today, it is co-designed with the Cognitive Fabric Processing Unit (CFPU) project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)). OS requirements feed back into hardware design via the `osreq-to-cfpu` issue template — a bidirectional co-design loop with documented historical precedents: the Inmos Transputer + Occam pairing (~1984, channel-based message passing both in HW and language) and the Symbolics Lisp Machine architecture (Moon, "Architecture of the Symbolics 3600", 1985 — tagged pointers and GC hardware support for the language runtime). Symphact + CFPU is a modern, .NET-based reimagination of these, fully open (Apache-2.0 + CERN-OHL-S). Every milestone's measurement sub-slice can yield a **concrete HW requirement**, backed by data — expected feedback paths include: configurable HW mailbox FIFO depth, supervision fault-notification primitives, HW-level CST cache, SHA-256 / BLAKE3 hash instruction (for the content-addressed journal), and SRAM-to-SRAM DMA for actor-state migration. **3–7 concrete `osreq-to-cfpu` issues are an explicit deliverable across the 12 months** — every feedback decision is public under Apache-2.0 / CERN-OHL-S, reproducible.
- **Formally verifiable:** The actor model's mathematical foundations (CSP, pi-calculus, Erlang's proven runtime) make Symphact a natural target for formal verification — unlike the ~40M lines of Linux kernel C code.
- **Multi-agent AI infrastructure as near-term commercial entry:** Transformer scaling is expected to plateau ($100M+ for a single frontier model training, exponential energy growth); the market is now discovering that **coordinating agent networks** are needed instead of a single LLM (Anthropic Agent SDK, OpenAI Agents/Swarm, LangGraph, AutoGen). These are **literally actor systems**, only their current implementations are poor — function-calling + JSON in Python. Symphact offers a **structured, capability-restricted, auditable multi-agent runtime** for the same workload. One proof deliverable of the 12 months is a LangChain-style multi-agent pipeline on Symphact actors with every agent capability-restricted, auditable, and supervised. This targets a **2–5 year market window**. *Concretely, why Akka.NET or Proto.Actor are not enough:* both are **userland frameworks** with shared GC and POSIX permissions; an LLM-driven agent **cannot be constructively constrained** in its activity (function-calling sandboxes can be circumvented, JIT-eval can be executed). Symphact's `TActor`, by contrast, is **capability-restricted** (an agent can only hold the `TActorRef`s it has been explicitly handed), and `TActorSystem.Send` validates the target at runtime + (on CFPU) at HW level — **this is full audit chain** with structured events. Relevant to M5+ regulated AI deployment (EU AI Act high-risk category).
- **European paradigm sovereignty:** Not an incremental Linux fork but the first engineering step of **European ownership of the next computing paradigm** — before the US-China silicon war closes the window. Apache-2.0 + CERN-OHL-S licensed, free of US/Asian IP dependencies, aligned with the core NGI mission. Natural alliance with the EU Human Brain Project (SpiNNaker, EBRAINS) and the Tiny Tapeout EU-IHP MPW track.

**Why now?** Five converging forces make this moment critical:

1. **Physical limits:** Dennard scaling has stalled since the mid-2000s (Bohr, IEEE 2007), coherent shared memory becomes increasingly expensive at high core counts (MOESI/MESIF overhead), and power/thermal limits lead to the dark silicon phenomenon (Esmaeilzadeh et al., ISCA 2011); AI inference/training energy remains high and growing. **This is not a matter of preference but engineering necessity:** the next paradigm will likely be share-nothing message-passing, because that is **the cleanest path** around core-count-dependent overhead.
2. **Hardware trend:** 100+ core chips are mainstream (Ampere AmpereOne 192, Intel Sierra Forest 288, AWS Graviton4 96, NVIDIA Grace 144), and shared-memory scaling has hit **diminishing returns**. Actor runtimes scale **near-linearly** with core count, without the bottleneck of shared-memory synchronisation.
3. **Open silicon maturity:** Tiny Tapeout, eFabless, IHP MPW make custom silicon prototyping **affordable** for the first time — the parallel CLI-CPU project **demonstrates this from the software side** with a fully tested CIL-T0 reference simulator and preliminary RTL; the actor-native silicon output is delivered by CFPU phases F3-F4 (expected 2027–2028).
4. **Multi-agent AI market is opening:** Anthropic Agent SDK, OpenAI Agents, LangGraph — the market is discovering a multi-agent coordination need that shows **structural similarity** to the actor model (message-passing, local state, supervision). Symphact can enter this 2–5 year market window with a **structured, capability-secured runtime** alongside current function-calling-based solutions.
5. **Regulatory environment:** EU CRA (Cyber Resilience Act, 2024), IEC 62443, ISO 21434, and EU AI Act all push toward provable safety, security, and auditability. The actor + capability model is a **natural target** for formal verification and certification (seL4 precedent); Linux (~40M lines of C) and function-calling-based AI-agent stacks do not fit these requirements.

**Historical precedent — winners and losers:** Those who attempted a paradigm shift and won: Unix 1969 (contemporary consensus: Multics is enough), the actor model 1973 (academic theory), Linux 1991 (hobby project), iPhone 2007 (PDA + phone = niche). Those who attempted and lost: Symbolics Lisp Machine (1980, silicon too expensive), Inmos Transputer + Occam (1984, "too little app software"), Intel iAPX 432 (1981, contemporary OO too slow), Microsoft Singularity Research (2003, remained academic). The difference was not in the size of the visionary ambition — **each of the losers was also smart** — but **(a) in the maturity of the developer ecosystem**, **(b) in the timing** relative to hardware readiness, and **(c) in sustainable community building** after the visionary pioneer. Symphact aligns with these three axes: ~8M existing .NET developer ecosystem; open silicon (Tiny Tapeout, IHP MPW) now first accessible; and cohort building is an **explicit M5 deliverable**, not a side effect.

## Have you been involved with projects or organisations relevant to this topic before?

The applicant has 35+ years of professional software and hardware experience:

- **National-scale production systems (1990s–2026):** As part of a 3-person team, developed "Atlasz" — a railway dispatch control system for MÁV (Hungarian National Railways), in continuous live national traffic-management operation since 2000 (service contract ending 2026-12-31) — 26 years of uninterrupted nationwide operation. Also Visual Restaurant / Visual Hotel & Restaurant suites (Delphi, later .NET), widely deployed in the Hungarian hospitality industry.
- **.NET ecosystem (20+ years):** Professional C#/.NET development including mandatory government data reporting integrations (NAV tax authority, NTAK tourism), Akka.NET actor systems (QCassa/JokerQ: 55+ actors in production), Avalonia UI cross-platform applications, and Android/iOS deployment.
- **Hungarian Tax Control Unit (Adóügyi Ellenőrző Egység):** Sole developer of the software for the original AEE project (regulation 48/2013 NGM). Current successor **QCassa/JokerQ** is a modern replacement secured with **LMS hash-based signatures (NIST SP 800-208 stateful hash-based signature scheme — quantum-resistant via SHA-256 collision resistance)**, with 55+ Akka.NET actors in supervision hierarchy, developed solo under regulation 8/2025 NGM. This project provides deep, hands-on experience with production actor-model architecture and hash-based signature integration that directly informs Symphact's AuthCode design (signer + bytecode blacklist using the same LMS family).
- **Hardware co-design experience:** Parallel development of the **CLI-CPU / CFPU** open silicon project ([github.com/FenySoft/CLI-CPU](https://github.com/FenySoft/CLI-CPU)) — 250+ xUnit tests on the reference simulator, 48 CIL-T0 opcodes, working Roslyn-based linker, preliminary Verilog RTL (ALU module: 41/41 cocotb tests). This hardware project is pursuing a separate NLnet grant (submitted 2026-04-14, same 13th open call, under review) and provides the bidirectional OS↔HW design loop.

The Symphact project itself began in April 2026. As of submission (May 2026), **186 passing xUnit tests** cover M0.1–M0.4 and the first slices of M0.5: actor core, inter-actor messaging, supervision (let-it-crash with OneForOne / AllForOne strategies, lifecycle hooks, hierarchy), the scheduler API with per-actor parallelism (`TInlineScheduler` + `TDedicatedThreadScheduler` simulating CFPU dedicated-core-per-actor), and the BCL-only in-memory event-sourcing journal + snapshot store. Approximately ~65 hours of focused TDD work delivered this baseline — **this velocity is documented in the roadmap** ([`docs/roadmap-en.md`](roadmap-en.md)) and forms the basis for the milestone estimates in this proposal.

## Requested Amount

**€30,000**

## Explain what the requested budget will be used for

> **Note on scope:** M0.3 supervision, M0.4 scheduler and the BCL-only reference slices of M0.5 persistence (in-memory journal + snapshot store) are already delivered (May 2026, ~65h self-funded) and are **not** funded by this grant. The milestones below cover the work remaining to reach a usable, secure, persistent actor runtime with kernel actors, device actors, and a CFPU integration demo.
>
> **Note on the bidirectional loop:** The applicant is **the developer of both projects** (Symphact OS and CLI-CPU / CFPU hardware). This creates a unique situation: the research sub-slice results **can feed back immediately** into the next iteration of the CFPU RTL — no separate coordination with an independent HW team. The 3–7 expected `osreq-to-cfpu` issues **also enter the CLI-CPU project's internal feature list**, and can reach silicon via the 2027 Tiny Tapeout / IHP MPW tape-out cycles — also funded by NLnet on a separate grant track. **Two open projects, one developer, one openly documented co-design loop.**

| Milestone | Description | Hours | Budget | Timeline | Expected `osreq-to-cfpu` issues |
|-----------|-------------|-------|--------|----------|---------------------------------|
| **M1: Persistence** (roadmap M0.5) | Builds on the already-delivered BCL-only `TInMemoryJournal` + `TInMemorySnapshotStore` (May 2026, 44 tests). Introduces `TCasJournal` and `TCasSnapshotStore`: content-addressable storage backend (Git/IPFS-style philosophy, filename = SHA-256 hash), append-only blobs with automatic deduplication. BCL-only — no external NuGet dependency, only `System.IO` + `System.Security.Cryptography`. `IPersistenceProvider` glue layer, snapshot + replay integration with the M0.3 supervision lifecycle, `RecoveryCompleted` signal, message stash during replay. ~30+ new xUnit tests. | ~80h | €2,900 | Month 1-3 | 1-2 issues (e.g. mailbox FIFO depth, SHA-256 hash instruction) |
| **M2: Remoting + Capability Registry** (roadmap M0.6 + M2.5) | TCP transport (`ITransport` / `TTcpTransport`), serialization, location-transparent `Send` routing, CST-based capability registry with delegation + revocation. ~80+ new tests. | ~160h | €5,800 | Month 2-7 | 1-2 issues (CST cache, capability invalidation broadcast) |
| **M3: Kernel + Device actors** (roadmap M2.1, M2.3, M3.1-M3.4) | `TRootSupervisor` boot sequence, `TRouter` actor, device actor framework + `uart_device`, `gpio_device`, `timer_device` on simulated MMIO. (Hot code loading explicitly deferred to follow-up grant.) ~80+ new tests. | ~180h | €6,500 | Month 5-10 | 1-2 issues (fault notification, MMIO mapping discipline) |
| **M4: CFPU integration demo** (roadmap M0.7 partial) | End-to-end demo: distributed counter actor across simulated cores via `FenySoft.CilCpu.Sim` NuGet (CLI-CPU C# reference simulator). Discover 3-5 concrete HW requirements, file as `osreq-to-cfpu` issues. | ~80h | €2,900 | Month 8-11 | 1 issue (SRAM-to-SRAM DMA, or explicit "not needed" conclusion) |
| **M5: Developer experience + docs + outreach** | NuGet package publication (`Symphact.Core`, `Symphact.Persistence`, `Symphact.Remoting`, `Symphact.Security`, `Symphact.Devices`), `symphact` CLI tool, bilingual architecture docs, contribution guide, **3 technical blog posts** (M1 persistence / M2 capability registry / M3-M4 unified primitive demo), **2-3 online technical demo videos** (for M1 / M2 / M3-M4 milestones, ~10-15 min each, YouTube or similar — travel-free, asynchronous, longer shelf life), public GitHub Pages docs + monthly research log. **Passive outreach target by Month 12:** 100+ GitHub stars, 5+ substantive issues / discussions from external participants, documented contact with ≥10 academic / industry touchpoints (contact list in the research log). **Active cohort-building is an INSTRUMENT, NOT A GOAL** in this phase: before the physical silicon demo (CLI-CPU F4 FPGA + F5 Tiny Tapeout, 2027–2028), industry / academic LoIs are **not realistic** — the actual cohort-building will be the **central deliverable of the follow-up grant**, once physically demonstrable HW exists. | ~180h | €6,500 | Ongoing | — (summary document + research log) |
| **M6: Formal groundwork** (roadmap M5.4 partial) | Initial formal specification of `send` / `receive` semantics in TLA+ or Dafny. Machine-checkable formalisation of capability model invariants, supervision lifecycle, and `TCapabilityRegistry`. **Groundwork for a later (at maturer project stage) independent security audit** — at v0.5 startup stage external audit is premature; what happens here is only the preparation of audit-input evidence. | ~80h | €2,900 | Month 9-12 | 0-1 issues (verification primitives) |
| **Engineering hours total** | | **~760h** | **€27,500** | **12 months** | **3-7 concrete HW requirements** |
| **Online outreach + documentation** (see Cost structure) | Video production, GitHub Pages hosting, expanded technical docs — **travel-free** | — | €2,500 | Ongoing | — |
| **Requested amount** | | | **€30,000** | | |

**Cost structure:**
- Personnel: ~760 hours part-time × €36/hour ≈ €27,500 (consistent with the parallel CLI-CPU proposal rate; ~63h/month part-time, in line with the M0.1–M0.4 actual velocity)
- Online outreach + documentation (video production, GitHub Pages hosting, expanded technical docs — **travel-free**): €2,500
- **No external security audit subcontract** — for a v0.5 startup-stage project this is premature; M6's formal spec serves as audit-preparation foundation
- **No conference travel** — alongside the parallel CLI-CPU project this would be capacity overload; an online video format replaces it
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

1. **Follow-up NLnet proposal (M5+ deliverable, Month 12–14):** After these 12 months, Symphact can be submitted to the next NGI Zero / NGI TALER / NGI Core round — the central deliverable being **cohort building** (academic LoIs, paid contributor onboarding) and **expansion of M3+ multi-agent AI / neuromorphic-adjacent demos**, now alongside CLI-CPU's F3-F4 phases (Tiny Tapeout silicon + FPGA multi-core, expected 2027–2028). This is the **most realistic sustainability path**, because the CLI-CPU NLnet grant's 18 months run in parallel and the two projects show **natural mutual scaffolding**.

2. **FenySoft Kft. cross-subsidy (ongoing, from 2026):** The applicant's FenySoft Kft. has **active QCassa/JokerQ revenue** (cash-register software regulated by NGM decree 8/2025), providing **cross-subsidy** for Symphact maintenance after the grant period. Concrete commitment: **~5 hours/month of personal work** on Symphact after grant expiry, for at least 12 months — sustaining CI/CD green status, NuGet package maintenance, and the monthly research log.

3. **Possible but NOT promised paths (only if conditions mature):** (a) `.NET Foundation project membership` — applicable if 1+ year of demonstrated project + min. 5 contributors (unlikely within these 12 months, possible at 24+); (b) `GitHub Sponsors / Open Collective` — $50–200/month appears realistic after a multi-agent AI market-window demo; (c) commercial consulting for `Akka.NET → Symphact migration` in regulated industries — only after the M3+ demo. **These are NOT promised, only mentioned as possibilities.**

**Deliberate capacity boundary (honest limit framing):** The applicant **openly states** that the 12-month NLnet-funded period, alongside the parallel CLI-CPU NLnet project, sits **at the upper bound of capacity**. Accordingly: (a) at the end of the 12 months, **no immediate active expansion is promised** — Symphact maintenance reduces to a slower pace (~5–10 hours/month); (b) the **follow-up NLnet proposal** is a natural continuation in Month 12–18, **only if CLI-CPU's F3-F4 phases have also progressed**; (c) **FenySoft Kft. cross-subsidy** guarantees minimum maintenance (~5 hours/month) regardless of whether the follow-up grant succeeds. **This is deliberate capacity boundary**, not a deficiency: the 12-month deliverables are **conservatively sized** so that they actually complete.

The project's **minimum guaranteed maintenance**: follow-up NLnet proposal + FenySoft Kft. cross-subsidy. This provides **2–3 years of calm maintenance** post-grant, regardless of whether the speculative paths materialise.

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
| **Anthropic Agent SDK / OpenAI Agents / LangGraph** | Multi-agent coordination framework in Python | Function-calling + JSON + JIT-eval; no capability-level isolation; ad-hoc audit | Capability-restricted actors, runtime + HW-level validation, supervision-based audit, EU AI Act-compliant foundations |
| **Tock** | Embedded OS in Rust | Embedded focus, not actor-based | Actor model, general-purpose |

**No existing project combines:** capability-based security + actor runtime + .NET ecosystem + open-source + co-designed with open silicon. Symphact is a new position in this space.

## Open research questions

Symphact applies four proven ideas (actor model, capability security, type-safe runtime, deterministic message passing) **in a hardware context that did not exist before**: a dedicated core per actor, private SRAM, HW mailbox FIFOs. As a result, several areas have **no precedent** to rely on — the 12 months produce **first measured answers**, not final optima.

1. **Scheduling 1000+ actors on a finite core count.** A one-actor-per-OS-thread model scales up to ~1000 actors. On CFPU, however, 10 000+ actors can coexist on a chip while only 256–1024 physical cores are available. **Open question:** hybrid strategy (static pinning + dynamic work-stealing) vs. compile-time profile-guided allocation. **Measurement:** throughput and tail latency across 100–10 000 actors.
   **→ Potential HW feedback:** if the hybrid strategy requires HW support (e.g. cheap context-switch within a dedicated core), `osreq-to-cfpu` issue → microarchitecture adaptation.

2. **Backpressure on dedicated cores.** If a producer actor emits 10× faster than the consumer, the HW mailbox FIFO fills. Erlang has unbounded mailboxes (memory exhaustion risk); Akka uses backpressure streams. **Open question:** what is the right semantics in a capability-based model? Drop, block-back, adaptive throttling — which is compatible with the let-it-crash philosophy?
   **→ Potential HW feedback:** HW-level mailbox flow-control bit / "mailbox full" interrupt → CFPU mailbox controller.

3. **Routing and capability resolution timing.** `TActorRef.SlotIndex` is an opaque token; the CST maps to an HW mailbox address. **Open question:** is the mapping spawn-time, send-time, or lazy? Can an actor be migrated to another core at runtime without invalidating the `TActorRef`? A combined performance / security question.
   **→ Potential HW feedback:** HW-level CST cache or lookup-assist.

4. **Share-nothing load balancing.** Classical SMP work-stealing assumes state moves cheaply. Migrating actor state held in private SRAM is expensive. **Open question:** profile-based static allocation vs. dynamic migration vs. "cold actor pool". **Measurement:** load imbalance ratio under different strategies on 4–256 simulated cores.
   **→ Potential HW feedback:** SRAM-to-SRAM DMA channel for actor-state migration, or "cold actor pool" HW support.

5. **Cross-core supervision latency.** The supervisor is on another core. When the child crashes, by what path (HW interrupt, mailbox, watchdog) is the supervisor notified, and with what latency? **Open question:** let-it-crash is trivial on SMP, but requires new design on dedicated cores.
   **→ Potential HW feedback:** dedicated fault-notification line for supervisor-child pairs, or a "shadow mailbox" hierarchy.

6. **Determinism vs. parallelism.** Formal verification (TLA+) requires deterministic scheduling; performance requires parallelism. **Open question:** can the same codebase serve both modes via configuration only? Replay-determinism via event sourcing?
   **→ Potential HW feedback:** optional global scheduling clock or synchronization primitive for verification runs.

7. **Memory strategy for heterogeneous actor sizes.** The .NET GC is global. On CFPU, per-core SRAM is finite. **Open question:** spill to DDR5, actor-scoped generational GC, or a compile-time size limit? This is a runtime *and* language-typing question at the same time.
   **→ Potential HW feedback:** spill mechanism between private SRAM and shared DDR5 with HW assist, or compile-time actor-size limit.

8. **Content-addressed event sourcing in a capability-based actor system.** `TCasJournal` applies Git/IPFS-style content-addressing to the event journal (filename = SHA-256 hash of blob content, automatic deduplication, immutable by construction). **Open question:** what integrity and performance properties arise, and how does hash verification integrate into the supervision restart path before the `RecoveryCompleted` signal?
   **→ Potential HW feedback:** SHA-256 / BLAKE3 hash instruction on CFPU (ARM SHA extensions and Intel SHA-NI as documented precedents).

9. **Verifying the unified primitive thesis.** The grant's central thesis is that **the same `TActor<TState>` primitive** serves as a classical OS process, hardware driver, AI-agent runtime, and neuromorphic-adjacent "smart neuron" — on the same homogeneous core grid. The concrete technical specification of the `TNeuronActor` role is in `docs/vision-en.md`: **LIF (Leaky Integrate-and-Fire) and Izhikevich neuron models**, where the membrane potential is the actor state and incoming/outgoing spikes are `SpikeMsg(weight: int)` messages; `TActorRef`s represent synaptic connections. **Open question:** does this homogeneity hold measurably in practice, or do the four roles require optimisations divergent enough to fork? What is the cost of unification in performance, and what is the cost of heterogeneity in complexity? **Measurement:** four reference actors (`TDeviceActor`, `LifNeuronActor`, `TAgentActor`, classical `TCounterActor`) on a shared benchmark — latency / throughput / code size / state size compared (the workload spectrum — µs UART vs. spike vs. second-scale LLM — explicitly documented).
   **→ Potential HW feedback:** if the unified primitive requires HW-level compromise (e.g. generalised, not segment-specific, mailbox size), `osreq-to-cfpu` issue → CFPU mailbox design unification. If the four roles do diverge, then segment-specific HW paths — this is an **architecture-level discovery** about the paradigm's maturity.

**Grant attitude:** we do **not promise final answers to these questions within 12 months**. We promise: every milestone carries a **measurement / experimental sub-slice** that measures at least one strategy, documents it, and feeds back into CLI-CPU hardware design via the `osreq-to-cfpu` issue template. A negative result (a strategy that does not work) is also a valuable deliverable — a conclusion of "this is not worth implementing in silicon" is itself useful to the hardware team.

---

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
- **Security-conscious sectors:** Healthcare, critical infrastructure, automotive (ISO 26262), medical devices (IEC 62304), industrial control (IEC 61508) — where formal certification of a monolithic kernel is extremely costly and slow.
- **CLI-CPU / CFPU hardware project:** Symphact is the primary software target for the open silicon work, producing concrete HW requirements via `osreq-to-cfpu`.
- **Formal methods research community:** TLA+ / Dafny specifications make Symphact a teaching and research target.
- **European digital sovereignty initiative:** Apache-2.0 licensed, fully auditable, no US/Asian IP dependencies.

**Community building plan:**
- **GitHub repository with CI/CD:** Every commit runs all tests; green badge visible on README.
- **NuGet package publication:** `Symphact.Core` published to nuget.org by Month 4.
- **Documentation website:** GitHub Pages site with tutorials, API reference, design rationale.
- **Blog posts:** 3 technical milestone posts (M1 / M2 / M3-M4), consistent with the M5 deliverable.
- **Online video outreach:** 2-3 online technical demo videos (for M1 / M2 / M3-M4 milestones, ~10-15 min, YouTube or similar platform) — travel-free, asynchronous, longer shelf life; replaces conference travel to avoid capacity overload alongside the parallel CLI-CPU project.
- **`osreq-to-cfpu` issue template:** Enables HW/OS co-design contribution.
- **Monthly progress reports** on the project website.

**Contributor growth commitment (single-applicant risk mitigation and its realistic limits):** Symphact is currently a single-developer project — a documented NLnet risk factor. **Partial mitigation** in these 12 months: (a) all work is **openly documented**, **bilingual** (Hungarian + English), **TDD-tested** — if the applicant drops out, the project is **takeoverable**; (b) `CONTRIBUTORS.md` and governance document created; (c) public research log + monthly reports on GitHub Pages; (d) `good-first-issue` and `help-wanted` labelling on onboarding tasks. **Realistic passive outreach target by Month 12:** 100+ GitHub stars, 5+ substantive external issues/discussions, ≥10 documented contacts with academic / industry touchpoints. **Actual paid or active contributor recruitment is unrealistic without a chip-level proof point** — therefore it is **explicitly the central deliverable of the follow-up grant**, once CLI-CPU's F3-F4 phases (Tiny Tapeout silicon + FPGA multi-core) provide the appeal.

**Why "cohort" rather than "community"?** The paradigm-shift path cannot be walked by one person alone (see the long-term vision section). "Cohort" here means the long-term research / engineering core team able to carry the path further (governance document, succession planning, `CONTRIBUTORS.md` core team) — not the user community. This commitment is therefore a **sustainability safeguard**, not an outreach metric.

**Non-goals (explicit scope boundaries for these 12 months — the long-term vision is in its own section):**
- **NOT a finished Linux replacement.** The long-term vision sees the post-Dennard paradigm as a 15–30 year path. This grant lays down the **engineering foundation** of that path, not the final destination.
- **NOT a bare-metal hardware kernel in this grant period.** Runs on .NET host OS; CFPU-native execution is a follow-up phase (expected tape-out 2028+).
- **NOT a production multi-node distributed system.** Location-transparency primitives land in M2 (TCP transport, capability registry), but multi-node production deployment is Phase 3 (follow-up grant).
- **NOT AGI or a frontier AI model.** The multi-agent AI infrastructure goal is to provide a **structured runtime** for existing LLMs (Claude, GPT, Llama) — not a new model.

---

## Long-term vision: the 15–30 year path of the post-Dennard paradigm

**Symphact + CFPU is not an incremental product but a paradigm foundation.** The current computing stack (Linux + GPU + cache coherence) has **hit physical walls**, and over the next 15–30 years it will necessarily transform. Symphact is the **first openly documented engineering layer** of that transition.

**Historical precedent it fits with:** Unix in 1969 (contemporary consensus: Multics is enough), the actor model in 1973 (contemporary consensus: purely theoretical), Linux in 1991 (contemporary consensus: a toy, not a serious OS), iPhone in 2007 (contemporary consensus: PDA + phone = niche). **Every paradigm shift starts with a visionary pioneer whom contemporary expert consensus deems too ambitious.** The present moment is the next iteration in this series, and owning the paradigm shift means **computing dominance in the second half of the 21st century**.

**Path segments (estimate, not promise):**

| Years | Milestone | Cohort |
|---|---|---|
| **2026** | M0.5–M3.2 (this grant) | Single applicant + 3+ contributors |
| **2027–2028** | M4–M6 + multi-agent AI market position + first CFPU Tiny Tapeout | Symphact community core (10–20) |
| **2028–2032** | Production-ready multi-agent runtime + IHP MPW multi-component CFPU + academic validation (TLA+, formal spec) | Founding cohort (50–100) + academic partners |
| **2032–2040** | Niche production deployment (automotive, medical, critical infrastructure) + general-purpose multi-agent AI infrastructure | European consortium, EU Horizon-scale funding |
| **2040+** | Wide post-Dennard paradigm adoption — "Linux compared to the 1970s" position | Global community |

**The founder's task** (in scope of this grant) **is not to walk the entire path**, but to **lay down the foundation and hand over the cohort**. John von Neumann did not live to see modern computers; Carl Hewitt did not live to see commercial actor systems. **The founder role is to prove the path is walkable and to build the cohort that walks it further.**

**What must be done by the end of the 12 months** for the path to be walkable:
1. **Proven:** the unified primitive thesis works — the same `TActor` serves classical, hardware, and AI roles (M3 demo)
2. **Documented:** first measured answers to the 9 research questions + 3–7 concrete HW requirements (M1–M6)
3. **Onboarded:** 3+ substantive external contributors + 2+ academic / industry support letters (M5 cohort building)
4. **Published:** 3 blog posts + 2-3 online technical demo videos + an open research log (M5 outreach)
5. **Continuable:** governance document + `CONTRIBUTORS.md` core team + succession planning (M5)

If these are in place, the **paradigm-shift path is walkable** — independently of the applicant's person.

## .NET independence and standards alignment

The CIL specification (ECMA-335) is an international standard ratified by ISO/IEC 23271. Symphact targets the bytecode format, not any proprietary Microsoft runtime. Alternative CIL implementations exist (Mono, legacy .NET Framework compilers, various Roslyn-independent front-ends). The runtime design operates at the CIL level and is independent of any upstream runtime changes.

**Apache-2.0 licensing** provides permissive use in any downstream project, including commercial use — consistent with the .NET ecosystem's norms and .NET Foundation project expectations.

---

## Attachments plan

PDF attachments (~15-20 pages total):
1. **Architecture overview** — excerpt from `docs/vision-en.md` (capability model, actor hierarchy, message routing)
2. **Roadmap** — `docs/roadmap-en.md` Phase 1-7 with hour estimates
3. **Current status snapshot** — 186 xUnit test output (M0.1–M0.4 ✅, M0.5 BCL-only slices ✅), code metrics, repo screenshot
4. **CLI-CPU ↔ Symphact interaction diagram** — OS requirements feedback loop
5. **Threat model summary** — capability forgery, supervisor escape, hot-load tampering
6. **1-page executive summary** — problem, approach, deliverables, budget, sustainability

---

## Changelog

| Version | Date | Summary |
|---------|------|---------|
| 1.3 | 2026-05-16 | Paradigm pivot: post-Dennard substrate + unified primitive thesis (`TActor` as OS process / hardware driver / AI agent / LIF neuron). Test count 142 → 186; `TCasJournal`; new "Open research questions" (9 items) + "Long-term vision" sections; cohort commitment → M5 (narrowed to realistic). Devil's-advocate fact-check: Apple analogy → Transputer/Lisp Machine; Dennard date sourced (Bohr 2007); "dark silicon" citation corrected; Armstrong quote softened; survivorship bias balanced; audit + conference removed (startup-stage / capacity); CLI-CPU submission date fixed (2026-04-14); Atlasz 26 years live operation. |
| 1.1 | 2026-05-01 | Submission-ready revision for the 13th open call; new M1–M6 milestone structure; €30,000 budget; CLI-CPU #5, #9 integrated. |
| 1.0 | 2026-04-23 | Initial draft. Scope: M0.3–M3.2 + CFPU demo + security audit; €30,000 / 12 months; scope-bounded from CLI-CPU grant. |
