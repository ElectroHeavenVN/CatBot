using System.Collections.Generic;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;

namespace CatBot.Music.YouTube
{
    internal static class YouTubeSearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (YouTubeMusic.regexMatchYTVideoLink.IsMatch(linkOrKeyword))
            {
                Video video = YouTubeMusic.ytClient.Videos.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                return new List<SearchResult>()
                {
                    new SearchResult(video.Url, video.Title, video.Author.ChannelTitle, video.Author.ChannelUrl, video.Thumbnails.TryGetWithHighestResolution().Url)
                };
            }
            IReadOnlyList<VideoSearchResult> videoSearchResults = YouTubeMusic.ytClient.Search.GetVideosAsync(linkOrKeyword).GetAwaiter().GetResult();
            List<SearchResult> videos = new List<SearchResult>();
            for (int i = 0; i < count; i++)
            {
                videos.Add(new SearchResult(videoSearchResults[i].Url, videoSearchResults[i].Title, videoSearchResults[i].Author.ChannelTitle, videoSearchResults[i].Author.ChannelUrl, videoSearchResults[i].Thumbnails.TryGetWithHighestResolution().Url));
            }
            return videos;
        }
    }
}
