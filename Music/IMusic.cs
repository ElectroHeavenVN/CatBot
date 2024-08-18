using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;

namespace CatBot.Music
{
    internal interface IMusic : IDisposable
    {
        MusicType MusicType { get; }
        string PathOrLink { get; }
        TimeSpan Duration { get; }
        string Title { get; }
        string TitleWithLink { get; }
        string[] Artists { get; }
        string[] ArtistsWithLinks { get; }
        string AllArtists { get; }
        string AllArtistsWithLinks { get; }
        string Album { get; }
        string AlbumWithLink { get; }
        string AlbumThumbnailLink { get; }
        SponsorBlockOptions? SponsorBlockOptions { get; set; }
        LyricData? GetLyric();
        Stream? MusicPCMDataStream { get; }
        DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed);
        string GetSongDesc(bool hasTimeStamp = false);
        string GetPCMFilePath();
        void DeletePCMFile();
        string[] GetFilesInUse();
        string GetIcon();
        bool isLinkMatch(string link);
        void Download();
        MusicFileDownload GetDownloadFile();
    }
}
