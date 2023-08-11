using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Voice
{
    public class TTSBaseCommands : BaseCommandModule
    {
        [Command("tts"), Description("Sử dụng bot để nói")]
        public async Task SpeakTTS(CommandContext ctx, [RemainingText, Description("Nội dung bạn muốn bot nói")] string tts)
        {
            List<string> strs = tts.Split(' ').ToList();
            if (Enum.TryParse(strs[0], true, out VoiceID result))
            {
                strs.RemoveAt(0);
                await TTSCore.SpeakTTS(ctx.Message, string.Join(" ", strs), Enum.GetName(typeof(VoiceID), result));
            }
            else 
                await TTSCore.SpeakTTS(ctx.Message, tts);
        }

        [Command("ttsvolume"), Aliases("ttsvol"), Description("Xem hoặc chỉnh âm lượng TTS của bot")]
        public async Task SetVolume(CommandContext ctx, [Description("Âm lượng (0 - 250)")] long volume = -1) => await TTSCore.SetVolume(ctx.Message, volume);
    }
}
