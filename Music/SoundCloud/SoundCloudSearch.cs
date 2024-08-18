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
                        linkOrKeyword = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, linkOrKeyword), HttpCompletionOption.ResponseHeadersRead).Result.RequestMessage.RequestUri.ToString();
                    if (linkOrKeyword.Contains("/sets/"))
                        type = "set";
                    linkOrKeyword = linkOrKeyword.ReplaceFirst(type, "").TrimEnd('/');
                    if (string.IsNullOrEmpty(type))
                        type = "tracks";
                    if (type == "tracks")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return new List<SearchResult>()
                        {
                            new SearchResult(user.PermalinkUrl.AbsoluteUri, "Nhạc đã tải lên", user.Username, user.PermalinkUrl.AbsoluteUri, user.AvatarUrl?.AbsoluteUri)
                        };
                    }
                    else if (type == "popular-tracks")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return new List<SearchResult>()
                        {
                            new SearchResult(user.PermalinkUrl.AbsoluteUri + "/popular-tracks", "Nhạc nổi bật", user.Username, user.PermalinkUrl.AbsoluteUri, user.AvatarUrl?.AbsoluteUri)
                        };
                    }
                    else if (type == "likes")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return new List<SearchResult>()
                        {
                            new SearchResult(user.PermalinkUrl.AbsoluteUri + "/likes", "Nhạc đã thích", user.Username, user.PermalinkUrl.AbsoluteUri, user.AvatarUrl?.AbsoluteUri)
                        };
                    }
                    else if (type == "reposts")
                    {
                        User user = SoundCloudMusic.scClient.Users.GetAsync(linkOrKeyword).Result;
                        return new List<SearchResult>()
                        {
                            new SearchResult(user.PermalinkUrl.AbsoluteUri + "/reposts", "Nhạc repost", user.Username, user.PermalinkUrl.AbsoluteUri, user.AvatarUrl?.AbsoluteUri)
                        };
                    }
                    else if (type == "set")
                    {
                        Playlist playlist = SoundCloudMusic.scClient.Playlists.GetAsync(linkOrKeyword).Result;
                        return new List<SearchResult>()
                        {
                            new SearchResult(playlist.PermalinkUrl.AbsoluteUri, playlist.Title, playlist.User.Username, playlist.User.PermalinkUrl.AbsoluteUri, playlist.User.AvatarUrl?.AbsoluteUri)
                        };
                    }
                }
                catch
                {
                    return new List<SearchResult>();
                }
            }
            if (SoundCloudMusic.GetRegexMatchSoundCloudLink().IsMatch(linkOrKeyword))
            {
                Track track;
                try
                {
                    track = SoundCloudMusic.scClient.Tracks.GetAsync(linkOrKeyword).Result;
                }
                catch 
                {
                    return new List<SearchResult>(); 
                }
                return new List<SearchResult>()
                {
                    new SearchResult(track.PermalinkUrl.AbsoluteUri, track.Title, track.User.Username, track.User.PermalinkUrl.AbsoluteUri, track.ArtworkUrl == null ? "" : track.ArtworkUrl.AbsoluteUri) 
                };
            }
            List<TrackSearchResult> searchResult = SoundCloudMusic.scClient.Search.GetTracksAsync(linkOrKeyword, 0, count).ToListAsync().Result;
            return searchResult.Select(sR => new SearchResult(sR.PermalinkUrl.AbsoluteUri, sR.Title, sR.User.Username, sR.User.PermalinkUrl.AbsoluteUri, sR.ArtworkUrl == null ? "" : sR.ArtworkUrl.AbsoluteUri)).ToList();
        }
    }
}
