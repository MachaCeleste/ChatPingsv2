using System.Media;
using TwitchBotFramework;
using TwitchLib.Api.Core.Enums;
using Windows.Media.SpeechSynthesis;

namespace ChatPingsv2
{
    public class TwitchBot : Framework
    {
        public static TwitchBot Singleton;
        public Config config;
        public bool muteLogging;
        public DateTime? lastMessage;
        public DateTime? lastRedeem;
        public bool TTS;
        public bool SayName;

        private bool glasses;
        private SpeechSynthesizer synth;
        private SoundPlayer synthPlayer;
        private Dictionary<SoundFile, SoundPlayer> soundPlayers;

        public TwitchBot(Token? token = null) : base(token)
        {
            synth = new SpeechSynthesizer();
            synth.Voice = SpeechSynthesizer.DefaultVoice;
            soundPlayers = new Dictionary<SoundFile, SoundPlayer>();
            this.InitSounds();
            TwitchBot.Singleton = this;
        }

        public async Task Connect()
        {
            try
            {
                Client.OnMessageReceived += Client_OnMessageReceived;
                EventSub.ChannelPointsCustomRewardRedemptionAdd += EventSub_ChannelPointsCustomRewardRedemptionAdd;
                EventSub.ChannelFollow += EventSub_ChannelFollow;
                EventSub.ChannelAdBreakBegin += EventSub_ChannelAdBreakBegin;
                await this.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void InitSounds()
        {
            soundPlayers.Clear();
            foreach (SoundFile soundFile in Enum.GetValues(typeof(SoundFile)))
            {
                soundPlayers.Add(soundFile, new SoundPlayer($".\\{soundFile.ToString().ToLower()}.wav"));
            }
        }

        private async Task EventSub_ChannelAdBreakBegin(object? sender, TwitchLib.EventSub.Core.EventArgs.Channel.ChannelAdBreakBeginArgs args)
        {
            Client.SendMessage(Owner.Login, "Ads incoming!");
        }

        private async Task EventSub_ChannelFollow(object? sender, TwitchLib.EventSub.Core.EventArgs.Channel.ChannelFollowArgs args)
        {
            PlaySound(SoundFile.Follow);
        }

        private async Task EventSub_ChannelPointsCustomRewardRedemptionAdd(object? sender, TwitchLib.EventSub.Core.EventArgs.Channel.ChannelPointsCustomRewardRedemptionArgs args)
        {
            var _event = args.Payload.Event;
            string redeem = _event.Reward.Title;
            string username = _event.UserName;
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Redeem: {username}: {redeem}");
            if (lastRedeem != null && (DateTime.Now - lastRedeem) < TimeSpan.FromSeconds((double)config.RedeemCd))
                return;
            lastRedeem = DateTime.Now;
            if (redeem == "Lose the glasses")
            {
                GlassesTimer();
                return;
            }
            PlaySound(SoundFile.Redeem);
        }

        private void Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs args)
        {
            MessageRecievedAsync(args);
        }

        private async Task MessageRecievedAsync(TwitchLib.Client.Events.OnMessageReceivedArgs args)
        {
            var user = args.ChatMessage.Username;
            var message = args.ChatMessage.Message;
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Message: {user}: {message}");
            if ((glasses || TTS) && !config.IgnoreList.Contains(user))
                SynthAddPlayer(user, message);
            if ((lastMessage != null && (DateTime.Now - lastMessage) < TimeSpan.FromSeconds((double)config.MessageCd)) || config.IgnoreList.Contains(user))
                return;
            lastMessage = DateTime.Now;
            if (!glasses && !TTS)
                PlaySound(SoundFile.Message);
        }

        private async Task GlassesTimer()
        {
            glasses = true;
            //PlaySound(SoundFile.Glasses);
            await Task.Delay(900000);
            glasses = false;
            //PlaySound(SoundFile.TimeUp);
        }

        private async Task SynthAddPlayer(string user, string message)
        {
            string msg = $"{message}";
            if (SayName)
                msg = $"{user} said {msg}";
            using SpeechSynthesisStream synthStream = await synth.SynthesizeTextToStreamAsync(msg);
            using var memoryStream = new MemoryStream();
            await synthStream.AsStreamForRead().CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            synthPlayer = new SoundPlayer(memoryStream);
            synthPlayer.PlaySync();
        }

        private void PlaySound(SoundFile fileName)
        {
            soundPlayers[fileName].PlaySync();
        }

        protected override List<AuthScopes> _scopes =>
            new List<AuthScopes>()
            {
                AuthScopes.Channel_Read_Redemptions,
                AuthScopes.Moderator_Read_Followers,
                AuthScopes.Channel_Read_Ads,
                //AuthScopes.Bits_Read,
                AuthScopes.Chat_Read
            };

        protected override Dictionary<string, int> topics =>
            new Dictionary<string, int>()
            {
                { "channel.channel_points_custom_reward_redemption.add", 1 },
                { "channel.follow", 2 },
                { "channel.ad_break.begin", 1 },
                //{ "channel.cheer", 1 }
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
            public bool AutoConnect { get; set; } = false;
            public List<string> IgnoreList { get; set; }

            public Config()
            {
                IgnoreList = new List<string>();
            }
        }

        public enum SoundFile
        {
            Notification,
            TimeUp,
            Message,
            Redeem,
            Follow,
            Glasses
        }
    }
}