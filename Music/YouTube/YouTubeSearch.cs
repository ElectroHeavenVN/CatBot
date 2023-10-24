using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using System.Linq;

namespace CatBot.Music.YouTube
{
    internal static class YouTubeSearch
    {
        private static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&type=video";

        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (YouTubePlaylist.regexMatchYTPlaylistLink.IsMatch(linkOrKeyword))
            {
                if (linkOrKeyword.Contains("@") || linkOrKeyword.Contains("channel/"))
                {
                    Channel channel;
                    if (linkOrKeyword.Contains("@"))
                        channel = YouTubeMusic.ytClient.Channels.GetByHandleAsync(linkOrKeyword).GetAwaiter().GetResult();
                    else
                        channel = YouTubeMusic.ytClient.Channels.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                    return new List<SearchResult>()
                    {
                        new SearchResult(channel.Url, "Video tải lên", channel.Title, channel.Url, channel.Thumbnails.TryGetWithHighestResolution().Url)
                    };
                }
                else if (linkOrKeyword.Contains("playlist?list="))
                {
                    Playlist playlist = YouTubeMusic.ytClient.Playlists.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                    return new List<SearchResult>()
                    {
                        new SearchResult(playlist.Url, playlist.Title, playlist.Author.ChannelTitle, playlist.Author.ChannelUrl, playlist.Thumbnails.TryGetWithHighestResolution().Url)
                    };
                }
            }
            if (YouTubeMusic.regexMatchYTVideoLink.IsMatch(linkOrKeyword))
            {
                Video video = YouTubeMusic.ytClient.Videos.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                return new List<SearchResult>()
                {
                    new SearchResult(video.Url, video.Title, video.Author.ChannelTitle, video.Author.ChannelUrl, video.Thumbnails.TryGetWithHighestResolution().Url)
                };
            }
            JObject searchResult = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{searchVideoAPI}&maxResults={count}&key={Config.gI().GoogleAPIKey}&q={Uri.EscapeUriString(linkOrKeyword)}"));
            return searchResult["items"].Select(sR => new SearchResult($"https://www.youtube.com/watch?v={sR["id"]["videoId"]}", WebUtility.HtmlDecode(sR["snippet"]["title"].ToString()), $"{WebUtility.HtmlDecode(sR["snippet"]["channelTitle"].ToString())}", $"https://www.youtube.com/channel/{sR["snippet"]["channelId"]}", sR["snippet"]["thumbnails"]["high"]["url"].ToString())).ToList();
        }
    }
}
