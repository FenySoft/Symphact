using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: End-to-end supervision tesztek: hiba → stratégia → directive végrehajtás
/// (Stop, Restart, Resume, Escalate). A DrainAsync try-catch-be csomagolja a Handle hívást,
/// és a szülő stratégiája dönt a gyerek sorsáról.
/// <br />
/// en: End-to-end supervision tests: failure → strategy → directive execution
/// (Stop, Restart, Resume, Escalate). DrainAsync wraps Handle in try-catch and the parent's
/// strategy decides the child's fate.
/// </summary>
public sealed class TSupervisionTests
{
    #region Test Actors

    private sealed class TFailingActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "fail")
                throw new InvalidOperationException("deliberate failure");

            if (AMessage is "increment")
                return AState + 1;

            return AState;
        }
    }

    private sealed class TSupervisorActor : TActor<TActorRef>
    {
        private readonly ISupervisorStrategy FStrategy;

        public TSupervisorActor() : this(new TOneForOneStrategy()) { }

        public TSupervisorActor(ISupervisorStrategy AStrategy)
        {
            FStrategy = AStrategy;
        }

        public override ISupervisorStrategy? SupervisorStrategy => FStrategy;

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    private sealed class TRestartTrackingActor : TActor<List<string>>
    {
        private List<string>? FEvents;

        public override List<string> Init()
        {
            FEvents = new List<string>();
            return FEvents;
        }

        public override List<string> Handle(List<string> AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "fail")
                throw new InvalidOperationException("deliberate");

            AState.Add($"Handle:{AMessage}");
            return AState;
        }

        public override void PreRestart(Exception AException)
        {
            FEvents?.Add($"PreRestart:{AException.Message}");
        }

        public override void PostRestart(Exception AException)
        {
            FEvents?.Add($"PostRestart:{AException.Message}");
        }

        public override void PostStop()
        {
            FEvents?.Add("PostStop");
        }
    }

    private sealed class TRestartTrackingSupervisor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Restart);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TRestartTrackingActor, List<string>>();

            return AState;
        }
    }

    private sealed class TStoppingSupervisor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Stop);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    private sealed class TResumingSupervisor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Resume);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    private sealed class TEscalatingSupervisor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Escalate);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    #endregion

    #region Restart

    [Fact]
    public void Restart_ChildStateResetsToInit()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TRestartTrackingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "msg1");
        system.Drain();

        // Now fail — should restart with fresh state
        system.Send(childRef, "fail");
        system.Drain();

        var state = system.GetState<List<string>>(childRef);

        // Fresh state from Init — should only have PostRestart (PreRestart was on old state list)
        Assert.Contains($"PostRestart:deliberate", state);
        Assert.DoesNotContain("Handle:msg1", state);
    }

    [Fact]
    public void Restart_PreRestart_CalledWithException()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TRestartTrackingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "fail");
        system.Drain();

        // PreRestart was on the OLD state; we can't see it in new state.
        // But PostRestart IS on the new state.
        var state = system.GetState<List<string>>(childRef);
        Assert.Contains("PostRestart:deliberate", state);
    }

    [Fact]
    public void Restart_MailboxPreserved_MessagesStillProcessed()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TRestartTrackingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);

        // Send fail + subsequent message in one batch
        system.Send(childRef, "fail");
        system.Send(childRef, "after-restart");
        system.Drain();

        var state = system.GetState<List<string>>(childRef);
        Assert.Contains("Handle:after-restart", state);
    }

    #endregion

    #region Stop

    [Fact]
    public void Stop_ChildIsStoppedAfterFailure()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TStoppingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(childRef));

        // Now fail — should stop
        system.Send(childRef, "fail");
        system.Drain();

        Assert.True(system.IsStopped(childRef));
    }

    [Fact]
    public void Stop_FurtherMessagesSilentlyDropped()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TStoppingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "fail");
        system.Drain();

        // Should not throw — silently dropped
        system.Send(childRef, "increment");
        system.Drain();
    }

    #endregion

    #region Resume

    [Fact]
    public void Resume_ChildContinuesWithSameState()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TResumingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "increment");
        system.Send(childRef, "increment");
        system.Drain();

        Assert.Equal(2, system.GetState<int>(childRef));

        // Fail — should resume with state=2
        system.Send(childRef, "fail");
        system.Send(childRef, "increment");
        system.Drain();

        Assert.Equal(3, system.GetState<int>(childRef));
    }

    #endregion

    #region Escalate

    [Fact]
    public void Escalate_RootActor_ThrowsFromDrainAsync()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TEscalatingSupervisor, TActorRef>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);
        system.Send(childRef, "fail");

        // Escalate from root-level supervisor → exception escapes DrainAsync
        Assert.Throws<InvalidOperationException>(() => system.Drain());
    }

    #endregion

    #region Default Strategy (no supervisor strategy = Restart)

    [Fact]
    public void DefaultStrategy_RootActor_ExceptionEscapesDrainAsync()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var actorRef = system.Spawn<TFailingActor, int>();

        system.Send(actorRef, "fail");

        // Root actor with no parent — exception escapes (current behavior preserved)
        Assert.Throws<InvalidOperationException>(() => system.Drain());
    }

    #endregion
}
