using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Voice
{
    public class TTSSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("speak", "Sử dụng bot để nói")]
        public async Task SpeakTTS(InteractionContext ctx, [Option("content", "Nội dung bạn muốn bot nói")] string content, [Option("voice", "Giọng nói bạn muốn bot nói")] VoiceID voiceId = VoiceID.NamBac) => await TTSCore.SpeakTTS(ctx.Interaction, content, Enum.GetName(typeof(VoiceID), voiceId));

        [SlashCommand("ttsvolume", "Xem hoặc chỉnh âm lượng TTS của bot")]
        public async Task SetSFXVolume(InteractionContext ctx, [Option("volume", "Âm lượng"), Minimum(0), Maximum(250)] long volume = -1) => await TTSCore.SetVolume(ctx.Interaction, volume);
    }
}
