namespace test_akka_persistence.Messages
{
    internal class PersistedEvent
    {
        public PersistedEvent(string iAmAnEvent)
        {
            Message = iAmAnEvent;
        }

        private string Message { get; }
    }
}