using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using SpotifyExplode.Search;
using SpotifyExplode.Tracks;

namespace CatBot.Music.Spotify
{
    internal class SpotifySearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (SpotifyMusic.regexMatchSpotifyLink.IsMatch(linkOrKeyword))
            {
                if (linkOrKeyword.Contains("spotify.link"))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, linkOrKeyword);
                    var response = new HttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                    linkOrKeyword = response.RequestMessage.RequestUri.ToString();
                }
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
