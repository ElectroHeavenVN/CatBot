using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CatBot.Music.ZingMP3
{
    internal static class ZingMP3Search
    {
        internal static List<SearchResult> Search(string linkOrKeyword, int count = 25)
        {
            if (linkOrKeyword.StartsWith(ZingMP3Music.zingMP3Link))
            {
                JToken songInfo = ZingMP3Music.GetSongInfo(linkOrKeyword);
                return new List<SearchResult>()
                {
                    new SearchResult(ZingMP3Music.GetSongID(linkOrKeyword), songInfo["title"].ToString(), songInfo["artistsNames"].ToString(), "", songInfo["thumbnailM"].ToString()) 
                };
            }
            else
            {
                JToken obj = ZingMP3Music.SearchSongs(linkOrKeyword, count);
                if (obj["items"] != null && ((JArray)obj["items"]).Count > 0)
                {
                    return ((JArray)obj["items"]).Select(jT => new SearchResult(ZingMP3Music.GetSongID(ZingMP3Music.zingMP3Link.TrimEnd('/') + jT["link"].ToString()), jT["title"].ToString(), jT["artistsNames"].ToString(), "", jT["thumbnailM"].ToString())).ToList();
                }
                else
                    return new List<SearchResult>();
            } 
        }
    }
}
