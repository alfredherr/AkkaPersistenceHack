namespace test_akka_persistence.AtLeastOnceDelivery.Messages
{
    public class Confirm
    {
        public Confirm(long deliveryId)
        {
            DeliveryId = deliveryId;
        }

        public long DeliveryId { get; }
    }
}