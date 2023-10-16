using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        [ChoiceName("Hàng đợi (Bài hát sẽ bị xóa khỏi hàng đợi khi phát)")]
        Queue = 0,
        [ChoiceName("Lặp hàng đợi (Bài hát sẽ không bị xóa khỏi hàng đợi khi phát)")]
        LoopQueue = 1,
        [ChoiceName("Tuần tự")]
        Incremental = 2,
        [ChoiceName("Ngẫu nhiên")]
        Random = 3,
        [ChoiceName("Không lặp lại bài hiện tại")]
        DontLoopSong = 4,
        [ChoiceName("Lặp lại bài hiện tại")]
        LoopASong = 5,
    }
}
