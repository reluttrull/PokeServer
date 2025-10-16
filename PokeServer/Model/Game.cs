using System.Text.Json;
using System.Xml.Linq;

namespace PokeServer.Model
{
    public class Game
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public Deck Deck { get; set; }
        public List<Card> Hand { get; set; } = new List<Card>();
        public List<Card> PrizeCards { get; set; } = new List<Card>();
        public List<Card> DiscardPile { get; set; } = new List<Card>();
        public List<PokemonCard> Bench { get; set; } = new List<PokemonCard>();
        public PokemonCard? ActivePokemon { get; set; } = null;
        public Game(int deckId)
        {
            using (StreamReader r = new StreamReader("TestData/TestDecks.json"))
            {
                string json = r.ReadToEnd();
                List<Deck> decks = JsonSerializer.Deserialize<List<Deck>>(json);
                Deck deck = decks.FirstOrDefault(d => d.DeckId == deckId);
                if (deck != null)
                {
                    Deck = deck;
                }
                else
                {
                    throw new ArgumentException($"Deck with ID {deckId} not found.");
                }
            }
        }
    }
}
