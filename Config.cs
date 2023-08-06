using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    internal class Config
    {
        static Dictionary<string, string> logValue = new Dictionary<string, string>();

        /// <summary>
        /// Server chính dùng để điều khiển bot (dùng lệnh /admin, báo lỗi, cache ảnh cho <see cref="Music.Local.LocalMusic"/>)
        /// </summary>
        internal static DiscordGuild mainServer;
        /// <summary>
        /// ID Server chính
        /// </summary>
        internal static ulong MainServerID => GetConfigValue<ulong>("MainServerID");

        /// <summary>
        /// Server chứa các thành viên được sử dụng SFX đặc biệt và được sử dụng lệnh /emoji với emoji trong server này
        /// </summary>
        internal static DiscordGuild adminServer;
        /// <summary>
        /// ID server admin
        /// </summary>
        internal static ulong AdminServerID => GetConfigValue<ulong>("AdminServerID");

        /// <summary>
        /// Kênh cache ảnh
        /// </summary>
        internal static DiscordChannel cacheImageChannel;
        /// <summary>
        /// ID kênh cache ảnh
        /// </summary>
        internal static ulong CacheImageChannelID => GetConfigValue<ulong>("CacheImageChannelID");

        /// <summary>
        /// Kênh báo lỗi bot
        /// </summary>
        internal static DiscordChannel exceptionReportChannel;
        /// <summary>
        /// ID kênh báo lỗi bot
        /// </summary>
        internal static ulong ExceptionReportChannelID => GetConfigValue<ulong>("ExceptionReportChannelID");

        /// <summary>
        /// ID tác giả của bot
        /// </summary>
        internal static ulong[] BotAuthorsID => GetConfigValue<string>("BotAuthorsID").Split(',').Select(s => ulong.Parse(s)).ToArray();

        /// <summary>
        /// Đường dẫn tới thư mục chứa nhạc
        /// </summary>
        internal static string MusicFolder => GetConfigValue<string>("MusicFolder") ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX
        /// </summary>
        internal static string SFXFolder => GetConfigValue<string>("SFXFolder") ?? "SFX";
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX đặc biệt
        /// </summary>
        internal static string SFXFolderSpecial => GetConfigValue<string>("SFXFolderSpecial") ?? "SFX\\Special";

        #region Zing MP3
        /// <summary>
        /// Mã khóa bí mật Zing MP3
        /// </summary>
        internal static string ZingMP3SecretKey => GetConfigValue<string>("ZingMP3SecretKey");
        /// <summary>
        /// API key Zing MP3
        /// </summary>
        internal static string ZingMP3APIKey => GetConfigValue<string>("ZingMP3APIKey");
        /// <summary>
        /// Cookie Zing MP3
        /// </summary>
        internal static string ZingMP3Cookie => "zmp3_app_version.1={0}; " + GetConfigValue<string>("ZingMP3Cookie");
        #endregion

        #region YouTube & YouTube Music
        /// <summary>
        /// API key Google
        /// </summary>
        internal static string GoogleAPIKey => GetConfigValue<string>("GoogleAPIKey");
        #endregion

        #region Zalo AI
        /// <summary>
        /// Cookie Zalo AI
        /// </summary>
        internal static string ZaloAICookie => GetConfigValue<string>("ZaloAICookie");
        #endregion

        #region Spotify
        internal static string SpotifyCookie => GetConfigValue<string>("SpotifyCookie");
        #endregion

        /// <summary>
        /// User agent
        /// </summary>
        internal static string UserAgent => GetConfigValue<string>("UserAgent");

        /// <summary>
        /// Sec-Ch-Ua header
        /// </summary>
        internal static string SecChUaHeader => GetConfigValue<string>("Sec-Ch-Ua");

        /// <summary>
        /// API tìm lời bài hát
        /// </summary>
        internal static string LyricAPI => GetConfigValue<string>("LyricAPI") ?? "https://lyrist.vercel.app/api/";

        internal static string LocalMusicIcon => GetConfigValue<string>("LocalMusicIcon");
        internal static string NCTIcon => GetConfigValue<string>("NCTIcon");
        internal static string ZingMP3Icon => GetConfigValue<string>("ZingMP3Icon");
        internal static string YouTubeIcon => GetConfigValue<string>("YouTubeIcon");
        internal static string YouTubeMusicIcon => GetConfigValue<string>("YouTubeMusicIcon");
        internal static string SoundCloudIcon => GetConfigValue<string>("SoundCloudIcon");
        internal static string SpotifyIcon => GetConfigValue<string>("SpotifyIcon");

        /// <summary>
        /// Token bot (để login vào Discord)
        /// </summary>
        internal static string BotToken =>
#if DEBUG
                GetConfigValue<string>("BotTokenDebug");
#else
                GetConfigValue<string>("BotToken");
#endif

        /// <summary>
        /// Prefix lệnh
        /// </summary>
        internal static string Prefix =>
#if DEBUG
                GetConfigValue<string>("PrefixDebug");
#else
                GetConfigValue<string>("Prefix"); 
#endif

        static T GetConfigValue<T>(string configName)
        {
            string configFile = "Config\\Config.txt";
            if (!File.Exists(configFile))
            {
                Console.WriteLine("Config file not found, creating one...");
                Directory.CreateDirectory("Config");
                File.Create(configFile);
                Process.Start(Path.GetFullPath(configFile));
                Environment.Exit(1);
            }
            if (string.IsNullOrWhiteSpace(File.ReadAllText(configFile)))
            {
                Console.WriteLine("Empty config file!");
                Process.Start(Path.GetFullPath(configFile));
                Environment.Exit(1);
            }
            IEnumerable<string> configs = File.ReadAllLines(configFile).Where(s => !s.StartsWith("#"));
            if (configs.Any(s => s.StartsWith(configName + '=')))
            {
                string value = configs.First(s => s.StartsWith(configName + '=')).Remove(0, configName.Length + 1);
                if (!logValue.ContainsKey(configName))
                {
                    logValue.Add(configName, value);
                    if (!configName.StartsWith("BotToken"))
                        Console.WriteLine("Loaded config \"" + configName + "\": " + value);
                }
                else if (logValue[configName] != value)
                {
                    if (!configName.StartsWith("BotToken"))
                        Console.WriteLine("Config \"" + configName + "\" changed: " + logValue[configName] + " => " + value);
                    logValue[configName] = value;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            else
            {
                Console.WriteLine("Config \"" + configName + "\" not found!");
                return default;
            }
        }
    }
}
