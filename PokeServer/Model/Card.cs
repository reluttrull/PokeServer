using System.Text.Json.Serialization;

namespace PokeServer.Model
{
    [JsonDerivedType(typeof(Card), "Card")]
    [JsonDerivedType(typeof(PokemonCard), "PokemonCard")]
    [JsonDerivedType(typeof(EnergyCard), "EnergyCard")]
    [JsonDerivedType(typeof(TrainerCard), "TrainerCard")]
    public class Card
    {
        public Guid NumberInDeck { get; set; } = Guid.NewGuid();
        public string Category { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
