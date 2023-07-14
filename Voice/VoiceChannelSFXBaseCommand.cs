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
    public class VoiceChannelSFXBaseCommand : BaseCommandModule
    {
        [Command("s")]
        public async Task Speak(CommandContext ctx, params string[] fileNames) => await VoiceChannelSFXCore.Speak(ctx.Message, fileNames);

        [Command("reconnect")]
        public async Task Reconnect(CommandContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx.Message);

        [Command("dictionary"), Aliases("dict")]
        public async Task Dictionary(CommandContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx.Message);

        [Command("stop")]
        public async Task StopSpeaking(CommandContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx.Message);

        [Command("disconnect"), Aliases("leave")]
        public async Task Disconnect(CommandContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx.Message);

        [Command("delay")]
        public async Task Delay(CommandContext ctx, long delay) => await VoiceChannelSFXCore.Delay(ctx.Message, (int)delay);

        [Command("volume"), Aliases("vol", "v")]
        public async Task SetVolume(CommandContext ctx, long volume) => await VoiceChannelSFXCore.SetVolume(ctx.Message, volume);
    }
}
