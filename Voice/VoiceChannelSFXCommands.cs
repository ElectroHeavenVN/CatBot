﻿using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CatBot.Voice
{
    public class VoiceChannelSFXCommands
    {
        [Command("speak-file"), TextAlias("s"), Description("Chọn file SFX để nói")]
        public async Task Speak(CommandContext ctx, [Parameter("file"), Description("Tên file (cách nhau bằng dấu cách) hoặc \"x\" + số lần lặp lại file SFX trước đó"), SlashAutoCompleteProvider(typeof(VoiceSFXChoiceProvider)), RemainingText] string fileNames) => await VoiceChannelSFXCore.Speak(ctx, fileNames.Split(' '));

        [Command("reconnect"), Description("Kết nối lại kênh thoại hiện tại")]
        public async Task Reconnect(CommandContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx);

        [Command("dictionary"), TextAlias("dict"), Description("Xem danh sách file")]
        public async Task Dictionary(CommandContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx);

        [Command("stops-speak"), TextAlias("stop"), Description("Dừng phát SFX và TTS")]
        public async Task StopSpeaking(CommandContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx);

        [Command("disconnect"), TextAlias("leave"), Description("Thoát kênh thoại hiện tại")]
        public async Task Disconnect(CommandContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx);

        [Command("delay"), Description("Chỉnh thời gian nghỉ giữa các SFX khi phát tuần tự")]
        public async Task Delay(CommandContext ctx, [Parameter("delay"), Description("Thời gian nghỉ (mili giây)"), MinMaxValue((long)0, (long)5000)] long delay = 250) => await VoiceChannelSFXCore.Delay(ctx, (int)delay);

        [Command("sfxvolume"), TextAlias("sfxvol"), Description("Xem hoặc chỉnh âm lượng SFX của bot")]
        public async Task SetSFXVolume(CommandContext ctx, [Parameter("volume"), Description("Âm lượng"), MinMaxValue((long)-1, (long)250)] long volume = -1) => await VoiceChannelSFXCore.SetVolume(ctx, volume);
    }

    internal class VoiceSFXChoiceProvider : IAutoCompleteProvider
    {
        public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            var result = new List<DiscordAutoCompleteChoice>();
            if (context.UserInput is null)
                return result;
            await Task.Run(() =>
            {
                List<FileInfo> sfxFiles = new DirectoryInfo(Config.gI().SFXFolder).GetFiles().Concat(new DirectoryInfo(Config.gI().SFXFolderSpecial).GetFiles()).ToList();
                sfxFiles.Sort((f1, f2) => f1.CreationTime.CompareTo(f2.CreationTime));
                string userInput = context.UserInput;
                if (string.IsNullOrWhiteSpace(userInput))
                    return;
                string[] fileNamesUserInput = userInput.Split(' ');
                int index = userInput.LastIndexOf(' ');
                if (index == -1)
                    index = 0;
                foreach (FileInfo sfxFile in sfxFiles.Where(f => f.Extension == ".pcm"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(sfxFile.Name);
                    if (fileName.ToLower().Contains(fileNamesUserInput.Last().ToLower()))
                    {
                        string str = userInput.Substring(0, index) + " " + fileName;
                        if (!result.Any(c => c.Name == str))
                            result.Add(new DiscordAutoCompleteChoice(str, str));
                    }
                    if (result.Count >= 25)
                        break;
                }
            });
            return result;
        }
    }
}
