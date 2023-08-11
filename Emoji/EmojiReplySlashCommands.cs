using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Emoji
{
    public class EmojiReplySlashCommands : ApplicationCommandModule
    {
        [SlashCommand("emoji", "chèn emoji từ các server có bot")]
        public async Task ReplyWithEmoji(InteractionContext ctx, [Option("emojiName", "Tên emoji (cách nhau bằng dấu cách)")] string emojiNames) => await EmojiReplyCore.ReplyWithEmoji(ctx.Interaction, emojiNames.Split(' '));

        [SlashCommand("catEmoji", "chèn emoji CatAndSoup")]
        public async Task InsertCatNSoupEmoji(InteractionContext ctx, [Option("emojiName", "Tên emoji"), Autocomplete(typeof(CatNSoupEmojiProvider))] string emojiName) => await EmojiReplyCore.InsertCatNSoupEmoji(ctx, emojiName);
    }

    internal class CatNSoupEmojiProvider : IAutocompleteProvider
    {
        static Regex regexMatchEmojiNames = new Regex("\\?name=(.*)&quality=lossless", RegexOptions.Compiled);

        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            List<DiscordAutoCompleteChoice> choices = new List<DiscordAutoCompleteChoice>();
            string userInput = ctx.FocusedOption.Value.ToString();
            foreach (string emojiURL in CatNSoupEmoji.emojiURLs)
            {
                string str = Uri.UnescapeDataString(regexMatchEmojiNames.Match(emojiURL).Groups[1].Value);
                if (str.ToLower().Contains(userInput.ToLower()))
                    choices.Add(new DiscordAutoCompleteChoice(":" + str + ":", str));
                if (choices.Count >= 25)
                    break;
            }
            return Task.FromResult(choices.AsEnumerable());
        }
    }
}
