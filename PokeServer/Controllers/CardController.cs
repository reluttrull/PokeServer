using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using PokeServer.Model;
using System;
using System.ComponentModel.Design;

namespace PokeServer.Controllers
{
    [ApiController]
    [Route("game")]
    public class CardController : ControllerBase
    {
        private readonly ILogger<DeckController> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHubContext<NotificationHub> _hubContext;

        public CardController(ILogger<DeckController> logger, IMemoryCache memoryCache, IHubContext<NotificationHub> hubContext)
        {
            _logger = logger;
            _memoryCache = memoryCache;
            _hubContext = hubContext;
        }

        #region move methods
        [HttpPut]
        [Route("sendtohand/{guid}")]
        public async Task<IActionResult> SendToHand(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            bool success = await RemoveCardFromCurrentLocation(game, card);
            if (!success) return NotFound("Card not in play.");

            game.Hand.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_RETURNED_TO_HAND, card));
            _logger.LogInformation("Card {card.Name} moved to hand for game {guid}.", card.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("sendtotemphand/{guid}")]
        public async Task<IActionResult> SendToTempHand(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            // currently, we have to figure out where this card is being discarded from
            if (game.Hand.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.Hand.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
            }
            else return NotFound("Card not in hand.");

            game.TempHand.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("TempHandChanged", game.TempHand);
            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            _logger.LogInformation("Card {card.Name} put in play for game {guid}.", card.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("movetobench/{guid}")]
        public async Task<IActionResult> MoveToBench(string guid, PlaySpot playSpot)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (playSpot.MainCard == null) return NotFound("No card to move.");

            bool success = await RemoveCardFromCurrentLocation(game, playSpot.MainCard);
            if (!success) return NotFound("Card not in play.");

            game.Bench.Add(playSpot);
            game.Bench.RemoveAll(spot => playSpot.AttachedCards.Any(ac => ac.NumberInDeck == spot.MainCard?.NumberInDeck));

            await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_BENCH, playSpot));
            _logger.LogInformation("Card {card.Name} moved from active to bench for game {guid}.", playSpot.MainCard?.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("movetoactive/{guid}")]
        public async Task<IActionResult> MoveToActive(string guid, PlaySpot playSpot)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (playSpot.MainCard == null) return NotFound("No card to move.");

            bool success = await RemoveCardFromCurrentLocation(game, playSpot.MainCard);
            if (!success) return NotFound("Card not in play.");

            game.Active = playSpot;

            await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_ACTIVE, playSpot));
            _logger.LogInformation("Card {card.Name} moved from bench to active for game {guid}.", playSpot.MainCard?.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("swapactivewithbench/{guid}")]
        public async Task<IActionResult> SwapActiveWithBench(string guid, List<PlaySpot> playSpotsToSwap)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Active.MainCard?.NumberInDeck.Equals(playSpotsToSwap[0].MainCard?.NumberInDeck) == true)
            {
                // make sure first is former bench
                playSpotsToSwap.Reverse();
            }
            game.Bench.Add(playSpotsToSwap[1]);
            game.Active = playSpotsToSwap[0];
            game.Bench.RemoveAll(spot => spot.MainCard?.NumberInDeck.Equals(playSpotsToSwap[0].MainCard?.NumberInDeck) == true);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_ACTIVE, playSpotsToSwap[0]));
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_BENCH, playSpotsToSwap[1]));

            await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
            await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
            _logger.LogInformation("Card {card.Name} moved from bench to active for game {guid}.", playSpotsToSwap[0].MainCard?.Name, guid);
            _logger.LogInformation("Card {card.Name} moved from active to bench for game {guid}.", playSpotsToSwap[1].MainCard?.Name, guid);

            return NoContent();
        }

        [HttpPut]
        [Route("movetostadium/{guid}")]
        public async Task<IActionResult> MoveToStadium(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (card is null) return NotFound("No card to move.");

            bool success = await RemoveCardFromCurrentLocation(game, card);
            if (!success) return NotFound("Card not in play.");

            game.Stadium = card;

            await _hubContext.Clients.Group(guid).SendAsync("StadiumChanged", game.Stadium);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_MOVED_TO_STADIUM, card));
            _logger.LogInformation("Card {card.Name} played as stadium for game {guid}.", card.Name, guid);

            return NoContent();
        }


        [HttpPut]
        [Route("attachcard/{guid}/{attachedToCardId}")]
        public async Task<IActionResult> AttachCard(string guid, string attachedToCardId, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            bool success = await RemoveCardFromCurrentLocation(game, card);
            if (!success) return NotFound("Card not in play.");

            if (game.Active.MainCard?.NumberInDeck.ToString() == attachedToCardId)
            {
                game.Active.AttachedCards.Add(card);
                await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
            }
            else if (game.Bench.Any(spot => spot.MainCard?.NumberInDeck.ToString() == attachedToCardId))
            {
                PlaySpot benchSpot = game.Bench.Where(spot => spot.MainCard?.NumberInDeck.ToString() == attachedToCardId).First();
                benchSpot.AttachedCards.Add(card);
                await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
            }

            return NoContent();
        }
        #endregion move methods

        #region draw methods

        [HttpGet]
        [Route("drawcardfromdeck/{guid}")]
        public async Task<IActionResult> DrawCardFromDeck(string guid) // top card
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Deck.Cards.Count < 1) return NotFound("No cards left in deck.");

            Card drawnCard = game.Deck.Cards[0];
            game.Hand.Add(drawnCard);
            game.Deck.Cards.RemoveAt(0);
            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            await _hubContext.Clients.Group(guid).SendAsync("DeckChanged", game.Deck.Cards.Count);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_DRAWN_FROM_DECK, drawnCard));
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Deck has {game.Deck.Cards.Count} cards remaining.");
            return Ok(drawnCard);
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
            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
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
            await _hubContext.Clients.Group(guid).SendAsync("DiscardChanged", game.DiscardPile);
            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_DRAWN_FROM_DISCARD, card));
            _logger.LogInformation($"1 card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"Discard pile has {game.DiscardPile.Count} cards remaining.");

            return NoContent();
        }

        [HttpGet]
        [Route("drawcardfromprizes/{guid}")]
        public async Task<IActionResult> DrawCardFromPrizes(string guid)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.PrizeCards.Count < 1) return NotFound("No prize cards left.");
            Card drawnCard = game.PrizeCards[0];
            game.Hand.Add(drawnCard);
            game.PrizeCards.RemoveAt(0);
            await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.PRIZE_CARD_TAKEN, drawnCard));
            _logger.LogInformation($"1 prize card drawn, hand has {game.Hand.Count} cards.");
            _logger.LogInformation($"{game.PrizeCards.Count} cards remaining.");
            PrizeCardWrapper prizeCardWrapper = new PrizeCardWrapper
            {
                PrizeCard = drawnCard,
                RemainingPrizes = game.PrizeCards.Count
            };
            return Ok(prizeCardWrapper);
        }

        #endregion draw methods

        #region discard methods

        [HttpPut]
        [Route("discardcard/{guid}")]
        public async Task<IActionResult> DiscardCard(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            bool success = await RemoveCardFromCurrentLocation(game, card);
            if (!success) return NotFound("Card not in play.");

            // add to discard pile
            game.DiscardPile.Insert(0, card);

            await _hubContext.Clients.Group(guid).SendAsync("DiscardChanged", game.DiscardPile);
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
            List<Card> discardedCards = new();
            foreach (Card card in game.Hand.ToList())
            {
                bool success = await RemoveCardFromCurrentLocation(game, card);
                if (!success) return NotFound("Card not in play.");

                game.DiscardPile.Insert(0, card);
                discardedCards.Add(card);
                _logger.LogInformation("Card {card.Name} placed in discard pile for game {guid}.", card.Name, guid);
            }

            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.HAND_MOVED_TO_DISCARD, discardedCards));
            await _hubContext.Clients.Group(guid).SendAsync("DiscardChanged", game.DiscardPile);

            return NoContent();
        }

        [HttpPut]
        [Route("placecardonbottomofdeck/{guid}")]
        public async Task<IActionResult> PlaceCardOnBottomOfDeck(string guid, Card card)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");

            bool success = await RemoveCardFromCurrentLocation(game, card);
            if (!success) return NotFound("Card not in play.");

            game.Deck.Cards.Add(card);

            await _hubContext.Clients.Group(guid).SendAsync("DeckChanged", game.Deck.Cards.Count);
            game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.CARD_RETURNED_TO_DECK, card));
            _logger.LogInformation("Card {card.Name} placed on bottom of deck for game {guid}.", card.Name, guid);

            return NoContent();
        }

        #endregion discard methods

        #region damage methods
        [HttpPut]
        [Route("adddamagecounters/{guid}/{toCardId}/{amount}")]
        public async Task<IActionResult> AddDamageCounters(string guid, string toCardId, int amount)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Active.MainCard?.NumberInDeck.ToString() == toCardId)
            {
                game.Active.DamageCounters += amount;
                await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DAMAGE_COUNTERS_ADDED_TO_ACTIVE_POKEMON, game.Active, $"{amount}"));
            }
            else if (game.Bench.Any(spot => spot.MainCard?.NumberInDeck.ToString() == toCardId))
            {
                PlaySpot benchSpot = game.Bench.Where(spot => spot.MainCard?.NumberInDeck.ToString() == toCardId).First();
                benchSpot.DamageCounters += amount;
                await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DAMAGE_COUNTERS_ADDED_TO_BENCH_POKEMON, benchSpot, $"{amount}"));
            }
            else return NotFound("Card not in play.");

            _logger.LogInformation("Added {amount} damage to card {toCardId} for game {guid}.", amount, toCardId, guid);
            return NoContent();
        }

        [HttpPut]
        [Route("removedamagecounters/{guid}/{toCardId}/{amount}")]
        public async Task<IActionResult> RemoveDamageCounters(string guid, string toCardId, int amount)
        {
            if (!_memoryCache.TryGetValue(guid, out Game? game) || game == null) return NotFound("Game not found.");
            if (game.Active.MainCard?.NumberInDeck.ToString() == toCardId)
            {
                game.Active.DamageCounters = Math.Max(game.Active.DamageCounters - amount, 0);
                await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DAMAGE_COUNTERS_REMOVED_FROM_ACTIVE_POKEMON, game.Active, $"{amount}"));
            }
            else if (game.Bench.Any(spot => spot.MainCard?.NumberInDeck.ToString() == toCardId))
            {
                PlaySpot benchSpot = game.Bench.Where(spot => spot.MainCard?.NumberInDeck.ToString() == toCardId).First();
                benchSpot.DamageCounters = Math.Max(benchSpot.DamageCounters - amount, 0);
                await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
                game.GameRecord.Logs.Add(new GameLog(Enums.GameEvent.DAMAGE_COUNTERS_REMOVED_FROM_BENCH_POKEMON, benchSpot, $"{amount}"));
            }
            else return NotFound("Card not in play.");

            _logger.LogInformation("Removed up to {amount} damage to card {toCardId} for game {guid}.", amount, toCardId, guid);
            return NoContent();
        }
        #endregion damage methods

        private async Task<bool> RemoveCardFromCurrentLocation(Game game, Card card)
        {
            string guid = game.Guid.ToString();
            // remove card from wherever it is
            if (game.Hand.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.Hand.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
                await _hubContext.Clients.Group(guid).SendAsync("HandChanged", game.Hand);
            }
            else if (game.TempHand.Any(c => c.NumberInDeck == card.NumberInDeck))
            {
                game.TempHand.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
                await _hubContext.Clients.Group(guid).SendAsync("TempHandChanged", game.TempHand);
            }
            else if (game.Bench.Any(spot => spot.MainCard?.NumberInDeck == card.NumberInDeck))
            {
                game.Bench.RemoveAll(spot => spot.MainCard?.NumberInDeck == card.NumberInDeck);
                await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
            }
            else if (game.Bench.Any(spot => spot.AttachedCards.Any(ac => ac.NumberInDeck == card.NumberInDeck)))
            {
                PlaySpot benchSpot = game.Bench.Where(spot => spot.AttachedCards.Any(ac => ac.NumberInDeck == card.NumberInDeck)).First();
                benchSpot.AttachedCards.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
                await _hubContext.Clients.Group(guid).SendAsync("BenchChanged", game.Bench);
            }
            else if (game.Active.MainCard?.NumberInDeck == card.NumberInDeck)
            {
                game.Active.MainCard = null;
                await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
            }
            else if (game.Active.AttachedCards.Any(ac => ac.NumberInDeck == card.NumberInDeck))
            {
                game.Active.AttachedCards.RemoveAll(c => c.NumberInDeck == card.NumberInDeck);
                await _hubContext.Clients.Group(guid).SendAsync("ActiveChanged", game.Active);
            }
            else if (game.Stadium?.NumberInDeck == card.NumberInDeck)
            {
                game.Stadium = null;
                await _hubContext.Clients.Group(guid).SendAsync("StadiumChanged", game.Stadium);
            }
            else return false;
            return true;
        }
    }
}