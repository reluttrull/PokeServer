namespace PokeServer.Model
{
    public class GameStart
    {
        public string GameGuid { get; set; } = string.Empty;
        public List<Card> Hand { get; set; } = new List<Card>();
        public int Mulligans { get; set; }
        public List<List<Card>> MulliganHands { get; set; } = new List<List<Card>>();
        public GameStart(string gameGuid, List<Card> deck)
        {
            GameGuid = gameGuid;
            // if entire deck has no basic Pokemon, avoid infinite loop
            if (!deck.Any(c => c is PokemonCard && ((PokemonCard)c).Stage == "Basic")) 
                throw new Exception("No basic Pokemon in deck!");
            // starting hand must have at least one basic Pokemon, otherwise draw again and count as a mulligan
            while (true)
            {
                var random = new Random();
                List<Card> testHand = deck.OrderBy(c => random.Next()).Take(7).ToList();
                if (testHand.Any(c => c is PokemonCard && ((PokemonCard)c).Stage == "Basic"))
                {
                    Hand = testHand;
                    break;
                }
                MulliganHands.Add(testHand);
                Mulligans++;
            }
        }
    }
}
