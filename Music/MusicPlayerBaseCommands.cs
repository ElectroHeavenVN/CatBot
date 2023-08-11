using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordBot.Voice;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace DiscordBot.Music
{
    public class MusicPlayerBaseCommands : BaseCommandModule
    {
        [Command("musicvolume"), Aliases("mvol"), Description("Xem hoặc chỉnh âm lượng nhạc của bot")]
        public async Task SetVolume(CommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await MusicPlayerCore.SetVolume(ctx.Message, volume);
    }
}
