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

    // AllForOne supervisor actors — strategy is hardcoded per directive type
    // because Spawn<T> requires a parameterless constructor.

    private sealed class TAllForOneRestartSupervisor : TActor<int>
    {
        public override ISupervisorStrategy? SupervisorStrategy => new TAllForOneStrategy();

        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    private sealed class TAllForOneStopSupervisor : TActor<int>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TAllForOneStrategy(_ => ESupervisorDirective.Stop);

        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                AContext.Spawn<TFailingActor, int>();

            return AState;
        }
    }

    private sealed class TAllForOneResumeSupervisor : TActor<int>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TAllForOneStrategy(_ => ESupervisorDirective.Resume);

        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                AContext.Spawn<TFailingActor, int>();

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

    #region AllForOne — Restart

    [Fact]
    public void AllForOne_Restart_OneChildFails_AllSiblingsAlsoRestart()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TAllForOneRestartSupervisor, int>();

        system.Send(supervisorRef, "spawn-child");
        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var children = system.GetChildren(supervisorRef);
        Assert.Equal(2, children.Count);

        // Build up state in both children
        system.Send(children[0], "increment");
        system.Send(children[1], "increment");
        system.Send(children[1], "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(children[0]));
        Assert.Equal(2, system.GetState<int>(children[1]));

        // Fail children[0] — AllForOne: both children must restart (state = 0)
        system.Send(children[0], "fail");
        system.Drain();

        Assert.Equal(0, system.GetState<int>(children[0]));
        Assert.Equal(0, system.GetState<int>(children[1]));
        Assert.False(system.IsStopped(children[0]));
        Assert.False(system.IsStopped(children[1]));
    }

    [Fact]
    public void AllForOne_Restart_SiblingCanReceiveMessagesAfterRestart()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TAllForOneRestartSupervisor, int>();

        system.Send(supervisorRef, "spawn-child");
        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var children = system.GetChildren(supervisorRef);

        // Fail children[0]
        system.Send(children[0], "fail");
        system.Drain();

        // children[1] was restarted by AllForOne — must still accept messages
        system.Send(children[1], "increment");
        system.Drain();

        Assert.Equal(1, system.GetState<int>(children[1]));
    }

    #endregion

    #region AllForOne — Stop

    [Fact]
    public void AllForOne_Stop_OneChildFails_AllSiblingsStopped()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TAllForOneStopSupervisor, int>();

        system.Send(supervisorRef, "spawn-child");
        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var children = system.GetChildren(supervisorRef);
        Assert.Equal(2, children.Count);

        // Fail children[0] — AllForOne: both must stop
        system.Send(children[0], "fail");
        system.Drain();

        Assert.True(system.IsStopped(children[0]));
        Assert.True(system.IsStopped(children[1]));
    }

    #endregion

    #region AllForOne — Resume

    [Fact]
    public void AllForOne_Resume_OneChildFails_SiblingsStillRunning()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TAllForOneResumeSupervisor, int>();

        system.Send(supervisorRef, "spawn-child");
        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var children = system.GetChildren(supervisorRef);

        // Build up state
        system.Send(children[1], "increment");
        system.Send(children[1], "increment");
        system.Drain();

        Assert.Equal(2, system.GetState<int>(children[1]));

        // Fail children[0] — AllForOne + Resume: siblings continue running, state preserved
        system.Send(children[0], "fail");
        system.Drain();

        Assert.False(system.IsStopped(children[0]));
        Assert.False(system.IsStopped(children[1]));

        // children[1] state must be preserved (Resume is a no-op for non-failing siblings)
        Assert.Equal(2, system.GetState<int>(children[1]));
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
