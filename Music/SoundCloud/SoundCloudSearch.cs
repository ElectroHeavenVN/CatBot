﻿using SoundCloudExplode.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SoundCloudExplode.Track;

namespace DiscordBot.Music.SoundCloud
{
    internal class SoundCloudSearch
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (linkOrKeyword.StartsWith(SoundCloudMusic.soundCloudLink))
            {
                List<TrackSearchResult> result = SoundCloudMusic.scClient.Search.GetTracksAsync(linkOrKeyword).GetAwaiter().GetResult();
                if (result.Count == 0)
                    return new List<SearchResult>();
                linkOrKeyword = result[0].PermalinkUrl.AbsoluteUri;
                TrackInformation track = SoundCloudMusic.scClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
                return new List<SearchResult>()
                {
                    new SearchResult(linkOrKeyword, track.Title, track.User.Username, track.User.PermalinkUrl.AbsoluteUri, track.ArtworkUrl.AbsoluteUri) 
                };
            }
            List<TrackSearchResult> searchResult = SoundCloudMusic.scClient.Search.GetTracksAsync(linkOrKeyword, 0, count).GetAwaiter().GetResult();
            return searchResult.Select(sR => new SearchResult(sR.PermalinkUrl.AbsoluteUri, sR.Title, sR.User.Username, sR.User.PermalinkUrl.AbsoluteUri, (sR.ArtworkUrl == null ? "" : sR.ArtworkUrl.AbsoluteUri))).ToList();
        }
    }
}