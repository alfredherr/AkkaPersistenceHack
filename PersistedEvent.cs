namespace test_akka_persistence
{
    internal class PersistedEvent
    {
        private string Message { get; }
        public PersistedEvent(string iAmAnEvent)
        {
            Message = iAmAnEvent;
        }
    }
}