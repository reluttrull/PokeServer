using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders.Physical;
using PokeServer.Model;

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
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.GAME_STARTED, gameStart.Hand));
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
            return gameStart;
        }


        [HttpGet]
        [Route("gethand/{guid}")]
        public async Task<List<Card>> GetHand(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");

            return game.Hand;
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
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.GAME_ENDED));
                _memoryCache.Remove(guid);
                _logger.LogInformation("Game {GameGuid} ended and removed from cache.", guid);
                return NoContent();
            }
            return NotFound("Game not found.");
        }

        #endregion game management

        #region device management
        [HttpPut]
        [Route("sendtoplayarea/{guid}")]
        public async Task<IActionResult> SendToPlayArea(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // currently, we have to figure out where this card is being discarded from
            if (game.Hand.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.Hand.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else return NotFound("Card not in hand.");

            game.InPlay.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("CardAddedToPlayArea", card);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_PLAY_AREA, card));
            _logger.LogInformation("Card {card.Name} put in play for game {guid}.", card.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("sendtohand/{guid}")]
        public async Task<IActionResult> SendToHand(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // currently, we have to figure out where this card is being discarded from
            if (game.InPlay.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.InPlay.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else return NotFound("Card not in play.");

            game.Hand.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("CardMovedToHand", card);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_RETURNED_TO_HAND, card));
            _logger.LogInformation("Card {card.Name} moved to hand for game {guid}.", card.Name, guid);

            return NoContent();
        }
        #endregion device management

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
            await _hubContext.Clients.Group(guid).SendAsync("CardMovedToHand", drawnCard);
            await _hubContext.Clients.Group(guid).SendAsync("DeckChanged", game.Deck.Cards.Count);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_DRAWN_FROM_DECK, drawnCard));
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");
            return drawnCard;
        }

        [HttpPut]
        [Route("drawthiscardfromdeck/{guid}")]
        public async Task<IActionResult> DrawThisCardFromDeck(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Deck.Cards.Count < 1) return NotFound("No cards left in deck.");

            if (!game.Deck.Cards.Any(c => c.NumberInDeck == card.NumberInDeck)) return NotFound("Card not found in deck.");
            game.Hand.Add(card);
            game.Deck.Cards.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            await _hubContext.Clients.Group(guid).SendAsync("CardMovedToHand", card);
            await _hubContext.Clients.Group(guid).SendAsync("DeckChanged", game.Deck.Cards.Count);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_DRAWN_FROM_DECK, card));
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");

            return NoContent();
        }

        [HttpPut]
        [Route("drawthiscardfromdiscard/{guid}")]
        public async Task<IActionResult> DrawThisCardFromDiscard(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.DiscardPile.Count < 1) return NotFound("No cards in discard.");

            if (!game.DiscardPile.Any(c => c.NumberInDeck == card.NumberInDeck)) return NotFound("Card not found in discard.");
            game.Hand.Add(card);
            game.DiscardPile.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            await _hubContext.Clients.Group(guid).SendAsync("CardMovedToHand", card);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_DRAWN_FROM_DISCARD, card));
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Discard pile has {game.DiscardPile.Count} cards remaining.");

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
            await _hubContext.Clients.Group(guid).SendAsync("CardMovedToHand", drawnCard);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.PRIZE_CARD_TAKEN, drawnCard));
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
            game.MoveCardToDiscard(card);

            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_DISCARD, card));
            _logger.LogInformation("Card {card.Name} placed in discard pile for game {guid}.", card.Name, guid);
            // TODO: change this method to also send DiscardUpdated update, and have client use this trigger

            return NoContent();
        }

        [HttpGet]
        [Route("discardhand/{guid}")]
        public async Task<IActionResult> DiscardHand(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            foreach (Card card in game.Hand.ToList())
            {
                game.MoveCardToDiscard(card);
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_DISCARD, card));
                _logger.LogInformation("Card {card.Name} placed in discard pile for game {guid}.", card.Name, guid);
            }

            await _hubContext.Clients.Group(guid).SendAsync("DiscardChanged", game.DiscardPile);

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

        [HttpGet]
        [Route("peekatallcardsindeck/{guid}")]
        public async Task<List<Card>> PeekAtAllCardsInDeck(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");

            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.PEEKED_AT_DECK));
            _logger.LogInformation("User peeking at {num} cards in deck.", game.Deck.Cards.Count);
            return game.Deck.Cards;
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
            else if (game.InPlay.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.InPlay.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else return NotFound("Card not in play.");

            game.Deck.Cards.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("DeckChanged", game.Deck.Cards.Count);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_RETURNED_TO_DECK, card));
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
        [Route("flipcoin/{guid}")]
        public async Task<bool> FlipCoin(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");

            Random rand = new Random();
            bool isHeads = GetFlip(rand);
            game.GameRecord.Logs.Add(new GameLog(isHeads ? Enums.GameEvent.COIN_FLIPPED_HEADS : Enums.GameEvent.COIN_FLIPPED_TAILS));
            _logger.LogInformation("Coin flipped: {Result}", isHeads ? "Heads" : "Tails");
            return isHeads;
        }

        [HttpGet]
        [Route("flipxcoins/{guid}/{x}")]
        public async Task<List<bool>> FlipXCoins(string guid, int x)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");

            if (x < 1) throw new ArgumentOutOfRangeException("Number of coins to flip must be at least 1.");
            if (x > 20) throw new ArgumentOutOfRangeException("Number of coins to flip must not exceed 20.");
            Random rand = new Random();
            List<bool> Coins = new List<bool>();
            for (int i = 0; i < x; i++)
            {
                bool isHeads = GetFlip(rand);
                Coins.Add(isHeads);
                game.GameRecord.Logs.Add(new GameLog(isHeads ? Enums.GameEvent.COIN_FLIPPED_HEADS : Enums.GameEvent.COIN_FLIPPED_TAILS));
            }
            _logger.LogInformation("{HeadsCount} heads flipped out of {TotalFlips} flips.", Coins.Count(c => c), x);
            return Coins;
        }

        [HttpGet]
        [Route("flipcoinsuntil/{guid}/{isHeads}")]
        public async Task<int> FlipCoinsUntil(string guid, bool isHeads)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");

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
            return flips;
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
        public async Task<GameRecord> GetGameHistory(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) throw new KeyNotFoundException("Game not found.");
            return game.GameRecord;
        }
        #endregion other methods
    }
}