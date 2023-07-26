using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    public enum MusicType
    {
        [ChoiceName("Nhạc local")]
        Local = 1,
        [ChoiceName("Nhạc từ Zing MP3")]
        ZingMP3 = 2,
        [ChoiceName("Nhạc từ NhacCuaTui")]
        NhacCuaTui = 3,
        [ChoiceName("Video YouTube hoặc nhạc từ YouTube Music")]
        YouTube = 4,
        [ChoiceName("Nhạc từ SoundCloud")]
        SoundCloud = 5,
        //[ChoiceName("Nhạc từ Spotify")]
        //Spotify = 6
        //...
    }
}
