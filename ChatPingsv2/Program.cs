using FoxCrypto;
using Newtonsoft.Json;
using OverlayFramework;
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
                switch (x[0]) // TODO: Clean up this mess and functionalize every case to avoid variable name conflicts
                {
                    case "connect":
                        await TwitchBot.Singleton.Connect();
                        break;
                    case "add":
                        if (x.Length < 2)
                        {
                            Console.WriteLine("Usage: add (username)");
                            break;
                        }
                        TwitchBot.Singleton.config.IgnoreList.Add(x[1]);
                        Console.WriteLine($"Adding {x[1]} to ignore list");
                        SaveConfig();
                        break;
                    case "remove":
                        if (x.Length < 2)
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
                    case "msgcd":
                        if (x.Length < 2)
                        {
                            Console.WriteLine("Usage: msgcd (cooldown in seconds)");
                            break;
                        }
                        if (int.TryParse(x[1], out int messageCd))
                        {
                            TwitchBot.Singleton.config.MessageCd = messageCd;
                            Console.WriteLine($"Setting message sound cooldown to {x[1]} seconds");
                            SaveConfig();
                        }
                        break;
                    case "ai":
                        if (!TwitchBot.Singleton.AiOn) Console.WriteLine("Ensure your AI is running!");
                        TwitchBot.Singleton.AiOn = !TwitchBot.Singleton.AiOn;
                        Console.WriteLine($"Setting AI-On to {(TwitchBot.Singleton.AiOn ? "ENABLED" : "DISABLED")}");
                        if (TwitchBot.Singleton.AiOn) TwitchBot.Singleton.InitKobold();
                        break;
                    case "aireset":
                        TwitchBot.Singleton.InitKobold();
                        Console.WriteLine("Re-Initializing AI...");
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
                        if (x.Length < 2)
                        {
                            Console.WriteLine("Usage: reroll (username)");
                            break;
                        }
                        TwitchBot.Singleton.RerollUserVoice(x[1]);
                        Console.WriteLine($"Rerolled user voice for {x[1]}!");
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
                    case "custom": // TODO: Can be simplified...
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
                        Console.WriteLine("Enter command cooldown in seconds: ");
                        if (!int.TryParse(Console.ReadLine(), out int cd) || cd < 3)
                        {
                            Console.WriteLine("Error: cooldown must be a number above 3!");
                            return;
                        }
                        TwitchBot.Singleton.AddCustomCommand(name, output, cd);
                        Console.WriteLine("Command added!");
                        break;
                    case "delete":
                        if (x.Length < 2)
                        {
                            Console.WriteLine("Usage: delete (command)");
                            break;
                        }
                        TwitchBot.Singleton.DeleteCustomCommand(x[1]);
                        break;
                    case "settings":
                        var conf = TwitchBot.Singleton.config;
                        Console.WriteLine($"  ----- Settings -----\n" +
                                        $"  Message : {conf.MessageCd}");
                        break;
                    case "overlay":
                        OverlayServer.Singleton.IsEnabled = !OverlayServer.Singleton.IsEnabled;
                        Console.WriteLine($"Overlay {(OverlayServer.Singleton.IsEnabled ? "ENABLED" : "DISABLED")}");
                        break;
                    case "message":
                        if (x.Length < 2 || int.TryParse(x[1], out int mduration) || mduration < 1)
                        {
                            Console.WriteLine("Usage: message (duration min:1)");
                            break;
                        }
                        OverlayServer.Singleton.MessageDuration = mduration * 1000;
                        Console.WriteLine($"Set message timeout duration to {mduration} seconds");
                        break;
                    case "notification":
                        if (x.Length < 2 || int.TryParse(x[1], out int nduration) || nduration < 1)
                        {
                            Console.WriteLine("Usage: notification (duration min:1)");
                            break;
                        }
                        OverlayServer.Singleton.NotificationDuration = nduration * 1000;
                        Console.WriteLine($"Set notification timeout duration to {nduration} seconds");
                        break;
                    case "cls":
                    case "clear":
                        Console.Clear();
                        break;
                    case "-h":
                    case "--help":
                    default:
                        Console.WriteLine($"" +
                                        $"       ----- Commands -----\n" +
                                        $"    -h/--help : Show this help text\n" +
                                        $"      connect : Connect to twitch\n" +
                                        $"         auto : Toggle autoconnect on/off\n" +
                                        $"       custom : Start adding new custom command\n" +
                                        $"     settings : Display all settings\n" +
                                        $"    cls/clear : Clear console\n" +
                                        $"       ----- Overlay -----\n" +
                                        $"      overlay : Toggle overlay on/off\n" +
                                        $"      message : Set timeout in seconds of messages in the overlay\n" +
                                        $" notification : Set timeout in seconds of notifications in the overlay\n" +
                                        $"       ----- Ignore List -----\n" +
                                        $"          add : Add username to ignore list\n" +
                                        $"       remove : Remove username from ignore list\n" +
                                        $"         list : Show ignore list\n" +
                                        $"       ----- Sounds -----\n" +
                                        $"        msgcd : Set the cooldown of message pings\n" +
                                        $"       reload : Reload all sounds from disk\n" +
                                        $"       ----- TTS -----\n" +
                                        $"       callin : Toggle Call-In on/off\n" +
                                        $"       hangup : End current call\n" +
                                        $"          tts : Toggle TTS on/off\n" +
                                        $"      sayname : Toggle saying name in TTS on/off\n" +
                                        $"       reroll : Remove the users voice so it is rerolled\n" +
                                        $"         stop : Stop the current TTS message\n" +
                                        $"       ----- Kobold -----\n" +
                                        $"           ai : Toggles AI use on/off\n" +
                                        $"      aireset : Re-Initialize the ai to a clean state\n" +
                                        $"");
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