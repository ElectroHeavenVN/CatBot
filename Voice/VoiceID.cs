using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Voice
{
    public enum VoiceID
    {
        [ChoiceName("Giọng nam miền Bắc")]
        NamBac = 4,
        [ChoiceName("Giọng nữ miền Bắc")]
        NuBac = 2,
        [ChoiceName("Giọng nam miền Nam")]
        NamNam = 3,
        [ChoiceName("Giọng nữ miền Nam")]
        NuNam = 1
    }
}
