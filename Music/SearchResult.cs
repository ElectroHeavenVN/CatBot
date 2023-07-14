using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    internal class SearchResult
    {
        internal SearchResult() { }
        internal SearchResult(string linkOrID, string title, string author, string authorLink, string thumbnail)
        {
            LinkOrID = linkOrID;
            Title = title;
            Author = author;
            AuthorLink = authorLink;
            Thumbnail = thumbnail;
        }

        internal string LinkOrID { get; set; }
        internal string Title { get; set; }
        internal string Author { get; set; }
        internal string AuthorLink { get; set; }
        internal string Thumbnail { get; set; }
    }
}
