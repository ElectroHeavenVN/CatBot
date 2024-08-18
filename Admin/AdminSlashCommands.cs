using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;

namespace CatBot.Admin
{
    [Command("admin")]
    [Description("Các lệnh dành cho quản trị viên")]
    public class AdminSlashCommands
    {
        [Command("join-voice")]
        [Description("Vào kênh thoại")]
        public async Task JoinVoiceChannel(SlashCommandContext ctx, [Parameter("channelID"), Description("ID kênh thoại")] string channelID) => await AdminCommandsCore.JoinVoiceChannel(ctx, channelID);

        [Command("leave-voice")]
        [Description("Rời kênh thoại")]
        public async Task LeaveVoiceChannel(SlashCommandContext ctx, [Parameter("serverID"), Description("ID Máy chủ")] string serverID) => await AdminCommandsCore.LeaveVoiceChannel(ctx, serverID);

        [Command("reset-instance")]
        [Description("Đặt lại instance bot của server")]
        public async Task ResetBotServerInstance(SlashCommandContext ctx, [Parameter("serverID"), Description("ID máy chủ")] string serverID = "this") => await AdminCommandsCore.ResetBotServerInstance(ctx, serverID);

        [Command("set-presence")]
        [Description("Đặt presence bot")]
        public async Task SetBotStatus(SlashCommandContext ctx, [Parameter("name"), Description("tên presence")] string name = "", [Parameter("activityType"), Description("Loại presence")] DiscordActivityType activityType = DiscordActivityType.Playing, [Parameter("state"), Description("trạng thái presence")] string state = "") => await AdminCommandsCore.SetBotStatus(ctx, activityType, name, state);
    }
}
