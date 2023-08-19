using System.Collections.Generic;
using System.Linq;
using SoundCloudExplode.Search;
using SoundCloudExplode.Tracks;

namespace DiscordBot.Music.SoundCloud
{
    internal class SoundCloudSearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (SoundCloudMusic.regexMatchSoundCloudLink.IsMatch(linkOrKeyword))
            {
                Track track;
                try
                {
                    track = SoundCloudMusic.scClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                }
                catch 
                {
                    return new List<SearchResult>(); 
                }
                return new List<SearchResult>()
                {
                    new SearchResult(track.PermalinkUrl.AbsoluteUri, track.Title, track.User.Username, track.User.PermalinkUrl.AbsoluteUri, track.ArtworkUrl.AbsoluteUri) 
                };
            }
            List<TrackSearchResult> searchResult = SoundCloudMusic.scClient.Search.GetTracksAsync(linkOrKeyword, 0, count).ToListAsync().GetAwaiter().GetResult();
            return searchResult.Select(sR => new SearchResult(sR.PermalinkUrl.AbsoluteUri, sR.Title, sR.User.Username, sR.User.PermalinkUrl.AbsoluteUri, (sR.ArtworkUrl == null ? "" : sR.ArtworkUrl.AbsoluteUri))).ToList();
        }
    }
}
