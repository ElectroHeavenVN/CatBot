using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CatBot.Music
{
    public enum MusicType
    {
        [ChoiceDisplayName("Nhạc local")]
        Local = 1,
        [ChoiceDisplayName("Nhạc từ Zing MP3")]
        ZingMP3 = 2,
        [ChoiceDisplayName("Nhạc từ NhacCuaTui")]
        NhacCuaTui = 3,
        [ChoiceDisplayName("Video YouTube hoặc nhạc từ YouTube Music")]
        YouTube = 4,
        [ChoiceDisplayName("Nhạc từ SoundCloud")]
        SoundCloud = 5,
        [ChoiceDisplayName("Nhạc từ Spotify")]
        Spotify = 6
        //...
    }
}
