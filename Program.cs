using System;
using System.IO;
using Akka.Actor;
using Akka.Configuration;
using Serilog;
using test_akka_persistence.Actors;
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
                .WriteTo.File("C:\\dev\\test_akka_persistence\\log-.txt", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Information()
                .CreateLogger();

            Log.Logger = logger;

            var config = GetConfiguration();
            var demoActorSystem = ActorSystem.Create("demoSystem", config);


            var saverActor = demoActorSystem.ActorOf(Props.Create<SaverActor>(), "SaverActor");
            saverActor.Tell(new BootUp("Get up SaverActor!"));


            Console.WriteLine("Running....");
            Console.ReadLine();
        }
    }
}