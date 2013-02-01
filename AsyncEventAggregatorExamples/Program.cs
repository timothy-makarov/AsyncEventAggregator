using System;
using System.Threading.Tasks;
using AsyncEventAggregator;

namespace AsyncEventAggregatorExamples
{
    internal class Program
    {
        private static void Main()
        {
            Example1();
            Example2();

            Console.WriteLine("Press <Enter> to exit.");
            Console.ReadLine();
        }

        /// <summary>
        ///     Ping-Pong.
        /// </summary>
        private static void Example1()
        {
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
        }

        static void Example2()
        {
            
        }
    }
}