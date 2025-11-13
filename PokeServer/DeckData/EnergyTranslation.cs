namespace PokeServer.DeckData
{
    public static class EnergyTranslation
    {
        public static readonly Dictionary<string, string> EnergyCodes = new Dictionary<string, string>
        {
            {"Double", "base1-96"},
            {"Fighting", "base1-97"},
            {"Fire", "base1-98"},
            {"Grass", "base1-99"},
            {"Lightning", "base1-100"},
            {"Psychic", "base1-101"},
            {"Water", "base1-102"}
            // Add more energy types as needed
        };
    }
}
