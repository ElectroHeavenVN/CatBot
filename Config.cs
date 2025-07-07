#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
using CatBot.Extensions;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.IO;

namespace CatBot
{
    internal class Config
    {
        internal static Config singletonInstance = new Config();
        internal static Config gI() => singletonInstance;

        /// <summary>
        /// ID Server chính dùng để điều khiển bot (dùng lệnh admin, báo lỗi, cache ảnh cho <see cref="Music.Local.LocalMusic"/>)
        /// </summary>
        [JsonProperty("MainServer")]
        internal ulong MainServerID { get; set; }
        internal DiscordGuild mainServer;

        /// <summary>
        ///  Danh sách các thành viên được sử dụng SFX đặc biệt
        /// </summary>
        [JsonProperty("AdminUsers")]
        internal ulong[] AdminUserIDs { get; set; } = [];

        /// <summary>
        /// ID kênh cache ảnh
        /// </summary>
        [JsonProperty("CacheImageChannel")]
        internal ulong CacheImageChannelID { get; set; }
        internal DiscordChannel cacheImageChannel;

        /// <summary>
        /// ID kênh báo lỗi bot
        /// </summary>
        [JsonProperty("LogExceptionChannel")]
        internal ulong LogExceptionChannelID { get; set; }
        internal DiscordChannel exceptionReportChannel;

        /// <summary>
        /// Kênh log lỗi
        /// </summary>
        [JsonProperty("DebugChannel")]
        internal ulong DebugChannelID { get; set; }
        internal DiscordChannel debugChannel;

        /// <summary>
        /// ID chủ của bot
        /// </summary>
        [JsonProperty("BotOwners")]
        internal ulong[] BotOwnerIDs { get; set; } = [];

        /// <summary>
        /// ID bot loại trừ
        /// </summary>
        [JsonProperty("ExcludeBots")]
        internal ulong[] ExcludeBotIDs { get; set; } = [];

        /// <summary>
        /// Đường dẫn tới thư mục chứa nhạc
        /// </summary>
        [JsonProperty(nameof(MusicFolder))]
        internal string MusicFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX
        /// </summary>
        [JsonProperty(nameof(SFXFolder))]
        internal string SFXFolder { get; set; } = "Files\\SFX";
        /// <summary>
        /// Đường dẫn tới thư mục chứa SFX đặc biệt
        /// </summary>
        [JsonProperty(nameof(SFXFolderSpecial))]
        internal string SFXFolderSpecial { get; set; } = "Files\\SFX\\Special";

        #region Zing MP3
        /// <summary>
        /// Mã khóa bí mật Zing MP3
        /// </summary>
        [JsonProperty(nameof(ZingMP3SecretKey))]
        internal string ZingMP3SecretKey { get; set; } = "";
        /// <summary>
        /// API key Zing MP3
        /// </summary>
        [JsonProperty(nameof(ZingMP3APIKey))]
        internal string ZingMP3APIKey { get; set; } = "";
        /// <summary>
        /// Cookie Zing MP3
        /// </summary>
        [JsonProperty(nameof(ZingMP3Cookie))]
        internal string ZingMP3Cookie { get; set; } = "";
        #endregion

        #region YouTube & YouTube Music
        /// <summary>
        /// API key Google
        /// </summary>
        [JsonProperty(nameof(GoogleAPIKey))]
        internal string GoogleAPIKey { get; set; } = "";
        #endregion

        #region Spotify
        /// <summary>
        /// Spotify cookie
        /// </summary>
        [JsonProperty(nameof(SpotifyCookie))]
        internal string SpotifyCookie { get; set; } = "";

        /// <summary>
        /// Spotify Client ID
        /// </summary>
        [JsonProperty(nameof(SpotifyClientID))]
        internal string SpotifyClientID { get; set; } = "";

        /// <summary>
        /// Spotify Client Secret
        /// </summary>
        [JsonProperty(nameof(SpotifyClientSecret))]
        internal string SpotifyClientSecret { get; set; } = "";

        #endregion

        #region SoundCloud
        /// <summary>
        /// Client ID SoundCloud
        /// </summary>
        [JsonProperty(nameof(SoundCloudClientID))]
        internal string SoundCloudClientID { get; set; } = "";
        #endregion

        /// <summary>
        /// User agent
        /// </summary>
        [JsonProperty(nameof(UserAgent))]
        internal string UserAgent { get; set; } = "";

        [JsonProperty(nameof(NCTIcon))]
        internal string NCTIcon { get; set; } = "";
        [JsonProperty(nameof(ZingMP3Icon))]
        internal string ZingMP3Icon { get; set; } = "";
        [JsonProperty(nameof(YouTubeIcon))]
        internal string YouTubeIcon { get; set; } = "";
        [JsonProperty(nameof(YouTubeMusicIcon))]
        internal string YouTubeMusicIcon { get; set; } = "";
        [JsonProperty(nameof(SoundCloudIcon))]
        internal string SoundCloudIcon { get; set; } = "";
        [JsonProperty(nameof(SpotifyIcon))]
        internal string SpotifyIcon { get; set; } = "";

        /// <summary>
        /// Token bot
        /// </summary>
        [JsonProperty(nameof(BotToken))]
        internal string BotToken { get; set; } = "";

        /// <summary>
        /// Prefix lệnh mặc định
        /// </summary>
        [JsonProperty(nameof(DefaultPrefix))]
        internal string DefaultPrefix { get; set; } = "";

        /// <summary>
        /// Trạng thái mặc định của bot
        /// </summary>
        [JsonProperty(nameof(DefaultPresences))]
        internal CustomDiscordActivity[] DefaultPresences { get; set; } = 
        [
            new CustomDiscordActivity(0, DiscordActivityType.ListeningTo, "Zing MP3"),
            new CustomDiscordActivity(0, DiscordActivityType.ListeningTo, "NhacCuaTui"),
            new CustomDiscordActivity(0, DiscordActivityType.ListeningTo, "YouTube Music"),
            new CustomDiscordActivity(0, DiscordActivityType.ListeningTo, "SoundCloud"),
            new CustomDiscordActivity(0, DiscordActivityType.ListeningTo, "Spotify"),
            new CustomDiscordActivity(0, DiscordActivityType.Watching, "YouTube")
        ];

        internal string LyricAPI => "https://lrclib.net/api/";

        internal static void ImportConfig(string configPath)
        {
            singletonInstance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath)) ?? throw new NullReferenceException();
            if (!Directory.Exists(singletonInstance.MusicFolder))
                singletonInstance.MusicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        }

        internal static void ExportConfig(string configPath) => File.WriteAllText(configPath, JsonConvert.SerializeObject(singletonInstance, Formatting.Indented));
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.