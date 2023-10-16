using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CatBot.Emoji
{
    public class EmojiReplyCore
    {
        internal static async Task onMessageReceived(DiscordMessage message)
        {
            //bool botAuthor = false;
            //if (Config.botAuthorsID.Contains(message.Author.Id))
            //    botAuthor = true;
            //if (!botAuthor)
            //    return;
            //if (message.Content.ToLower().Contains("khiêm lợn"))
            //    await message.RespondAsync("<:khiem_1:1116025526063210567>" + DiscordEmoji.FromName(DiscordBotMain.botClient, ":pig:", true));
            //if (message.Content.ToLower().Contains("lợn khiêm"))
            //    await message.RespondAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":pig:", true) + "<:khiem_1:1116025526063210567>");
            //if (message.Content.ToLower().Contains("khiêm ăn cứt"))
            //    await message.RespondAsync("<:khiem_1:1116025526063210567>" + DiscordEmoji.FromName(DiscordBotMain.botClient, ":yum:", true) + DiscordEmoji.FromName(DiscordBotMain.botClient, ":poop:", true)); 
            //if (message.Content.ToLower().Contains("cứt ăn khiêm"))
            //    await message.RespondAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":poop:", true) + DiscordEmoji.FromName(DiscordBotMain.botClient, ":yum:", true) + "<:khiem_1:1116025526063210567>");
            //if (message.Content.ToLower().Contains("khiêm gà"))
            //    await message.RespondAsync("<:khiem_1:1116025526063210567>" + DiscordEmoji.FromName(DiscordBotMain.botClient, ":chicken:", true));
            //if (message.Content.ToLower().Contains("khiêm sẽ gầy") || message.Content.Contains("khiem gay") || message.Content.Contains("khiêm gay"))
            //    await message.RespondAsync("<:khiem_1:1116025526063210567>" + DiscordEmoji.FromName(DiscordBotMain.botClient, ":rainbow_flag:", true));
        }

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
                            if (server == Config.adminServer && !Config.adminServer.Members.Keys.Contains(obj.TryGetUser().Id))
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
