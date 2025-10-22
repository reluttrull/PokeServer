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

                if (db.KeyExists(cardIds[i])) // if don't need to call api
                {
                    cardJson = db.StringGet(cardIds[i]);
                }
                else // if we do need to call api
                {
                    HttpResponseMessage response = await new HttpClient().GetAsync($"https://api.tcgdex.net/v2/en/cards/{cardIds[i]}");
                    if (!response.IsSuccessStatusCode) throw new HttpRequestException("failed to retrieve card data from TCGDex API");
                    cardJson = await response.Content.ReadAsStringAsync();
                    db.StringSet(cardIds[i], cardJson);
                }
                // deserialize what we got
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
            return cards;
        }
    }
}
