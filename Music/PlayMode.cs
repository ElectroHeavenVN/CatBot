using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    public enum PlayMode
    {
        Queue = 0b0,          
        LoopQueue = 0b1,      
        Incremental = 0b00,
        Random = 0b10,   
        DontLoopSong = 0b000,
        LoopASong = 0b100, 
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
