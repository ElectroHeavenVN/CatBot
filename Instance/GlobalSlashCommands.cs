using System.Threading.Tasks;
using DiscordBot.Admin;
using DSharpPlus.SlashCommands;

namespace DiscordBot.Instance
{
    public class GlobalSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("volume", "Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(InteractionContext ctx, [Option("volume", "Âm lượng"), Minimum(0), Maximum(250)] long volume = -1) => await BotServerInstance.SetVolume(ctx.Interaction, volume);

        [SlashCommand("reset", "Đặt lại bot (dùng trong trường hợp bot bị lỗi)")]
        public async Task ResetBotServerInstance(InteractionContext ctx) => await AdminCommandsCore.ResetBotServerInstance(ctx, "this");
    }
}
