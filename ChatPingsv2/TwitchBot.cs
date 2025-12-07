using System.Media;
using TwitchBotFramework;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using System.Speech.Synthesis;

namespace ChatPingsv2
{
    public class TwitchBot : Framework
    {
        public static TwitchBot Singleton;
        public Config config;
        public bool muteLogging;
        public bool TTS;
        public bool SayName;
        public bool CallIn;
        public Dictionary<string, Rewards> AddedRewards;

        private bool InCall;
        private DateTime? lastMessage;
        private Dictionary<SoundFile, SoundPlayer> soundPlayers;
        private bool glasses;
        private string callInUsername;
        private TaskCompletionSource<Closer> CallTCS = new TaskCompletionSource<Closer>(TaskCreationOptions.RunContinuationsAsynchronously);

        private SpeechSynthesizer synth;
        private Dictionary<string, string> UserVoices;
        private SoundPlayer synthPlayer;

        public TwitchBot(Token? token = null) : base(token)
        {
            synth = new SpeechSynthesizer();
            UserVoices = new Dictionary<string, string>();
            soundPlayers = new Dictionary<SoundFile, SoundPlayer>();
            this.InitSounds();
            TwitchBot.Singleton = this;
        }

        public async Task Connect()
        {
            try
            {
                Client.OnJoinedChannel += Client_OnJoinedChannel;
                Client.OnMessageReceived += Client_OnMessageReceived;
                Client.OnChatCommandReceived += Client_OnChatCommandReceived;
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

        public bool IsInCall()
        {
            return InCall;
        }

        public void StopSynth()
        {
            synthPlayer.Stop();
        }

        public void UserHangup()
        {
            CallTCS.TrySetResult(Closer.User);
        }

        public void RerollUserVoice(string username)
        {
            if (!UserVoices.ContainsKey(username.ToLower()))
            {
                Console.WriteLine("Error: Couldn't find user voice");
                return;
            }
            UserVoices.Remove(username.ToLower());
        }

        public async Task SetRewardEnabled(string rewardId, bool state)
        {
            try
            {
                await Api.Helix.ChannelPoints.UpdateCustomRewardAsync(Owner.Id, rewardId, new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest()
                {
                    IsEnabled = state
                });
            }
            catch (Exception ex)
            {
                LoggingAsync($"Error: {rewardId} -> {ex.ToString()}");
            }
        }

        public async Task RemoveRewards()
        {
            if (AddedRewards == null) return;
            LoggingAsync($"System: Removing rewards...");
            foreach (var r in AddedRewards)
            {
                try
                {
                    if (r.Value.RedemtionIds.Any()) await UpdateRedeemAsync(r.Value.RewardId, r.Value.RedemtionIds, false);
                    await Api.Helix.ChannelPoints.DeleteCustomRewardAsync(Owner.Id, r.Value.RewardId);
                }
                catch (Exception ex)
                {
                    LoggingAsync($"Error: {r} -> {ex.ToString()}");
                }
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
            ChannelPointsCustomRewardRedemption _event = args.Payload.Event;
            string rewardId = _event.Reward.Id;
            string redemptionId = _event.Id;
            string redeem = _event.Reward.Title;
            string username = _event.UserName;
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Redeem: {username}: {redeem}");
            if (AddedRewards.ContainsKey(redeem)) AddedRewards[redeem].RedemtionIds.Add(redemptionId);
            switch (redeem)
            {
                case "Lose the glasses":
                    synth.SelectVoice(UserVoices[username.ToLower()]);
                    SynthAddAudioPlayer($"{username} redeemed Lose the glasses. Five minute timer started.");
                    if (InCall) CallTCS.TrySetResult(Closer.Owner);
                    CallIn = false;
                    SetRewardEnabled(AddedRewards["Call-In"].RewardId, false);
                    GlassesTimer();
                    break;
                case "Call-In":
                    HandleCallAsync(username, rewardId, redemptionId);
                    break;
                default:
                    PlaySound(SoundFile.Redeem);
                    break;
            }
        }

        private async Task HandleCallAsync(string user, string rewardId, string redemtionId)
        {
            PlaySound(SoundFile.Ringer);
            var res = MessageBox.Show($"You are getting a Call-In from {user}\nAccept Call?", "Call-In", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                UpdateRedeemAsync(rewardId, new List<string>() { redemtionId }, true);
                SetRewardEnabled(rewardId, false);
                InCall = true;
                callInUsername = user.ToLower();
                PlaySound(SoundFile.Pickup);
                Closer closer = await CallTCS.Task;
                if (closer == Closer.User)
                    PlaySound(SoundFile.Dialtone);
                PlaySound(SoundFile.Hangup);
                InCall = false;
                callInUsername = string.Empty;
                SetRewardEnabled(rewardId, true);
            }
            else
                UpdateRedeemAsync(rewardId, new List<string>() { redemtionId }, false);
        }

        private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            if (AddedRewards != null) return;
            AddRewards();
        }

        private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs args)
        {
            MessageRecievedAsync(args);
        }

        private async Task MessageRecievedAsync(OnMessageReceivedArgs args)
        {
            var user = args.ChatMessage.Username;

            if (!UserVoices.ContainsKey(user.ToLower()))
            {
                Random rng = new Random(Guid.NewGuid().GetHashCode());
                var voices = synth.GetInstalledVoices();
                UserVoices.Add(user.ToLower(), voices[rng.Next(voices.Count - 1)].VoiceInfo.Name);
            }

            var message = args.ChatMessage.Message;
            if (message.StartsWith('!')) return;
            Console.WriteLine($"{DateTime.Now.ToString("hh:mm:ss")}: Message: {user}: {message}");
            synth.SelectVoice(UserVoices[user.ToLower()]);
            if (!InCall && (glasses || TTS) && !config.IgnoreList.Contains(user))
            {
                string msg = $"{message}";
                if (SayName)
                    msg = $"{user} said {msg}";
                SynthAddAudioPlayer(msg);
            }
            else if (InCall && user == callInUsername)
            {
                SynthAddAudioPlayer(message);
            }
            if ((lastMessage != null && (DateTime.Now - lastMessage) < TimeSpan.FromSeconds((double)config.MessageCd)) || config.IgnoreList.Contains(user))
                return;
            lastMessage = DateTime.Now;
            if ((!glasses && !TTS) || (InCall && user.ToLower() != callInUsername))
                PlaySound(SoundFile.Message);
        }

        private void Client_OnChatCommandReceived(object? sender, OnChatCommandReceivedArgs e)
        {
            var cmd = e.Command;
            Console.WriteLine($"Command received: {cmd.CommandText}");
            switch (cmd.CommandText)
            {
                case "hangup":
                    if (cmd.ChatMessage.Username != callInUsername) return;
                    CallTCS.TrySetResult(Closer.User);
                    break;
            }
        }

        private async Task GlassesTimer()
        {
            glasses = true;
            await Task.Delay(300000);
            glasses = false;
            synth.SelectVoice(UserVoices.First().Value);
            SynthAddAudioPlayer($"Lose the glasses redeem has ended.");
        }

        private void PlaySound(SoundFile fileName)
        {
            soundPlayers[fileName].PlaySync();
        }

        private void SynthAddAudioPlayer(string message)
        {
            MemoryStream ms = new MemoryStream();
            synth.SetOutputToWaveStream(ms);
            //synth.Speak(message); // decided to let the user raw-dog the SSML, what could possibly go wrong
            var msg = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
                            <voice name='" + synth.Voice.Name + @"'>
                                " + message + @"
                            </voice>
                        </speak>";
            synth.SpeakSsml(msg);
            ms.Position = 0;
            synthPlayer = new SoundPlayer(ms);
            synthPlayer.Play();
        }

        private async Task AddRewards()
        {
            AddedRewards = new Dictionary<string, Rewards>();
            foreach (var r in RewardsList)
            {
                try
                {
                    var res = await Api.Helix.ChannelPoints.CreateCustomRewardsAsync(Owner.Id, new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest()
                    {
                        Title = r.Name,
                        Cost = r.Cost,
                        IsUserInputRequired = r.Prompt == string.Empty ? false : true,
                        Prompt = r.Prompt,
                        GlobalCooldownSeconds = r.Cooldown,
                        IsGlobalCooldownEnabled = r.Global,
                        IsMaxPerStreamEnabled = r.MaxStream == 0 ? false : true,
                        MaxPerStream = r.MaxStream,
                        IsMaxPerUserPerStreamEnabled = r.MaxUser == 0 ? false : true,
                        MaxPerUserPerStream = r.MaxUser,
                        ShouldRedemptionsSkipRequestQueue = r.SkipQueue,
                        IsEnabled = r.StartEnabled
                    });
                    AddedRewards[r.Name] = (new Rewards() { RewardId = res.Data[0].Id });
                }
                catch (Exception ex)
                {
                    LoggingAsync($"Error: {r.Name} -> {ex.ToString()}");
                }
            }
        }

        private async Task UpdateRedeemAsync(string rewardId, List<string> redeemIds, bool fulfill)
        {
            try
            {
                await Api.Helix.ChannelPoints.UpdateRedemptionStatusAsync(Owner.Id, rewardId, redeemIds, new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest()
                {
                    Status = (fulfill ? CustomRewardRedemptionStatus.FULFILLED : CustomRewardRedemptionStatus.CANCELED)
                });
            }
            catch (Exception ex)
            {
                LoggingAsync($"Error: {rewardId} {ex.ToString()}");
            }
        }

        protected override List<AuthScopes> _scopes =>
            new List<AuthScopes>()
            {
                AuthScopes.Channel_Read_Redemptions,
                AuthScopes.Channel_Manage_Redemptions,
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
            public bool AutoConnect { get; set; } = false;
            public List<string> IgnoreList { get; set; }

            public Config()
            {
                IgnoreList = new List<string>();
            }
        }

        public class SynthUser
        {
            public string Voice { get; set; }
            public int Volume { get; set; }
            public int Rate { get; set; }
        }

        public class Reward
        {
            public string? Name { get; set; }
            public int Cost { get; set; } = 10;
            public string Prompt { get; set; } = string.Empty;
            public int Cooldown { get; set; } = 60;
            public bool Global { get; set; } = false;
            public int MaxUser { get; set; } = 0;
            public int MaxStream { get; set; } = 0;
            public bool SkipQueue { get; set; } = true;
            public bool StartEnabled { get; set; } = true;
        }

        public class Rewards
        {
            public string RewardId { get; set; }
            public List<string> RedemtionIds { get; set; }

            public Rewards()
            {
                RedemtionIds = new List<string>();
            }
        }

        public enum Closer
        {
            Owner,
            User
        }

        public enum SoundFile
        {
            Notification,
            TimeUp,
            Message,
            Redeem,
            Follow,
            Ringer,
            Dialtone,
            Pickup,
            Hangup
        }

        private static List<Reward> RewardsList = new List<Reward>()
        {
            new Reward{
                Name = "Call-In",
                Cost = 100,
                Cooldown = 120,
                SkipQueue = false,
                StartEnabled = false
            }
        };
    }
}