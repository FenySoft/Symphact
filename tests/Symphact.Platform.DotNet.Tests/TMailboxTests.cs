using Symphact.Core;
using Symphact.Platform.DotNet;

namespace Symphact.Platform.DotNet.Tests;

/// <summary>
/// hu: Az in-memory mailbox (TMailbox) viselkedésének tesztjei. Ez az alapköve a teljes aktor
/// runtime-nak, ezért minden egyes viselkedési garanciát teszttel lefedünk (FIFO, thread-safety,
/// üres állapot kezelés). A CFPU hardveres mailbox FIFO-jának szoftveres megfelelője.
/// <br />
/// en: Tests for the in-memory mailbox (TMailbox) behaviour. This is the foundation of the entire
/// actor runtime, so every behavioural guarantee is covered by a test (FIFO ordering, thread-safety,
/// empty-state handling). Software equivalent of the CFPU's hardware mailbox FIFO.
/// </summary>
public sealed class TMailboxTests
{
    [Fact]
    public void NewMailbox_IsEmpty()
    {
        var mailbox = new TMailbox();

        Assert.Equal(0, mailbox.Count);
    }

    [Fact]
    public void TryReceive_OnEmpty_ReturnsFalse()
    {
        var mailbox = new TMailbox();

        var received = mailbox.TryReceive(out var message);

        Assert.False(received);
        Assert.Null(message);
    }

    [Fact]
    public void Post_SingleMessage_IncrementsCount()
    {
        var mailbox = new TMailbox();

        mailbox.Post("hello");

        Assert.Equal(1, mailbox.Count);
    }

    [Fact]
    public void Post_ThenTryReceive_ReturnsMessage()
    {
        var mailbox = new TMailbox();
        mailbox.Post("hello");

        var received = mailbox.TryReceive(out var message);

        Assert.True(received);
        Assert.Equal("hello", message);
        Assert.Equal(0, mailbox.Count);
    }

    [Fact]
    public void Post_MultipleMessages_CountMatches()
    {
        var mailbox = new TMailbox();

        mailbox.Post("a");
        mailbox.Post("b");
        mailbox.Post("c");

        Assert.Equal(3, mailbox.Count);
    }

    [Fact]
    public void TryReceive_FollowsFifoOrder()
    {
        var mailbox = new TMailbox();
        mailbox.Post("first");
        mailbox.Post("second");
        mailbox.Post("third");

        mailbox.TryReceive(out var m1);
        mailbox.TryReceive(out var m2);
        mailbox.TryReceive(out var m3);

        Assert.Equal("first", m1);
        Assert.Equal("second", m2);
        Assert.Equal("third", m3);
    }

    [Fact]
    public void TryReceive_EmptiesCountToZero()
    {
        var mailbox = new TMailbox();
        mailbox.Post("x");
        mailbox.Post("y");

        mailbox.TryReceive(out _);
        mailbox.TryReceive(out _);

        Assert.Equal(0, mailbox.Count);
    }

    [Fact]
    public void Post_Null_Throws()
    {
        var mailbox = new TMailbox();

        Assert.Throws<ArgumentNullException>(() => mailbox.Post(null!));
    }

    [Fact]
    public async Task Post_ConcurrentWritesAreThreadSafe()
    {
        var mailbox = new TMailbox();
        const int threadCount = 8;
        const int messagesPerThread = 1000;

        var tasks = new Task[threadCount];

        for (var t = 0; t < threadCount; t++)
        {
            var threadIndex = t;

            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < messagesPerThread; i++)
                    mailbox.Post($"t{threadIndex}-m{i}");
            });
        }

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount * messagesPerThread, mailbox.Count);
    }

    [Fact]
    public async Task PostAndReceive_ConcurrentlyAreThreadSafe()
    {
        var mailbox = new TMailbox();
        const int messageCount = 10_000;
        var received = 0;

        var producer = Task.Run(() =>
        {
            for (var i = 0; i < messageCount; i++)
                mailbox.Post(i);
        });

        var consumer = Task.Run(() =>
        {
            while (received < messageCount)
            {
                if (mailbox.TryReceive(out _))
                    Interlocked.Increment(ref received);
            }
        });

        await Task.WhenAll(producer, consumer);

        Assert.Equal(messageCount, received);
        Assert.Equal(0, mailbox.Count);
    }
}
