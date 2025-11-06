using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeServer.Model
{
    public class Deck
    {
        public int DeckId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public List<string> CardIds { get; set; } = new List<string>();
        [JsonIgnore]
        public List<Card> Cards { get; set; } = new List<Card>();

    }
}
