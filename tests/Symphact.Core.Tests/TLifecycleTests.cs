using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: Az aktor lifecycle hook-ok (PreStart, PostStop, PreRestart, PostRestart) tesztjei.
/// Ezek a supervision alapjai: az aktor életciklus-események megfelelő sorrendben és időben
/// hívódnak.
/// <br />
/// en: Tests for actor lifecycle hooks (PreStart, PostStop, PreRestart, PostRestart).
/// These are the foundation of supervision: actor lifecycle events are invoked in correct
/// order and timing.
/// </summary>
public sealed class TLifecycleTests
{
    #region Test Actors

    private sealed record TLifecycleState(List<string> Events);

    private sealed class TLifecycleActor : TActor<TLifecycleState>
    {
        public List<string> LifecycleEvents { get; } = new();

        public override TLifecycleState Init() => new(LifecycleEvents);

        public override TLifecycleState Handle(TLifecycleState AState, object AMessage, IActorContext AContext)
        {
            AState.Events.Add($"Handle:{AMessage}");
            return AState;
        }

        public override void PreStart()
        {
            LifecycleEvents.Add("PreStart");
        }

        public override void PostStop()
        {
            LifecycleEvents.Add("PostStop");
        }

        public override void PreRestart(Exception AException)
        {
            LifecycleEvents.Add($"PreRestart:{AException.Message}");
        }

        public override void PostRestart(Exception AException)
        {
            LifecycleEvents.Add($"PostRestart:{AException.Message}");
        }
    }

    private sealed class TNoOverrideActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext) => AState + 1;
    }

    #endregion

    #region PreStart

    [Fact]
    public void Spawn_CallsPreStart()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        var actorRef = system.Spawn<TLifecycleActor, TLifecycleState>();

        var state = system.GetState<TLifecycleState>(actorRef);
        Assert.Contains("PreStart", state.Events);
    }

    [Fact]
    public void Spawn_PreStart_CalledBeforeAnyMessage()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        var actorRef = system.Spawn<TLifecycleActor, TLifecycleState>();
        system.Send(actorRef, "msg1");
        system.Drain();

        var state = system.GetState<TLifecycleState>(actorRef);
        Assert.Equal("PreStart", state.Events[0]);
        Assert.Equal("Handle:msg1", state.Events[1]);
    }

    [Fact]
    public void Spawn_NoOverride_DoesNotThrow()
    {
        using var system = new TActorSystem(new TDotNetPlatform());

        var actorRef = system.Spawn<TNoOverrideActor, int>();

        Assert.Equal(0, system.GetState<int>(actorRef));
    }

    #endregion
}
