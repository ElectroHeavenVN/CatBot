using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;

namespace CatBot.Music.Dummy
{
    internal class DummyMusic : IMusic
    {
        public string Title { get; set; } = "";
        public string[] Artists { get; set; } = [];
        public string AllArtists => string.Join(", ", Artists);
        public string Album { get; set; } = "";
        public string AlbumThumbnailLink { get; set; } = "";

        public LyricData? GetLyric()
        {
            if (this.TryGetLyricsFromLRCLIB(out LyricData? result))
                return result;
            return new LyricData("Không tìm thấy lời bài hát!");
        }

        public MusicType MusicType => throw new NotImplementedException();
        public string PathOrLink => "";
        public TimeSpan Duration => TimeSpan.Zero;
        public string TitleWithLink => Title;
        public string[] ArtistsWithLinks => Artists;
        public string AllArtistsWithLinks => AllArtists;
        public string AlbumWithLink => Album;
        public SponsorBlockOptions? SponsorBlockOptions { get => null; set { } }
        public Stream? MusicPCMDataStream => throw new NotImplementedException();
        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed;
        public void DeletePCMFile() { }
        public void Dispose() { }
        public void Download() { }
        public MusicFileDownload GetDownloadFile() => throw new NotImplementedException();
        public string[] GetFilesInUse() => [];
        public string GetIcon() => "";
        public string GetPCMFilePath() => "";
        public string GetSongDesc(bool hasTimeStamp = false) => "";
        public bool isLinkMatch(string link) => false;
    }
}
