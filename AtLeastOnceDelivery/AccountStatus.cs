using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace test_akka_persistence.AtLeastOnceDelivery
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AccountStatus
    {
        Active = 0,
        Inactive,
        Cancelled,
        Created,
        Boarded,
        Upgraded,
        Removed,
        Closed
    }
}