using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.SlashCommands;

namespace DiscordBot.Instance
{
    public class GlobalSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("volume", "Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(InteractionContext ctx, [Option("volume", "Âm lượng"), Minimum(0), Maximum(250)] long volume = -1) => await BotServerInstance.SetVolume(ctx.Interaction, volume);
    }
}
