namespace PokeServer.Model
{
    public class GameLog
    {
        public Enums.GameEvent EventType { get; set; }
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Card>? InvolvedCards { get; set; }
        public string? AdditionalInfo { get; set; } = null;
        public GameLog(Enums.GameEvent eventType)
        {
            EventType = eventType;
            Name = eventType.ToString();
            Timestamp = DateTime.UtcNow;
        }
        public GameLog(Enums.GameEvent eventType, List<Card> involvedCards)
        {
            EventType = eventType;
            Name = eventType.ToString();
            InvolvedCards = involvedCards;
            Timestamp = DateTime.UtcNow;
        }
        public GameLog(Enums.GameEvent eventType, Card involvedCard)
        {
            EventType = eventType;
            Name = eventType.ToString();
            InvolvedCards = new List<Card> { involvedCard };
            Timestamp = DateTime.UtcNow;
        }
        public GameLog(Enums.GameEvent eventType, PlaySpot playSpot, string? additionalInfo = null)
        {
            EventType = eventType;
            Name = eventType.ToString();
            InvolvedCards = new List<Card>();
            if (playSpot.MainCard != null)
            {
                InvolvedCards.Add(playSpot.MainCard);
            }
            InvolvedCards.AddRange(playSpot.AttachedCards);
            Timestamp = DateTime.UtcNow;
        }
    }
}
