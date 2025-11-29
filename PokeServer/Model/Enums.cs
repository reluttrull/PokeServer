namespace PokeServer.Model
{
    public class Enums
    {
        public enum CardCategory
        {
            Pokemon,
            Energy,
            Trainer
        }
        public enum EnergyType
        {
            Basic,
            Special
        }

        public enum EnergyColor
        {
            Colorless,
            Darkness,
            Dragon,
            Fairy,
            Fighting,
            Fire,
            Grass,
            Lightning,
            Metal,
            Psychic,
            Water
        }

        public enum GameEvent
        {
            // draw: 1-100, move: 101-200, attach: 201-300, evolve: 301-400, damage: 401-500, misc. game state: 501-600, 
            // game start: 0, game end: 1000
            GAME_STARTED = 0,
            CARD_DRAWN_FROM_DECK = 1,
            CARD_DRAWN_FROM_DISCARD = 2,      
            PRIZE_CARD_TAKEN = 3,
            CARD_RETURNED_TO_HAND = 101,
            CARD_MOVED_TO_PLAY_AREA = 102,
            CARD_RETURNED_TO_DECK = 103,
            CARD_MOVED_TO_DISCARD = 104,
            CARD_MOVED_TO_BENCH = 105,
            CARD_MOVED_TO_ACTIVE = 106,
            HAND_MOVED_TO_DISCARD = 107,
            CARD_MOVED_TO_STADIUM = 108,
            //HAND_MOVED_TO_DECK = 109,
            //CARD_ATTACHED_TO_ACTIVE_POKEMON = 201,
            //CARD_ATTACHED_TO_BENCH_POKEMON = 202,
            //POKEMON_EVOLVED = 301,
            //POKEMON_DEVOLVED = 302,
            //DAMAGE_DEALT_TO_ACTIVE_POKEMON = 401,
            //DAMAGE_DEALT_TO_BENCH_POKEMON = 402,
            DAMAGE_COUNTERS_ADDED_TO_ACTIVE_POKEMON = 403,
            DAMAGE_COUNTERS_ADDED_TO_BENCH_POKEMON = 404,
            DAMAGE_COUNTERS_REMOVED_FROM_ACTIVE_POKEMON = 405,
            DAMAGE_COUNTERS_REMOVED_FROM_BENCH_POKEMON = 406,
            //POKEMON_KNOCKED_OUT = 407,
            DECK_SHUFFLED = 501,
            PEEKED_AT_DECK = 502,
            DRAW_MULLIGAN = 503,
            COIN_FLIPPED_HEADS = 504,
            COIN_FLIPPED_TAILS = 505,
            GAME_ENDED = 1000
        }
    }
}
