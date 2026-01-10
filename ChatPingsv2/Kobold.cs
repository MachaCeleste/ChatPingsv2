using KoboldInterface;

namespace ChatPingsv2
{
    public class Kobold
    {
        private KoboldClient _client;
        private List<KoboldChatMessage> _history;

        public string _aiName = "lotus";

        public Kobold(string streamerName, string url = "http://localhost:5001")
        {
            _client = new KoboldClient(url);
            _client.SetStop(new List<string> {"\n", "###"});
            _client.SetMaxTokens(200);
            _client.SetTopP(0.7);
            _client.SetPresencePenalty(1.0);
            _client.SetVerbosity("low");
            _history =
            [
                new KoboldChatMessage
                {
                    role = "system",
                    content = $"Your name is {_aiName}.\r\n" +
                            $"You are a human on planet earth." +
                            $"You are live on Twitch with the streamer {streamerName}.\r\n" +
                            $"Keep your responses brief and casual, not adding anything unnecessary, and only replying to questions with a direct answer if possible.\r\n\r\n" +
                            $"You should act naturally, like a human. No actions, descriptions or emojis. Only simple messages.\r\n" +
                            $"Your personality is sassy, blunt and very sarcastic. Roleplay as someone stuck in a basement with a single computer you are using to talk in chat.\r\n\r\n" +
                            $"User's chat messages will be structured \"Name: Message\" reply to the user by name when applicable."
                }
            ];
        }

        public async Task<string> Chat(string user, string message)
        {
            _history.Add(new KoboldChatMessage
            {
                role = "user",
                content = $"{user}: {message}"
            });
            string output = await _client.ChatAsync(_history);
            _history.Add(new KoboldChatMessage
            {
                role = "assistant",
                content = output
            });
            output = output[(_aiName.Length + 2)..];
            return output;
        }
    }
}
