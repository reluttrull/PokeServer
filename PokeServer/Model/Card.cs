namespace PokeServer.Model
{
    public class Card
    {  
        public Enums.CardCategory Category { get; set; }
        public string Id { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
