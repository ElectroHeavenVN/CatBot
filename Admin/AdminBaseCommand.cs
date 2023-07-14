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
        [Command("addsfx"), Description("(Lệnh chỉ dành cho tác giả của bot) Thêm SFX vào danh sách SFX")]
        public async Task AddSFX(CommandContext ctx, [Description("SFX có phải SFX đặc biệt không?")] string isSpecial = "", [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx.Message, isSpecial, sfxName);

        [Command("deletesfx"), Aliases("delsfx"), Description("(Lệnh chỉ dành cho tác giả của bot) Xóa SFX khỏi danh sách SFX")]
        public async Task DeleteSFX(CommandContext ctx, [Description("Tên SFX")] string sfxName, [Description("SFX có phải SFX đặc biệt không?")] string isSpecial = "") => await AdminCommandsCore.DeleteSFX(ctx.Message, sfxName, isSpecial);

        [Command("downloadmusic"), Description("(Lệnh chỉ dành cho tác giả của bot) Thêm nhạc vào danh sách nhạc local")]
        public async Task DownloadMusic(CommandContext ctx) => await AdminCommandsCore.DownloadMusic(ctx.Message);
    }
}
