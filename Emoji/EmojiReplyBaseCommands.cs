using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Emoji
{
    public class EmojiReplyBaseCommands : BaseCommandModule
    {
        [Command("emoji"), Aliases("e"), Description("Trả lời tin nhắn của bạn bằng emoji từ các server có bot và emoji CatAndSoup")]
        public async Task ReplyWithEmoji(CommandContext ctx, [Description("Tên emoji")] params string[] emoteNames) => await EmojiReplyCore.ReplyWithEmoji(ctx.Message, emoteNames);
    }
}
