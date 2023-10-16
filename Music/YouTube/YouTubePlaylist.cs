using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace CatBot.Music.YouTube
{
    internal class YouTubePlaylist : IPlaylist
    {
        internal static readonly string getPlaylistVideosAPI = "https://youtube.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&key={0}&playlistId={1}&pageToken={2}";
        internal static readonly string getPlaylistInfoAPI = "https://youtube.googleapis.com/youtube/v3/playlists?part=snippet&key={0}&id={1}";
        internal static readonly string getChannelInfoAPI = "https://youtube.googleapis.com/youtube/v3/channels?part=snippet%2Cstatistics&key={0}&id={1}";

        internal static Regex regexMatchYTPlaylistLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu\\.be))(\\/(?:@|playlist\\?list=|channel\\/))([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);

        List<IMusic> videoList = new List<IMusic>();
        string thumbnailLink;
        string title;
        string description;
        bool isYouTubeMusicPlaylist;
        string author;
        string subCount;
        int hiddenVideos;

        public YouTubePlaylist() { }
        public YouTubePlaylist(string link) 
        {
            if (regexMatchYTPlaylistLink.IsMatch(link))
            {
                Match regexMatch = regexMatchYTPlaylistLink.Match(link);
                string playlistID = regexMatch.Groups[5].Value;
                string pageToken = "";
                isYouTubeMusicPlaylist = link.Contains("music.youtube.com");
                if (link.Contains('@') || link.Contains("channel/"))
                {
                    try
                    {
                        string webpage = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(link);
                        string channelID = webpage.Substring(webpage.IndexOf("\"externalId\":\"") + 14, 24);
                        playlistID = 'U' + channelID.Remove(1, 1);
                        JObject channelInfo = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getChannelInfoAPI, Config.GoogleAPIKey, channelID)));
                        title = $"Video tải lên của [{channelInfo["items"][0]["snippet"]["title"]}](https://www.youtube.com/channel/{channelID})";
                        author = $"[{channelInfo["items"][0]["snippet"]["title"]}](https://www.youtube.com/channel/{channelID})";
                        description = channelInfo["items"][0]["snippet"]["description"].ToString();
                        thumbnailLink = channelInfo["items"][0]["snippet"]["thumbnails"]["high"]["url"].ToString();
                        if (!channelInfo["items"][0]["statistics"]["hiddenSubscriberCount"].Value<bool>())
                        {
                            uint subs = uint.Parse(channelInfo["items"][0]["statistics"]["subscriberCount"].ToString());
                            if (subs < 1000)
                                subCount = subs.ToString();
                            if (subs > 1000 && subs < 1000000)
                                subCount = (subs / 1000f).ToString("0.00") + " N";
                            if (subs > 1000000)
                                subCount = (subs / 1000000f).ToString("0.00") + " Tr";
                        }
                    }
                    catch (Exception) { throw new MusicException("YT: channel not found"); }
                }
                else if (link.Contains("playlist?list="))
                {
                    JObject playlistInfo = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getPlaylistInfoAPI, Config.GoogleAPIKey, playlistID)));
                    try
                    {
                        title = $"[{playlistInfo["items"][0]["snippet"]["title"]}]({link})";
                        description = playlistInfo["items"][0]["snippet"]["description"].ToString();
                        author = $"[{playlistInfo["items"][0]["snippet"]["channelTitle"]}](https://www.youtube.com/channel/{playlistInfo["items"][0]["snippet"]["channelId"]})";
                        thumbnailLink = playlistInfo["items"][0]["snippet"]["thumbnails"]["high"]["url"].ToString();
                    }
                    catch (Exception) { throw new MusicException("Ex: playlist not found"); }
                }
                JObject playlistItemList;
                do
                {
                    playlistItemList = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(string.Format(getPlaylistVideosAPI, Config.GoogleAPIKey, playlistID, pageToken)));
                    if (playlistItemList.ContainsKey("nextPageToken"))
                        pageToken = playlistItemList["nextPageToken"].ToString();
                    foreach (JToken playlistItem in playlistItemList["items"])
                    {
                        try
                        {
                            videoList.Add(new YouTubeMusic($"https://{(isYouTubeMusicPlaylist ? "music" : "www")}.youtube.com/watch?v={playlistItem["contentDetails"]["videoId"]}"));
                        }
                        catch (MusicException ex)
                        { 
                            if (ex.Message == "YT: video not found")
                                hiddenVideos++; 
                        }
                    }
                }
                while (playlistItemList.ContainsKey("nextPageToken"));
            }
            else
                throw new NotAPlaylistException();
        }

        public List<IMusic> Tracks => videoList;

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicPlaylist ? " Music" : ""), isYouTubeMusicPlaylist ? YouTubeMusic.youTubeMusicIconLink : YouTubeMusic.youTubeIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = $"Danh sách phát: {title} ({hiddenVideos} video không xem được)" + Environment.NewLine;
            playlistDesc += $"Tải lên bởi: {author} " + (!string.IsNullOrEmpty(subCount) ? $"({subCount} người đăng ký)" : "") + Environment.NewLine;
            playlistDesc += $"Số {(isYouTubeMusicPlaylist ? "bài nhạc" : "video")}: {videoList.Count}" + Environment.NewLine;
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        public bool isLinkMatch(string link) => regexMatchYTPlaylistLink.IsMatch(link);
    }
}
