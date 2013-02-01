## Asynchronous Event Aggregator
### C# .NET TPL Based Event Aggregator with Subscribe/Publish Interface

Event Aggregator aggregates events from multiple objects into itself, passing that same event onto its observers.

### Example
<pre>
  var p1 = new Program();
  var p2 = new Program();

  p1.Subscribe<Ping>(
      async p =>
          {
              Console.Write("Ping... ");
              await Task.Delay(250);
              await p1.Publish(new Pong().AsTask());
          });

  p2.Subscribe<Pong>(
      async p =>
          {
              Console.WriteLine("Pong!");
              await Task.Delay(500);
              await p2.Publish(new Ping().AsTask());
          });

  p2.Publish(new Ping().AsTask());

  Console.ReadLine();

  p1.Unsubscribe<Ping>();
  p2.Unsubscribe<Pong>();
</pre>
