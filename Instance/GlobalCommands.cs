#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.ComponentModel;
using CatBot.Admin;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;

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
        static List<string> slashCommandMentions = new List<string>();

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
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle(DiscordBotMain.botClient.CurrentUser.Username + "#" + DiscordBotMain.botClient.CurrentUser.Discriminator).WithDescription(
                $"### Made with {DiscordEmoji.FromName(DiscordBotMain.botClient, ":hearts:")} by <@!650357286526648350>\r\n" +
                "Chức năng:\r\n" +
                $"- Phát SFX (lệnh {speakFile} | {prefix}s)\r\n" +
                $"- Phát nhạc từ các nền tảng âm nhạc online (lệnh {play} | {nextup}):\r\n" +
                $"  - {Formatter.MaskedUrl("YouTube", new Uri("https://www.youtube.com/"))} và {Formatter.MaskedUrl("YouTube Music", new Uri("https://music.youtube.com/"))} (cung cấp bởi {Formatter.MaskedUrl("yt-dlp", new Uri("https://github.com/yt-dlp/yt-dlp"))})\r\n" +
                $"  - {Formatter.MaskedUrl("NhacCuaTui", new Uri("https://www.nhaccuatui.com/"))}\r\n" +
                $"  - {Formatter.MaskedUrl("Zing MP3", new Uri("https://zingmp3.vn/"))}\r\n" +
                $"  - {Formatter.MaskedUrl("SoundCloud", new Uri("https://soundcloud.com/"))} (cung cấp bởi {Formatter.MaskedUrl("SoundCloudExplode", new Uri("https://github.com/jerry08/SoundCloudExplode"))})\r\n" +
                $"  - {Formatter.MaskedUrl("Spotify", new Uri("https://spotify.com/"))} (cung cấp bởi {Formatter.MaskedUrl("SpotifyExplode", new Uri("https://github.com/jerry08/SpotifyExplode"))} và {Formatter.MaskedUrl("SpotifyDown", new Uri("https://spotifydown.com/"))})\r\n" +
                $"- Phát nhạc offline có sẵn trên máy\r\n" +
                $"Sử dụng lệnh {help} | {prefix}help để xem danh sách lệnh.\r\n" +
                Formatter.Bold($"Bot bị lỗi? Dùng lệnh {reset} nếu bạn có quyền quản trị viên hoặc liên hệ <@!650357286526648350>!")
                ).WithFooter("Source code của bot: https://github.com/ElectroHeavenVN/CatBot", "https://github.githubassets.com/favicons/favicon-dark.png");
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