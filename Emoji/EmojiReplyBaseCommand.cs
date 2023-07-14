using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Emoji
{
    public class EmojiReplyBaseCommand : BaseCommandModule
    {
        [Command("e")]
        public async Task ReplyWithEmoji(CommandContext ctx, params string[] emoteNames) => await EmojiReplyCore.ReplyWithEmoji(ctx.Message, emoteNames);
    }
}
