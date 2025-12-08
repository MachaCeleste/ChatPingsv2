using FoxCrypto;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace ChatPingsv2
{
    class Program
    {
        private static string _filePath;
        private static string? _passkey;
        private static readonly ManualResetEventSlim _exit = new ManualResetEventSlim();
        static async Task Main()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPressed;
            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileInfo.CompanyName, fileInfo.ProductName);
            await Login();
            Console.WriteLine("Bot ready.");
            if (TwitchBot.Singleton.config.AutoConnect)
                await TwitchBot.Singleton.Connect();
            while (true)
            {
                Console.Write("> ");
                string[] x = Console.ReadLine().Split(' ');
                switch (x[0])
                {
                    case "connect":
                        await TwitchBot.Singleton.Connect();
                        break;
                    case "add":
                        if (x.Length < 1)
                        {
                            Console.WriteLine("Usage: add (username)");
                            break;
                        }
                        TwitchBot.Singleton.config.IgnoreList.Add(x[1]);
                        Console.WriteLine($"Adding {x[1]} to ignore list");
                        SaveConfig();
                        break;
                    case "remove":
                        if (x.Length < 1)
                        {
                            Console.WriteLine("Usage: remove (username)");
                            break;
                        }
                        TwitchBot.Singleton.config.IgnoreList.Remove(x[1]);
                        Console.WriteLine($"Removing {x[1]} from ignore list");
                        SaveConfig();
                        break;
                    case "list":
                        string listOutput = $" ----- Ignore List -----";
                        var ignoreList = TwitchBot.Singleton.config.IgnoreList;
                        if (ignoreList.Count == 0)
                            listOutput += "\n[ EMPTY ]";
                        else
                        {
                            foreach (string username in ignoreList)
                            {
                                listOutput += $"\n{username}";
                            }
                        }
                        Console.WriteLine(listOutput);
                        break;
                    case "message":
                        if (x.Length < 1)
                        {
                            Console.WriteLine("Usage: message (cooldown in seconds)");
                            break;
                        }
                        if (int.TryParse(x[1], out int messageCd))
                        {
                            TwitchBot.Singleton.config.MessageCd = messageCd;
                            Console.WriteLine($"Setting message cooldown to {x[1]} seconds");
                            SaveConfig();
                        }
                        break;
                    case "tts":
                        TwitchBot.Singleton.TTS = !TwitchBot.Singleton.TTS;
                        if (TwitchBot.Singleton.CallIn)
                        {
                            TwitchBot.Singleton.SetRewardEnabled(TwitchBot.Singleton.AddedRewards["Call-In"].RewardId, false);
                            TwitchBot.Singleton.CallIn = false;
                        }
                        Console.WriteLine($"Setting TTS to {(TwitchBot.Singleton.TTS ? "ENABLED" : "DISABLED")}");
                        break;
                    case "stop":
                        TwitchBot.Singleton.StopSynth();
                        break;
                    case "reroll":
                        if (x.Length < 1)
                        {
                            Console.WriteLine("Usage: reroll (username)");
                            break;
                        }
                        TwitchBot.Singleton.RerollUserVoice(x[1]);
                        break;
                    case "callin":
                        TwitchBot.Singleton.CallIn = !TwitchBot.Singleton.CallIn;
                        if (TwitchBot.Singleton.TTS) TwitchBot.Singleton.TTS = false;
                        TwitchBot.Singleton.SetRewardEnabled(TwitchBot.Singleton.AddedRewards["Call-In"].RewardId, TwitchBot.Singleton.CallIn);
                        Console.WriteLine($"Setting Call-In to {(TwitchBot.Singleton.CallIn ? "ENABLED" : "DISABLED")}");
                        break;
                    case "hangup":
                        if (TwitchBot.Singleton.IsInCall())
                            TwitchBot.Singleton.UserHangup();
                        break;
                    case "auto":
                        TwitchBot.Singleton.config.AutoConnect = !TwitchBot.Singleton.config.AutoConnect;
                        Console.WriteLine($"Setting auto connect to {(TwitchBot.Singleton.config.AutoConnect ? "ENABLED" : "DISABLED")}");
                        SaveConfig();
                        break;
                    case "sayname":
                        TwitchBot.Singleton.SayName = !TwitchBot.Singleton.SayName;
                        Console.WriteLine($"Setting Say Name to {(TwitchBot.Singleton.SayName ? "ENABLED" : "DISABLED")}");
                        break;
                    case "reload":
                        TwitchBot.Singleton.InitSounds();
                        Console.WriteLine("Reloaded sounds");
                        break;
                    case "custom":
                        // TODO add custom command here
                        Console.WriteLine("Enter command name: ");
                        string name = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            Console.WriteLine("Error: command name cannot be null or empty!");
                            return;
                        }
                        Console.WriteLine("Enter command output: ");
                        string output = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine("Error: command output cannot be null or empty!");
                            return;
                        }
                        TwitchBot.Singleton.AddCustomCommand(name, output);
                        Console.WriteLine("Command added!");
                        break;
                    case "settings":
                        var conf = TwitchBot.Singleton.config;
                        Console.WriteLine($"  ----- Settings -----\n" +
                                        $"  Message : {conf.MessageCd}");
                        break;
                    case "cls":
                    case "clear":
                        Console.Clear();
                        break;
                    case "-h":
                    case "--help":
                    default:
                        Console.WriteLine($"       ----- Commands -----\n" +
                                        $"   connect : Connect to twitch.\n" +
                                        $"       add : Add username to ignore list\n" +
                                        $"    remove : Remove username from ignore list\n" +
                                        $"      list : Show ignore list\n" +
                                        $"   message : Set the cooldown of message pings\n" +
                                        $"       tts : Toggle TTS on/off\n" +
                                        $"      stop : Stop the current TTS message\n" +
                                        $"    reroll : Remove the users voice so it is rerolled\n" +
                                        $"    callin : Toggle Call-In on/off\n" +
                                        $"    hangup : End current call\n" +
                                        $"   sayname : Toggle saying name in TTS on/off\n" +
                                        $"      auto : Toggle autoconnect on/off\n" +
                                        $"    reload : Reload all sounds from disk\n" +
                                        $"    custom : Start adding new custom command\n" +
                                        $" cls/clear : Clear console\n" +
                                        $" -h/--help : Show this help text");
                        break;
                }
            }
        }

        private static async Task Login()
        {
            if (!File.Exists(_filePath))
                Directory.CreateDirectory(_filePath);
            TwitchBot.Config config = File.Exists(Path.Combine(_filePath, "config.dat")) ? LoadConfig() : null;
            if (config == null)
                new TwitchBot();
            else 
                new TwitchBot(config.Token);
            if (TwitchBot.Singleton == null)
            {
                Console.WriteLine("Error while instantiating bot.");
                Environment.Exit(0);
            }
            TwitchBot.Singleton._filePath = _filePath;
            TwitchBot.Singleton.muteLogging = true;
            if (config == null || string.IsNullOrEmpty(config.ClientId) || string.IsNullOrEmpty(config.Secret))
            {
                var info = GetBotInfo(config);
                config = new TwitchBot.Config()
                {
                    ClientId = info.Key,
                    Secret = info.Value,
                };
            }
            config.Token = await TwitchBot.Singleton.InitAsync(config.ClientId, config.Secret);
            TwitchBot.Singleton.config = config;
            if (!SaveConfig())
                Environment.Exit(0);
        }

        private static TwitchBot.Config LoadConfig()
        {
            string? json = null;
            while (true)
            {
                Console.Write("Please ender datacrypt password: ");
                string? pass = ReadPassword();
                if (string.IsNullOrEmpty(pass))
                {
                    Console.WriteLine("Invalid password!");
                    continue;
                }
                if (pass == "exit" || pass == "cancel")
                    Environment.Exit(0);
                _passkey = Crypto.GetPasskey(pass);
                string input = File.ReadAllText(Path.Combine(_filePath, "config.dat"));
                json = Crypto.Run(input, _passkey, "dec");
                if (json == null)
                {
                    Console.WriteLine("Invalid password!");
                    continue;
                }
                break;
            }
            Console.WriteLine("Config loaded.");
            TwitchBot.Config? config = JsonConvert.DeserializeObject<TwitchBot.Config>(json);
            return config;
        }

        private static bool SaveConfig()
        {
            while (true)
            {
                if (string.IsNullOrEmpty(_passkey))
                {
                    Console.Write("Please enter a datacrypt password: ");
                    string? pass = ReadPassword();
                    if (string.IsNullOrEmpty(pass))
                    {
                        Console.WriteLine("Invalid password!");
                        continue;
                    }
                    _passkey = Crypto.GetPasskey(pass);
                }
                break;
            }
            string json = JsonConvert.SerializeObject(TwitchBot.Singleton.config);
            string? output = Crypto.Run(json, _passkey);
            try
            {
                File.WriteAllText(Path.Combine(_filePath, "config.dat"), output);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            Console.WriteLine("Config saved.");
            return true;
        }

        private static KeyValuePair<string, string> GetBotInfo(TwitchBot.Config config)
        {
            string? clientId = null;
            string? secret = null;
            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ClientId))
                    clientId = config.ClientId;
                if (!string.IsNullOrEmpty(config.Secret))
                    secret = config.Secret;
            }
            while (true)
            {
                if (string.IsNullOrEmpty(clientId))
                {
                    Console.Write("Enter your bot Client ID: ");
                    clientId = ReadPassword();
                    if (string.IsNullOrEmpty(clientId))
                    {
                        Console.WriteLine("Invalid client ID.");
                        continue;
                    }
                }
                if (string.IsNullOrEmpty(secret))
                {
                    Console.Write("Enter your bot Secret: ");
                    secret = ReadPassword();
                    if (string.IsNullOrEmpty(secret))
                    {
                        Console.WriteLine("Invalid secret!");
                        continue;
                    }
                }
                break;
            }
            KeyValuePair<string, string> info = new KeyValuePair<string, string>(clientId, secret);
            return info;
        }

        private static string ReadPassword()
        {
            StringBuilder password = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            return password.ToString();
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                await TwitchBot.Singleton.RemoveRewards();
                _exit.Set();
            });
            _exit.Wait();
        }

        private static void OnCancelKeyPressed(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            Task.Run(async () =>
            {
                await TwitchBot.Singleton.RemoveRewards();
                Environment.Exit(0);
            });
        }
    }
}