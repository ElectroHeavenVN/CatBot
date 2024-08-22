using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CatBot.Voice
{
    public class VoiceChannelSFXSlashCommands
    {
        [Command("speak-file"), Description("Chọn file SFX để nói")]
        public async Task Speak(SlashCommandContext ctx, [Parameter("file"), Description("Tên file (cách nhau bằng dấu cách) hoặc \"x\" + số lần lặp lại file SFX trước đó"), SlashAutoCompleteProvider(typeof(VoiceSFXChoiceProvider))] string fileNames) => await VoiceChannelSFXCore.Speak(ctx.Interaction, fileNames.Split(' '));

        [Command("reconnect"), Description("Kết nối lại kênh thoại hiện tại")]
        public async Task Reconnect(SlashCommandContext ctx) => await VoiceChannelSFXCore.Reconnect(ctx.Interaction);

        [Command("dictionary"), Description("Xem danh sách file")]
        public async Task Dictionary(SlashCommandContext ctx) => await VoiceChannelSFXCore.Dictionary(ctx.Interaction);

        [Command("stops-speak"), Description("Dừng phát SFX và TTS")]
        public async Task StopSpeaking(SlashCommandContext ctx) => await VoiceChannelSFXCore.StopSpeaking(ctx.Interaction);

        [Command("disconnect"), Description("Thoát kênh thoại hiện tại")]
        public async Task Disconnect(SlashCommandContext ctx) => await VoiceChannelSFXCore.Disconnect(ctx.Interaction);

        [Command("delay"), Description("Chỉnh thời gian nghỉ giữa các SFX khi phát tuần tự")]
        public async Task Delay(SlashCommandContext ctx, [Parameter("delay"), Description("Thời gian nghỉ (mili giây)"), MinMaxValue((long)0, (long)5000)] long delay = 250) => await VoiceChannelSFXCore.Delay(ctx.Interaction, (int)delay);

        [Command("sfxvolume"), Description("Xem hoặc chỉnh âm lượng SFX của bot")]
        public async Task SetSFXVolume(SlashCommandContext ctx, [Parameter("volume"), Description("Âm lượng"), MinMaxValue((long)-1, (long)250)] long volume = -1) => await VoiceChannelSFXCore.SetVolume(ctx.Interaction, volume);
    }

    internal class VoiceSFXChoiceProvider : IAutoCompleteProvider
    {
        public async ValueTask<IReadOnlyDictionary<string, object>> AutoCompleteAsync(AutoCompleteContext context)
        {
            var result = new Dictionary<string, object>();
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
                        if (!result.ContainsKey(str))
                            result.Add(str, str);
                    }
                    if (result.Count >= 25)
                        break;
                }
            });
            return result;
        }
    }
}
