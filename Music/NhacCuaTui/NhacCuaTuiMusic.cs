using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CatBot.Music.NhacCuaTui
{
    internal class NhacCuaTuiMusic : IMusic
    {
        static readonly string searchLink = "https://www.nhaccuatui.com/ajax/search?type=song&q=";
        internal static readonly string nhacCuaTuiLink = "https://www.nhaccuatui.com/";
        internal static Regex regexMatchIDSong = new Regex(".*-\\.([a-zA-Z0-9]*)\\.html", RegexOptions.Compiled);
        static readonly string findArtistLink = "https://www.nhaccuatui.com/tim-kiem?b=singer&q=";
        static readonly string nhacCuaTuiIconLink = "https://cdn.discordapp.com/emojis/1124397223359299725.webp?quality=lossless";  //You may need to change this
        string link;
        TimeSpan duration;
        string title = "";
        string artists = "";
        string album;
        string albumThumbnailLink;
        string mp3FilePath;
        string pcmFile;
        FileStream musicPCMDataStream;
        bool canGetStream;
        bool _disposed;
        string mp3Link;

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
                title = $"[{obj["name"]}]({obj["url"]})";
                foreach (JToken singer in obj["singer"])
                    artists += $"[{singer["name"]}]({singer["url"]})";
            }
            XmlDocument xmlDoc = GetXML(link);
            if (linkOrKeyword.StartsWith("ID: "))
                link = xmlDoc.DocumentElement["track"].SelectSingleNode("info").InnerText;
            if (string.IsNullOrWhiteSpace(title))
                title = $"[{xmlDoc.DocumentElement["track"].SelectSingleNode("title").InnerText}]({link})";
            if (string.IsNullOrWhiteSpace(artists))
            {
                string[] array = xmlDoc.DocumentElement["track"].SelectSingleNode("creator").InnerText.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < array.Length; i++)
                {
                    if (i == 0)
                    {
                        string firstArtistsLink = xmlDoc.DocumentElement["track"].SelectSingleNode("newtab").InnerText;
                        artists += $"[{array[i].Trim()}]({firstArtistsLink}), ";
                    }
                    else
                        artists += $"[{array[i].Trim()}]({findArtistLink + Uri.EscapeUriString(array[i].Trim())}), ";
                }
                artists = artists.TrimEnd(", ".ToCharArray());
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
            new WebClient().DownloadFile(mp3Link, mp3FilePath);
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            File.Delete(mp3FilePath);
            mp3FilePath = null;
        }

        ~NhacCuaTuiMusic() => Dispose(false);

        public MusicType MusicType => MusicType.NhacCuaTui;

        public string PathOrLink => link;

        public TimeSpan Duration => duration;

        public string Title => title;

        public string Artists => artists;

        public string Album => album;

        public string AlbumThumbnailLink => albumThumbnailLink;

        public Stream MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                return musicPCMDataStream;
            }
        }

        public SponsorBlockOptions SponsorBlockOptions
        {
            get => null;
            set { }
        }
        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by NhacCuaTui", nhacCuaTuiIconLink);

        public LyricData GetLyric()
        {
            XmlDocument xmlDoc = GetXML(link);
            string lyricLink = xmlDoc.DocumentElement["track"].SelectSingleNode("lyric").InnerText;
            if (string.IsNullOrWhiteSpace(lyricLink) || lyricLink == "https://lrc-nct.nixcdn.com/null")
            {
                string html = MusicUtils.GetHttpRequestWithCookie().Get(link).ToString();
                int startIndex = html.IndexOf("<p id=\"divLyric\" class=\"pd_lyric trans\" style=\"height:auto;max-height:255px;overflow:hidden;\">") + 94;
                string htmlLyric = html.Substring(startIndex, html.IndexOf("<div class=\"more_add\" id=\"divMoreAddLyric\">") - startIndex).Replace("<br />", "");
                string lyric = string.Join(Environment.NewLine, WebUtility.HtmlDecode(htmlLyric).Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())).Replace("</p>", "").Trim();
                if (lyric.StartsWith("- Hiện chưa có lời bài hát"))
                    return new LyricData("Bài hát này không có lời trên server của NhacCuaTui!");
                else
                    return new LyricData(MusicUtils.RemoveEmbedLink(title), MusicUtils.RemoveEmbedLink(artists), lyric, albumThumbnailLink);
            }
            string encryptedLyric = "";
            try
            { 
                encryptedLyric = new WebClient().DownloadString(lyricLink);
            }
            catch (WebException) 
            { 
                return new LyricData("Lỗi khi lấy lời bài hát!");
            }
            string decryptedLyric = RemoveEmptyLines(MusicUtils.RemoveLyricTimestamps(DecryptLyric(encryptedLyric)));
            return new LyricData(MusicUtils.RemoveEmbedLink(title), MusicUtils.RemoveEmbedLink(artists), decryptedLyric, albumThumbnailLink);
        }

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {title}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {artists}" + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(album))
                musicDesc += $"Album: {album}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public string[] GetFilesInUse() => new string[] { mp3FilePath, pcmFile };

        public string GetIcon() => Config.gI().NCTIcon;

        public bool isLinkMatch(string link) => link.StartsWith(nhacCuaTuiLink);

        internal static XmlDocument GetXML(string link)
        {
            string html = MusicUtils.GetHttpRequestWithCookie().Get(link).ToString();
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
            string xml = MusicUtils.GetHttpRequestWithCookie().Get(linkContainInfo).ToString();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return xmlDoc;
        }

        static JToken FindSongInfo(string keyword)
        {
            JObject obj = JObject.Parse(MusicUtils.GetHttpRequestWithCookie().Get(searchLink + Uri.EscapeUriString(keyword)).ToString());
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

        static string RemoveEmptyLines(string str)
        {
            return string.Join(Environment.NewLine, str.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
        }

        internal static string GetSongID(string link)
        {
            if (link.StartsWith(nhacCuaTuiLink))
                link = regexMatchIDSong.Match(link).Groups[1].Value;
            return link;
        }

        public string GetPCMFilePath() => pcmFile;

        public void DeletePCMFile()
        {
            try
            {
                File.Delete(pcmFile);
                pcmFile = null;
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
