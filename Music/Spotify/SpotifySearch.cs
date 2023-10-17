using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using SpotifyExplode.Albums;
using SpotifyExplode.Artists;
using SpotifyExplode.Playlists;
using SpotifyExplode.Search;
using SpotifyExplode.Tracks;

namespace CatBot.Music.Spotify
{
    internal class SpotifySearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (linkOrKeyword.Contains("spotify.link"))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, linkOrKeyword);
                var response = new HttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                linkOrKeyword = response.RequestMessage.RequestUri.ToString();
            }
            if (SpotifyPlaylist.regexMatchSpotifyPlaylist.IsMatch(linkOrKeyword))
            {
                try
                {
                    Match match = SpotifyPlaylist.regexMatchSpotifyPlaylist.Match(linkOrKeyword);
                    string type = match.Groups[1].Value;
                    if (type == "artist")
                    {
                        Artist artist = SpotifyMusic.spClient.Artists.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                        return new List<SearchResult>()
                        {
                            new SearchResult("https://open.spotify.com/artist/" + artist.Id, "Nhạc phổ biến", artist.Name, "https://open.spotify.com/artist/" + artist.Id, artist.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url)
                        };
                    }
                    else if (type == "playlist")
                    {
                        Playlist playlist = SpotifyMusic.spClient.Playlists.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                        return new List<SearchResult>()
                        {
                            new SearchResult("https://open.spotify.com/playlist/" + playlist.Id, playlist.Name, playlist.Owner.DisplayName, "https://open.spotify.com/user/" + playlist.Owner.Id, "")
                        };
                    }
                    else if (type == "album")
                    {
                        Album album = SpotifyMusic.spClient.Albums.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                        return new List<SearchResult>()
                        {
                            new SearchResult("https://open.spotify.com/album/" + album.Id, album.Name, string.Join(", ", album.Artists.Select(artist => artist.Name)), "", "")
                        };
                    }
                }

                catch
                {
                    return new List<SearchResult>();
                }
            }
            if (SpotifyMusic.regexMatchSpotifyLink.IsMatch(linkOrKeyword))
            {
                Track track;
                try
                {
                    track = SpotifyMusic.spClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                }
                catch
                {
                    return new List<SearchResult>();
                }
                return new List<SearchResult>()
                {
                    new SearchResult(track.Url, track.Title, string.Join(", ", track.Artists.Select(a => a.Name)), "", track.Album.Images.Count > 0 ? track.Album.Images[0].Url : "")
                };
            }
            List<TrackSearchResult> searchResult = SpotifyMusic.spClient.Search.GetTracksAsync(linkOrKeyword, 0, count).GetAwaiter().GetResult();
            return searchResult.Select(sR => new SearchResult(sR.Url, sR.Title, string.Join(", ", sR.Artists.Select(a => a.Name)), "", sR.Album.Images.Count > 0 ? sR.Album.Images[0].Url : "")).ToList();
        }
    }
}
