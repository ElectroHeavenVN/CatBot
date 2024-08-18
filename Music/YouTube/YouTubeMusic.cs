using System.Net;
using System.Text.RegularExpressions;
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
    internal partial class YouTubeMusic : IMusic
    {
        internal static readonly string youTubeIconLink = "https://www.gstatic.com/youtube/img/branding/favicon/favicon_144x144.png";
        internal static readonly string youTubeMusicIconLink = "https://www.gstatic.com/youtube/media/ytm/images/applauncher/music_icon_144x144.png";
        internal static readonly string sponsorBlockSegmentsAPI = "https://sponsor.ajay.app/api/skipSegments";
        internal static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=1&type=video&key={0}&q={1}";
        internal static readonly string getVideoInfoAPI = "https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2CcontentDetails&key={0}&id={1}";

        [GeneratedRegex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu\\.be))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/|shorts\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled)]
        internal static partial Regex GetRegexMatchYTVideoLink();
        [GeneratedRegex("^((?:https?:)?\\/\\/)?((?:music\\.youtube\\.com))(\\/(?:[\\w\\-]+\\?v=|embed\\/|v\\/)?)([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled)]
        private static partial Regex GetRegexMatchYTMusicLink();

        TimeSpan durationBeforeSponsorBlock;
        SponsorBlockOptions? sponsorBlockOptions;
        internal static YoutubeClient ytClient = new YoutubeClient();
        SponsorBlockSkipSegment[] sponsorBlockSkipSegments = [];
        string videoID = "";
        bool isYouTubeMusicVideo;
        string webmFilePath = "";
        bool hasSponsorBlockSegment;
        string link = "";
        TimeSpan duration;
        string title = "";
        string[] artists = [];
        string[] artistsWithLinks = [];
        string albumThumbnailLink = "";
        string pcmFile = "";
        FileStream? musicPCMDataStream;
        bool canGetStream;
        bool _disposed;

        public YouTubeMusic() { }

        public YouTubeMusic(string linkOrKeyword)
        {
            if (GetRegexMatchYTVideoLink().IsMatch(linkOrKeyword))
            {
                isYouTubeMusicVideo = GetRegexMatchYTMusicLink().IsMatch(linkOrKeyword);
                try
                {
                    Video video = ytClient.Videos.GetAsync(linkOrKeyword).Result;
                    videoID = video.Id;
                    link = video.Url;
                    if (isYouTubeMusicVideo)
                        link = link.Replace("www.youtube.com", "music.youtube.com");
                    title = video.Title;
                    artists = [video.Author.ChannelTitle];
                    artistsWithLinks = [Formatter.MaskedUrl(video.Author.ChannelTitle, new Uri(video.Author.ChannelUrl))];
                    albumThumbnailLink = video.Thumbnails.TryGetWithHighestResolution()?.Url ?? "";
                    durationBeforeSponsorBlock = video.Duration.Value;
                }
                catch (Exception) { throw new MusicException(MusicType.YouTube, "video not found"); }
            }
            else
            {
                HttpClient httpClient = new HttpClient();
                JToken searchResource = JObject.Parse(httpClient.GetStringAsync(string.Format(searchVideoAPI, Config.gI().GoogleAPIKey, Uri.EscapeDataString(linkOrKeyword))).Result)["items"][0];
                videoID = searchResource["id"]["videoId"].ToString();
                link = $"https://www.youtube.com/watch?v={searchResource["id"]["videoId"]}";
                title = $"[{WebUtility.HtmlDecode(searchResource["snippet"]["title"].ToString())}]({link})";
                artists = [WebUtility.HtmlDecode(searchResource["snippet"]["channelTitle"].ToString())];
                artistsWithLinks = [Formatter.MaskedUrl(WebUtility.HtmlDecode(searchResource["snippet"]["channelTitle"].ToString()), new Uri($"https://www.youtube.com/channel/{searchResource["snippet"]["channelId"]}"))];
                albumThumbnailLink = searchResource["snippet"]["thumbnails"]["high"]["url"].ToString();
                string json = httpClient.GetStringAsync(string.Format(getVideoInfoAPI, Config.gI().GoogleAPIKey, Uri.EscapeDataString(searchResource["id"]["videoId"].ToString()))).Result;
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
                HttpClient httpClient = new HttpClient();
                string sponsorBlockJSON = httpClient.GetStringAsync($"{sponsorBlockSegmentsAPI}?videoID={videoID}{string.Join("", sponsorBlockOptions.GetCategory().Select(s => "&category=" + s))}").Result;
                sponsorBlockSkipSegments = JsonConvert.DeserializeObject<SponsorBlockSkipSegment[]>(sponsorBlockJSON);
                hasSponsorBlockSegment = sponsorBlockSkipSegments.Length > 0;
            }
            catch (WebException) { }
            MusicUtils.DownloadWEBMFromYouTube(link, ref webmFilePath, sponsorBlockSkipSegments);
            TagLib.File webmFile = TagLib.File.Create(webmFilePath, "taglib/webm", TagLib.ReadStyle.Average);
            duration = webmFile.Properties.Duration;
            webmFile.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(webmFilePath, ref pcmFile));
            //File.Delete(webmFilePath);
            //webmFilePath = null;
        }

        ~YouTubeMusic() => Dispose(false);

        public MusicType MusicType => MusicType.YouTube;
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
            get => sponsorBlockOptions;
            set => sponsorBlockOptions = value;
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

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicVideo ? " Music" : "") + (sponsorBlockOptions != null && sponsorBlockOptions.Enabled && hasSponsorBlockSegment ? " x SponsorBlock" : ""), isYouTubeMusicVideo ? youTubeMusicIconLink : youTubeIconLink);

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

        public LyricData? GetLyric() => new LyricData("Video YouTube không có lời!");

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"{(isYouTubeMusicVideo? "Bài hát" : "Video")}: {TitleWithLink}" + Environment.NewLine;
            musicDesc += $"Tải lên bởi: {AllArtistsWithLinks}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            if (sponsorBlockOptions != null && sponsorBlockOptions.Enabled && hasSponsorBlockSegment)
                musicDesc += $" ({durationBeforeSponsorBlock.toString()})"; 
            return musicDesc;
        }

        public string[] GetFilesInUse() => [webmFilePath, pcmFile];

        public string GetIcon() => isYouTubeMusicVideo ? Config.gI().YouTubeMusicIcon : Config.gI().YouTubeIcon;

        public bool isLinkMatch(string link) => GetRegexMatchYTVideoLink().IsMatch(link);

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload(".webm", new FileStream(webmFilePath, FileMode.Open, FileAccess.Read));

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
