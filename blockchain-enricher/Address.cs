using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace blockchain_enricher
{
    public static class Conversion
    {
        public const decimal SatoshiToBtc = 1 / (100000000m);
        public const long BtcToSatoshi = 100000000;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionContainer
    {
        [JsonProperty("txs")]
        public IList<Transaction> Transactions { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Address
    {
        [JsonProperty("addrStr")]
        public string AddressHash { get; set; }

        [JsonProperty("txApperances")]
        public long NumberOfTransactions { get; set; }

        [JsonProperty("totalReceivedSat")]
        public long TotalReceived { get; set; }
        [JsonProperty("totalSentSat")]
        public long TotalSent { get; set; }
        [JsonProperty("balanceSat")]
        public long FinalBalance { get; set; }
        
        [JsonProperty("transactions")]
        public IList<string> TransactionHashes { get; set; } 

        public bool IsDirectFind { get; set; }

        public IList<Transaction> Transactions { get; set; }

        public DateTime? GetEarliestTransactionDate()
        {
            if (Transactions.Any())
            {
                return Transactions.OrderBy(t => t.TimeInLong).First().Time;
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<string> GetOtherBitpayAddresses()
        {
            foreach (var t in Transactions)
            {
                if (t.IsAggregationTransaction(AddressHash))
                {
                    foreach (var input in t.Inputs)
                    {
                        yield return input.AddressHash;
                    }
                }
            }      
        }
    }
}