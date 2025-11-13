using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PokeServer.DeckData;
using PokeServer.Model;
using System.Collections.Specialized;
using System.Text.Json;

namespace PokeServer.Controllers
{
    [ApiController]
    [Route("deck")]
    public class DeckController : ControllerBase
    {
        private readonly ILogger<DeckController> _logger;
        private readonly IMemoryCache _memoryCache;

        public DeckController(ILogger<DeckController> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;
        }

        #region deck briefs
        [HttpGet]
        [Route("getpublicdeckbriefs")]
        public async Task<List<DeckBrief>> GetPublicDeckBriefs()
        {
            List<DeckBrief> deckBriefs = new();


            using (StreamReader r = new StreamReader("DeckData/TestDecks.json"))
            {
                string json = r.ReadToEnd();
                List<Deck> decks = JsonSerializer.Deserialize<List<Deck>>(json);
                foreach (Deck deck in decks.Where(d => d.IsDefault))
                {
                    DeckBrief deckBrief = new DeckBrief
                    {
                        DeckId = deck.DeckId,
                        Name = deck.Name,
                        Description = deck.Description
                    };
                    deckBriefs.Add(deckBrief);
                }
            }
            return deckBriefs;
        }

        [HttpGet]
        [Route("getalldeckbriefs")]
        public async Task<List<DeckBrief>> GetAllDeckBriefs()
        {
            List<DeckBrief> deckBriefs = new();


            using (StreamReader r = new StreamReader("DeckData/TestDecks.json"))
            {
                string json = r.ReadToEnd();
                List<Deck> decks = JsonSerializer.Deserialize<List<Deck>>(json);
                foreach (Deck deck in decks)
                {
                    DeckBrief deckBrief = new DeckBrief
                    {
                        DeckId = deck.DeckId,
                        Name = deck.Name,
                        Description = deck.Description
                    };
                    deckBriefs.Add(deckBrief);
                }
            }
            return deckBriefs;
        }
        #endregion deck briefs

        #region deck import
        [HttpPut]
        [Route("importdeck")]
        public async Task<IActionResult> ImportDeck([FromBody] string decklist)
        {
            // gather all card ids
            List<string> cardIds = SplitIntoCardIds(decklist);
            // translate card ids into TCGdex ids
            cardIds = TranslateCardIds(cardIds);
            // gather card data from Redis/TCGdex
            List<Card> populatedCards = await ApiHelper.PopulateCardList(cardIds);
            // perform validation
            if (populatedCards.Count != 60) return BadRequest("Deck must contain exactly 60 cards.");
            foreach (var group in populatedCards.GroupBy(c => c.Name))
            {
                if (group.Count() <= 4) continue;
                var card = group.First();
                if (card.Category == "Energy" && ((EnergyCard)card).EnergyType == "Normal") continue;
                return BadRequest($"Deck cannot contain more than 4 copies of {card.Name}.");
            };
            if (!populatedCards.Any(card => card.Category == "Pokemon" && ((PokemonCard)card).Stage == "Basic")) 
                return BadRequest("Deck must contain at least one Basic Pokemon.");
            Deck deck = new Deck() { Cards = populatedCards };
            Guid deckId = Guid.NewGuid();
            // store deck in memory for ~5 minutes
            if (!_memoryCache.TryGetValue(deckId.ToString(), out Deck? value))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                _memoryCache.Set<Deck>(deckId.ToString(), deck, cacheEntryOptions);
            }
            // return unique deck id

            return Ok(deckId);
        }

        private List<string> SplitIntoCardIds(string decklist)
        {
            var lines = decklist.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cardIds = new();
            foreach (var line in lines)
            {
                if (!char.IsDigit(line[0])) continue;
                var parts = line.Split(' ', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out int quantity))
                {
                    string name = parts[1].Trim();
                    while (quantity > 0)
                    {
                        cardIds.Add(name);
                        quantity--;
                    }
                }
            }
            return cardIds;
        }

        private List<string> TranslateCardIds(List<string> untranslatedCardIds)
        {
            List<string> translatedCardIds = new();
            foreach (string cardId in untranslatedCardIds)
            {
                string[] words = cardId.Split(" ");
                int.TryParse(words[words.Length - 1], out int id);
                if (words[words.Length - 2] == "Energy") // Limitless sometimes just lists energy without specific card ref
                {
                    string energyFullId = EnergyTranslation.EnergyCodes[words[0]];
                    translatedCardIds.Add(energyFullId);
                    continue;
                }
                string set = SetTranslation.SetCodes[words[words.Length - 2]];
                translatedCardIds.Add($"{set}-{id}");
            }
            return translatedCardIds;
        }
        #endregion deck import
    }
}