using PokeServer.Model;
using System.Text.Json.Nodes;

namespace PokeServer
{
    public class ApiHelper
    {
        public static async Task<List<Card>> PopulateCardList(List<string> cardIds)
        {
            List<Card> cards = new List<Card>();
            for (int i = 0; i < cardIds.Count; i++)
            {
                HttpResponseMessage response = await new HttpClient().GetAsync($"https://api.tcgdex.net/v2/en/cards/{cardIds[i]}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };
                    var root = JsonNode.Parse(jsonResponse)!;
                    var jsonCategory = root["category"]?.GetValue<string>();
                    Enums.CardCategory category = (Enums.CardCategory)Enum.Parse(typeof(Enums.CardCategory), jsonCategory);
                    switch (category)
                    {
                        case Enums.CardCategory.Pokemon:
                            PokemonCard pCard = System.Text.Json.JsonSerializer.Deserialize<PokemonCard>(jsonResponse, options);
                            if (pCard != null)
                            {
                                cards.Add(pCard);
                            }
                            break;
                        case Enums.CardCategory.Trainer:
                            TrainerCard tCard = System.Text.Json.JsonSerializer.Deserialize<TrainerCard>(jsonResponse, options);
                            if (tCard != null)
                            {
                                cards.Add(tCard);
                            }
                            break;
                        case Enums.CardCategory.Energy:
                            EnergyCard eCard = System.Text.Json.JsonSerializer.Deserialize<EnergyCard>(jsonResponse, options);
                            if (eCard != null)
                            {
                                cards.Add(eCard);
                            }
                            break;
                        default:
                            Card oCard = System.Text.Json.JsonSerializer.Deserialize<Card>(jsonResponse, options);
                            if (oCard != null)
                            {
                                cards.Add(oCard);
                            }
                            break;
                    }
                }
            }
            return cards;
        }   
        public static async Task<PokemonCard> PopulatePokemonCardInfo(Card card)
        {
            HttpResponseMessage response = await new HttpClient().GetAsync($"https://api.tcgdex.net/v2/en/cards/{card.Id}");
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                var detailedCard = System.Text.Json.JsonSerializer.Deserialize<PokemonCard>(jsonResponse, options);
                if (detailedCard != null)
                {
                    return detailedCard;
                }
            }
            return (PokemonCard)card;
        }
    }
}
