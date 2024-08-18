namespace CatBot.Music
{
    internal class MusicFileDownload
    {
        internal string Extension { get; set; }
        internal Stream Stream { get; set; }

        internal MusicFileDownload(string extension, Stream stream)
        {
            Extension = extension;
            Stream = stream;
        }
    }
}
