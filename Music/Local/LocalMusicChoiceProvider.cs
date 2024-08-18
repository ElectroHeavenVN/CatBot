using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CatBot.Music.Local
{
    internal class LocalMusicChoiceProvider : IAutoCompleteProvider
    {
        static Dictionary<string, object> cachedLocalMusicChoices = new Dictionary<string, object>();

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

        static void CacheLocalMusic()
        {
            List<FileInfo> musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            if (cachedLocalMusicChoices.Count == musicFiles.Count)
                return;
            musicFiles.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            Dictionary<string, object> cachedLocalMusics = new Dictionary<string, object>();
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
                        cachedLocalMusics.Add(name, Path.GetFileNameWithoutExtension(musicFile.Name).Min(100));
                    }
                    catch (Exception ex) { Utils.LogException(ex); }
            }
            cachedLocalMusicChoices = cachedLocalMusics;
        }

        public async ValueTask<IReadOnlyDictionary<string, object>> AutoCompleteAsync(AutoCompleteContext context)
        {
            var result = new Dictionary<string, object>();
            await Task.Run(() =>
            {
                for (int i = 0; i < cachedLocalMusicChoices.Count; i++)
                {
                    var choice = cachedLocalMusicChoices.ElementAt(i);
                    if (choice.Key.Contains(context.UserInput, StringComparison.CurrentCultureIgnoreCase))
                        result.Add(choice.Key, choice.Value);
                    if (result.Count >= 25)
                        break;
                }
            });
            return result;
        }
    }

}
