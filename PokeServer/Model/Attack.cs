namespace PokeServer.Model
{
    public class Attack
    {
        public List<string> Cost { get; set; } = new List<string>();
        public string Name { get; set; } = string.Empty;
        public string Effect { get; set; } = string.Empty;
        public int Damage { get; set; }
    }
}
