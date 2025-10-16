namespace PokeServer.Model
{
    public class EnergyCard : Card
    {
        public string Effect { get; set; } = string.Empty;
        public string EnergyType { get; set; } = string.Empty;
        public string EnergyColor { get; set; } = string.Empty;
    }
}
