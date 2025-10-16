using Microsoft.AspNetCore.Mvc;
using PokeServer.Model;

namespace PokeServer.Controllers
{
    [ApiController]
    [Route("game")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;

        public GameController(ILogger<GameController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("getnewgame/{deckId}")]
        public async Task<string> GetNewGame(int DeckId)
        {
            Game game = new Game(DeckId);
            _logger.LogInformation("New game created with DeckId: {DeckId}", DeckId);
            if (game.Deck.Cards.Count == 0)
            {
                _logger.LogInformation("Populating card list for deck {DeckId}", DeckId);
                game.Deck.Cards = await ApiHelper.PopulateCardList(game.Deck.CardIds);
                _logger.LogInformation("Populated {CardCount} cards for deck {DeckId}", game.Deck.Cards.Count, DeckId);
            }
            // TODO: retrieve each card from cache or API
            // TODO: persist game state to data store
            return game.Deck.Name; // proving deck deserialization was successful
        }
    }
}
