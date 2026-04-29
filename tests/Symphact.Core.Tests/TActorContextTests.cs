using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: Az IActorContext integráció tesztjei. Lefedi: context.Self helyes értékét, context.Send
/// üzenet-kézbesítést, actor-to-actor ping-pong multi-round forgatókönyvet, lánc-továbbítást,
/// hibás hívások exception viselkedését, és a DrainAsync végtelen-loop védelmét.
/// <br />
/// en: Tests for IActorContext integration. Covers: correct context.Self, context.Send delivery,
/// actor-to-actor ping-pong multi-round scenario, chain forwarding, error-call exception behaviour,
/// and DrainAsync infinite-loop protection.
/// </summary>
public sealed class TActorContextTests
{
    // ── Message types ─────────────────────────────────────────────────────────

    private sealed record TSetPartnerMsg(TActorRef Partner);
    private sealed record TPingMsg;
    private sealed record TPongMsg;

    // ── State types ───────────────────────────────────────────────────────────

    private sealed record TForwarderState(TActorRef Partner = default);
    private sealed record TPingPongState(TActorRef Partner = default, int Count = 0);

    // ── Helper actors ─────────────────────────────────────────────────────────

    /// <summary>
    /// hu: Bármilyen üzenetre elmenti context.Self-et az állapotba.
    /// <br />
    /// en: On any message, saves context.Self into state.
    /// </summary>
    private sealed class TSelfReportActor : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
            => AContext.Self;
    }

    /// <summary>
    /// hu: TSetPartnerMsg-re beállítja a partnert; minden más üzenetet context.Send-del
    /// a partnernek továbbít.
    /// <br />
    /// en: On TSetPartnerMsg sets the partner; all other messages are forwarded via context.Send.
    /// </summary>
    private sealed class TForwarderActor : TActor<TForwarderState>
    {
        public override TForwarderState Init() => new();

        public override TForwarderState Handle(TForwarderState AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TSetPartnerMsg msg)
                return AState with { Partner = msg.Partner };

            if (AState.Partner.IsValid)
                AContext.Send(AState.Partner, AMessage);

            return AState;
        }
    }

    /// <summary>
    /// hu: Egyszerű számláló: "increment" üzenetre növeli az állapotot.
    /// <br />
    /// en: Simple counter: increments state on "increment" message.
    /// </summary>
    private sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
            => AMessage is "increment" ? AState + 1 : AState;
    }

    /// <summary>
    /// hu: Ping oldal: "start"-ra TPingMsg-et küld, TPongMsg-re (ha Count lt 5) szintén;
    /// mindkét esetben növeli a számlálót.
    /// <br />
    /// en: Ping side: sends TPingMsg on "start", also on TPongMsg (if Count lt 5);
    /// increments counter in both cases.
    /// </summary>
    private sealed class TPingActor : TActor<TPingPongState>
    {
        public override TPingPongState Init() => new();

        public override TPingPongState Handle(TPingPongState AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TSetPartnerMsg msg)
                return AState with { Partner = msg.Partner };

            if (AMessage is "start" && AState.Partner.IsValid)
            {
                AContext.Send(AState.Partner, new TPingMsg());
                return AState with { Count = AState.Count + 1 };
            }

            if (AMessage is TPongMsg && AState.Count < 5 && AState.Partner.IsValid)
            {
                AContext.Send(AState.Partner, new TPingMsg());
                return AState with { Count = AState.Count + 1 };
            }

            return AState;
        }
    }

    /// <summary>
    /// hu: Pong oldal: TPingMsg-re (ha Count lt 5) TPongMsg-et küld vissza a partnernek,
    /// és növeli a számlálót.
    /// <br />
    /// en: Pong side: on TPingMsg (if Count lt 5) sends TPongMsg back to partner,
    /// increments counter.
    /// </summary>
    private sealed class TPongActor : TActor<TPingPongState>
    {
        public override TPingPongState Init() => new();

        public override TPingPongState Handle(TPingPongState AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TSetPartnerMsg msg)
                return AState with { Partner = msg.Partner };

            if (AMessage is TPingMsg && AState.Count < 5 && AState.Partner.IsValid)
            {
                AContext.Send(AState.Partner, new TPongMsg());
                return AState with { Count = AState.Count + 1 };
            }

            return AState;
        }
    }

    /// <summary>
    /// hu: Végtelen visszaküldő: minden "ping" üzenetre visszaküld "ping"-et a partnernek,
    /// korlát nélkül — a DrainAsync loop-védelmi teszt eszköze.
    /// <br />
    /// en: Infinite echo: on every "ping" sends "ping" back to partner without limit —
    /// tool for the DrainAsync loop-protection test.
    /// </summary>
    private sealed class TInfiniteEchoActor : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TSetPartnerMsg msg)
                return msg.Partner;

            if (AState.IsValid && AMessage is "ping")
                AContext.Send(AState, "ping");

            return AState;
        }
    }

    /// <summary>
    /// hu: Érvénytelen referenciára küld üzenetet — az InvalidOperationException terjesztési
    /// viselkedésének teszteléséhez.
    /// <br />
    /// en: Sends a message to an invalid reference — for testing InvalidOperationException
    /// propagation behaviour.
    /// </summary>
    private sealed class TBadRefSendActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            AContext.Send(TActorRef.Invalid, "oops");
            return AState;
        }
    }

    /// <summary>
    /// hu: Null üzenetet küld a partnernek — az ArgumentNullException terjesztési
    /// viselkedésének teszteléséhez.
    /// <br />
    /// en: Sends a null message to its partner — for testing ArgumentNullException
    /// propagation behaviour.
    /// </summary>
    private sealed class TNullMsgSendActor : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is TSetPartnerMsg msg)
                return msg.Partner;

            if (AState.IsValid && AMessage is "go")
                AContext.Send(AState, null!);

            return AState;
        }
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Context_Self_ReturnsCorrectActorRef()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var actorRef = system.Spawn<TSelfReportActor, TActorRef>();

        system.Send(actorRef, "report");
        system.Drain();

        var savedSelf = system.GetState<TActorRef>(actorRef);

        Assert.Equal(actorRef, savedSelf);
    }

    [Fact]
    public void Context_Send_DeliversMessageToTargetActor()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var counterRef = system.Spawn<TCounterActor, int>();
        var forwarderRef = system.Spawn<TForwarderActor, TForwarderState>();

        system.Send(forwarderRef, new TSetPartnerMsg(counterRef));
        system.Send(forwarderRef, "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(counterRef));
    }

    [Fact]
    public void Context_Send_PingPong_MultiRound()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var pingRef = system.Spawn<TPingActor, TPingPongState>();
        var pongRef = system.Spawn<TPongActor, TPingPongState>();

        system.Send(pingRef, new TSetPartnerMsg(pongRef));
        system.Send(pongRef, new TSetPartnerMsg(pingRef));
        system.Drain();

        system.Send(pingRef, "start");
        system.Drain();

        Assert.Equal(5, system.GetState<TPingPongState>(pingRef).Count);
        Assert.Equal(5, system.GetState<TPingPongState>(pongRef).Count);
    }

    [Fact]
    public void Context_Send_ChainForwarding_ThreeActors()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var cRef = system.Spawn<TCounterActor, int>();
        var bRef = system.Spawn<TForwarderActor, TForwarderState>();
        var aRef = system.Spawn<TForwarderActor, TForwarderState>();

        system.Send(bRef, new TSetPartnerMsg(cRef));
        system.Send(aRef, new TSetPartnerMsg(bRef));
        system.Drain();

        system.Send(aRef, "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(cRef));
    }

    [Fact]
    public void Context_Send_DisposedSystem_ThrowsObjectDisposedException()
    {
        var system = new TActorSystem(new TDotNetPlatform());
        system.Spawn<TCounterActor, int>();
        system.Dispose();

        Assert.Throws<ObjectDisposedException>(() => system.Drain());
    }

    [Fact]
    public void Context_Send_InvalidRef_ThrowsInvalidOperationException()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var badRef = system.Spawn<TBadRefSendActor, int>();

        system.Send(badRef, "trigger");

        Assert.Throws<InvalidOperationException>(() => system.Drain());
    }

    [Fact]
    public void Context_Send_NullMessage_ThrowsArgumentNullException()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var targetRef = system.Spawn<TCounterActor, int>();
        var senderRef = system.Spawn<TNullMsgSendActor, TActorRef>();

        system.Send(senderRef, new TSetPartnerMsg(targetRef));
        system.Drain();

        system.Send(senderRef, "go");

        Assert.Throws<ArgumentNullException>(() => system.Drain());
    }

    [Fact]
    public void DrainAsync_CircularSend_RespectsMaxRounds()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var aRef = system.Spawn<TInfiniteEchoActor, TActorRef>();
        var bRef = system.Spawn<TInfiniteEchoActor, TActorRef>();

        system.Send(aRef, new TSetPartnerMsg(bRef));
        system.Send(bRef, new TSetPartnerMsg(aRef));
        system.Drain();

        system.Send(aRef, "ping");

        Assert.Throws<InvalidOperationException>(() => system.Drain(AMaxRounds: 10));
    }

    [Fact]
    public void DrainAsync_WithContextSend_ProcessesMultipleRounds()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var cRef = system.Spawn<TCounterActor, int>();
        var bRef = system.Spawn<TForwarderActor, TForwarderState>();
        var aRef = system.Spawn<TForwarderActor, TForwarderState>();

        system.Send(bRef, new TSetPartnerMsg(cRef));
        system.Send(aRef, new TSetPartnerMsg(bRef));
        system.Send(aRef, "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(cRef));
    }

    [Fact]
    public void DrainAsync_AMaxRoundsZero_ThrowsImmediately()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");

        Assert.Throws<InvalidOperationException>(() => system.Drain(AMaxRounds: 0));
    }

    [Fact]
    public void DrainAsync_AMaxRoundsNegative_ThrowsImmediately()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");

        Assert.Throws<InvalidOperationException>(() => system.Drain(AMaxRounds: -1));
    }

    [Fact]
    public void Context_Self_DifferentForEachActor()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var aRef = system.Spawn<TSelfReportActor, TActorRef>();
        var bRef = system.Spawn<TSelfReportActor, TActorRef>();

        system.Send(aRef, "report");
        system.Send(bRef, "report");
        system.Drain();

        var aSelf = system.GetState<TActorRef>(aRef);
        var bSelf = system.GetState<TActorRef>(bRef);

        Assert.Equal(aRef, aSelf);
        Assert.Equal(bRef, bSelf);
        Assert.NotEqual(aSelf, bSelf);
    }
}
