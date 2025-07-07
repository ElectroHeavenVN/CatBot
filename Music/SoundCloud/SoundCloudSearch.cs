using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using SoundCloudExplode.Playlists;
using SoundCloudExplode.Search;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace CatBot.Music.SoundCloud
{
    internal class SoundCloudSearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (SoundCloudPlaylist.GetRegexMatchSoundCloudPlaylistLink().IsMatch(linkOrKeyword))
            {
                try
                {
                    Match match = SoundCloudPlaylist.GetRegexMatchSoundCloudPlaylistLink().Match(linkOrKeyword);
                    string domain = match.Groups[1].Value;
                    string type = match.Groups[4].Value;
                    if (domain == "on.soundcloud.com")
                        linkOrKeyword = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, linkOrKeyword), HttpCompletionOption.ResponseHeadersRead).Result?.RequestMessage?.RequestUri?.ToString() ?? linkOrKeyword;
                    if (linkOrKeyword.Contains("/sets/"))
                        type = "set";
                    linkOrKeyword = linkOrKeyword.ReplaceFirst(type, "").TrimEnd('/');
                    if (string.IsNullOrEmpty(type))
                        type = "tracks";
                    if (type == "tracks")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return
                        [
                            new SearchResult(user.PermalinkUrl ?? "", $"Nhạc {user.Username} đã tải lên", user.Username ?? "", user.PermalinkUrl ?? "", user.AvatarUrl?.AbsoluteUri ?? "")
                        ];
                    }
                    else if (type == "popular-tracks")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return
                        [
                            new SearchResult(user.PermalinkUrl + "/popular-tracks", $"Nhạc nổi bật của {user.Username}", user.Username ?? "", user.PermalinkUrl ?? "", user.AvatarUrl?.AbsoluteUri ?? "")
                        ];
                    }
                    else if (type == "likes")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return
                        [
                            new SearchResult(user.PermalinkUrl + "/likes", $"Nhạc {user.Username} đã thích", user.Username ?? "", user.PermalinkUrl ?? "", user.AvatarUrl?.AbsoluteUri ?? "")
                        ];
                    }
                    else if (type == "reposts")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return
                        [
                            new SearchResult(user.PermalinkUrl + "/reposts", $"Nhạc {user.Username} đã repost", user.Username ?? "", user.PermalinkUrl ?? "", user.AvatarUrl?.AbsoluteUri ?? "")
                        ];
                    }
                    else if (type == "set")
                    {
                        Playlist playlist = SoundCloudMusic.scClient.Playlists.GetAsync(linkOrKeyword).Result;
                        return
                        [
                            new SearchResult(playlist.PermalinkUrl?.AbsoluteUri ?? "", playlist.Title ?? "", playlist.User?.Username ?? "", playlist.User?.PermalinkUrl?.AbsoluteUri ?? "", playlist.User?.AvatarUrl?.AbsoluteUri ?? "")
                        ];
                    }
                }
                catch
                {
                    return [];
                }
            }
            if (SoundCloudMusic.GetRegexMatchSoundCloudLink().IsMatch(linkOrKeyword))
            {
                Track track;
                try
                {
                    track = SoundCloudMusic.scClient.Tracks?.GetAsync(linkOrKeyword).Result ?? throw new Exception();
                }
                catch 
                {
                    return []; 
                }
                return
                [
                    new SearchResult(track.PermalinkUrl?.AbsoluteUri ?? "", track.Title ?? "", track.User?.Username ?? "", track.User?.PermalinkUrl ?? "", track.ArtworkUrl?.AbsoluteUri ?? "") 
                ];
            }
            List<TrackSearchResult> searchResult = SoundCloudMusic.scClient.Search.GetTracksAsync(linkOrKeyword, 0, count).ToListAsync().Result;
            return searchResult.Select(sR => new SearchResult(sR.PermalinkUrl?.AbsoluteUri ?? "", sR.Title ?? "", sR.User?.Username ?? "", sR.User?.PermalinkUrl ?? "", sR.ArtworkUrl?.AbsoluteUri ?? "")).ToList();
        }
    }
}
