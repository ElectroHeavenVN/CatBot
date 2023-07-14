using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Music.YouTube
{
    internal class YouTubeMusic : IMusic
    {
        private static readonly string youTubeIconLink = "https://www.gstatic.com/youtube/img/branding/favicon/favicon_144x144.png";
        private static readonly string youTubeMusicIconLink = "https://www.gstatic.com/youtube/media/ytm/images/applauncher/music_icon_144x144.png";
        private static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=1&type=video";
        internal static Regex regexMatchYTLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        internal static Regex regexMatchYTMusicLink = new Regex("^((?:https?:)?\\/\\/)?((?:music)\\.)?((?:youtube\\.com|youtu.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        string link;
        TimeSpan duration;
        string title = "";
        string artists = "";
        string album;
        string albumThumbnailLink;
        string webmFilePath;
        string pcmFile;
        FileStream musicPCMDataStream;
        bool canGetStream;
        bool _disposed;
        bool isYouTubeMusicVideo;

        public YouTubeMusic() { }

        public YouTubeMusic(string linkOrKeyword)
        {
            if (regexMatchYTLink.IsMatch(linkOrKeyword))
            {
                isYouTubeMusicVideo = regexMatchYTMusicLink.IsMatch(linkOrKeyword);
                string videoID = regexMatchYTLink.Match(linkOrKeyword).Groups[5].Value;
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{searchVideoAPI}&key={Config.GoogleAPIKey}&q={Uri.EscapeUriString(videoID)}");
                JObject searchResult = JObject.Parse(json);
                link = $"https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/watch?v={searchResult["items"][0]["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResult["items"][0]["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(searchResult["items"][0]["snippet"]["channelTitle"].ToString())}](https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/channel/{searchResult["items"][0]["snippet"]["channelId"]})";
                albumThumbnailLink = searchResult["items"][0]["snippet"]["thumbnails"]["high"]["url"].ToString();
            }
            else
            {
                JObject searchResult = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{searchVideoAPI}&key={Config.GoogleAPIKey}&q={Uri.EscapeUriString(linkOrKeyword)}"));
                link = $"https://www.youtube.com/watch?v={searchResult["items"][0]["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResult["items"][0]["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(searchResult["items"][0]["snippet"]["channelTitle"].ToString())}](https://www.youtube.com/channel/{searchResult["items"][0]["snippet"]["channelId"]})";
                albumThumbnailLink = searchResult["items"][0]["snippet"]["thumbnails"]["high"]["url"].ToString();
            }
            new Thread(GetDuration) { IsBackground = true }.Start();
        }

        void GetDuration()
        {
            DownloadWEBM(link, ref webmFilePath);
            TagLib.File webmFile = TagLib.File.Create(webmFilePath, "taglib/webm", TagLib.ReadStyle.Average);
            duration = webmFile.Properties.Duration;
            webmFile.Dispose();
            canGetStream = true;
        }

        ~YouTubeMusic() => Dispose(false);

        public MusicType MusicType => MusicType.YouTube;

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
                if (musicPCMDataStream == null)
                {
                    while (!canGetStream)
                        Thread.Sleep(500);
                    musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(webmFilePath, ref pcmFile));
                    File.Delete(webmFilePath);
                    webmFilePath = null;
                }
                return musicPCMDataStream;
            }
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicVideo ? " Music" : ""), isYouTubeMusicVideo ? youTubeMusicIconLink : youTubeIconLink);

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

        public LyricData GetLyric() => new LyricData("Video YouTube không có lời!");

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"{(isYouTubeMusicVideo? "Bài hát" : "Video")}: {title}" + Environment.NewLine;
            musicDesc += $"Tải lên bởi: {artists}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + "/" + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public string[] GetFilesInUse() => new string[] { webmFilePath, pcmFile };

        public string GetIcon() => isYouTubeMusicVideo ? "<:YouTubeMusic:1126482892332224522>" : "<:YouTube:1125189836194709595>";

        static void DownloadWEBM(string link, ref string tempFile)
        {
            tempFile = Path.GetTempFileName();
            Console.WriteLine("--------------yt-dlp Console output--------------");
            Process yt_dlp_x86 = new Process() 
            { 
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp_x86",
                    Arguments = $"-f \"bestaudio\" --paths {Path.GetDirectoryName(tempFile)} -o {Path.GetFileName(tempFile)} --force-overwrites {link}",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            yt_dlp_x86.OutputDataReceived += (_, e) => Console.WriteLine(e.Data);
            yt_dlp_x86.ErrorDataReceived += (_, e) => Console.WriteLine(e.Data);
            yt_dlp_x86.Start();
            yt_dlp_x86.BeginErrorReadLine();
            yt_dlp_x86.BeginOutputReadLine();
            yt_dlp_x86.WaitForExit();
            Console.WriteLine("--------------End of yt-dlp Console output--------------");
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
                    File.Delete(webmFilePath);
                }
                catch (Exception) { }
            }
            musicPCMDataStream = null;
        }
    }
}
