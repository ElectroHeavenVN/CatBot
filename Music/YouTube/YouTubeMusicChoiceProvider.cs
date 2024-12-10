using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CatBot.Music.YouTube
{
    internal class YouTubeMusicChoiceProvider : IAutoCompleteProvider
    {
        public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
        {
            var result = new List<DiscordAutoCompleteChoice>();
            if (context.UserInput is null)
                return result;
            await Task.Run(() =>
            {
                string linkOrKeyword = context.UserInput;
                if (string.IsNullOrWhiteSpace(linkOrKeyword))
                    return;
                YouTubeSearch.Search(linkOrKeyword).ForEach(sR =>
                {
                    string name = sR.Title + " - " + sR.Author;
                    if (name.Length > 100)
                    {
                        if (sR.Author.Length <= sR.Title.Length)
                            name = sR.Title.Substring(0, 100 - 3 - sR.Author.Length - 3) + "..." + " - " + sR.Author;
                        else
                            name = name.Substring(0, 97) + "...";
                    }
                    if (!result.Any(c => c.Name == name))
                        result.Add(new DiscordAutoCompleteChoice(name, sR.LinkOrID));
                });
            });
            return result;
        }
    }
}
