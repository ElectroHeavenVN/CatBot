using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Admin
{
    public class AdminBaseCommand : BaseCommandModule
    {
        [Command("addsfx")]
        public async Task AddSFX(CommandContext ctx, string sfxName, string isSecret = "") => await AdminCommandsCore.AddSFX(ctx.Message, sfxName, isSecret);

        [Command("deletesfx"), Aliases("delsfx")]
        public async Task DeleteSFX(CommandContext ctx, string sfxName, string isSecret = "") => await AdminCommandsCore.DeleteSFX(ctx.Message, sfxName, isSecret);

        [Command("downloadmusic")]
        public async Task DownloadMusic(CommandContext ctx) => await AdminCommandsCore.DownloadMusic(ctx.Message);
    }
}
