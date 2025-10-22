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

        internal void SetStartingPosition(List<Card> hand)
        {
            Hand = hand;
            // remove hand from deck
            Deck.Cards.RemoveAll(c => Hand.Contains(c));
            // shuffle deck and leave the order alone
            Random random = new Random();
            Deck.Cards = Deck.Cards.OrderBy(Random => random.Next()).ToList();
            // Set aside 6 prize cards
            PrizeCards = Deck.Cards.Take(6).ToList();
            // remove prize cards from deck
            Deck.Cards.RemoveRange(0, 6);

        }
    }
}
