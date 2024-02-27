using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CatBot.Music.Local
{
    internal class LocalMusicChoiceProvider : IAutocompleteProvider
    {
        static List<DiscordAutoCompleteChoice> cachedLocalMusicChoices = new List<DiscordAutoCompleteChoice>();

        static DateTime lastTimeCachedLocalMusic = DateTime.MinValue;

        internal static void UpdateCachedLocalMusic()
        {
            while (true)
            {
                if ((DateTime.Now - lastTimeCachedLocalMusic).TotalHours > 1)
                {
                    lastTimeCachedLocalMusic = DateTime.Now;
                    new Thread(CacheLocalMusic) { IsBackground = true }.Start();
                }
                Thread.Sleep(60000);
            }
        }

        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            List<DiscordAutoCompleteChoice> choices = new List<DiscordAutoCompleteChoice>();
            //List<FileInfo> musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            //musicFiles.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            //foreach (FileInfo musicFile in musicFiles)
            //{
            //    string musicFileName = Path.GetFileNameWithoutExtension(musicFile.Name);
            //    TagLib.File taglibMusicFile = TagLib.File.Create(Path.Combine(Config.gI().MusicFolder, musicFileName + ".mp3"));
            //    string title = string.IsNullOrWhiteSpace(taglibMusicFile.Tag.Title) ? musicFileName : taglibMusicFile.Tag.Title;
            //    string artists = string.Join(", ", taglibMusicFile.Tag.Performers);
            //    string name = title + " - " + artists;
            //    if (name.Length > 100)
            //    {
            //        if (artists.Length <= title.Length)
            //            name = title.Substring(0, 100 - 3 - artists.Length - 3) + "..." + " - " + artists;
            //        else
            //            name = name.Substring(0, 97) + "...";
            //    }
            //    if (musicFileName.ToLower().Contains(ctx.FocusedOption.Value.ToString().ToLower()))
            //        choices.Add(new DiscordAutoCompleteChoice(name, Path.GetFileNameWithoutExtension(musicFile.Name).Min(100)));
            //    if (choices.Count >= 25)
            //        break;
            //}
            foreach (DiscordAutoCompleteChoice choice in cachedLocalMusicChoices)
            {
                if (choice.Name.ToLower().Contains(ctx.FocusedOption.Value.ToString().ToLower()))
                    choices.Add(choice);
                if (choices.Count >= 25)
                    break;
            }
            return Task.FromResult(choices.AsEnumerable());
        }

        static void CacheLocalMusic()
        {
            List<FileInfo> musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            if (cachedLocalMusicChoices.Count == musicFiles.Count)
                return;
            musicFiles.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            List<DiscordAutoCompleteChoice> cachedLocalMusics = new List<DiscordAutoCompleteChoice>();
                foreach (FileInfo musicFile in musicFiles)
                {
                    try
                    {
                        string musicFileName = Path.GetFileNameWithoutExtension(musicFile.Name);
                        TagLib.File taglibMusicFile = TagLib.File.Create(Path.Combine(Config.gI().MusicFolder, musicFileName + ".mp3"));
                        string title = string.IsNullOrWhiteSpace(taglibMusicFile.Tag.Title) ? musicFileName : taglibMusicFile.Tag.Title;
                        string artists = string.Join(", ", taglibMusicFile.Tag.Performers);
                        string name = title + " - " + artists;
                        if (name.Length > 100)
                        {
                            if (artists.Length <= title.Length)
                                name = title.Substring(0, 100 - 3 - artists.Length - 3) + "..." + " - " + artists;
                            else
                                name = name.Substring(0, 97) + "...";
                        }
                        cachedLocalMusics.Add(new DiscordAutoCompleteChoice(name, Path.GetFileNameWithoutExtension(musicFile.Name).Min(100)));
                    }
                    catch (Exception ex) { Utils.LogException(ex); }
            }
            cachedLocalMusicChoices = cachedLocalMusics;
        }
    }

}
