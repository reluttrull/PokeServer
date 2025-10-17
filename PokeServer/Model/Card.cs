using System.Text.Json.Serialization;

namespace PokeServer.Model
{
    public class Card
    {
        [JsonIgnore]
        public int DeckNumber { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
