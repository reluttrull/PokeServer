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
            _logger.LogInformation($"Starting hand set with {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");
            _logger.LogInformation($"Starting prize cards set with {game.PrizeCards.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

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

        [HttpGet]
        [Route("drawcardfromdeck/{guid}")]
        public async Task<Card> DrawCardFromDeck(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new Exception("Game not found.");
            if (game.Deck.Cards.Count < 1) throw new Exception("No cards left in deck.");

            Card drawnCard = game.Deck.Cards[0];
            game.Hand.Add(drawnCard);
            game.Deck.Cards.RemoveAt(0);
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");
            return drawnCard;
        }

        [HttpPut]
        [Route("discardcard/{guid}")]
        public async Task<IActionResult> DiscardCard(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            if (game.Hand.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.Hand.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else if (game.Bench.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.Bench.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else if (game.ActivePokemon != null && game.ActivePokemon.NumberInDeck == card.NumberInDeck)
            {
                game.ActivePokemon = null;
            }
            else return NotFound("Card not in play.");

            game.DiscardPile.Add(card);

            return NoContent();
        }

        [HttpPut]
        [Route("endgame/{guid}")]
        public async Task<IActionResult> EndGame(string guid)
        {
            if (_memoryCache.TryGetValue(guid, out Game? game) && game != null)
            {
                _memoryCache.Remove(guid);
                _logger.LogInformation("Game {GameGuid} ended and removed from cache.", guid);
                return NoContent();
            }
            return NotFound("Game not found.");
        }
    }
}