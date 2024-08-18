using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees.Metadata;

namespace CatBot.Voice
{
    public class VoiceChannelSFXBaseCommands
    {
        [Command("s"), Description("Chọn file SFX để nói")]
        public async Task Speak(TextCommandContext ctx, [Description("Tên file (cách nhau bằng dấu cách) hoặc \"x\" + số lần lặp lại file SFX trước đó")] params string[] fileNames) => await VoiceChannelSFXCore.Speak(ctx.Message, fileNames);

        //[Command("reconnect"), Description("Kết nối lại kênh thoại hiện tại")]
        //public async Task Reconnect(TextCommandContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx.Message);

        [Command("dict"), Description("Xem danh sách file")]
        public async Task Dictionary(TextCommandContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx.Message);

        [Command("stop"), Description("Dừng phát SFX")]
        public async Task StopSpeaking(TextCommandContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx.Message);

        [TextAlias("leave"), Description("Thoát kênh thoại hiện tại")]
        public async Task Disconnect(TextCommandContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx.Message);

        //[Command("delay"), Description("Chỉnh thời gian nghỉ giữa các SFX khi phát tuần tự")]
        //public async Task Delay(TextCommandContext ctx, [Description("Thời gian nghỉ (mili giây)")] long delay) => await VoiceChannelSFXCore.Delay(ctx.Message, (int)delay);

        [Command("sfxvol"), Description("Xem hoặc chỉnh âm lượng SFX của bot")]
        public async Task SetVolume(TextCommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await VoiceChannelSFXCore.SetVolume(ctx.Message, volume);
    }
}
