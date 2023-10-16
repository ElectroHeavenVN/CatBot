using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace CatBot.Music.Spotify
{
    internal class SpotifyMusicChoiceProvider : IAutocompleteProvider
    {
        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            Task<IEnumerable<DiscordAutoCompleteChoice>> result = null;
            string linkOrKeyword = (string)ctx.FocusedOption.Value;
            if (string.IsNullOrWhiteSpace(linkOrKeyword))
                result = Task.FromResult(new List<DiscordAutoCompleteChoice>().AsEnumerable());
            else
                result = Task.FromResult(SpotifySearch.Search(linkOrKeyword).Select(sR =>
                {
                    string name = sR.Title + " - " + sR.Author;
                    if (name.Length > 100)
                    {
                        if (100 - 3 - sR.Author.Length - 3 < sR.Title.Length)
                            name = sR.Title.Substring(0, 100 - 3 - sR.Author.Length - 3) + "..." + " - " + sR.Author;
                        else
                            name = name.Substring(0, 97) + "...";
                    }
                    return new DiscordAutoCompleteChoice(name, sR.LinkOrID);
                }));
            return result;
        }
    }
}
