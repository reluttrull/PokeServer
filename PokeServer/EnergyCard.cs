using PokeServer.Model;

namespace PokeServer
{
    public class EnergyCard : Card
    {
        public string Effect { get; set; } = string.Empty;
        public Enums.EnergyType EnergyType { get; set; }
        public Enums.EnergyColor EnergyColor { get; set; }
    }
}
