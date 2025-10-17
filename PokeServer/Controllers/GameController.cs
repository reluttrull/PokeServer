using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PokeServer.Model;

namespace PokeServer.Controllers
{
    [ApiController]
    [Route("game")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;
        private readonly IMemoryCache _memoryCache;

        public GameController(ILogger<GameController> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;
        }

        [HttpGet]
        [Route("getnewgame/{deckId}")]
        public async Task<GameStart> GetNewGame(int DeckId)
        {
            Game game = new Game(DeckId);
            _logger.LogInformation("New game created with DeckId: {DeckId}", DeckId);
            // TODO: retrieve each card from cache if stored (reduce API calls)
            if (game.Deck.Cards.Count == 0)
            {
                _logger.LogInformation("Populating card list for deck {DeckId}", DeckId);
                game.Deck.Cards = await ApiHelper.PopulateCardList(game.Deck.CardIds);
                _logger.LogInformation("Populated {CardCount} cards for deck {DeckId}", game.Deck.Cards.Count, DeckId);
            }

            _logger.LogInformation("Shuffling deck and drawing hand for game {GameGuid}", game.Guid);
            GameStart gameStart = new GameStart(game.Guid.ToString(), game.Deck.Cards);

            if (gameStart.Hand.Count == 0)
            {
                _logger.LogError("Failed to draw a valid starting hand for game {GameGuid}", game.Guid);
                throw new Exception("Failed to draw a valid starting hand.");
            }

            game.SetStartingHand(gameStart.Hand);

            // TODO: switch to Redis?
            if (!_memoryCache.TryGetValue(game.Guid.ToString(), out Game? value))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(3));
                _memoryCache.Set<Game>(game.Guid.ToString(), game, cacheEntryOptions);
            }
            return gameStart; // TODO: return game start data along with Guid
        }

        [HttpGet]
        [Route("checkgameactive/{guid}")]
        public async Task<bool> CheckGameActive(string guid)
        {
            if (_memoryCache.TryGetValue(guid, out Game? value)) return true;
            return false;
        }
    }
}
