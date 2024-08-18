using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;

namespace CatBot.Music
{
    public class MusicPlayerBaseCommands
    {
        [Command("mvol"), Description("Xem hoặc chỉnh âm lượng nhạc của bot")]
        public async Task SetVolume(TextCommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await MusicPlayerCore.SetVolume(ctx.Message, volume);
    }
}
