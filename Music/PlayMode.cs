using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CatBot.Music
{
    public class PlayMode
    {
        public bool isLoopQueue;
        public bool isRandom;
        public bool isLoopASong;
    }

    public enum PlayModeChoice
    {
        [ChoiceDisplayName("Hàng đợi (Bài hát sẽ bị xóa khỏi hàng đợi khi phát)")]
        Queue = 0,
        [ChoiceDisplayName("Lặp hàng đợi (Bài hát sẽ không bị xóa khỏi hàng đợi khi phát)")]
        LoopQueue = 1,
        [ChoiceDisplayName("Tuần tự")]
        Incremental = 2,
        [ChoiceDisplayName("Ngẫu nhiên")]
        Random = 3,
        [ChoiceDisplayName("Không lặp lại bài hiện tại")]
        DontLoopSong = 4,
        [ChoiceDisplayName("Lặp lại bài hiện tại")]
        LoopASong = 5,
    }
}
