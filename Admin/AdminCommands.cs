using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CatBot.Admin
{
    public class AdminCommands
    {
        [Command("add-sfx")]
        [Description("Thêm SFX vào danh sách SFX")]
        public async Task AddSFX(TextCommandContext ctx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx, sfxName, false);

        [Command("add-sfx")]
        [Description("Thêm SFX vào danh sách SFX")]
        public async Task AddSFX(SlashCommandContext ctx, [Description("Tệp SFX")] DiscordAttachment sfx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx, sfx, sfxName, false);

        [Command("add-special-sfx")]
        [Description("Thêm SFX vào danh sách SFX đặc biệt")]
        public async Task AddSpecialSFX(TextCommandContext ctx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx, sfxName, true);

        [Command("add-special-sfx")]
        [Description("Thêm SFX vào danh sách SFX đặc biệt")]
        public async Task AddSpecialSFX(SlashCommandContext ctx, [Description("Tệp SFX")] DiscordAttachment sfx, [Description("Tên SFX")] string sfxName = "") => await AdminCommandsCore.AddSFX(ctx, sfx, sfxName, true);

        [Command("download-music")]
        [Description("Tải nhạc vào thư mục nhạc local")]
        public async Task DownloadMusic(TextCommandContext ctx) => await AdminCommandsCore.DownloadMusic(ctx);

        [Command("download-music")]
        [Description("Tải nhạc vào thư mục nhạc local")]
        public async Task DownloadMusic(SlashCommandContext ctx, [Description("Tệp nhạc")] DiscordAttachment file) => await AdminCommandsCore.DownloadMusic(ctx, file);

        [Command("delete-sfx")]
        [Description("Xóa SFX khỏi danh sách SFX")]
        public async Task DeleteSFX(CommandContext ctx, [Description("Tên SFX")] string sfxName) => await AdminCommandsCore.DeleteSFX(ctx, sfxName);
    
        [Command("join-voice")]
        [Description("Vào kênh thoại")]
        public async Task JoinVoiceChannel(CommandContext ctx, [Parameter("channelID"), Description("ID kênh thoại")] string channelID) => await AdminCommandsCore.JoinVoiceChannel(ctx, channelID);

        [Command("leave-voice")]
        [Description("Rời kênh thoại")]
        public async Task LeaveVoiceChannel(CommandContext ctx, [Parameter("serverID"), Description("ID Máy chủ")] string serverID) => await AdminCommandsCore.LeaveVoiceChannel(ctx, serverID);

        [Command("reset-instance")]
        [Description("Đặt lại instance bot của server")]
        public async Task ResetBotServerInstance(CommandContext ctx, [Parameter("serverID"), Description("ID máy chủ")] string serverID = "this") => await AdminCommandsCore.ResetBotServerInstance(ctx, serverID);

        [Command("set-presence")]
        [Description("Đặt presence bot")]
        public async Task SetBotStatus(CommandContext ctx, [Parameter("name"), Description("tên presence")] string name = "", [Parameter("activityType"), Description("Loại presence")] DiscordActivityType activityType = DiscordActivityType.Playing, [Parameter("state"), Description("trạng thái presence")] string state = "") => await AdminCommandsCore.SetBotStatus(ctx, activityType, name, state);

        [Command("restart")]
        [Description("Khởi động lại bot")]
        public async Task RestartBot(CommandContext ctx) => await AdminCommandsCore.RestartBot(ctx);
    }
}
