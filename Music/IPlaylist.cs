using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;

namespace CatBot.Music
{
    internal interface IPlaylist
    {
        bool isLinkMatch(string link);
        long TracksCount { get; }
        string Title { get; }
        string Description { get; }
        string Author { get; }
        string ThumbnailLink { get; }
        CancellationTokenSource? AddSongsInPlaylistCTS { get; set; }
        DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed);
        string GetPlaylistDesc();
        void SetSponsorBlockOptions(SponsorBlockOptions sponsorBlockOptions);
    }
}
