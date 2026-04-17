using NeuronOS.Core;

namespace NeuronOS.Core.Tests;

/// <summary>
/// hu: A TActorSystem és TActor integráció tesztjei. Ez a runtime szíve: aktor spawn,
/// üzenet küldés, üzenet feldolgozás, állapot transzformáció, rendezett leállás.
/// <br />
/// en: Tests for TActorSystem and TActor integration. This is the heart of the runtime:
/// actor spawning, message sending, message processing, state transformation, orderly shutdown.
/// </summary>
public sealed class TActorSystemTests
{
    /// <summary>
    /// hu: Egyszerű számláló aktor: az állapota egy egész szám, minden "increment" üzenetre nő.
    /// <br />
    /// en: Simple counter actor: state is an integer, incremented on each "increment" message.
    /// </summary>
    private sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext) => AMessage switch
        {
            "increment" => AState + 1,
            "decrement" => AState - 1,
            _ => AState
        };
    }

    [Fact]
    public void Spawn_ReturnsValidActorRef()
    {
        using var system = new TActorSystem();

        var actorRef = system.Spawn<TCounterActor, int>();

        Assert.True(actorRef.IsValid);
    }

    [Fact]
    public void Spawn_Twice_ReturnsDifferentRefs()
    {
        using var system = new TActorSystem();

        var a = system.Spawn<TCounterActor, int>();
        var b = system.Spawn<TCounterActor, int>();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetState_InitialValue_IsFromInit()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        var state = system.GetState<int>(actorRef);

        Assert.Equal(0, state);
    }

    [Fact]
    public async Task Send_SingleMessage_AdvancesState()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");
        await system.DrainAsync();

        Assert.Equal(1, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task Send_MultipleMessages_AccumulatesState()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");
        system.Send(actorRef, "increment");
        system.Send(actorRef, "increment");
        system.Send(actorRef, "decrement");
        await system.DrainAsync();

        Assert.Equal(2, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task Send_UnknownMessage_LeavesStateUnchanged()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Send(actorRef, "increment");
        system.Send(actorRef, "some-unknown-command");
        await system.DrainAsync();

        Assert.Equal(1, system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task Send_IsolatesStateBetweenActors()
    {
        using var system = new TActorSystem();
        var a = system.Spawn<TCounterActor, int>();
        var b = system.Spawn<TCounterActor, int>();

        system.Send(a, "increment");
        system.Send(a, "increment");
        system.Send(b, "increment");
        await system.DrainAsync();

        Assert.Equal(2, system.GetState<int>(a));
        Assert.Equal(1, system.GetState<int>(b));
    }

    [Fact]
    public void Send_ToInvalidRef_Throws()
    {
        using var system = new TActorSystem();

        Assert.Throws<InvalidOperationException>(() =>
            system.Send(TActorRef.Invalid, "x"));
    }

    [Fact]
    public void Send_NullMessage_Throws()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        Assert.Throws<ArgumentNullException>(() =>
            system.Send(actorRef, null!));
    }

    [Fact]
    public void GetState_AfterShutdown_Throws()
    {
        var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        system.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            system.GetState<int>(actorRef));
    }

    [Fact]
    public async Task DrainAsync_WithoutMessages_CompletesImmediately()
    {
        using var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();

        await system.DrainAsync();

        Assert.Equal(0, system.GetState<int>(actorRef));
    }

    [Fact]
    public void GetState_UnknownRef_Throws()
    {
        using var system = new TActorSystem();

        Assert.Throws<InvalidOperationException>(() =>
            system.GetState<int>(new TActorRef(9999)));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var system = new TActorSystem();
        system.Spawn<TCounterActor, int>();

        system.Dispose();
        system.Dispose();
    }

    [Fact]
    public void Spawn_AfterDispose_ThrowsObjectDisposedException()
    {
        var system = new TActorSystem();
        system.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            system.Spawn<TCounterActor, int>());
    }

    [Fact]
    public void Send_AfterDispose_ThrowsObjectDisposedException()
    {
        var system = new TActorSystem();
        var actorRef = system.Spawn<TCounterActor, int>();
        system.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            system.Send(actorRef, "increment"));
    }

    [Fact]
    public void Send_ToNonExistentButValidRef_Throws()
    {
        using var system = new TActorSystem();

        var fakeRef = new TActorRef(999);

        Assert.True(fakeRef.IsValid);
        Assert.Throws<InvalidOperationException>(() =>
            system.Send(fakeRef, "test"));
    }
}
