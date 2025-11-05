namespace PokeServer.Model
{
    public class GameRecord
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<GameLog> Logs { get; set; }
        public GameRecord()
        {
            StartTime = DateTime.UtcNow;
            Logs = new List<GameLog>();
        }
    }
}
