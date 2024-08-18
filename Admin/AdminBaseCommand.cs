using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees.Metadata;

namespace CatBot.Admin
{
    public class AdminBaseCommand
    {
        [Command("addsfx"), Description("(Lệnh chỉ dành cho tác giả của bot) Thêm SFX vào danh sách SFX")]
        public async Task AddSFX(TextCommandContext ctx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx.Message, sfxName, false);
        
        [Command("addsfxspecial"), Description("(Lệnh chỉ dành cho tác giả của bot) Thêm SFX vào danh sách SFX đặc biệt")]
        public async Task AddSFXSpecial(TextCommandContext ctx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx.Message, sfxName, true);

        [Command("deletesfx"), TextAlias("delsfx"), Description("(Lệnh chỉ dành cho tác giả của bot) Xóa SFX khỏi danh sách SFX")]
        public async Task DeleteSFX(TextCommandContext ctx, [Description("Tên SFX")] string sfxName, [Description("SFX có phải SFX đặc biệt không?")] string isSpecial = "") => await AdminCommandsCore.DeleteSFX(ctx.Message, sfxName, isSpecial);

        [Command("downloadmusic"), Description("(Lệnh chỉ dành cho tác giả của bot) Thêm nhạc vào danh sách nhạc local")]
        public async Task DownloadMusic(TextCommandContext ctx) => await AdminCommandsCore.DownloadMusic(ctx.Message);
    }
}
