using System.Collections.Concurrent;
using Symphact.Core;

namespace Symphact.Platform.DotNet;

/// <summary>
/// hu: In-memory, thread-safe FIFO mailbox implementáció. A .NET ConcurrentQueue-ra épül,
/// amely lock-free MPMC (multi-producer multi-consumer) szemantikát biztosít. Ez a referencia
/// implementáció tetszőleges .NET hoszton fut; a jövőbeli CFPU-backed implementációk
/// ugyanezt az IMailbox interfészt fogják megvalósítani.
/// <br />
/// en: In-memory, thread-safe FIFO mailbox implementation. Built on .NET ConcurrentQueue, which
/// provides lock-free MPMC (multi-producer multi-consumer) semantics. This reference implementation
/// runs on any .NET host; future CFPU-backed implementations will implement the same IMailbox
/// interface.
/// </summary>
public sealed class TMailbox : IMailbox
{
    private readonly ConcurrentQueue<object> FQueue = new();

    /// <inheritdoc />
    public int Count => FQueue.Count;

    /// <inheritdoc />
    public void Post(object AMessage)
    {
        ArgumentNullException.ThrowIfNull(AMessage);

        FQueue.Enqueue(AMessage);
    }

    /// <inheritdoc />
    public bool TryReceive(out object? AMessage)
    {
        if (FQueue.TryDequeue(out var item))
        {
            AMessage = item;
            return true;
        }

        AMessage = null;
        return false;
    }
}
