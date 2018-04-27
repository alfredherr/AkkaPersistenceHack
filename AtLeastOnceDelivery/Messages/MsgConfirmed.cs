namespace test_akka_persistence.AtLeastOnceDelivery.Messages
{
    public class MsgConfirmed : IEvent
    {
        public MsgConfirmed(long deliveryId)
        {
            DeliveryId = deliveryId;
        }

        public long DeliveryId { get; }
    }
}