using CatBot.Music.SponsorBlock;
using CatBot.Voice;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using Leaf.xNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CatBot.Music.ZingMP3
{
    internal class ZingMP3Music : IMusic
    {
        internal static readonly string zingMP3Link = "https://zingmp3.vn/";
        static readonly string zingMP3IconLink = "https://static-zmp3.zmdcdn.me/skins/zmp3-v5.2/images/icon_zing_mp3_60.png";
        internal static string zingMP3Version;
        internal static Regex regexMatchIDSong = new Regex("/([a-zA-Z0-9]+).html", RegexOptions.Compiled);
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

        public ZingMP3Music() { }

        public ZingMP3Music(string linkOrKeyword)
        {
            JToken songDesc;
            if (linkOrKeyword.StartsWith(zingMP3Link))
            {
                link = linkOrKeyword;
                songDesc = GetSongInfo(link);
            }
            else if (linkOrKeyword.StartsWith("ID: "))
            {
                songDesc = GetSongInfo(linkOrKeyword.Remove(0, 4));
                link = zingMP3Link.TrimEnd('/') + songDesc["link"];
            }
            else
            {
                songDesc = GetSongInfo(FindSongID(linkOrKeyword));
                link = zingMP3Link.TrimEnd('/') + songDesc["link"];
            }
            title = $"[{songDesc["title"]}]({link})";
            if (songDesc["artists"] != null)
            {
                foreach (JToken artist in songDesc["artists"])
                    artists += $"[{artist["name"]}]({zingMP3Link.TrimEnd('/') + artist["link"]}), ";
                artists = artists.TrimEnd(" ,".ToCharArray());
            }
            else
                artists += songDesc["artistsNames"];
            if (songDesc["album"] != null)
                album = $"[{songDesc["album"]["title"]}]({zingMP3Link.TrimEnd('/') + songDesc["album"]["link"]})";
            albumThumbnailLink = songDesc["thumbnailM"].ToString();
        }

        public void Download()
        {
            mp3FilePath = Path.GetTempFileName();
            new WebClient().DownloadFile(GetMP3Link(link), mp3FilePath);
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            File.Delete(mp3FilePath);
            mp3FilePath = null;
        }

        ~ZingMP3Music() => Dispose(false);

        public MusicType MusicType => MusicType.ZingMP3;

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
        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Zing MP3", zingMP3IconLink);

        public LyricData GetLyric()
        {
            JObject jsonLyricData = (JObject)GetSongLyricInfo(link);
            if (!jsonLyricData.ContainsKey("file"))
                return new LyricData("Bài hát này không có lời trên server của Zing MP3!");
            string lyric = MusicUtils.RemoveLyricTimestamps(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(jsonLyricData["file"].ToString()));
            return new LyricData(MusicUtils.RemoveEmbedLink(title), MusicUtils.RemoveEmbedLink(artists), lyric, albumThumbnailLink);
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

        public string GetIcon() => Config.gI().ZingMP3Icon;

        public bool isLinkMatch(string link) => link.StartsWith(zingMP3Link);

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
            JObject obj = GetInfoFromZingMP3("/api/v2/search", $"q={Uri.EscapeUriString(name)}", "type=song", "page=1", $"count=1");
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
            HttpRequest http = MusicUtils.GetHttpRequestWithCookie();
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
            string str = http.Get(getSongInfoUrl, null).ToString();
            JObject obj = JObject.Parse(str);
            return obj;
        }

        internal static string GetSongID(string link)
        {
            if (link.StartsWith(zingMP3Link))
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
