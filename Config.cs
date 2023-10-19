using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot
{
    internal class Config
    {
        static Dictionary<string, string> logValue = new Dictionary<string, string>();

        internal static Config singletonInstance = new Config();
        internal static Config gI() => singletonInstance;

        /// <summary>
        /// ID Server chính dùng để điều khiển bot (dùng lệnh /admin, báo lỗi, cache ảnh cho <see cref="Music.Local.LocalMusic"/>)
        /// </summary>
        [JsonProperty("MainServerID")] 
        internal ulong MainServerID { get; set; } 
        internal DiscordGuild mainServer;

        /// <summary>
        /// ID server admin (Server chứa các thành viên được sử dụng SFX đặc biệt và được sử dụng lệnh /emoji với emoji trong server này)
        /// </summary>
        [JsonProperty("AdminServerID")] 
        internal ulong AdminServerID { get; set; }
        internal DiscordGuild adminServer;

        /// <summary>
        /// ID kênh cache ảnh
        /// </summary>
        [JsonProperty("CacheImageChannelID")] 
        internal ulong CacheImageChannelID { get; set; } 
        internal DiscordChannel cacheImageChannel;

        /// <summary>
        /// ID kênh báo lỗi bot
        /// </summary>
        [JsonProperty("LogExceptionChannelID")] 
        internal ulong LogExceptionChannelID { get; set; } 
        internal DiscordChannel exceptionReportChannel;

        /// <summary>
        /// Kênh log lỗi
        /// </summary>
        [JsonProperty("DebugChannelID")]
        internal ulong DebugChannelID { get; set; }
        internal DiscordChannel debugChannel;

        /// <summary>
        /// ID chủ của bot
        /// </summary>
        [JsonProperty("BotOwnersID")]
        internal ulong[] BotOwnersID { get; set; } = new ulong[0];

        /// <summary>
        /// Đường dẫn tới thư mục chứa nhạc
        /// </summary>
        [JsonProperty("MusicFolder")]
        internal string MusicFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX
        /// </summary>
        [JsonProperty("SFXFolder")]
        internal string SFXFolder { get; set; } = "SFX";
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX đặc biệt
        /// </summary>
        [JsonProperty("SFXFolderSpecial")]
        internal string SFXFolderSpecial { get; set; } = "SFX\\Special";
        /// <summary>
        /// Cho biết lệnh chat điều khiển bot có được kích hoạt hay không
        /// </summary>
        [JsonProperty("EnableCommandsNext")]
        internal bool EnableCommandsNext { get; set; } = true;

        #region Zing MP3
        /// <summary>
        /// Mã khóa bí mật Zing MP3
        /// </summary>
        [JsonProperty("ZingMP3SecretKey")]
        internal string ZingMP3SecretKey { get; set; } = "";
        /// <summary>
        /// API key Zing MP3
        /// </summary>
        [JsonProperty("ZingMP3APIKey")]
        internal string ZingMP3APIKey { get; set; } = "";
        /// <summary>
        /// Cookie Zing MP3
        /// </summary>
        [JsonProperty("ZingMP3Cookie")]
        internal string ZingMP3Cookie { get; set; } = "";
        #endregion

        #region YouTube & YouTube Music
        /// <summary>
        /// API key Google
        /// </summary>
        [JsonProperty("GoogleAPIKey")]
        internal string GoogleAPIKey { get; set; } = "";
        #endregion

        #region Zalo AI
        /// <summary>
        /// Zalo AI cookie
        /// </summary>
        [JsonProperty("ZaloAICookie")] 
        internal string ZaloAICookie { get; set; } = "";
        #endregion

        #region Spotify
        /// <summary>
        /// Spotify cookie
        /// </summary>
        [JsonProperty("SpotifyCookie")]
        internal string SpotifyCookie { get; set; } = "";

        /// <summary>
        /// Tài khoản Spotify
        /// </summary>
        [JsonProperty("SpotifyUsername")] 
        internal string SpotifyUsername { get; set; } = "";

        /// <summary>
        /// Mật khẩu Spotify
        /// </summary>
        [JsonProperty("SpotifyPassword")]
        internal string SpotifyPassword { get; set; } = "";
        #endregion

        /// <summary>
        /// User agent
        /// </summary>
        [JsonProperty("UserAgent")]
        internal string UserAgent { get; set; } = "";

        /// <summary>
        /// API tìm lời bài hát
        /// </summary>
        [JsonProperty("LyricAPI")]
        internal string LyricAPI { get; set; } = "https://lyrist.vercel.app/api/";

        [JsonProperty("LocalMusicIcon")]
        internal string LocalMusicIcon { get; set; } = "";
        [JsonProperty("NCTIcon")]
        internal string NCTIcon { get; set; } = "";
        [JsonProperty("ZingMP3Icon")]
        internal string ZingMP3Icon { get; set; } = "";
        [JsonProperty("YouTubeIcon")]
        internal string YouTubeIcon { get; set; } = "";
        [JsonProperty("YouTubeMusicIcon")]
        internal string YouTubeMusicIcon { get; set; } = "";
        [JsonProperty("SoundCloudIcon")]
        internal string SoundCloudIcon { get; set; } = "";
        [JsonProperty("SpotifyIcon")]
        internal string SpotifyIcon { get; set; } = "";

        /// <summary>
        /// Token bot
        /// </summary>
        [JsonProperty("BotToken")]
        internal string BotToken { get; set; } = "";

        /// <summary>
        /// Prefix lệnh mặc định
        /// </summary>
        [JsonProperty("DefaultPrefix")]
        internal string DefaultPrefix { get; set; } = "";

        internal static void ImportConfig(string configPath) => singletonInstance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

        internal static void ExportConfig(string configPath) => File.WriteAllText(configPath, JsonConvert.SerializeObject(singletonInstance, Formatting.Indented));
    }
}
