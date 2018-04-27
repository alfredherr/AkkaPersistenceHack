namespace test_akka_persistence.AtLeastOnceDelivery.Messages
{
    public class MessageSent : IEvent
    {
        public MessageSent(object message)
        {
            Message = message;
        }

        public object Message { get; }
    }
}