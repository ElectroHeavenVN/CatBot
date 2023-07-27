using DiscordBot.Instance;
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
using System.Xml;

namespace DiscordBot.Music.YouTube
{
    internal class YouTubeMusic : IMusic
    {
        private static readonly string youTubeIconLink = "https://www.gstatic.com/youtube/img/branding/favicon/favicon_144x144.png";
        private static readonly string youTubeMusicIconLink = "https://www.gstatic.com/youtube/media/ytm/images/applauncher/music_icon_144x144.png";
        private static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=1&type=video";
        private static readonly string getVideoInfoAPI = "https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2CcontentDetails";
        private static readonly string sponsorBlockSegmentsAPI = "https://sponsor.ajay.app/api/skipSegments";
        internal static Regex regexMatchYTLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        internal static Regex regexMatchYTMusicLink = new Regex("^((?:https?:)?\\/\\/)?((?:music)\\.)?((?:youtube\\.com|youtu.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        string link;
        TimeSpan duration;
        TimeSpan durationBeforeSponsorBlock;
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
        SponsorBlockOptions sponsorBlockOptions;
        bool hasSponsorBlockSegment;
        string videoID;

        public YouTubeMusic() { }

        public YouTubeMusic(string linkOrKeyword)
        {
            if (regexMatchYTLink.IsMatch(linkOrKeyword))
            {
                isYouTubeMusicVideo = regexMatchYTMusicLink.IsMatch(linkOrKeyword);
                videoID = regexMatchYTLink.Match(linkOrKeyword).Groups[5].Value;
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{getVideoInfoAPI}&key={Config.GoogleAPIKey}&id={Uri.EscapeUriString(videoID)}");
                JToken videoResource = JObject.Parse(json)["items"][0];
                videoID = videoResource["id"].ToString();
                link = $"https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/watch?v={videoResource["id"]}";
                title = $"[{WebUtility.HtmlDecode(videoResource["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(videoResource["snippet"]["channelTitle"].ToString())}](https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/channel/{videoResource["snippet"]["channelId"]})";
                albumThumbnailLink = videoResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                durationBeforeSponsorBlock = XmlConvert.ToTimeSpan(videoResource["contentDetails"]["duration"].ToString());
            }
            else
            {
                JToken searchResource = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{searchVideoAPI}&key={Config.GoogleAPIKey}&q={Uri.EscapeUriString(linkOrKeyword)}"))["items"][0];
                videoID = searchResource["id"]["videoId"].ToString();
                link = $"https://www.youtube.com/watch?v={searchResource["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["channelTitle"].ToString())}](https://www.youtube.com/channel/{searchResource["snippet"]["channelId"]})";
                albumThumbnailLink = searchResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{getVideoInfoAPI}&key={Config.GoogleAPIKey}&id={Uri.EscapeUriString(searchResource["id"]["videoId"].ToString())}");
                JToken videoResource = JObject.Parse(json)["items"][0];
                durationBeforeSponsorBlock = XmlConvert.ToTimeSpan(videoResource["contentDetails"]["duration"].ToString());
            }
            new Thread(GetDuration) { IsBackground = true }.Start();
        }

        void GetDuration()
        {
            DownloadWEBM(link, ref webmFilePath);
            TagLib.File webmFile = TagLib.File.Create(webmFilePath, "taglib/webm", TagLib.ReadStyle.Average);
            duration = webmFile.Properties.Duration;
            webmFile.Dispose();
            while (sponsorBlockOptions == null)
                Thread.Sleep(100);
            try
            {
                hasSponsorBlockSegment = new WebClient().DownloadString($"{sponsorBlockSegmentsAPI}?videoID={videoID}{string.Join(",", sponsorBlockOptions.GetCategory().Select(s => "&category=" + s))}") != "Not Found";
            }
            catch (WebException) { }
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

        public SponsorBlockOptions SponsorBlockOptions
        {
            get => sponsorBlockOptions;
            set => sponsorBlockOptions = value;
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicVideo ? " Music" : "") + (sponsorBlockOptions.Enabled && hasSponsorBlockSegment ? " x SponsorBlock" : ""), isYouTubeMusicVideo ? youTubeMusicIconLink : youTubeIconLink);

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
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            if (sponsorBlockOptions.Enabled && hasSponsorBlockSegment)
                musicDesc += $" ({durationBeforeSponsorBlock.toString()})"; 
            return musicDesc;
        }

        public string[] GetFilesInUse() => new string[] { webmFilePath, pcmFile };

        public string GetIcon() => isYouTubeMusicVideo ? Config.YouTubeMusicIcon : Config.YouTubeIcon;

        public bool isLinkMatch(string link) => regexMatchYTLink.IsMatch(link);

        void DownloadWEBM(string link, ref string tempFile)
        {
            tempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{Utils.RandomString(10)}.webm");
            Thread.Sleep(100);
            Process yt_dlp_x86 = new Process() 
            { 
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp\\yt-dlp_x86",
                    Arguments = $"-f bestaudio --ffmpeg-location ../ffmpeg{sponsorBlockOptions.GetArgument()} --paths {Path.GetDirectoryName(tempFile)} -o {Path.GetFileName(tempFile)} --force-overwrites {link}",
                    WorkingDirectory = "yt-dlp",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------yt-dlp Console output--------------");
            yt_dlp_x86.Start();
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
