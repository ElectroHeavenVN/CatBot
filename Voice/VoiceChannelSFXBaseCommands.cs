using DiscordBot.Instance;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Voice
{
    public class VoiceChannelSFXBaseCommands : BaseCommandModule
    {
        [Command("s"), Description("Chọn file SFX để nói")]
        public async Task Speak(CommandContext ctx, [Description("Tên file (cách nhau bằng dấu cách) hoặc \"x\" + số lần lặp lại file SFX trước đó")] params string[] fileNames) => await VoiceChannelSFXCore.Speak(ctx.Message, fileNames);

        [Command("reconnect"), Description("Kết nối lại kênh thoại hiện tại")]
        public async Task Reconnect(CommandContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx.Message);

        [Command("dictionary"), Aliases("dict"), Description("Xem danh sách file")]
        public async Task Dictionary(CommandContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx.Message);

        [Command("stop"), Description("Dừng phát SFX và TTS")]
        public async Task StopSpeaking(CommandContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx.Message);

        [Command("disconnect"), Aliases("leave"), Description("Thoát kênh thoại hiện tại")]
        public async Task Disconnect(CommandContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx.Message);

        [Command("delay"), Description("Chỉnh thời gian nghỉ giữa các SFX khi phát tuần tự")]
        public async Task Delay(CommandContext ctx, [Description("Thời gian nghỉ (mili giây)")] long delay) => await VoiceChannelSFXCore.Delay(ctx.Message, (int)delay);

        [Command("sfxvolume"), Aliases("sfxvol"), Description("Xem hoặc chỉnh âm lượng SFX của bot")]
        public async Task SetVolume(CommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await VoiceChannelSFXCore.SetVolume(ctx.Message, volume);
    }
}
