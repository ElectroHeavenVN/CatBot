using System;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace CatBot.Emoji
{
    public class EmojiReplyCore
    {
        public static async Task ReplyWithEmoji(SnowflakeObject obj, params string[] emojiNames)
        {
            string replyMessage1 = "";
            string replyMessage2 = "";
            foreach (string emojiName in emojiNames)
            {
                string emojiMessage = "";
                if (CatNSoupEmoji.getLink(emojiName, out string link))
                    emojiMessage = link + Environment.NewLine;
                else
                {
                    if (DiscordEmoji.TryFromName(DiscordBotMain.botClient, ':' + emojiName + ':', false, out DiscordEmoji emoji))
                        emojiMessage = emoji.Name;
                    else
                    {
                        foreach (DiscordGuild server in DiscordBotMain.botClient.Guilds.Values)
                        {
                            if (!obj.TryGetUser().isInAdminUser())
                                continue;
                            bool foundEmoji = false;
                            foreach (DiscordEmoji emoji2 in server.Emojis.Values)
                            {
                                if (emojiName != emoji2.Name)
                                    continue;
                                emojiMessage = $"<:{emoji2.Name}:{emoji2.Id}>";
                                foundEmoji = true;
                                break;
                            }
                            if (foundEmoji)
                                break;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(emojiMessage))
                    replyMessage2 += emojiName + ", ";
                else
                    replyMessage1 += emojiMessage;
            }
            replyMessage2 = replyMessage2.TrimEnd(',', ' ');
            string replyMessage = replyMessage1;
            if (!string.IsNullOrWhiteSpace(replyMessage2))
                replyMessage = (replyMessage1 + Environment.NewLine + "Không tìm thấy emoji " + replyMessage2 + "!").TrimStart('\r', '\n');
            await obj.TryRespondAsync(replyMessage);
        }

        public static async Task InsertCatNSoupEmoji(InteractionContext ctx, string emojiName)
        {
            if (CatNSoupEmoji.getLink(emojiName, out string link))
                await ctx.CreateResponseAsync(link);
            else 
                await ctx.CreateResponseAsync($"Không tìm thấy emoji {emojiName}!");
        }

    }
}
