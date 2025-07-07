using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace CatBot.Music.ZingMP3
{
    internal static class ZingMP3Search
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (linkOrKeyword.StartsWith(ZingMP3Music.zingMP3Link))
            {
                JToken songInfo = ZingMP3Music.GetSongInfo(linkOrKeyword);
                return
                [
                    new SearchResult(ZingMP3Music.GetSongID(linkOrKeyword), songInfo["title"]?.ToString() ?? "", songInfo["artistsNames"]?.ToString() ?? "", "", songInfo["thumbnailM"]?.ToString() ?? "") 
                ];
            }
            else
            {
                JArray? arr = ZingMP3Music.SearchSongs(linkOrKeyword, count)?["items"] as JArray;
                if (arr?.Count > 0)
                {
                    return arr.Select(jT => new SearchResult(ZingMP3Music.GetSongID(ZingMP3Music.zingMP3Link.TrimEnd('/') + jT["link"]?.ToString() ?? ""), jT["title"]?.ToString() ?? "", jT["artistsNames"]?.ToString() ?? "", "", jT["thumbnailM"]?.ToString() ?? "")).ToList();
                }
                else
                    return [];
            } 
        }
    }
}
