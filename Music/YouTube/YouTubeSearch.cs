using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music.YouTube
{
    internal static class YouTubeSearch
    {
        private static readonly string searchVideoAPI = "https://youtube.googleapis.com/youtube/v3/search?part=snippet&type=video";

        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (YouTubeMusic.regexMatchYTLink.IsMatch(linkOrKeyword))
            {
                string videoID = YouTubeMusic.regexMatchYTLink.Match(linkOrKeyword).Groups[5].Value;
                string json = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{YouTubeMusic.getVideoInfoAPI}&key={Config.GoogleAPIKey}&id={Uri.EscapeUriString(videoID)}");
                JToken videoResource = JObject.Parse(json)["items"][0];
                return new List<SearchResult>()
                {
                    new SearchResult($"https://www.youtube.com/watch?v={videoResource["id"]}", $"{WebUtility.HtmlDecode(videoResource["snippet"]["title"].ToString())}", $"{WebUtility.HtmlDecode(videoResource["snippet"]["channelTitle"].ToString())}", $"https://www.youtube.com/channel/{videoResource["snippet"]["channelId"]}", videoResource["snippet"]["thumbnails"]["high"]["url"].ToString())
                };
            }
            JObject searchResult = JObject.Parse(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString($"{searchVideoAPI}&maxResults={count}&key={Config.GoogleAPIKey}&q={Uri.EscapeUriString(linkOrKeyword)}"));
            return searchResult["items"].Select(sR => new SearchResult($"https://www.youtube.com/watch?v={sR["id"]["videoId"]}", WebUtility.HtmlDecode(sR["snippet"]["title"].ToString()), $"{WebUtility.HtmlDecode(sR["snippet"]["channelTitle"].ToString())}", $"https://www.youtube.com/channel/{sR["snippet"]["channelId"]}", sR["snippet"]["thumbnails"]["high"]["url"].ToString())).ToList();
        }
    }
}
