using System;
using Akka.Actor;

namespace test_akka_persistence.Messages
{
    public class BootUp
    {
        public int IntMessage;
        public string StringMessage;
        public IActorRef ActorMessage;
        
        
        public BootUp(IActorRef message)
        {
            ActorMessage = message;
        }
        
        public BootUp(string message)
        {
            StringMessage = message;
        }
        public BootUp(int message)
        {
            IntMessage = message;
        }
    }
}