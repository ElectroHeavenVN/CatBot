using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace CatBot.Music.NhacCuaTui
{
    internal partial class NhacCuaTuiMusic : IMusic
    {
        [GeneratedRegex(".*-\\.([a-zA-Z0-9]*)\\.html", RegexOptions.Compiled)]
        private static partial Regex GetRegexMatchIDSong();

        static readonly string searchLink = "https://www.nhaccuatui.com/ajax/search?type=song&q=";
        internal static readonly string nhacCuaTuiLink = "https://www.nhaccuatui.com/";
        static readonly string findArtistLink = "https://www.nhaccuatui.com/tim-kiem?b=singer&q=";
        static readonly string nhacCuaTuiIconLink = "https://cdn.discordapp.com/emojis/1124397223359299725.webp?quality=lossless";  
        string link = "";
        TimeSpan duration;
        string title = "";
        string[] artists = [];
        string[] artistsWithLinks = [];
        string albumThumbnailLink = "";
        string mp3FilePath = "";
        string pcmFile = "";
        FileStream? musicPCMDataStream;
        bool canGetStream;
        bool _disposed;
        string mp3Link = "";
        static HttpClient? httpRequestWithCookie;
        LyricData? lyric;

        public NhacCuaTuiMusic() { }

        public NhacCuaTuiMusic(string linkOrKeyword)
        {
            if (linkOrKeyword.StartsWith(nhacCuaTuiLink))
                link = linkOrKeyword;
            else if (linkOrKeyword.StartsWith("ID: "))
                    link = nhacCuaTuiLink + "bai-hat/." + linkOrKeyword.Remove(0, 4) + ".html";
            else
            {
                JToken obj = FindSongInfo(linkOrKeyword);
                link = obj["url"].ToString();
                title = obj["name"].ToString();
                artists = obj["singer"].Select(singer => singer["name"].ToString()).ToArray();
                artistsWithLinks = obj["singer"].Select(singer => Formatter.MaskedUrl(singer["name"].ToString(), new Uri(singer["url"].ToString()))).ToArray();
            }
            XmlDocument xmlDoc = GetXML(link);
            if (linkOrKeyword.StartsWith("ID: "))
                link = xmlDoc.DocumentElement["track"].SelectSingleNode("info").InnerText;
            if (string.IsNullOrWhiteSpace(title))
                title = xmlDoc.DocumentElement["track"].SelectSingleNode("title").InnerText;
            if (artists.Length == 0)
            {
                string[] array = xmlDoc.DocumentElement["track"].SelectSingleNode("creator").InnerText.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                artists = new string[array.Length];
                artistsWithLinks = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    if (i == 0)
                    {
                        string firstArtistsLink = xmlDoc.DocumentElement["track"].SelectSingleNode("newtab").InnerText;
                        artists[i] = array[i].Trim();
                        artistsWithLinks[i] = Formatter.MaskedUrl(array[i].Trim(), new Uri(firstArtistsLink));
                    }
                    else
                    {
                        artists[i] = array[i].Trim();
                        artistsWithLinks[i] = Formatter.MaskedUrl(array[i].Trim(), new Uri(findArtistLink + Uri.EscapeDataString(array[i].Trim())));
                    }
                }
            }
            albumThumbnailLink = xmlDoc.DocumentElement["track"].SelectSingleNode("avatar").InnerText;
            mp3Link = xmlDoc.DocumentElement["track"].SelectSingleNode("location").InnerText;
            string hasHQ = xmlDoc.DocumentElement["track"].SelectSingleNode("hasHQ").InnerText;
            if (!string.IsNullOrWhiteSpace(hasHQ) && bool.Parse(hasHQ))
                mp3Link = xmlDoc.DocumentElement["track"].SelectSingleNode("locationHQ").InnerText;
        }

        public void Download()
        {
            mp3FilePath = Path.GetTempFileName();
            using (HttpClient client = new HttpClient())
            {
                var data = client.GetByteArrayAsync(mp3Link).Result;
                File.WriteAllBytes(mp3FilePath, data);
            }
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            //File.Delete(mp3FilePath);
            //mp3FilePath = null;
        }

        ~NhacCuaTuiMusic() => Dispose(false);

        public MusicType MusicType => MusicType.NhacCuaTui;
        public string PathOrLink => link;
        public TimeSpan Duration => duration;
        public string Title => title;
        public string TitleWithLink => Formatter.MaskedUrl(title, new Uri(link));
        public string[] Artists => artists;
        public string[] ArtistsWithLinks => artistsWithLinks;
        public string AllArtists => string.Join(", ", artists);
        public string AllArtistsWithLinks => string.Join(", ", artistsWithLinks);
        public string Album => "";
        public string AlbumWithLink => "";
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

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by NhacCuaTui", nhacCuaTuiIconLink);

        public LyricData? GetLyric()
        {
            if (lyric != null)
                return lyric;
            XmlDocument xmlDoc = GetXML(link);
            string lyricLink = xmlDoc.DocumentElement["track"].SelectSingleNode("lyric").InnerText;

            string plainLyrics = "";
            if (string.IsNullOrWhiteSpace(lyricLink) || lyricLink == "https://lrc-nct.nixcdn.com/null")
            {
                string html = GetHttpClientWithCookie().GetStringAsync(link).Result;
                int startIndex = html.IndexOf("<p id=\"divLyric\" class=\"pd_lyric trans\" style=\"height:auto;max-height:255px;overflow:hidden;\">") + 94;
                string htmlLyric = html.Substring(startIndex, html.IndexOf("<div class=\"more_add\" id=\"divMoreAddLyric\">") - startIndex).Replace("<br />", "");
                string lyric = string.Join(Environment.NewLine, WebUtility.HtmlDecode(htmlLyric).Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())).Replace("</p>", "").Trim();
                if (lyric.StartsWith("- Hiện chưa có lời bài hát"))
                    return new LyricData("Bài hát này không có lời trên server của NhacCuaTui!");
                else
                    plainLyrics = lyric;
            }
            string encryptedLyric = "";
            try
            { 
                encryptedLyric = GetHttpClientWithCookie().GetStringAsync(lyricLink).Result;
            }
            catch (WebException) 
            { 
                if (string.IsNullOrWhiteSpace(plainLyrics))
                    return new LyricData("Lỗi khi lấy lời bài hát!");
                else 
                    return lyric = new LyricData(title, AllArtists, "", albumThumbnailLink) { PlainLyrics = plainLyrics };
            }
            string syncedLyrics = DecryptLyric(encryptedLyric);
            return lyric = new LyricData(title, AllArtists, "", albumThumbnailLink)
            {
                PlainLyrics = plainLyrics,
                SyncedLyrics = syncedLyrics
            };
        }

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {TitleWithLink}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {AllArtistsWithLinks}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public string[] GetFilesInUse() => [mp3FilePath, pcmFile];

        public string GetIcon() => Config.gI().NCTIcon;

        public bool isLinkMatch(string link) => link.StartsWith(nhacCuaTuiLink);

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload(".mp3", new FileStream(mp3FilePath, FileMode.Open, FileAccess.Read));

        internal static XmlDocument GetXML(string link)
        {
            string html = GetHttpClientWithCookie().GetStringAsync(link).Result;
            Regex regex = new Regex("player\\.peConfig\\.xmlURL = \"(.*)\";");
            string linkContainInfo = regex.Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(linkContainInfo))
            {
                if (html.Contains("Sorry, this content is currently not available in your country"))
                    throw new MusicException(MusicType.NhacCuaTui, "not available");
                if (html.Contains("bài hát này dành riêng cho người dùng VIP. Mời bạn nâng cấp để thưởng thức."))
                    throw new MusicException(MusicType.NhacCuaTui, "VIP only");
                throw new MusicException("not found");
            }
            string xml = GetHttpClientWithCookie().GetStringAsync(linkContainInfo).Result;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return xmlDoc;
        }

        static JToken FindSongInfo(string keyword)
        {
            JObject obj = JObject.Parse(GetHttpClientWithCookie().GetStringAsync(searchLink + Uri.EscapeDataString(keyword)).Result);
            if (obj["error_code"].ToString() != "0")
                throw new MusicException("songs not found");
            if (obj["data"]["song"].Count() == 0)
                throw new MusicException("songs not found");
            return obj["data"]["song"][0];
        }

        static string DecryptLyric(string encryptedLyric)
        {
            //class and variable name are the same as JS
            var a = PUtils.HexToArray(encryptedLyric);
            var b = PUtils.HexToArray(PUtils.HexFromString("Lyr1cjust4"));
            var c = new ARC4();
            c.Load(b);
            var d = c.Decrypt(a);
            return PUtils.HexFromArray(d);
        }

        internal static string GetSongID(string link)
        {
            if (link.StartsWith(nhacCuaTuiLink))
                link = GetRegexMatchIDSong().Match(link).Groups[1].Value;
            return link;
        }

        static HttpClient GetHttpClientWithCookie()
        {
            if (httpRequestWithCookie == null)
            {
                httpRequestWithCookie = MusicUtils.CreateHttpClientWithCookies("");
                httpRequestWithCookie.DefaultRequestHeaders.Add("User-Agent", Config.gI().UserAgent);
                httpRequestWithCookie.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                httpRequestWithCookie.DefaultRequestHeaders.Add("Referer", nhacCuaTuiLink);
                httpRequestWithCookie.DefaultRequestHeaders.Add("Accept", "*/*");
                httpRequestWithCookie.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9");
                httpRequestWithCookie.GetAsync(nhacCuaTuiLink).Wait();
            }
            return httpRequestWithCookie;
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
