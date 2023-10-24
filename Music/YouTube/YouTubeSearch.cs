using System;
using System.Collections.Generic;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;

namespace CatBot.Music.YouTube
{
    internal static class YouTubeSearch
    {
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
            IReadOnlyList<VideoSearchResult> videoSearchResults = YouTubeMusic.ytClient.Search.GetVideosAsync(linkOrKeyword).GetAwaiter().GetResult();
            List<SearchResult> videos = new List<SearchResult>();
            for (int i = 0; i < Math.Min(videoSearchResults.Count, count); i++)
            {
                videos.Add(new SearchResult(videoSearchResults[i].Url, videoSearchResults[i].Title, videoSearchResults[i].Author.ChannelTitle, videoSearchResults[i].Author.ChannelUrl, videoSearchResults[i].Thumbnails.TryGetWithHighestResolution().Url));
            }
            return videos;
        }
    }
}
