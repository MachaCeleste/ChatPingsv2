using System.Media;
using TwitchBotFramework;
using TwitchLib.Api.Core.Enums;

namespace ChatPingsv2
{
    public class TwitchBot : Framework
    {
        public static TwitchBot Singleton;
        public Config config;
        public bool muteLogging;
        public DateTime? lastMessage;
        public DateTime? lastRedeem;

        public TwitchBot(Token? token = null) : base(token)
        {
            TwitchBot.Singleton = this;
        }

        public async Task Connect()
        {
            try
            {
                Client.OnMessageReceived += Client_OnMessageReceived;
                EventSub.ChannelPointsCustomRewardRedemptionAdd += EventSub_ChannelPointsCustomRewardRedemptionAdd;
                await this.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task EventSub_ChannelPointsCustomRewardRedemptionAdd(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelPointsCustomRewardRedemptionArgs args)
        {
            var _event = args.Notification.Payload.Event;
            string redeem = _event.Reward.Title;
            string username = _event.UserName;
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Redeem: {username}: {redeem}");
            if (lastRedeem != null && (DateTime.Now - lastRedeem) < TimeSpan.FromSeconds((double)config.RedeemCd))
                return;
            lastRedeem = DateTime.Now;
            PlaySound(1);
        }

        private void Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Message: {e.ChatMessage.Username}: {e.ChatMessage.Message}");
            if ((lastMessage != null && (DateTime.Now - lastMessage) < TimeSpan.FromSeconds((double)config.MessageCd)) || config.IgnoreList.Contains(e.ChatMessage.Username))
                return;
            lastMessage = DateTime.Now;
            PlaySound(0);
        }

        private static void PlaySound(int sound = 0)
        {
            List<string> sounds = new List<string>
            {
                ".\\message.wav",
                ".\\redeem.wav"
            };
            new SoundPlayer(sounds[sound]).PlaySync();
        }

        protected override List<AuthScopes> _scopes =>
            new List<AuthScopes>()
            {
                AuthScopes.Helix_Bits_Read,
                AuthScopes.Helix_Channel_Read_Redemptions,
                AuthScopes.Chat_Read
            };

        protected override Dictionary<string, int> topics =>
            new Dictionary<string, int>()
            {
                { "channel.channel_points_custom_reward_redemption.add", 1 }
            };

        protected override async Task LoggingAsync(string msg)
        {
            if (muteLogging && msg.StartsWith("Received: ")) return;
            Console.WriteLine(msg);
        }

        public class Config
        {
            public Token? Token { get; set; }
            public string? ClientId { get; set; }
            public string? Secret { get; set; }
            public int MessageCd { get; set; } = 30;
            public int RedeemCd { get; set; } = 30;
            public List<string> IgnoreList { get; set; }

            public Config()
            {
                IgnoreList = new List<string>();
            }
        }
    }
}