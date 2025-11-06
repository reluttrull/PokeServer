using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PokeServer.Model;
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

        [HttpGet]
        [Route("getalldeckbriefs")]
        public async Task<List<DeckBrief>> GetAllDeckBriefs()
        {
            List<DeckBrief> deckBriefs = new();


            using (StreamReader r = new StreamReader("TestData/TestDecks.json"))
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
    }
}