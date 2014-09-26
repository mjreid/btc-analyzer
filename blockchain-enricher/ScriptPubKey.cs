using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace blockchain_enricher
{
    public class ScriptPubKey
    {
        [JsonProperty("addresses")]
        public IList<string> Addresses { get; set; }
    }
}