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

        #region game management

        [HttpGet]
        [Route("getnewgame/{deckId}")]
        public async Task<GameStart> GetNewGame(int DeckId)
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
                throw new Exception("Failed to draw a valid starting hand.");
            }

            // populate game object with starting hand and draw prize cards
            game.SetStartingPosition(gameStart.Hand);
            _logger.LogInformation($"Starting hand set with {game.Hand.Count} cards.");
            _logger.LogInformation($"Starting prize cards set with {game.PrizeCards.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

            // save game to memory cache
            if (!_memoryCache.TryGetValue(game.Guid.ToString(), out Game? value))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(3));
                _memoryCache.Set<Game>(game.Guid.ToString(), game, cacheEntryOptions);
            }
            return gameStart;
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
            if (_memoryCache.TryGetValue(guid, out Game? game) && game != null)
            {
                _memoryCache.Remove(guid);
                _logger.LogInformation("Game {GameGuid} ended and removed from cache.", guid);
                return NoContent();
            }
            return NotFound("Game not found.");
        }

        #endregion game management

        #region draw methods

        [HttpGet]
        [Route("drawcardfromdeck/{guid}")]
        public async Task<Card> DrawCardFromDeck(string guid) // top card
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");
            if (game.Deck.Cards.Count < 1) throw new IndexOutOfRangeException("No cards left in deck.");

            Card drawnCard = game.Deck.Cards[0];
            game.Hand.Add(drawnCard);
            game.Deck.Cards.RemoveAt(0);
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");
            return drawnCard;
        }

        [HttpPut]
        [Route("drawthiscardfromdeck/{guid}")]
        public async Task<IActionResult> DrawThisCardFromDeck(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");
            if (game.Deck.Cards.Count < 1) throw new IndexOutOfRangeException("No cards left in deck.");

            if (!game.Deck.Cards.Any(c => c.NumberInDeck == card.NumberInDeck)) return NotFound("Card not found in deck.");
            game.Hand.Add(card);
            game.Deck.Cards.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

            return NoContent();
        }

        [HttpGet]
        [Route("drawcardfromprizes/{guid}")]
        public async Task<PrizeCardWrapper> DrawCardFromPrizes(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");
            if (game.PrizeCards.Count < 1) throw new IndexOutOfRangeException("No prize cards left.");
            Card drawnCard = game.PrizeCards[0];
            game.Hand.Add(drawnCard);
            game.PrizeCards.RemoveAt(0);
            _logger.LogInformation($"1 prize card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"{game.PrizeCards.Count} cards remaining.");
            PrizeCardWrapper prizeCardWrapper = new PrizeCardWrapper
            {
                PrizeCard = drawnCard,
                RemainingPrizes = game.PrizeCards.Count
            };
            return prizeCardWrapper;
        }

        #endregion draw methods

        #region discard methods

        [HttpPut]
        [Route("discardcard/{guid}")]
        public async Task<IActionResult> DiscardCard(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // currently, we have to figure out where this card is being discarded from
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

            _logger.LogInformation("Card {card.Name} placed in discard pile for game {guid}.", card.Name, guid);

            return NoContent();
        }

        #endregion discard methods

        #region peek methods
        [HttpGet]
        [Route("peekatcardindeckatposition/{guid}/{pos}")]
        public async Task<Card> PeekAtCardInDeckAtPosition(string guid, int pos)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");
            if (game.Deck.Cards.Count < pos + 1) throw new IndexOutOfRangeException($"Fewer than {pos + 1} cards in deck!");

            Card peek = game.Deck.Cards[pos];
            _logger.LogInformation("User peeking at card {peek} at position {pos}.", peek.NumberInDeck, pos);
            return peek;
        }
        #endregion peek methods

        #region deck management methods
        [HttpPut]
        [Route("placecardonbottomofdeck/{guid}")]
        public async Task<IActionResult> PlaceCardOnBottomOfDeck(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // currently, we have to figure out where this card is being discarded from
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

            game.Deck.Cards.Add(card);

            _logger.LogInformation("Card {card.Name} placed on bottom of deck for game {guid}.", card.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("shuffledeck/{guid}")]
        public async Task<IActionResult> ShuffleDeck(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            Random random = new Random();
            game.Deck.Cards = game.Deck.Cards.OrderBy(Random => random.Next()).ToList();
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
        [Route("flipcoin")]
        public async Task<bool> FlipCoin()
        {
            Random rand = new Random();
            bool isHeads = GetFlip(rand);
            _logger.LogInformation("Coin flipped: {Result}", isHeads ? "Heads" : "Tails");
            return isHeads;
        }

        [HttpGet]
        [Route("flipxcoins/{x}")]
        public async Task<List<bool>> FlipXCoins(int x)
        {
            if (x < 1) throw new ArgumentOutOfRangeException("Number of coins to flip must be at least 1.");
            if (x > 20) throw new ArgumentOutOfRangeException("Number of coins to flip must not exceed 20.");
            Random rand = new Random();
            List<bool> Coins = new List<bool>();
            for (int i = 0; i < x; i++)
            {
                bool isHeads = GetFlip(rand);
                Coins.Add(isHeads);
            }
            _logger.LogInformation("{HeadsCount} heads flipped out of {TotalFlips} flips.", Coins.Count(c => c), x);
            return Coins;
        }

        [HttpGet]
        [Route("flipcoinsuntil/{isHeads}")]
        public async Task<int> FlipCoinsUntil(bool isHeads)
        {
            // if first flip matches, returns 0
            Random rand = new Random();
            int flips = -1;
            bool result = !isHeads;
            while (result != isHeads)
            {
                flips++;
                result = GetFlip(rand);
            }
            _logger.LogInformation("Flipped {FlipCount} times until {DesiredResult}.", flips, isHeads ? "Heads" : "Tails");
            return flips;
        }

        #endregion coin flip methods
    }
}