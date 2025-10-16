using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PokeServer.Model
{
    public class Attack
    {
        public List<string> Cost { get; set; } = new List<string>();
        public string Name { get; set; } = string.Empty;
        public string Effect { get; set; } = string.Empty;
        [JsonConverter(typeof(StringConverter))]
        public string Damage { get; set; } = string.Empty;
    }
}
