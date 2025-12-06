namespace PokeServer.Model
{
    public class PokemonCard : Card
    {
        public int Hp { get; set; }
        public List<string> Types { get; set; } = new List<string>();
        public string EvolveFrom { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public List<Ability> Abilities { get; set; } = new List<Ability>(); // empty while we only use base set
        public List<Attack> Attacks { get; set; } = new List<Attack>();
        public List<Weakness> Weaknesses { get; set; } = new List<Weakness>();
        public List<Resistance> Resistances { get; set; } = new List<Resistance>();
        public int RetreatCost { get; set; }
    }
}
