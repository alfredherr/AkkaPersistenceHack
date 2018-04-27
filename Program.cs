using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Routing;
using Serilog;
using test_akka_persistence.Actors;
using test_akka_persistence.AtLeastOnceDelivery;
using test_akka_persistence.Messages;

namespace test_akka_persistence
{
    internal class Program
    {
        private static Config GetConfiguration()
        {
            var hocon = File.ReadAllText(Directory.GetCurrentDirectory() + "/Configuration/HOCONConfiguration.hocon");
            var conf = ConfigurationFactory.ParseString(hocon);
            return conf;
        }

        private static void Main(string[] args)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .WriteTo.File("/demo/log-.txt", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Information()
                .CreateLogger();

            Log.Logger = logger;

            var config = GetConfiguration();
            ActorSystem demoActorSystem = ActorSystem.Create("demoSystem", config);

            var publisherActor = demoActorSystem.ActorOf(Props.Create<DBPublisher>(), "PublisherActor");
            
    
            var routerGroup = _InitDBPersisterRouteeGroup(demoActorSystem,100);
            
            publisherActor.Tell(new BootUp(routerGroup));
            
            Console.WriteLine("Running....");
            Console.ReadLine();
        }
        private static IActorRef _InitDBPersisterRouteeGroup(ActorSystem system, int howManyWorkers)
        {
            var workersList = new List<IActorRef>();
            for (int i = 0; i < howManyWorkers; i++)
            {
                workersList.Add(system.ActorOf(Props.Create<DBPersister>(), $"worker{i}"));
            }
            
            return system.ActorOf(
                Props
                    .Empty
                    .WithRouter(new ConsistentHashingGroup(workersList.Select(c => c.Path.ToString())))
                , "DBpersisterGroup");
        }
    }
}