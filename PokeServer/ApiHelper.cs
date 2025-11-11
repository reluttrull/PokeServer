using PokeServer.Model;
using System.Text.Json.Nodes;
using StackExchange.Redis;

namespace PokeServer
{
    public class ApiHelper
    {
        public static async Task<List<Card>> PopulateCardList(List<string> cardIds)
        {
            string connectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            var redis = ConnectionMultiplexer.Connect(connectionString);
            IDatabase db = redis.GetDatabase();

            List<Card> cards = new List<Card>();
            for (int i = 0; i < cardIds.Count; i++)
            {
                string cardJson = "";

                if (db.KeyExists(cardIds[i])) // if we already have cached card
                {
                    cardJson = db.StringGet(cardIds[i]);
                }
                else // if we do need to call api
                {
                    HttpResponseMessage response = await new HttpClient().GetAsync($"https://api.tcgdex.net/v2/en/cards/{cardIds[i]}");
                    if (!response.IsSuccessStatusCode)
                    {
                        var splitId = cardIds[i].Split('-');
                        string paddedId = splitId[1].PadLeft(3, '0');
                        string fullPaddedId = $"{splitId[0]}-{paddedId}";
                        response = await new HttpClient().GetAsync($"https://api.tcgdex.net/v2/en/cards/{fullPaddedId}");
                        if (!response.IsSuccessStatusCode) throw new HttpRequestException("failed to retrieve card data from TCGDex API");
                    }
                    cardJson = await response.Content.ReadAsStringAsync();
                    db.StringSet(cardIds[i], cardJson);
                }
                // deserialize whatever we got
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                var root = JsonNode.Parse(cardJson)!;
                var jsonCategory = root["category"]?.GetValue<string>();
                Enums.CardCategory category = (Enums.CardCategory)Enum.Parse(typeof(Enums.CardCategory), jsonCategory);
                switch (category)
                {
                    case Enums.CardCategory.Pokemon:
                        PokemonCard pCard = System.Text.Json.JsonSerializer.Deserialize<PokemonCard>(cardJson, options);
                        if (pCard != null)
                        {
                            cards.Add(pCard);
                        }
                        break;
                    case Enums.CardCategory.Trainer:
                        TrainerCard tCard = System.Text.Json.JsonSerializer.Deserialize<TrainerCard>(cardJson, options);
                        if (tCard != null)
                        {
                            cards.Add(tCard);
                        }
                        break;
                    case Enums.CardCategory.Energy:
                        EnergyCard eCard = System.Text.Json.JsonSerializer.Deserialize<EnergyCard>(cardJson, options);
                        if (eCard != null)
                        {
                            cards.Add(eCard);
                        }
                        break;
                    default:
                        Card oCard = System.Text.Json.JsonSerializer.Deserialize<Card>(cardJson, options);
                        if (oCard != null)
                        {
                            cards.Add(oCard);
                        }
                        break;
                }
            }
            redis.Close();
            return cards;
        }

        public static async Task<List<string>> GetValidEvolutionNames(string pokemonName)
        {
            HttpResponseMessage response = await new HttpClient().GetAsync($"http://api.tcgdex.net/v2/en/cards?evolveFrom={pokemonName}");
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("failed to retrieve evolution data from TCGDex API");
            string responseJson = await response.Content.ReadAsStringAsync();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var root = JsonNode.Parse(responseJson)!;
            //var dataArray = root["data"]!.AsArray();
            var dataArray = root.AsArray();
            HashSet<string> evolutionNames = new HashSet<string>();
            foreach (var item in dataArray)
            {
                PokemonCard pCard = System.Text.Json.JsonSerializer.Deserialize<PokemonCard>(item.ToJsonString(), options);
                if (pCard != null && !string.IsNullOrEmpty(pCard.Name))
                {
                    evolutionNames.Add(pCard.Name);
                }
            }
            return evolutionNames.ToList();
        }
    }
}
