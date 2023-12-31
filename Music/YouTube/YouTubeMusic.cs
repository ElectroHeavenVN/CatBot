﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

namespace CatBot.Music.YouTube
{
    internal class YouTubeMusic : IMusic
    {
        internal static readonly string youTubeIconLink = "https://www.gstatic.com/youtube/img/branding/favicon/favicon_144x144.png";
        internal static readonly string youTubeMusicIconLink = "https://www.gstatic.com/youtube/media/ytm/images/applauncher/music_icon_144x144.png";
        internal static readonly string sponsorBlockSegmentsAPI = "https://sponsor.ajay.app/api/skipSegments";
        internal static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=1&type=video&key={0}&q={1}";
        internal static readonly string getVideoInfoAPI = "https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2CcontentDetails&key={0}&id={1}";

        internal static Regex regexMatchYTVideoLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu\\.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/|shorts\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        internal static Regex regexMatchYTMusicLink = new Regex("^((?:https?:)?\\/\\/)?((?:music\\.youtube\\.com))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);
        string link;
        TimeSpan duration;
        TimeSpan durationBeforeSponsorBlock;
        string title = "";
        string artists = "";
        string albumThumbnailLink;
        string webmFilePath;
        string pcmFile;
        FileStream musicPCMDataStream;
        bool canGetStream;
        bool _disposed;
        bool isYouTubeMusicVideo;
        SponsorBlockOptions sponsorBlockOptions;
        internal static YoutubeClient ytClient = new YoutubeClient();
        bool hasSponsorBlockSegment;
        string videoID;
        SponsorBlockSkipSegment[] sponsorBlockSkipSegments = new SponsorBlockSkipSegment[0];

        public YouTubeMusic() { }

        public YouTubeMusic(string linkOrKeyword)
        {
            if (regexMatchYTVideoLink.IsMatch(linkOrKeyword))
            {
                isYouTubeMusicVideo = regexMatchYTMusicLink.IsMatch(linkOrKeyword);
                try
                {
                    Video video = ytClient.Videos.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                    videoID = video.Id;
                    link = video.Url;
                    if (isYouTubeMusicVideo)
                        link = link.Replace("www.youtube.com", "music.youtube.com");
                    title = Formatter.MaskedUrl(video.Title, new Uri(link));
                    artists = Formatter.MaskedUrl(video.Author.ChannelTitle, new Uri(video.Author.ChannelUrl));
                    albumThumbnailLink = video.Thumbnails.TryGetWithHighestResolution().Url;
                    durationBeforeSponsorBlock = video.Duration.Value;
                }
                catch (Exception) { throw new MusicException(MusicType.YouTube, "video not found"); }
            }
            else
            {
                JToken searchResource = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(searchVideoAPI, Config.gI().GoogleAPIKey, Uri.EscapeUriString(linkOrKeyword))))["items"][0];
                videoID = searchResource["id"]["videoId"].ToString();
                link = $"https://www.youtube.com/watch?v={searchResource["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["title"].ToString())}]({link})";
                artists = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["channelTitle"].ToString())}](https://www.youtube.com/channel/{searchResource["snippet"]["channelId"]})";
                albumThumbnailLink = searchResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getVideoInfoAPI, Config.gI().GoogleAPIKey, Uri.EscapeUriString(searchResource["id"]["videoId"].ToString())));
                JToken videoResource = JObject.Parse(json)["items"][0];
                durationBeforeSponsorBlock = XmlConvert.ToTimeSpan(videoResource["contentDetails"]["duration"].ToString());
            }
        }

        public void Download()
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
            MusicUtils.DownloadWEBMFromYouTube(link, ref webmFilePath);
            TagLib.File webmFile = TagLib.File.Create(webmFilePath, "taglib/webm", TagLib.ReadStyle.Average);
            duration = webmFile.Properties.Duration;
            webmFile.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(webmFilePath, ref pcmFile));
            File.Delete(webmFilePath);
            webmFilePath = null;
        }

        ~YouTubeMusic() => Dispose(false);

        public MusicType MusicType => MusicType.YouTube;

        public string PathOrLink => link;

        public TimeSpan Duration => duration;

        public string Title => title;

        public string Artists => artists;

        public string Album => null;

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

        public string GetIcon() => isYouTubeMusicVideo ? Config.gI().YouTubeMusicIcon : Config.gI().YouTubeIcon;

        public bool isLinkMatch(string link) => regexMatchYTVideoLink.IsMatch(link);

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
