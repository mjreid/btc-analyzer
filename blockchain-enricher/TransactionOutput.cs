using System.Linq;
using Newtonsoft.Json;

namespace blockchain_enricher
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionOutput
    {
        [JsonProperty("value")]
        public string ValueString { get; set; }

        public long Value
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ValueString))
                    return 0;
                else
                    return (long)(decimal.Parse(ValueString) * Conversion.BtcToSatoshi);
            }
        }

        [JsonProperty("scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }

        public string Address
        {
            get { return ScriptPubKey.Addresses.First(); }
        }

        public bool HasMultipleAddresses()
        {
            return ScriptPubKey.Addresses.Count > 1;
        }
    }
}