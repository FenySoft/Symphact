using Symphact.Core;

namespace Symphact.Core.Tests;

/// <summary>
/// hu: A supervisor stratégia típusok (ESupervisorDirective, ISupervisorStrategy, TOneForOneStrategy)
/// viselkedési tesztjei. Ezek a supervision alapépítőkövei — minden jövőbeli failure handling
/// ezekre épül.
/// <br />
/// en: Behavioural tests for the supervisor strategy types (ESupervisorDirective, ISupervisorStrategy,
/// TOneForOneStrategy). These are the foundation of supervision — all future failure handling
/// builds on them.
/// </summary>
public sealed class TSupervisorStrategyTests
{
    #region ESupervisorDirective

    [Fact]
    public void ESupervisorDirective_HasFourValues()
    {
        var values = Enum.GetValues<ESupervisorDirective>();

        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(ESupervisorDirective.Resume, 0)]
    [InlineData(ESupervisorDirective.Restart, 1)]
    [InlineData(ESupervisorDirective.Stop, 2)]
    [InlineData(ESupervisorDirective.Escalate, 3)]
    public void ESupervisorDirective_HasExpectedValues(ESupervisorDirective ADirective, int AExpectedValue)
    {
        Assert.Equal(AExpectedValue, (int)ADirective);
    }

    #endregion

    #region TOneForOneStrategy — default decider

    [Fact]
    public void OneForOne_DefaultDecider_ReturnsRestart()
    {
        var strategy = new TOneForOneStrategy();
        var childRef = new TActorRef(1);

        var directive = strategy.Decide(childRef, new InvalidOperationException("test"));

        Assert.Equal(ESupervisorDirective.Restart, directive);
    }

    [Fact]
    public void OneForOne_DefaultDecider_ReturnsRestart_ForAnyException()
    {
        var strategy = new TOneForOneStrategy();
        var childRef = new TActorRef(42);

        Assert.Equal(ESupervisorDirective.Restart, strategy.Decide(childRef, new ArgumentException("arg")));
        Assert.Equal(ESupervisorDirective.Restart, strategy.Decide(childRef, new NullReferenceException()));
        Assert.Equal(ESupervisorDirective.Restart, strategy.Decide(childRef, new TimeoutException()));
    }

    #endregion

    #region TOneForOneStrategy — custom decider

    [Fact]
    public void OneForOne_CustomDecider_ReturnsStop_ForArgumentException()
    {
        var strategy = new TOneForOneStrategy(AException =>
            AException is ArgumentException ? ESupervisorDirective.Stop : ESupervisorDirective.Restart);

        var childRef = new TActorRef(1);

        Assert.Equal(ESupervisorDirective.Stop, strategy.Decide(childRef, new ArgumentException("bad")));
        Assert.Equal(ESupervisorDirective.Restart, strategy.Decide(childRef, new InvalidOperationException("other")));
    }

    [Fact]
    public void OneForOne_CustomDecider_CanReturnEscalate()
    {
        var strategy = new TOneForOneStrategy(_ => ESupervisorDirective.Escalate);
        var childRef = new TActorRef(5);

        Assert.Equal(ESupervisorDirective.Escalate, strategy.Decide(childRef, new Exception("fatal")));
    }

    [Fact]
    public void OneForOne_CustomDecider_CanReturnResume()
    {
        var strategy = new TOneForOneStrategy(_ => ESupervisorDirective.Resume);
        var childRef = new TActorRef(3);

        Assert.Equal(ESupervisorDirective.Resume, strategy.Decide(childRef, new Exception("minor")));
    }

    #endregion

    #region TOneForOneStrategy — null decider

    [Fact]
    public void OneForOne_NullDecider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TOneForOneStrategy(null!));
    }

    #endregion
}
