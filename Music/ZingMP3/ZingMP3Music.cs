using System.Net;
using System.Text.RegularExpressions;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace CatBot.Music.ZingMP3
{
    internal partial class ZingMP3Music : IMusic
    {
        internal static readonly string zingMP3Link = "https://zingmp3.vn/";
        static readonly string zingMP3IconLink = "https://static-zmp3.zmdcdn.me/skins/zmp3-v5.2/images/icon_zing_mp3_60.png";

        [GeneratedRegex(@"zingmp3\.vn\/bai-hat\/.*?\/(Z[A-Z0-9]{7})\.html", RegexOptions.Compiled)]
        private static partial Regex GetRegexMatchIDSongInLink();
        [GeneratedRegex("Z[A-Z0-9]{7}", RegexOptions.Compiled)]
        private static partial Regex GetRegexMatchIDSong();
        [GeneratedRegex("https://zjs.zmdcdn.me/zmp3-desktop/releases/(.*?)/static/js/main.min.js", RegexOptions.Compiled)]
        private static partial Regex GetMainMinJSRegex();

        static string zingMP3Version = "";
        string link = "";
        string mp3FilePath = "";
        TimeSpan duration;
        string title = "";
        string[] artists = [];
        string[] artistsWithLinks = [];
        string albumThumbnailLink = "";
        string pcmFile = "";
        string album = "";
        string albumWithLink = "";
        FileStream? musicPCMDataStream;
        bool canGetStream;
        bool _disposed;
        LyricData? lyric;

        static HttpClient? httpRequestWithCookie;
        static DateTime lastTimeCheckZingMP3Version = DateTime.Now.Subtract(new TimeSpan(12, 1, 0));

        public ZingMP3Music() { }

        public ZingMP3Music(string linkOrKeyword)
        {
            JToken songDesc;
            if (linkOrKeyword.StartsWith(zingMP3Link))
            {
                link = linkOrKeyword;
                songDesc = GetSongInfo(link);
            }
            else if (GetRegexMatchIDSong().IsMatch(linkOrKeyword))
            {
                songDesc = GetSongInfo(linkOrKeyword);
                link = zingMP3Link.TrimEnd('/') + songDesc["link"];
            }
            else
            {
                songDesc = GetSongInfo(FindSongID(linkOrKeyword));
                link = zingMP3Link.TrimEnd('/') + songDesc["link"];
            }
            title = songDesc["title"].Value<string>();
            if (songDesc["artists"] != null)
            {
                artistsWithLinks = songDesc["artists"].Select(artist => Formatter.MaskedUrl(artist["name"].Value<string>(), new Uri(zingMP3Link.TrimEnd('/') + artist["link"]))).ToArray();
                artists = songDesc["artists"].Select(artist => artist["name"].Value<string>()).ToArray();
            }
            else
                artists = artistsWithLinks = songDesc["artistsNames"].Value<string>().Split(",").Select(artist => artist.Trim()).ToArray();
            if (songDesc["album"] != null)
            {
                albumWithLink = Formatter.MaskedUrl(songDesc["album"]["title"].Value<string>(), new Uri(zingMP3Link.TrimEnd('/') + songDesc["album"]["link"]));
                album = songDesc["album"]["title"].Value<string>();
            }
            albumThumbnailLink = songDesc["thumbnailM"].Value<string>();
        }

        public void Download()
        {
            mp3FilePath = Path.GetTempFileName();
            HttpClient httpClient = new HttpClient();
            byte[] data = httpClient.GetByteArrayAsync(GetMP3Link(link)).Result;
            File.WriteAllBytes(mp3FilePath, data);
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            //File.Delete(mp3FilePath);
            //mp3FilePath = null;
        }

        ~ZingMP3Music() => Dispose(false);

        public MusicType MusicType => MusicType.ZingMP3;
        public string PathOrLink => link;
        public TimeSpan Duration => duration;
        public string Title => title;
        public string TitleWithLink => Formatter.MaskedUrl(title, new Uri(link));
        public string[] Artists => artists;
        public string[] ArtistsWithLinks => artistsWithLinks;
        public string AllArtists => string.Join(", ", artists);
        public string AllArtistsWithLinks => string.Join(", ", artistsWithLinks);
        public string Album => album;
        public string AlbumWithLink => albumWithLink;
        public string AlbumThumbnailLink => albumThumbnailLink;
        public SponsorBlockOptions? SponsorBlockOptions
        {
            get => null;
            set { }
        }

        public Stream? MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                return musicPCMDataStream;
            }
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Zing MP3", zingMP3IconLink);

        public LyricData? GetLyric()
        {
            if (lyric != null)
                return lyric;
            JObject jsonLyricData = (JObject)GetSongLyricInfo(link);
            if (!jsonLyricData.ContainsKey("file") && !jsonLyricData.ContainsKey("sentences"))
                return new LyricData("Bài hát này không có lời trên Zing MP3!");
            string plainLyrics = "";
            string syncedLyrics = "";
            string enhancedLyrics = "";
            if (jsonLyricData.ContainsKey("file"))
            {
                HttpClient httpClient = new HttpClient();
                syncedLyrics = httpClient.GetStringAsync(jsonLyricData["file"].Value<string>()).Result;
                plainLyrics = MusicUtils.RemoveLyricTimestamps(syncedLyrics);
            }
            if (jsonLyricData.ContainsKey("sentences"))
            {
                long lastSentenceTimestamp = 0;
                foreach (JToken sentence in jsonLyricData["sentences"])
                {
                    string lyricSentence = "";
                    long lastTimestamp = 0;
                    foreach (JToken word in sentence["words"])
                    {
                        long timestamp = word["startTime"].Value<long>();
                        if (timestamp != lastTimestamp)
                        {
                            if (string.IsNullOrEmpty(lyricSentence))
                                lyricSentence += $"[{TimeSpan.FromMilliseconds(timestamp):mm\\:ss\\.ff}]";
                            else
                                lyricSentence += $"<{TimeSpan.FromMilliseconds(timestamp):mm\\:ss\\.ff}>";
                            lastTimestamp = timestamp;
                        }
                        lyricSentence += word["data"].Value<string>() + ' ';
                    }
                    lyricSentence = lyricSentence.Trim();
                    long lastWordTimestamp = sentence["words"].Last()["endTime"].Value<long>();
                    lyricSentence += $"<{TimeSpan.FromMilliseconds(lastWordTimestamp):mm\\:ss\\.ff}>";
                    if (lastWordTimestamp - lastSentenceTimestamp > 5000)
                        enhancedLyrics += $"[{TimeSpan.FromMilliseconds(lastSentenceTimestamp + 500):mm\\:ss\\.ff}]♪{Environment.NewLine}";
                    lastSentenceTimestamp = lastWordTimestamp;
                    enhancedLyrics += lyricSentence + Environment.NewLine;
                }
                enhancedLyrics += $"[{TimeSpan.FromMilliseconds(jsonLyricData["sentences"].Last()["words"].Last()["endTime"].Value<long>()):mm\\:ss\\.ff}]♪";
            }
            return lyric = new LyricData(title, AllArtists, album, albumThumbnailLink)
            {
                PlainLyrics = plainLyrics,
                SyncedLyrics = syncedLyrics,
                EnhancedLyrics = enhancedLyrics
            };
        }

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {TitleWithLink}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {AllArtistsWithLinks}" + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(AlbumWithLink))
                musicDesc += $"Album: {AlbumWithLink}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public string[] GetFilesInUse() => [mp3FilePath, pcmFile];

        public string GetIcon() => Config.gI().ZingMP3Icon;

        public bool isLinkMatch(string link) => link.StartsWith(zingMP3Link);

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload(".mp3", new FileStream(mp3FilePath, FileMode.Open, FileAccess.Read));

        internal static JToken GetSongInfo(string linkOrID)
        {
            JObject obj = GetInfoFromZingMP3("/api/v2/page/get/song", $"id={GetSongID(linkOrID)}");
            if (obj["err"].ToString() == "0")
                return obj["data"];
            throw new MusicException(MusicType.ZingMP3, obj["err"] + ": " + obj["msg"]);
        }

        static JToken GetSongLyricInfo(string linkOrID)
        {
            JObject obj = GetInfoFromZingMP3("/api/v2/lyric/get/lyric", $"id={GetSongID(linkOrID)}");
            if (obj["err"].ToString() == "0")
                return obj["data"];
            throw new MusicException(MusicType.ZingMP3, obj["err"] + ": " + obj["msg"]);
        }

        static string GetMP3Link(string zingMP3Link)
        {
            JObject obj = GetInfoFromZingMP3("/api/v2/song/get/streaming", $"id={GetSongID(zingMP3Link)}");
            if (obj["err"].ToString() == "0")
                return obj["data"]["128"].ToString();
            throw new MusicException(MusicType.ZingMP3, obj["err"] + ": " + obj["msg"]);
        }

        internal static string FindSongID(string name)
        {
            JObject obj = GetInfoFromZingMP3("/api/v2/search", $"q={Uri.EscapeDataString(name)}", "type=song", "page=1", $"count=1");
            if (obj["err"].ToString() != "0")
                throw new MusicException("songs not found");
            return obj["data"]["items"][0]["encodeId"].ToString();
        }

        internal static JToken SearchSongs(string query, int count)
        {
            JObject obj = GetInfoFromZingMP3("/api/v2/search", $"q={query}", "type=song", "page=1", $"count={count}");
            if (obj["err"].ToString() == "0")
                return obj["data"];
            throw new MusicException(MusicType.ZingMP3, obj["err"] + ": " + obj["msg"]);
        }

        static JObject GetInfoFromZingMP3(string apiEndpoint, params string[] parameters)
        {
            HttpClient http = GetHttpClientWithCookie();
            string ctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string secretKey = Config.gI().ZingMP3SecretKey;
            string apiKey = Config.gI().ZingMP3APIKey;
            List<string> listParameters = parameters.ToList();
            listParameters.Add($"ctime={ctime}");
            listParameters.Add($"version={zingMP3Version}");
            string webParameter = string.Join("&", listParameters.ToArray());
            listParameters = listParameters.Where(s => s.StartsWith("ctime") || s.StartsWith("id") || s.StartsWith("type") || s.StartsWith("page") || s.StartsWith("count") || s.StartsWith("version")).ToList();
            listParameters.Sort(string.Compare);
            string parameter = string.Join("", listParameters.ToArray());
            string hash = apiEndpoint + MusicUtils.HashSHA256(parameter);
            string sig = MusicUtils.HashSHA512(hash, secretKey);
            string getSongInfoUrl = $"{zingMP3Link.TrimEnd('/')}{apiEndpoint}?{webParameter}&sig={sig}&apiKey={apiKey}";
            string str = http.GetStringAsync(getSongInfoUrl).Result;
            JObject obj = JObject.Parse(str);
            return obj;
        }

        internal static string GetSongID(string link)
        {
            if (link.StartsWith(zingMP3Link))
                link = GetRegexMatchIDSongInLink().Match(link).Groups[1].Value;
            return link;
        }

        static HttpClient GetHttpClientWithCookie()
        {
            if (httpRequestWithCookie == null)
            {
                zingMP3Version = "1.10.50";
                UpdateCookies();
            }
            CheckZingMP3Version();
            return httpRequestWithCookie;
        }

        internal static void CheckZingMP3Version()
        {
            if ((DateTime.Now - lastTimeCheckZingMP3Version).TotalHours <= 12)
                return;
            int count = 0;
            string ver = "";
            do
            {
                if (count++ > 3)
                {
                    ver = "1.10.50";
                    break;
                }
                string zingMP3Web = httpRequestWithCookie.GetStringAsync(zingMP3Link).Result;
                int startIndex = zingMP3Web.LastIndexOf("/static/js/main.min.js") - 100;
                if (startIndex >= 0)
                {
                    zingMP3Web = zingMP3Web.Substring(startIndex);
                    ver = GetMainMinJSRegex().Match(zingMP3Web).Groups[1].Value.Replace("v", "");
                }
                if (string.IsNullOrWhiteSpace(ver))
                    Thread.Sleep(2000);
            }
            while (string.IsNullOrWhiteSpace(ver));
            if (ver != zingMP3Version)
            {
                zingMP3Version = ver;
                UpdateCookies();
                httpRequestWithCookie.GetAsync(zingMP3Link).Wait();
            }
            lastTimeCheckZingMP3Version = DateTime.Now;
        }

        static void UpdateCookies()
        {
            httpRequestWithCookie = MusicUtils.CreateHttpClientWithCookies($"zmp3_app_version.1={zingMP3Version.Replace(".", "")}; " + Config.gI().ZingMP3Cookie);
            httpRequestWithCookie.DefaultRequestHeaders.Add("User-Agent", Config.gI().UserAgent);
            httpRequestWithCookie.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpRequestWithCookie.DefaultRequestHeaders.Add("Referer", zingMP3Link);
            httpRequestWithCookie.DefaultRequestHeaders.Add("Accept", "*/*");
            httpRequestWithCookie.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9");
        }

        public string GetPCMFilePath() => pcmFile;

        public void DeletePCMFile()
        {
            try
            {
                File.Delete(pcmFile);
                pcmFile = "";
                musicPCMDataStream?.Dispose();
                musicPCMDataStream = null;
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (disposing)
            {
                musicPCMDataStream?.Dispose();
                DeletePCMFile();
                try
                {
                    File.Delete(mp3FilePath);
                }
                catch (Exception) { }
            }
            musicPCMDataStream = null;
        }
    }
}
