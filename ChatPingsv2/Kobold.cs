using KoboldInterface;

namespace ChatPingsv2
{
    public class Kobold
    {
        private KoboldClient _client;
        private string _aiName;
        private string _streamerName;
        private int _tokenLimit;

        private string _history = "";
        private string _memory = "";

        public Kobold(string prepend, string aiName, string streamerName, int tokenLimit, string url = "http://localhost:5001")
        {
            _client = new KoboldClient(url);
            _aiName = aiName;
            _streamerName = streamerName;
            _tokenLimit = tokenLimit;
            _history = prepend;
        }

        public async Task<string> Generate(string user, string message, string remember = "")
        {
            _history += $"\n{user}: {message}\n{_aiName}";
            _memory += $"\n{remember}";
            string output = await _client.GenerateAsync(_history, new List<string>() { $"{user}:", $"\n{user} ", $"\n{_aiName}: ", $"\n{_streamerName}", $"\n" }, _memory, max_length: _tokenLimit);
            _history += $" {output}";
            return output;
        }
    }
}
