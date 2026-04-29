using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: Watch/DeathWatch tesztek. Egy aktor figyelhet egy másikat — ha a figyelt aktor megáll,
/// a figyelő TTerminated üzenetet kap. Kaszkád stop: szülő megállítása megállítja gyerekeit is.
/// <br />
/// en: Watch/DeathWatch tests. An actor can watch another — when the watched actor stops,
/// the watcher receives a TTerminated message. Cascading stop: stopping a parent stops children.
/// </summary>
public sealed class TDeathWatchTests
{
    #region Test Actors

    private sealed record TWatcherState(List<TActorRef> Terminated);

    private sealed class TWatcherActor : TActor<TWatcherState>
    {
        public override TWatcherState Init() => new(new List<TActorRef>());

        public override TWatcherState Handle(TWatcherState AState, object AMessage, IActorContext AContext)
        {
            switch (AMessage)
            {
                case TActorRef target:
                    AContext.Watch(target);
                    break;

                case TTerminated terminated:
                    AState.Terminated.Add(terminated.Actor);
                    break;

                case ("unwatch", TActorRef unwatchTarget):
                    AContext.Unwatch(unwatchTarget);
                    break;
            }

            return AState;
        }
    }

    private sealed class TFailingChildActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "fail")
                throw new InvalidOperationException("deliberate");

            return AState + 1;
        }
    }

    private sealed class TStoppingSupervisorActor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Stop);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingChildActor, int>();

            return AState;
        }
    }

    private sealed class TParentWithChildren : TActor<List<TActorRef>>
    {
        public override List<TActorRef> Init() => new();

        public override List<TActorRef> Handle(List<TActorRef> AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn")
            {
                AState.Add(AContext.Spawn<TFailingChildActor, int>());
            }

            return AState;
        }
    }

    #endregion

    #region Watch + TTerminated

    [Fact]
    public void Watch_StoppedActor_DeliversTTerminated()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TStoppingSupervisorActor, TActorRef>();
        var watcherRef = system.Spawn<TWatcherActor, TWatcherState>();

        // Spawn child
        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);

        // Watch child
        system.Send(watcherRef, childRef);
        system.Drain();

        // Kill child
        system.Send(childRef, "fail");
        system.Drain();

        var watcherState = system.GetState<TWatcherState>(watcherRef);
        Assert.Contains(childRef, watcherState.Terminated);
    }

    [Fact]
    public void Unwatch_NoTTerminatedDelivered()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TStoppingSupervisorActor, TActorRef>();
        var watcherRef = system.Spawn<TWatcherActor, TWatcherState>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);

        // Watch then unwatch
        system.Send(watcherRef, childRef);
        system.Drain();
        system.Send(watcherRef, ("unwatch", childRef));
        system.Drain();

        // Kill child
        system.Send(childRef, "fail");
        system.Drain();

        var watcherState = system.GetState<TWatcherState>(watcherRef);
        Assert.DoesNotContain(childRef, watcherState.Terminated);
    }

    [Fact]
    public void Watch_AlreadyStopped_DeliversTTerminatedImmediately()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var supervisorRef = system.Spawn<TStoppingSupervisorActor, TActorRef>();
        var watcherRef = system.Spawn<TWatcherActor, TWatcherState>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);

        // Kill child first
        system.Send(childRef, "fail");
        system.Drain();

        // Watch after death — should immediately deliver TTerminated
        system.Send(watcherRef, childRef);
        system.Drain();

        var watcherState = system.GetState<TWatcherState>(watcherRef);
        Assert.Contains(childRef, watcherState.Terminated);
    }

    [Fact]
    public void Restart_DoesNotSendTTerminated()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        // Use a restarting supervisor
        var supervisorRef = system.Spawn<TRestartingSupervisorActor, TActorRef>();
        var watcherRef = system.Spawn<TWatcherActor, TWatcherState>();

        system.Send(supervisorRef, "spawn-child");
        system.Drain();

        var childRef = system.GetState<TActorRef>(supervisorRef);

        // Watch child
        system.Send(watcherRef, childRef);
        system.Drain();

        // Fail — triggers restart, NOT stop
        system.Send(childRef, "fail");
        system.Drain();

        var watcherState = system.GetState<TWatcherState>(watcherRef);
        Assert.DoesNotContain(childRef, watcherState.Terminated);
    }

    #endregion

    #region Cascading Stop

    [Fact]
    public void StopParent_StopsAllChildren()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        // Use a top-level supervisor that stops on failure
        var topRef = system.Spawn<TTopStoppingSupervisor, TActorRef>();

        system.Send(topRef, "spawn-parent");
        system.Drain();

        var parentRef = system.GetState<TActorRef>(topRef);
        system.Send(parentRef, "spawn");
        system.Send(parentRef, "spawn");
        system.Drain();

        var children = system.GetChildren(parentRef);
        Assert.Equal(2, children.Count);

        // Stop parent via failure
        system.Send(parentRef, "fail-self");
        system.Drain();

        Assert.True(system.IsStopped(parentRef));
        Assert.True(system.IsStopped(children[0]));
        Assert.True(system.IsStopped(children[1]));
    }

    [Fact]
    public void StopParent_WatchersOfChildrenGetTTerminated()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        var topRef = system.Spawn<TTopStoppingSupervisor, TActorRef>();
        var watcherRef = system.Spawn<TWatcherActor, TWatcherState>();

        system.Send(topRef, "spawn-parent");
        system.Drain();

        var parentRef = system.GetState<TActorRef>(topRef);
        system.Send(parentRef, "spawn");
        system.Drain();

        var children = system.GetChildren(parentRef);
        var childRef = children[0];

        // Watch child
        system.Send(watcherRef, childRef);
        system.Drain();

        // Stop parent → cascades to child
        system.Send(parentRef, "fail-self");
        system.Drain();

        var watcherState = system.GetState<TWatcherState>(watcherRef);
        Assert.Contains(childRef, watcherState.Terminated);
    }

    #endregion

    #region Additional Test Actors

    private sealed class TRestartingSupervisorActor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Restart);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
                return AContext.Spawn<TFailingChildActor, int>();

            return AState;
        }
    }

    private sealed class TTopStoppingSupervisor : TActor<TActorRef>
    {
        public override ISupervisorStrategy? SupervisorStrategy =>
            new TOneForOneStrategy(_ => ESupervisorDirective.Stop);

        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-parent")
                return AContext.Spawn<TCascadeParent, List<TActorRef>>();

            return AState;
        }
    }

    private sealed class TCascadeParent : TActor<List<TActorRef>>
    {
        public override List<TActorRef> Init() => new();

        public override List<TActorRef> Handle(List<TActorRef> AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn")
            {
                AState.Add(AContext.Spawn<TFailingChildActor, int>());
                return AState;
            }

            if (AMessage is "fail-self")
                throw new InvalidOperationException("parent failure");

            return AState;
        }
    }

    #endregion
}
