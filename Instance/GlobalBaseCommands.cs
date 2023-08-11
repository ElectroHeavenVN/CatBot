using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext;

namespace DiscordBot.Instance
{
    public class GlobalBaseCommands : BaseCommandModule
    {
        [Command("volume"), Aliases("vol", "v"), Description("Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(CommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await BotServerInstance.SetVolume(ctx.Message, volume);
    }
}
