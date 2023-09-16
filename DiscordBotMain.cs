using DiscordBot.Admin;
using DiscordBot.Emoji;
using DiscordBot.Instance;
using DiscordBot.Music;
using DiscordBot.Voice;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot
{
    internal class DiscordBotMain
    {
        internal static DiscordClient botClient = new DiscordClient(new DiscordConfiguration()
        {
            TokenType = TokenType.Bot,
            Token = Config.BotToken,
            Intents = DiscordIntents.All,
            MinimumLogLevel = LogLevel.Information,
        });

        internal static DiscordRestClient restClient = new DiscordRestClient(new DiscordConfiguration()
        {
            TokenType = TokenType.Bot,
            Token = Config.BotToken,
            Intents = DiscordIntents.All,
            MinimumLogLevel = LogLevel.Information
        });

        internal static DiscordActivity activity;

        static DiscordBotMain()
        {
            botClient.MessageCreated += BotClient_MessageCreated; 
            botClient.GuildDownloadCompleted += BotClient_GuildDownloadCompleted;
            botClient.VoiceStateUpdated += BotClient_VoiceStateUpdated;
        }

        internal static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Console.OutputEncoding = Encoding.Unicode;
            try
            {
                new Harmony("patchDeafen").PatchAll();
            }
            catch (Exception ex) { Utils.LogException(ex); }
            new Thread(GCThread) { IsBackground = true, Name = nameof(GCThread) }.Start();
            new Thread(DeleteTempFile) { IsBackground = true, Name = nameof(DeleteTempFile) }.Start();
            new Thread(UpdateYTdlp) { IsBackground = true, Name = nameof(UpdateYTdlp) }.Start();
            MainAsync().GetAwaiter().GetResult();
        }

        private static void UpdateYTdlp()
        {
            while (true)
            {
                Process yt_dlp_x86 = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "yt-dlp\\yt-dlp_x86",
                        Arguments = "-U",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                    },
                    EnableRaisingEvents = true,
                };
                Console.WriteLine("--------------yt-dlp Console output--------------");
                yt_dlp_x86.Start();
                yt_dlp_x86.WaitForExit();
                Console.WriteLine("--------------End of yt-dlp Console output--------------");
                Thread.Sleep(1000 * 60 * 60 * 24);
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject);
            Console.ReadLine();
        }

        static void GCThread()
        {
            while (true)
            {
                Thread.Sleep(15000);
                GC.Collect();
            }
        }

        static void DeleteTempFile()
        {
            while (true)
            {
                Thread.Sleep(15000);
                List<string> allFilesInUse = Utils.GetAllFilesInUse();
                DirectoryInfo temp = new DirectoryInfo(Environment.ExpandEnvironmentVariables("%temp%"));
                foreach (FileInfo file in temp.GetFiles().Where(f => f.Name.StartsWith("tmp") && (f.Extension == ".tmp" || f.Extension == ".webm")))
                {
                    try
                    {
                        if (!allFilesInUse.Contains(file.FullName))
                            file.Delete();
                    }
                    catch (Exception) { }
                }
            }
        }

        public static async Task MainAsync()
        {
            //DiscordRestClient restClient = new DiscordRestClient(new DiscordConfiguration()
            //{
            //    TokenType = TokenType.Bot,
            //    Token = Config.BotToken,
            //    Intents = DiscordIntents.All,
            //    MinimumLogLevel = LogLevel.Information
            //});
            //await restClient.InitializeAsync();
            //await restClient.DeleteGuildApplicationCommandAsync(1115634791321190420, 1123285178375217183);
            //return;

            if (Config.EnableCommandsNext)
            {
                CommandsNextExtension commandNext = botClient.UseCommandsNext(new CommandsNextConfiguration()
                {
                    StringPrefixes = new string[] { Config.Prefix },
                });
                commandNext.RegisterCommands(typeof(DiscordBotMain).Assembly);
                commandNext.SetHelpFormatter<HelpFormatter>();
            }
            SlashCommandsExtension slashCommand = botClient.UseSlashCommands(new SlashCommandsConfiguration());

            slashCommand.RegisterCommands<VoiceChannelSFXSlashCommands>();
            slashCommand.RegisterCommands<EmojiReplySlashCommands>();
            slashCommand.RegisterCommands<TTSSlashCommands>();
            slashCommand.RegisterCommands<MusicPlayerSlashCommands>();
            slashCommand.RegisterCommands<GlobalSlashCommands>();
            slashCommand.RegisterCommands<AdminSlashCommands>(Config.MainServerID);

            botClient.UseVoiceNext();

            await restClient.InitializeAsync();
            await botClient.ConnectAsync(new DiscordActivity(), UserStatus.Online);
            await Task.Delay(Timeout.Infinite);
        }

        private static async Task BotClient_GuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
        {
            await Task.Run(() =>
            {
                Config.mainServer = botClient.Guilds.First(g => g.Key == Config.MainServerID).Value;
                Config.adminServer = botClient.Guilds.First(g => g.Key == Config.AdminServerID).Value;
                Config.cacheImageChannel = Config.mainServer.Channels.Values.First(ch => ch.Id == Config.CacheImageChannelID);
                Config.exceptionReportChannel = Config.mainServer.Channels.Values.First(ch => ch.Id == Config.ExceptionReportChannelID);
            });
            new Thread(async() => await ChangeStatus()) { IsBackground = true }.Start();
        }

        private static async Task ChangeStatus()
        {
            int count = 0;
            while (true)
            {
                if (activity == null)
                {
                    DiscordActivity discordActivity = new DiscordActivity();
                    if (count == 0)
                        discordActivity = new DiscordActivity("Zing MP3", ActivityType.ListeningTo);
                    else if (count == 1)
                        discordActivity = new DiscordActivity("NhacCuaTui", ActivityType.ListeningTo);
                    else if (count == 2)
                        discordActivity = new DiscordActivity("YouTube Music", ActivityType.ListeningTo);
                    else if (count == 3)
                        discordActivity = new DiscordActivity("SoundCloud", ActivityType.ListeningTo);
                    else if (count == 4)
                        discordActivity = new DiscordActivity("Spotify", ActivityType.ListeningTo);
                    else if (count == 5)
                    {
                        discordActivity = new DiscordActivity("YouTube", ActivityType.Watching);
                        count = -1;
                    }
                    count++;
                    await botClient.UpdateStatusAsync(discordActivity, UserStatus.Online);
                    for (int i = 0; i < 30; i++)
                    {
                        if (activity != null)
                            break;
                        await Task.Delay(1000);
                    }
                }
                if (activity != null)
                {
                    await botClient.UpdateStatusAsync(activity, UserStatus.Online);
                    for (int i = 0; i < 30; i++)
                    {
                        if (activity == null)
                            break;
                        await Task.Delay(1000);
                    }
                }
            }
        }

        private static async Task BotClient_MessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Message.Author.Id == botClient.CurrentUser.Id)
                return;
            await EmojiReplyCore.onMessageReceived(args.Message);
        }

        private static async Task BotClient_VoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs args) => new Thread(async () => await BotServerInstance.OnVoiceStateUpdated(args)) { IsBackground = true }.Start();
    }
}
