using System;

namespace test_akka_persistence.Messages
{
    public class BootUp
    {
        public BootUp(string message)
        {
            Console.WriteLine(message);
        }
    }
}