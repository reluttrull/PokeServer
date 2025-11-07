namespace PokeServer.Model
{
    public class PlaySpot
    {
        public Card? MainCard { get; set; }
        public List<Card> AttachedCards { get; set; } = new List<Card>();
        public int DamageCounters { get; set; } = 0;
    }
}
