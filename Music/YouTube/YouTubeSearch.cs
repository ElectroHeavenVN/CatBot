using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace CatBot.Music.YouTube
{
    internal static class YouTubeSearch
    {
        private static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&type=video";

        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (YouTubePlaylist.GetRegexMatchYTPlaylistLink().IsMatch(linkOrKeyword))
            {
                if (linkOrKeyword.Contains("@") || linkOrKeyword.Contains("channel/"))
                {
                    Channel channel;
                    if (linkOrKeyword.Contains("@"))
                        channel = YouTubeMusic.ytClient.Channels.GetByHandleAsync(linkOrKeyword).Result;
                    else
                        channel = YouTubeMusic.ytClient.Channels.GetAsync(linkOrKeyword).Result;
                    return
                    [
                        new SearchResult(channel.Url, $"Video {channel.Title} tải lên", channel.Title, channel.Url, channel.Thumbnails?.TryGetWithHighestResolution()?.Url ?? "")
                    ];
                }
                else if (linkOrKeyword.Contains("playlist?list="))
                {
                    Playlist playlist = YouTubeMusic.ytClient.Playlists.GetAsync(linkOrKeyword).Result;
                    return
                    [
                        new SearchResult(playlist.Url, playlist.Title, playlist.Author?.ChannelTitle ?? "", playlist.Author?.ChannelUrl ?? "", playlist.Thumbnails?.TryGetWithHighestResolution()?.Url ?? "")
                    ];
                }
            }
            if (YouTubeMusic.GetRegexMatchYTVideoLink().IsMatch(linkOrKeyword))
            {
                Video video = YouTubeMusic.ytClient.Videos.GetAsync(linkOrKeyword).Result;
                return
                [
                    new SearchResult(video.Url, video.Title, video.Author.ChannelTitle, video.Author.ChannelUrl, video.Thumbnails?.TryGetWithHighestResolution()?.Url ?? "")
                ];
            }
            HttpClient httpClient = new HttpClient();
            JObject searchResult = JObject.Parse(httpClient.GetStringAsync($"{searchVideoAPI}&maxResults={count}&key={Config.gI().GoogleAPIKey}&q={Uri.EscapeDataString(linkOrKeyword)}").Result);
            return searchResult["items"]?.Select(sR => new SearchResult($"https://www.youtube.com/watch?v={sR["id"]?["videoId"]}", WebUtility.HtmlDecode(sR["snippet"]?["title"]?.ToString() ?? ""), $"{WebUtility.HtmlDecode(sR["snippet"]?["channelTitle"]?.ToString() ?? "")}", $"https://www.youtube.com/channel/{sR["snippet"]?["channelId"]}", sR["snippet"]?["thumbnails"]?["high"]?["url"]?.ToString() ?? ""))?.ToList() ?? [];
        }
    }
}
