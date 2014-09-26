using Newtonsoft.Json;

namespace blockchain_enricher
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionInput
    {
        [JsonProperty("addr")]
        public string AddressHash { get; set; }

        [JsonProperty("valueSat")]
        public long Value { get; set; }
    }
}