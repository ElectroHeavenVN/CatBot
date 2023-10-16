using CatBot.Voice;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Admin
{
    [SlashCommandGroup("admin", "admin commands")]
    public class AdminSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("join-voice", "Vào kênh thoại")]
        public async Task JoinVoiceChannel(InteractionContext ctx, [Option("channelID", "ID kênh thoại")] string channelID) => await AdminCommandsCore.JoinVoiceChannel(ctx, channelID);

        [SlashCommand("leave-voice", "Thoát kênh thoại")]
        public async Task LeaveVoiceChannel(InteractionContext ctx, [Option("serverID", "ID Máy chủ")] string serverID) => await AdminCommandsCore.LeaveVoiceChannel(ctx, serverID);

        [SlashCommand("reset-instance", "Đặt lại instance bot của server")]
        public async Task ResetBotServerInstance(InteractionContext ctx, [Option("serverID", "ID máy chủ")] string serverID = "this") => await AdminCommandsCore.ResetBotServerInstance(ctx, serverID);

        [SlashCommand("set-status", "Đặt trạng thái bot")]
        public async Task SetBotStatus(InteractionContext ctx, [Option("name", "tên trạng thái")] string name = "", [Option("activityType", "Loại trạng thái")] ActivityType activityType = ActivityType.Playing) => await AdminCommandsCore.SetBotStatus(ctx, name, activityType);
    }
}
