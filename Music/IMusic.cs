using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    internal interface IMusic : IDisposable
    {
        MusicType MusicType { get; }
        string PathOrLink { get; }
        TimeSpan Duration { get; }
        string Title { get; }
        string Artists { get; }
        string Album { get; }
        string AlbumThumbnailLink { get; }
        SponsorBlockOptions SponsorBlockOptions { get; set; }
        LyricData GetLyric();
        Stream MusicPCMDataStream { get; }
        DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed);
        string GetSongDesc(bool hasTimeStamp = false);
        string GetPCMFilePath();
        void DeletePCMFile();
        string[] GetFilesInUse();
        string GetIcon();
        bool isLinkMatch(string link);
        void Download();
    }
}
