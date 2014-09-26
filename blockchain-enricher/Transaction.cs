using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace blockchain_enricher
{
    public class Transaction
    {
        [JsonIgnore] 
        private const int AggregationInputMinimum = 15;

        [JsonIgnore]
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        [JsonProperty("time")]
        public long TimeInLong { get; set; }

        [JsonIgnore]
        public DateTime Time { get { return UnixEpoch.AddSeconds(TimeInLong);  } }

        [JsonProperty("vin")]
        public IList<TransactionInput> Inputs { get; set; } 

        [JsonProperty("vout")]
        public IList<TransactionOutput> Outputs { get; set; }

        [JsonProperty("txid")]
        public string Hash { get; set; }

        [JsonProperty("valueIn")]
        public decimal ValueInDecimal { get; set; }
        [JsonIgnore]
        public long ValueIn { get { return (long)(ValueInDecimal * Conversion.BtcToSatoshi); } }

        [JsonProperty("valueOut")]
        public decimal ValueOutDecimal { get; set; }
        [JsonIgnore]
        public long ValueOut { get { return (long)(ValueOutDecimal * Conversion.BtcToSatoshi); } }

        [JsonProperty("fees")]
        public decimal FeesDecimal { get; set; }
        [JsonIgnore]
        public long Fees { get { return (long)(FeesDecimal * Conversion.BtcToSatoshi); } }

        public bool IsAggregationTransaction(string addressHash)
        {
            return Inputs.Count > AggregationInputMinimum && Inputs.Any(i => i.AddressHash == addressHash);
        }
    }
}