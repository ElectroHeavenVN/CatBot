using System.ComponentModel;
using CatBot.Admin;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;

namespace CatBot.Instance
{
    public class GlobalSlashCommands
    {
        static string speakFile;
        //static string speak;
        static string nextup;
        static string play;
        static string youtube;
        static string nextup_yt;
        static string nhaccuatui;
        static string nextup_nct;
        static string nextup_zing;
        static string zingmp3;
        static string nextup_sc;
        static string soundcloud;
        static string nextup_sp;
        static string spotify;
        static string play_local;
        static string play_local_all;
        static string nextup_local;
        static string help;
        static string reset;
        static List<string> slashCommandMentions = new List<string>();

        [Command("volume"), Description("Xem hoặc chỉnh âm lượng tổng của bot")]
        public async Task SetVolume(SlashCommandContext ctx, [Parameter("volume"), Description("Âm lượng"), MinMaxValue(0, 250)] long volume = -1) => await BotServerInstance.SetVolume(ctx.Interaction, volume);

        [Command("reset"), Description("Đặt lại bot (dùng trong trường hợp bot bị lỗi)")]
        public async Task ResetBotServerInstance(SlashCommandContext ctx) => await AdminCommandsCore.ResetBotServerInstance(ctx, "this");

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
                //$"- Text to speech (cung cấp bởi {Formatter.MaskedUrl("Zalo AI", new Uri("https://zalo.ai/products/text-to-audio-converter"))}) (lệnh {speak} | {prefix}tts)\r\n" +
                $"- Phát nhạc từ các nền tảng âm nhạc online (lệnh {play} | {nextup}):\r\n" +
                $"  - {Formatter.MaskedUrl("YouTube", new Uri("https://www.youtube.com/"))} và {Formatter.MaskedUrl("YouTube Music", new Uri("https://music.youtube.com/"))} (cung cấp bởi {Formatter.MaskedUrl("yt-dlp", new Uri("https://github.com/yt-dlp/yt-dlp"))}) (lệnh {youtube} | {nextup_yt})\r\n" +
                $"  - {Formatter.MaskedUrl("NhacCuaTui", new Uri("https://www.nhaccuatui.com/"))} (lệnh {nhaccuatui} | {nextup_nct})\r\n" +
                $"  - {Formatter.MaskedUrl("Zing MP3", new Uri("https://zingmp3.vn/"))} (lệnh {zingmp3} | {nextup_zing})\r\n" +
                $"  - {Formatter.MaskedUrl("SoundCloud", new Uri("https://soundcloud.com/"))} (cung cấp bởi {Formatter.MaskedUrl("SoundCloudExplode", new Uri("https://github.com/jerry08/SoundCloudExplode"))}) (lệnh {soundcloud} | {nextup_sc})\r\n" +
                $"  - {Formatter.MaskedUrl("Spotify", new Uri("https://spotify.com/"))} (cung cấp bởi {Formatter.MaskedUrl("SpotifyExplode", new Uri("https://github.com/jerry08/SpotifyExplode"))} và {Formatter.MaskedUrl("SpotifyDown", new Uri("https://spotifydown.com/"))}) (lệnh {spotify} | {nextup_sp})\r\n" +
                $"- Phát nhạc lưu trong bộ nhớ (lệnh {play_local} | {play_local_all} | {nextup_local})\r\n" +
                $"Sử dụng lệnh {help} | {prefix}help để xem danh sách lệnh.\r\n" +
                Formatter.Bold($"Bot bị lỗi? Dùng lệnh {reset} nếu bạn có quyền quản trị viên hoặc liên hệ <@!650357286526648350>!")
                ).WithFooter("Source code của bot: https://github.com/ElectroHeavenVN/CatBot", "https://github.githubassets.com/favicons/favicon-dark.png");
            await ctx.RespondAsync(embed.Build());
        }

        internal static async Task GetMentionStrings()
        {
            slashCommandMentions = (await DiscordBotMain.botClient.GetGlobalApplicationCommandsAsync()).Select(c => c.Mention).ToList();
            speakFile = slashCommandMentions.First(s => s.Contains("</speak-file:"));
            //speak = slashCommandMentions.First(s => s.Contains("</speak:"));
            play = slashCommandMentions.First(s => s.Contains("</play:"));
            nextup = slashCommandMentions.First(s => s.Contains("</nextup:"));
            youtube = slashCommandMentions.First(s => s.Contains("</youtube:"));
            nextup_yt = slashCommandMentions.First(s => s.Contains("</nextup-yt:"));
            nhaccuatui = slashCommandMentions.First(s => s.Contains("</nhaccuatui:"));
            nextup_nct = slashCommandMentions.First(s => s.Contains("</nextup-nct:"));
            zingmp3 = slashCommandMentions.First(s => s.Contains("</zingmp3:"));
            nextup_zing = slashCommandMentions.First(s => s.Contains("</nextup-zing:"));
            soundcloud = slashCommandMentions.First(s => s.Contains("</soundcloud:"));
            nextup_sc = slashCommandMentions.First(s => s.Contains("</nextup-sc:"));
            spotify = slashCommandMentions.First(s => s.Contains("</spotify:"));
            nextup_sp = slashCommandMentions.First(s => s.Contains("</nextup-sp:"));
            play_local = slashCommandMentions.First(s => s.Contains("</play-local:"));
            play_local_all = slashCommandMentions.First(s => s.Contains("</play-local-all:"));
            nextup_local = slashCommandMentions.First(s => s.Contains("</nextup-local:"));
            help = slashCommandMentions.First(s => s.Contains("</help:"));
            reset = slashCommandMentions.First(s => s.Contains("</reset:"));
        }
    }
}
