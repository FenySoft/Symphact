using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A TActorRef value type viselkedésének tesztjei. A TActorRef az aktorokra mutató
/// capability token — az egyetlen módja, hogy üzenetet küldjünk egy aktor felé. Ezért
/// az értékszemantika (egyenlőség, hash, default) kritikus a router és a capability
/// registry helyes működéséhez.
/// <br />
/// en: Tests for the TActorRef value type. TActorRef is the capability token referring to
/// an actor — the only way to send a message to an actor. Value semantics (equality, hash,
/// default) are therefore critical for correct router and capability registry behaviour.
/// </summary>
public sealed class TActorRefTests
{
    [Fact]
    public void ActorRef_WithSameId_AreEqual()
    {
        var a = new TActorRef(42);
        var b = new TActorRef(42);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void ActorRef_WithDifferentIds_AreNotEqual()
    {
        var a = new TActorRef(1);
        var b = new TActorRef(2);

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void ActorRef_SameId_HaveSameHashCode()
    {
        var a = new TActorRef(100);
        var b = new TActorRef(100);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ActorRef_Default_IsInvalid()
    {
        var defaultRef = default(TActorRef);

        Assert.False(defaultRef.IsValid);
    }

    [Fact]
    public void ActorRef_WithPositiveId_IsValid()
    {
        var actorRef = new TActorRef(1);

        Assert.True(actorRef.IsValid);
    }

    [Fact]
    public void ActorRef_WithZeroId_IsInvalid()
    {
        var actorRef = new TActorRef(0);

        Assert.False(actorRef.IsValid);
    }

    [Fact]
    public void ActorRef_ToString_ContainsId()
    {
        var actorRef = new TActorRef(7);

        var text = actorRef.ToString();

        Assert.Contains("7", text);
    }

    [Fact]
    public void ActorRef_Invalid_Constant_MatchesDefault()
    {
        Assert.Equal(default, TActorRef.Invalid);
        Assert.False(TActorRef.Invalid.IsValid);
    }
}
