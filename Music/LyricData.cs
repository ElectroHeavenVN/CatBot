using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music
{
    internal class LyricData
    {
        internal LyricData(string title, string artists, string lyric, string albumThumbnailLink)
        {
            Title = title;
            Artists = artists;
            Lyric = lyric;
            AlbumThumbnailLink = albumThumbnailLink;
        }

        internal LyricData(string notFoundMessage) => NotFoundMessage = notFoundMessage;

        internal string Title { get; set; }
        internal string Artists { get; set; }
        internal string Lyric { get; set; }
        internal string AlbumThumbnailLink { get; set; }
        internal string NotFoundMessage { get; private set; }
    }
}
