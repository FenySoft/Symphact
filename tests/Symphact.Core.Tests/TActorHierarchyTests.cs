using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: Szülő-gyerek aktor hierarchia tesztjei. A context.Spawn létrehozza a gyerek aktort,
/// és automatikusan felépíti a szülő-gyerek kapcsolatot.
/// <br />
/// en: Parent-child actor hierarchy tests. context.Spawn creates the child actor and
/// automatically establishes the parent-child relationship.
/// </summary>
public sealed class TActorHierarchyTests
{
    #region Test Actors

    private sealed record TParentState(TActorRef ChildRef);

    private sealed class TParentActor : TActor<TParentState>
    {
        public override TParentState Init() => new(TActorRef.Invalid);

        public override TParentState Handle(TParentState AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-child")
            {
                var childRef = AContext.Spawn<TChildActor, int>();
                return AState with { ChildRef = childRef };
            }

            return AState;
        }
    }

    private sealed class TChildActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "increment")
                return AState + 1;

            return AState;
        }
    }

    private sealed class TGrandparentActor : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn-parent")
                return AContext.Spawn<TParentActor, TParentState>();

            return AState;
        }
    }

    #endregion

    #region Context.Spawn

    [Fact]
    public void ContextSpawn_CreatesValidChildRef()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var parentRef = system.Spawn<TParentActor, TParentState>();

        system.Send(parentRef, "spawn-child");
        system.Drain();

        var parentState = system.GetState<TParentState>(parentRef);
        Assert.True(parentState.ChildRef.IsValid);
    }

    [Fact]
    public void ContextSpawn_ChildIsAliveAndProcessesMessages()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var parentRef = system.Spawn<TParentActor, TParentState>();

        system.Send(parentRef, "spawn-child");
        system.Drain();

        var parentState = system.GetState<TParentState>(parentRef);
        system.Send(parentState.ChildRef, "increment");
        system.Send(parentState.ChildRef, "increment");
        system.Drain();

        var childState = system.GetState<int>(parentState.ChildRef);
        Assert.Equal(2, childState);
    }

    [Fact]
    public void ContextSpawn_ChildPreStartIsCalled()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var parentRef = system.Spawn<TSpawningParentWithLifecycleChild, TActorRef>();

        system.Send(parentRef, "spawn");
        system.Drain();

        var childRef = system.GetState<TActorRef>(parentRef);
        var childState = system.GetState<List<string>>(childRef);
        Assert.Contains("PreStart", childState);
    }

    [Fact]
    public void RootActor_HasNoParent()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var rootRef = system.Spawn<TChildActor, int>();

        // Root actor's parent is Invalid (no parent)
        var parent = system.GetParent(rootRef);
        Assert.Equal(TActorRef.Invalid, parent);
    }

    [Fact]
    public void ContextSpawn_ChildHasCorrectParent()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var parentRef = system.Spawn<TParentActor, TParentState>();

        system.Send(parentRef, "spawn-child");
        system.Drain();

        var parentState = system.GetState<TParentState>(parentRef);
        var childParent = system.GetParent(parentState.ChildRef);
        Assert.Equal(parentRef, childParent);
    }

    [Fact]
    public void ContextSpawn_ParentKnowsItsChildren()
    {
        using var system = new TActorSystem(new TDotNetPlatform());
        var parentRef = system.Spawn<TParentActor, TParentState>();

        system.Send(parentRef, "spawn-child");
        system.Drain();

        var children = system.GetChildren(parentRef);
        var parentState = system.GetState<TParentState>(parentRef);
        Assert.Contains(parentState.ChildRef, children);
    }

    #endregion

    #region Helper Actors

    private sealed class TLifecycleChildActor : TActor<List<string>>
    {
        public override List<string> Init() => new();

        public override List<string> Handle(List<string> AState, object AMessage, IActorContext AContext) => AState;

        public override void PreStart()
        {
            // We can't easily access state here, so we'll verify via a trick:
            // The test will check state after spawn, PreStart is called after Init
        }
    }

    private sealed class TSpawningParentWithLifecycleChild : TActor<TActorRef>
    {
        public override TActorRef Init() => TActorRef.Invalid;

        public override TActorRef Handle(TActorRef AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is "spawn")
                return AContext.Spawn<TPreStartTrackingChild, List<string>>();

            return AState;
        }
    }

    private sealed class TPreStartTrackingChild : TActor<List<string>>
    {
        private List<string>? FEvents;

        public override List<string> Init()
        {
            FEvents = new List<string>();
            return FEvents;
        }

        public override List<string> Handle(List<string> AState, object AMessage, IActorContext AContext) => AState;

        public override void PreStart()
        {
            FEvents?.Add("PreStart");
        }
    }

    #endregion
}
