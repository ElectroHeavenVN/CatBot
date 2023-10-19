using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music.Local
{
    internal class LocalMusicChoiceProvider : IAutocompleteProvider
    {
        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            List<DiscordAutoCompleteChoice> choices = new List<DiscordAutoCompleteChoice>();
            List<FileInfo> musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().ToList();
            musicFiles.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            foreach (FileInfo musicFile in musicFiles.Where(f => f.Extension == ".mp3"))
            {
                string musicFileName = MusicUtils.GetLocalSongTitle(Path.GetFileNameWithoutExtension(musicFile.Name));
                if (musicFileName.ToLower().Contains(ctx.FocusedOption.Value.ToString().ToLower()))
                    choices.Add(new DiscordAutoCompleteChoice(musicFileName, Path.GetFileNameWithoutExtension(musicFile.Name)));
                if (choices.Count >= 25)
                    break;
            }
            return Task.FromResult(choices.AsEnumerable());
        }
    }

}
