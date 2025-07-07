#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using CatBot.Admin;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace CatBot.Instance
{
    public class GlobalCommands
    {
        static string speakFile;
        static string nextup;
        static string play;
        static string play_local;
        static string play_local_all;
        static string nextup_local;
        static string help;
        static string reset;
        static List<string> slashCommandMentions = [];

        [Command("volume"), TextAlias("vol"), Description("Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(CommandContext ctx, [Parameter("volume"), Description("Âm lượng"), MinMaxValue((long)-1, (long)250)] long volume = -1) => await BotServerInstance.SetVolume(ctx, volume);

        [Command("reset"), Description("Đặt lại bot (dùng trong trường hợp bot bị lỗi)")]
        public async Task ResetBotServerInstance(CommandContext ctx) => await AdminCommandsCore.ResetBotServerInstance(ctx, "this");

        [Command("help"), Description("Xem trợ giúp về lệnh slash")]
        public async Task Help(CommandContext ctx)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Danh sách lệnh slash");
            embed.Description = string.Join(", ", slashCommandMentions) + Environment.NewLine;
            embed.Description += "Ấn vào từng lệnh để xem chi tiết.";
            await ctx.RespondAsync(embed.Build());
        }

        [Command("about"), Description("Xem thông tin về bot")]
        public async Task About(CommandContext ctx)
        {
            string prefix = Config.gI().DefaultPrefix;
            DiscordClient botClient = DiscordBotMain.botClient;
            string mention = DiscordBotMain.botClient.CurrentApplication.Owners?.FirstOrDefault()?.Mention ?? "<@!650357286526648350>";
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                .WithTitle(botClient.CurrentUser.Username + "#" + botClient.CurrentUser.Discriminator)
                .WithDescription($"""
                Made with :hearts: by {mention}
                Chức năng:
                - Phát SFX (lệnh {speakFile} | {prefix}s)
                - Phát nhạc từ các nền tảng âm nhạc online (lệnh {play} | {nextup}):
                  - [NhacCuaTui](https://www.nhaccuatui.com/)
                  - [Zing MP3](https://zingmp3.vn/)
                  - [SoundCloud](https://soundcloud.com/) (cung cấp bởi [SoundCloudExplode](https://github.com/jerry08/SoundCloudExplode))
                  - [Spotify](https://spotify.com/) (cung cấp bởi [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET))
                - Phát nhạc offline có sẵn trên máy (lệnh {play_local} | {play_local_all} | {nextup_local})
                Sử dụng lệnh {help} | {prefix}help để xem danh sách lệnh.
                **Bot bị lỗi? Dùng lệnh {reset} nếu bạn có quyền quản trị viên hoặc liên hệ {mention} để được hỗ trợ!**
                """)
                .WithFooter("Source code của bot: https://github.com/ElectroHeavenVN/CatBot", "https://github.githubassets.com/favicons/favicon-dark.png");
            await ctx.RespondAsync(embed.Build());
        }

        internal static async Task GetMentionStrings()
        {
            slashCommandMentions = (await DiscordBotMain.botClient.GetGlobalApplicationCommandsAsync()).Select(c => c.Mention).ToList();
            speakFile = slashCommandMentions.First(s => s.Contains("</speak_file:"));
            play = slashCommandMentions.First(s => s.Contains("</play:"));
            nextup = slashCommandMentions.First(s => s.Contains("</nextup:"));
            play_local = slashCommandMentions.First(s => s.Contains("</play_local:"));
            play_local_all = slashCommandMentions.First(s => s.Contains("</play_local_all:"));
            nextup_local = slashCommandMentions.First(s => s.Contains("</nextup_local:"));
            help = slashCommandMentions.First(s => s.Contains("</help:"));
            reset = slashCommandMentions.First(s => s.Contains("</reset:"));
        }
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.