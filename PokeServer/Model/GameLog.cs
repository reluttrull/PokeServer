namespace PokeServer.Model
{
    public class GameLog
    {
        public Enums.GameEvent EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string>? InvolvedCardIds { get; set; }
        public GameLog(Enums.GameEvent eventType)
        {
            EventType = eventType;
            Timestamp = DateTime.UtcNow;
        }
        public GameLog(Enums.GameEvent eventType, List<Card> involvedCards)
        {
            EventType = eventType;
            InvolvedCardIds = involvedCards.Select(c => c.NumberInDeck.ToString()).ToList();
            Timestamp = DateTime.UtcNow;
        }
        public GameLog(Enums.GameEvent eventType, Card involvedCard)
        {
            EventType = eventType;
            InvolvedCardIds = new List<string> { involvedCard.NumberInDeck.ToString() };
            Timestamp = DateTime.UtcNow;
        }
    }
}
