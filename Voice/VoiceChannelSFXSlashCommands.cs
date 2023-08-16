using DiscordBot.Instance;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Voice
{
    public class VoiceChannelSFXSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("speakfile", "Chọn file SFX để nói")]
        public async Task Speak(InteractionContext ctx, [Option("file", "Tên file (cách nhau bằng dấu cách) hoặc \"x\" + số lần lặp lại file SFX trước đó"), Autocomplete(typeof(VoiceSFXChoiceProvider))] string fileNames) => await VoiceChannelSFXCore.Speak(ctx.Interaction, fileNames.Split(' '));

        [SlashCommand("reconnect", "Kết nối lại kênh thoại hiện tại")]
        public async Task Reconnect(InteractionContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx.Interaction);

        [SlashCommand("dictionary", "Xem danh sách file")]
        public async Task Dictionary(InteractionContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx.Interaction);

        [SlashCommand("stopsspeak", "Dừng phát SFX và TTS")]
        public async Task StopSpeaking(InteractionContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx.Interaction);

        [SlashCommand("disconnect", "Thoát kênh thoại hiện tại")]
        public async Task Disconnect(InteractionContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx.Interaction);

        [SlashCommand("delay", "Chỉnh thời gian nghỉ giữa các SFX khi phát tuần tự")]
        public async Task Delay(InteractionContext ctx, [Option("delay", "Thời gian nghỉ (mili giây)"), Minimum(0), Maximum(5000)] long delay = 250) => await VoiceChannelSFXCore.Delay(ctx.Interaction, (int)delay);

        [SlashCommand("sfxvolume", "Xem hoặc chỉnh âm lượng SFX của bot")]
        public async Task SetSFXVolume(InteractionContext ctx, [Option("volume", "Âm lượng"), Minimum(0), Maximum(250)] long volume = -1) => await VoiceChannelSFXCore.SetVolume(ctx.Interaction, volume);
    }

    internal class VoiceSFXChoiceProvider : IAutocompleteProvider
    {
        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            List<DiscordAutoCompleteChoice> choices = new List<DiscordAutoCompleteChoice>();
            List<FileInfo> sfxFiles = new DirectoryInfo(Config.SFXFolder).GetFiles().Concat(new DirectoryInfo(Config.SFXFolderSpecial).GetFiles()).ToList();
            sfxFiles.Sort((f1, f2) => f1.CreationTime.CompareTo(f2.CreationTime));
            string userInput = ctx.FocusedOption.Value.ToString();
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
                    choices.Add(new DiscordAutoCompleteChoice(str, str));
                }
                if (choices.Count >= 25)
                    break;
            }
            return Task.FromResult(choices.AsEnumerable());
        }
    }
}
