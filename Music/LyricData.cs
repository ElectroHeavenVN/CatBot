namespace CatBot.Music
{
    internal class LyricData
    {
        internal string Title { get; set; }
        internal string Artists { get; set; }
        internal string Album { get; set; }
        internal string PlainLyrics { get; set; } = "";
        internal string SyncedLyrics { get; set; } = "";
        internal string EnhancedLyrics { get; set; } = "";
        internal string AlbumThumbnail { get; set; }
        internal string NotFoundMessage { get; private set; }

        internal LyricData(string title, string artists, string album, string albumThumbnail)
        {
            Title = title;
            Artists = artists;
            Album = album;
            AlbumThumbnail = albumThumbnail;
            NotFoundMessage = "";
        }

        internal LyricData(string notFoundMessage)
        {
            Title = Artists = Album = AlbumThumbnail = "";
            NotFoundMessage = notFoundMessage;
        }
    }
}
