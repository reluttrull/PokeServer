using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders.Physical;
using PokeServer.Model;
using System;

namespace PokeServer.Controllers
{
    [ApiController]
    [Route("game")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHubContext<NotificationHub> _hubContext;
        private int memoryCacheTimeoutHours = 3;

        public GameController(ILogger<GameController> logger, IMemoryCache memoryCache, IHubContext<NotificationHub> hubContext)
        {
            _logger = logger;
            _memoryCache = memoryCache;
            _hubContext = hubContext;
        }

        #region game management

        [HttpGet]
        [Route("getnewgame/{deckId}")]
        public async Task<IActionResult> GetNewGame(int DeckId)
        {
            // create Game object
            Game game = new Game(DeckId);
            _logger.LogInformation("New game created with DeckId: {DeckId}", DeckId);

            // populate deck from deck matching specified DeckId
            if (game.Deck.Cards.Count == 0)
            {
                _logger.LogInformation("Populating card list for deck {DeckId}", DeckId);
                game.Deck.Cards = await ApiHelper.PopulateCardList(game.Deck.CardIds);
                _logger.LogInformation("Populated {CardCount} cards for deck {DeckId}", game.Deck.Cards.Count, DeckId);
            }

            _logger.LogInformation("Shuffling deck and drawing hand for game {GameGuid}...", game.Guid);
            // create GameStart object (draw starting hand)
            GameStart gameStart = new GameStart(game.Guid.ToString(), game.Deck.Cards);

            if (gameStart.Hand.Count == 0)
            {
                _logger.LogError("Failed to draw a valid starting hand for game {GameGuid}", game.Guid);
                return BadRequest("Failed to draw a valid starting hand.");
            }

            // populate game object with starting hand and draw prize cards
            game.SetStartingPosition(gameStart.Hand);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.GAME_STARTED, new List<Card>(gameStart.Hand)));
            foreach (List<Card> mulliganHand in gameStart.MulliganHands)
            {
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DRAW_MULLIGAN, mulliganHand));
            }
            _logger.LogInformation($"Starting hand set with {game.Hand.Count} cards.");
            _logger.LogInformation($"Starting prize cards set with {game.PrizeCards.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

            // save game to memory cache
            if (!_memoryCache.TryGetValue(game.Guid.ToString(), out Game? value))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(memoryCacheTimeoutHours));
                _memoryCache.Set<Game>(game.Guid.ToString(), game, cacheEntryOptions);
            }
            return Ok(gameStart);
        }

        [HttpGet]
        [Route("getnewgamefromimporteddeck/{deckGuid}")]
        public async Task<IActionResult> GetNewGameFromImportedDeck(string deckGuid)
        {
            if (!_memoryCache.TryGetValue(deckGuid, out Deck? deckValue)) return NotFound("Deck not found.");
            Game game = new Game(deckValue);
            _logger.LogInformation("Shuffling deck and drawing hand for game {GameGuid}...", game.Guid);
            // create GameStart object (draw starting hand)
            GameStart gameStart = new GameStart(game.Guid.ToString(), game.Deck.Cards);

            if (gameStart.Hand.Count == 0)
            {
                _logger.LogError("Failed to draw a valid starting hand for game {GameGuid}", game.Guid);
                return BadRequest("Failed to draw a valid starting hand.");
            }

            // populate game object with starting hand and draw prize cards
            game.SetStartingPosition(gameStart.Hand);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.GAME_STARTED, new List<Card>(gameStart.Hand)));
            foreach (List<Card> mulliganHand in gameStart.MulliganHands)
            {
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DRAW_MULLIGAN, mulliganHand));
            }
            _logger.LogInformation($"Starting hand set with {game.Hand.Count} cards.");
            _logger.LogInformation($"Starting prize cards set with {game.PrizeCards.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

            // save game to memory cache
            if (!_memoryCache.TryGetValue(game.Guid.ToString(), out Game? value))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(memoryCacheTimeoutHours));
                _memoryCache.Set<Game>(game.Guid.ToString(), game, cacheEntryOptions);
            }
            return Ok(gameStart);
        }

        [HttpGet]
        [Route("checkgameactive/{guid}")]
        public async Task<bool> CheckGameActive(string guid)
        {
            if (_memoryCache.TryGetValue(guid, out Game? value)) return true;
            return false;
        }

        [HttpPut]
        [Route("endgame/{guid}")]
        public async Task<IActionResult> EndGame(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.GAME_ENDED));
            _memoryCache.Remove(guid);
            await _hubContext.Clients.Group(guid).SendAsync("GameOver");
            _logger.LogInformation("Game {GameGuid} ended and removed from cache.", guid);
            return NoContent();
        }

        #endregion game management

        #region collection getters

        [HttpGet]
        [Route("gethand/{guid}")]
        public async Task<IActionResult> GetHand(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            return Ok(game.Hand);
        }

        [HttpGet]
        [Route("gettemphand/{guid}")]
        public async Task<IActionResult> GetTempHand(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            return Ok(game.TempHand);
        }

        [HttpGet]
        [Route("getactive/{guid}")]
        public async Task<IActionResult> GetActive(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            return Ok(game.Active);
        }

        [HttpGet]
        [Route("getbench/{guid}")]
        public async Task<IActionResult> GetBench(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            return Ok(game.Bench);
        }

        [HttpGet]
        [Route("getdiscard/{guid}")]
        public async Task<IActionResult> GetDiscard(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            return Ok(game.DiscardPile);
        }

        #endregion collection getters

        #region peek methods
        [HttpGet]
        [Route("peekatcardindeckatposition/{guid}/{pos}")]
        public async Task<IActionResult> PeekAtCardInDeckAtPosition(string guid, int pos)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Deck.Cards.Count < pos + 1) return NotFound($"Fewer than {pos + 1} cards in deck!");

            Card peek = game.Deck.Cards[pos];
            _logger.LogInformation("User peeking at card {peek} at position {pos}.", peek.NumberInDeck, pos);
            return Ok(peek);
        }

        [HttpGet]
        [Route("peekatallcardsindeck/{guid}")]
        public async Task<IActionResult> PeekAtAllCardsInDeck(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.PEEKED_AT_DECK));
            _logger.LogInformation("User peeking at {num} cards in deck.", game.Deck.Cards.Count);
            return Ok(game.Deck.Cards);
        }
        #endregion peek methods

        #region deck management methods
        [HttpPut]
        [Route("shuffledeck/{guid}")]
        public async Task<IActionResult> ShuffleDeck(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            Random random = new Random();
            game.Deck.Cards = game.Deck.Cards.OrderBy(Random => random.Next()).ToList();
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DECK_SHUFFLED));
            _logger.LogInformation("Deck shuffled for game {GameGuid}.", guid);
            return NoContent();
        }
        #endregion deck management methods

        #region coin flip methods

        private bool GetFlip(Random rand)
        {
            return rand.Next(2) == 0;
        }

        [HttpGet]
        [Route("flipcoin/")]
        public async Task<bool> FlipCoin()
        {
            Random rand = new Random();
            bool isHeads = GetFlip(rand);
            _logger.LogInformation("Coin flipped: {Result}", isHeads ? "Heads" : "Tails");
            return isHeads;
        }

        [HttpGet]
        [Route("flipcoin/{guid}")]
        public async Task<IActionResult> FlipCoin(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            Random rand = new Random();
            bool isHeads = GetFlip(rand);
            game.GameRecord.Logs.Add(new GameLog(isHeads ? Enums.GameEvent.COIN_FLIPPED_HEADS : Enums.GameEvent.COIN_FLIPPED_TAILS));
            _logger.LogInformation("Coin flipped: {Result}", isHeads ? "Heads" : "Tails");
            return Ok(isHeads);
        }

        [HttpGet]
        [Route("flipxcoins/{guid}/{x}")]
        public async Task<IActionResult> FlipXCoins(string guid, int x)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            if (x < 1) return BadRequest("Number of coins to flip must be at least 1.");
            if (x > 20) return BadRequest("Number of coins to flip must not exceed 20.");
            Random rand = new Random();
            List<bool> Coins = new List<bool>();
            for (int i = 0; i < x; i++)
            {
                bool isHeads = GetFlip(rand);
                Coins.Add(isHeads);
                game.GameRecord.Logs.Add(new GameLog(isHeads ? Enums.GameEvent.COIN_FLIPPED_HEADS : Enums.GameEvent.COIN_FLIPPED_TAILS));
            }
            _logger.LogInformation("{HeadsCount} heads flipped out of {TotalFlips} flips.", Coins.Count(c => c), x);
            return Ok(Coins);
        }

        [HttpGet]
        [Route("flipcoinsuntil/{guid}/{isHeads}")]
        public async Task<IActionResult> FlipCoinsUntil(string guid, bool isHeads)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // if first flip matches, returns 0
            Random rand = new Random();
            int flips = -1;
            bool result = !isHeads;
            while (result != isHeads)
            {
                flips++;
                result = GetFlip(rand);
                game.GameRecord.Logs.Add(new GameLog(result ? Enums.GameEvent.COIN_FLIPPED_HEADS : Enums.GameEvent.COIN_FLIPPED_TAILS));
            }
            _logger.LogInformation("Flipped {FlipCount} times until {DesiredResult}.", flips, isHeads ? "Heads" : "Tails");
            return Ok(flips);
        }

        #endregion coin flip methods

        #region other methods
        [HttpGet]
        [Route("getvalidevolutions/{pokemonName}")]
        public async Task<List<string>> GetValidEvolutions(string pokemonName)
        {
            return await ApiHelper.GetValidEvolutionNames(pokemonName);
        }

        [HttpGet]
        [Route("getgamehistory/{guid}")]
        public async Task<IActionResult> GetGameHistory(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            return Ok(game.GameRecord);
        }
        #endregion other methods
    }
}