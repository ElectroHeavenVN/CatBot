using DiscordBot.Instance;
using DiscordBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using Newtonsoft.Json;
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
        internal static readonly string youTubeIconLink = "https://www.gstatic.com/youtube/img/branding/favicon/favicon_144x144.png";
        internal static readonly string youTubeMusicIconLink = "https://www.gstatic.com/youtube/media/ytm/images/applauncher/music_icon_144x144.png";
        internal static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=1&type=video&key={0}&q={1}";
        internal static readonly string getVideoInfoAPI = "https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2CcontentDetails&key={0}&id={1}";
        internal static readonly string sponsorBlockSegmentsAPI = "https://sponsor.ajay.app/api/skipSegments";
        internal static Regex regexMatchYTVideoLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu\\.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/|shorts\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        internal static Regex regexMatchYTMusicLink = new Regex("^((?:https?:)?\\/\\/)?((?:music\\.youtube\\.com))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
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
        SponsorBlockSkipSegment[] sponsorBlockSkipSegments;

        public YouTubeMusic() { }

        public YouTubeMusic(string linkOrKeyword)
        {
            if (regexMatchYTVideoLink.IsMatch(linkOrKeyword))
            {
                isYouTubeMusicVideo = regexMatchYTMusicLink.IsMatch(linkOrKeyword);
                videoID = regexMatchYTVideoLink.Match(linkOrKeyword).Groups[5].Value;
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getVideoInfoAPI, Config.GoogleAPIKey, Uri.EscapeUriString(videoID)));
                try
                {
                    JToken videoResource = JObject.Parse(json)["items"][0];
                    videoID = videoResource["id"].ToString();
                    link = $"https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/watch?v={videoResource["id"]}";
                    title = $"[{WebUtility.HtmlDecode(videoResource["snippet"]["title"].ToString())}]({link})";
                    artists = $"[{WebUtility.HtmlDecode(videoResource["snippet"]["channelTitle"].ToString())}](https://{(isYouTubeMusicVideo ? "music" : "www")}.youtube.com/channel/{videoResource["snippet"]["channelId"]})";
                    albumThumbnailLink = videoResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                    durationBeforeSponsorBlock = XmlConvert.ToTimeSpan(videoResource["contentDetails"]["duration"].ToString());
                }
                catch (Exception) { throw new MusicException("YT: video not found"); }
            }
            else
            {
                JToken searchResource = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(searchVideoAPI, Config.GoogleAPIKey, Uri.EscapeUriString(linkOrKeyword))))["items"][0];
                videoID = searchResource["id"]["videoId"].ToString();
                link = $"https://www.youtube.com/watch?v={searchResource["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["channelTitle"].ToString())}](https://www.youtube.com/channel/{searchResource["snippet"]["channelId"]})";
                albumThumbnailLink = searchResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getVideoInfoAPI, Config.GoogleAPIKey, Uri.EscapeUriString(searchResource["id"]["videoId"].ToString())));
                JToken videoResource = JObject.Parse(json)["items"][0];
                durationBeforeSponsorBlock = XmlConvert.ToTimeSpan(videoResource["contentDetails"]["duration"].ToString());
            }
        }

        public void Download()
        {
            try
            {
                while (sponsorBlockOptions == null)
                    Thread.Sleep(100);
                try
                {
                    string sponsorBlockJSON = new WebClient().DownloadString($"{sponsorBlockSegmentsAPI}?videoID={videoID}{string.Join(",", sponsorBlockOptions.GetCategory().Select(s => "&category=" + s))}");
                    sponsorBlockSkipSegments = JsonConvert.DeserializeObject<SponsorBlockSkipSegment[]>(sponsorBlockJSON);
                    hasSponsorBlockSegment = sponsorBlockSkipSegments.Length > 0;
                }
                catch (WebException) { }
                DownloadWEBM(link, ref webmFilePath);
                TagLib.File webmFile = TagLib.File.Create(webmFilePath, "taglib/webm", TagLib.ReadStyle.Average);
                duration = webmFile.Properties.Duration;
                webmFile.Dispose();
                canGetStream = true;
                musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(webmFilePath, ref pcmFile));
                File.Delete(webmFilePath);
                webmFilePath = null;
            }
            catch (Exception ex) { Utils.LogException(ex); }
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
                return musicPCMDataStream;
            }
        }

        public SponsorBlockOptions SponsorBlockOptions
        {
            get => sponsorBlockOptions;
            set => sponsorBlockOptions = value;
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicVideo ? " Music" : "") + (sponsorBlockOptions.Enabled && hasSponsorBlockSegment ? " x SponsorBlock" : ""), isYouTubeMusicVideo ? youTubeMusicIconLink : youTubeIconLink);

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

        public bool isLinkMatch(string link) => regexMatchYTVideoLink.IsMatch(link);

        void DownloadWEBM(string link, ref string tempFile)
        {
            string randomString = Utils.RandomString(10);
            tempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.webm");
            Thread.Sleep(100);
            Process yt_dlp_x86 = new Process() 
            { 
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp\\yt-dlp_x86",
                    Arguments = $"-f bestaudio --paths {Path.GetDirectoryName(tempFile)} -o {Path.GetFileName(tempFile)} --force-overwrites {link}",
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
            if (sponsorBlockSkipSegments.Length > 0)
            {
                string tempWEBMFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.temp.webm");
                string concatFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.temp.webm.concat");
                File.Move(tempFile, tempWEBMFile);
                WriteConcatFile(concatFile, tempWEBMFile);
                Process ffmpeg = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg\\ffmpeg",
                        Arguments = $"-y -hide_banner -f concat -safe 0 -i \"{concatFile}\" -map 0 -dn -ignore_unknown -c copy -movflags +faststart \"{tempFile}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                    },
                    EnableRaisingEvents = true,
                };
                Console.WriteLine("--------------FFMpeg Console output--------------");
                ffmpeg.Start();
                ffmpeg.WaitForExit();
                Console.WriteLine("--------------End of FFMpeg Console output--------------");
                File.Delete(tempWEBMFile);
                File.Delete(concatFile);
            }
        }

        private void WriteConcatFile(string concatFile, string tempWEBMFile)
        {
            string concatFileContent = "ffconcat version 1.0" + Environment.NewLine;
            foreach (SponsorBlockSkipSegment segment in sponsorBlockSkipSegments)
            {
                if (segment.Segment.IsLengthZero())
                    continue;
                concatFileContent += "file '" + tempWEBMFile + "'" + Environment.NewLine;
                if (segment.Segment.Start > 0)
                    concatFileContent += "outpoint " + segment.Segment.Start.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
                if (segment.VideoDuration - segment.Segment.End > 0)
                    concatFileContent += "inpoint " + segment.Segment.End.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
            }
            File.WriteAllText(concatFile, concatFileContent);
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
