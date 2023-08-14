using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace DiscordBot.Music
{
    internal interface IPlaylist
    {
        bool isLinkMatch(string link);
        List<IMusic> Tracks { get; }
        string Title { get; }
        string Description { get; }
        string Author { get; }
        string ThumbnailLink { get; }
        DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed);
        string GetPlaylistDesc();
    }
}
