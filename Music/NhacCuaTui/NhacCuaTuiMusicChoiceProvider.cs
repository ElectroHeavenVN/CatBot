using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CatBot.Music.NhacCuaTui
{
    internal class NhacCuaTuiMusicChoiceProvider : IAutoCompleteProvider
    {
        public async ValueTask<IReadOnlyDictionary<string, object>> AutoCompleteAsync(AutoCompleteContext context)
        {
            var result = new Dictionary<string, object>();
            await Task.Run(() =>
            {
                string linkOrKeyword = context.UserInput;
                if (string.IsNullOrWhiteSpace(linkOrKeyword))
                    return;
                NhacCuaTuiSearch.Search(linkOrKeyword).ForEach(sR =>
                {
                    string name = sR.Title + " - " + sR.Author;
                    if (name.Length > 100)
                    {
                        if (sR.Author.Length <= sR.Title.Length)
                            name = sR.Title.Substring(0, 100 - 3 - sR.Author.Length - 3) + "..." + " - " + sR.Author;
                        else
                            name = name.Substring(0, 97) + "...";
                    }
                    result.Add(name, sR.LinkOrID);
                });
            });
            return result;
        }
    }
}
