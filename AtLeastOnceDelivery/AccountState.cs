using System;
using System.Collections.Immutable;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace test_akka_persistence.AtLeastOnceDelivery
{
    public class AccountState
    {
          
        [JsonProperty(Order = 1)]
        public long AccountNumber { get; set; }

        [JsonProperty(Order = 2)]
        public string UserName { get; set;}

        [JsonProperty(Order = 3)]
        public double LastPaymentAmount { get; set;}

        [JsonProperty(Order = 4)]
        public DateTime LastPaymentDate { get; set;}

        [JsonProperty(Order = 5)]
        public string Inventroy { get; set;}

        [JsonProperty(Order = 6)]
        public double OpeningBalance { get;set; }

        [JsonProperty(Order = 7)]
        public decimal CurrentBalance { get; set;}

        [JsonProperty(Order = 8)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AccountStatus AccountStatus { get; set; }

        [JsonProperty(Order = 9)]
        public ImmutableDictionary<string, string> Obligations { get; set;}


    }
}