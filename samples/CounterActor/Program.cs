// hu: Minimal Symphact demó — egy számláló aktor (TCounterActor) Increment / Decrement / Query
//     üzeneteket fogad. A Query egy capability-tokent (válaszcímet) tartalmaz; a counter
//     visszaküldi az aktuális értéket egy TQueryReply aktornak, amely kiírja a konzolra.
//     Egyetlen futtatás ~50 ms alatt lefut, és bemutatja az alapvető API-t:
//       - TActorSystem(IPlatform) létrehozás
//       - Spawn<TActorType, TState>() új aktort indít
//       - Send(ref, msg) üzenetet küld
//       - Drain() szinkron módban feldolgozza a teljes üzenet-gráfot
//       - GetState<TState>(ref) diagnosztikai célból visszaadja az állapotot
//
// en: Minimal Symphact demo — a counter actor (TCounterActor) accepts Increment / Decrement /
//     Query messages. The Query carries a capability token (reply address); the counter
//     sends the current value to a TQueryReply actor which prints it to the console.
//     A single run completes in ~50 ms and showcases the basic API:
//       - TActorSystem(IPlatform) creation
//       - Spawn<TActorType, TState>() to start a new actor
//       - Send(ref, msg) to send a message
//       - Drain() to synchronously process the full message graph
//       - GetState<TState>(ref) for diagnostic access to actor state
//
// Run: dotnet run --project samples/CounterActor/CounterActor.csproj

using Symphact.Core;
using Symphact.Platform.DotNet;
using Symphact.Samples.CounterActor;

Console.WriteLine("Symphact CounterActor sample — sending 5 Increment + 2 Decrement + 1 Query");
Console.WriteLine();

using var system = new TActorSystem(new TDotNetPlatform());

var counter = system.Spawn<TCounterActor, int>();
var printer = system.Spawn<TQueryReply, object?>();

for (var i = 0; i < 5; i++)
    system.Send(counter, new MsgIncrement());

system.Send(counter, new MsgDecrement());
system.Send(counter, new MsgDecrement());
system.Send(counter, new MsgQuery(printer));

system.Drain();

var finalValue = system.GetState<int>(counter);
Console.WriteLine($"Final state via GetState<int>: {finalValue}");

namespace Symphact.Samples.CounterActor
{
    // hu: A számláló aktor üzenetei — immutable record-ok az aktor-modell konvencióinak megfelelően.
    // en: Messages handled by the counter actor — immutable records per actor-model convention.
    internal sealed record MsgIncrement;
    internal sealed record MsgDecrement;
    internal sealed record MsgQuery(TActorRef ReplyTo);
    internal sealed record MsgQueryReply(int Value);

    // hu: A számláló aktor: int állapot, három üzenettípus. A Handle pure az aktor állapota
    //     szempontjából — az egyetlen mellékhatás a context.Send a Query válaszának küldésére.
    // en: The counter actor: int state, three message types. Handle is pure w.r.t. actor state —
    //     the only side-effect is context.Send for delivering the Query reply.
    internal sealed class TCounterActor : TActor<int>
    {
        public override int Init() => 0;

        public override int Handle(int AState, object AMessage, IActorContext AContext)
        {
            switch (AMessage)
            {
                case MsgIncrement:
                    return AState + 1;

                case MsgDecrement:
                    return AState - 1;

                case MsgQuery query:
                    AContext.Send(query.ReplyTo, new MsgQueryReply(AState));
                    return AState;

                default:
                    return AState;
            }
        }
    }

    // hu: Reply-aktor: csak naplózza a kapott választ. A state nem fontos (object?) — ez egy
    //     "side-effect aktor", ami a Console.WriteLine-on át kommunikál a külvilággal.
    // en: Reply actor: just logs the received reply. State is irrelevant (object?) — this is a
    //     "side-effect actor" that communicates with the outside world via Console.WriteLine.
    internal sealed class TQueryReply : TActor<object?>
    {
        public override object? Init() => null;

        public override object? Handle(object? AState, object AMessage, IActorContext AContext)
        {
            if (AMessage is MsgQueryReply reply)
                Console.WriteLine($"Query reply received: counter = {reply.Value}");

            return AState;
        }
    }
}
