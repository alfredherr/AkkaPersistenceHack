using Akka.Routing;

namespace test_akka_persistence.AtLeastOnceDelivery.Messages
{
    public class PersistAccountState : IConsistentHashable
    {
        public PersistAccountState(long deliveryId, AccountState message)
        {
            DeliveryId = deliveryId;
            Message = message;
        }

        public long DeliveryId { get; }

        public AccountState Message { get; }

        public object ConsistentHashKey => Message.AccountNumber % 100;
    }
}