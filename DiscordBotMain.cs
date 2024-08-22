using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Reflection;
using CatBot.Admin;
using CatBot.Extension;
using CatBot.Instance;
using CatBot.Music;
using CatBot.Music.Local;
using CatBot.Voice;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CatBot
{
    internal class DiscordBotMain
    {
        internal static DiscordClient? botClient;

        //internal static DiscordRestClient botRESTClient = new DiscordRestClient(new DiscordConfiguration()
        //{
        //    TokenType = TokenType.Bot,
        //    Token = Config.gI().BotToken,
        //    Intents = DiscordIntents.All,
        //    MinimumLogLevel = LogLevel.Information
        //});

        internal static CustomDiscordActivity? activity;

        internal static void Main()
        {
            //ServicePointManager.Expect100Continue = true;
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback((_, _, _, _) => true);
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            try
            {
                new Harmony("Hook").PatchAll();
            }
            catch (Exception ex) { Utils.LogException(ex, false); }
            string configPath = $"{Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName)}_config.json";
            try
            {
                Config.ImportConfig(configPath);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, false);
                if (!File.Exists(configPath))
                    Console.Write("Config file not found, press Enter to create a template config file...");
                else
                    Console.Write("Config file corrupted, press Enter to create a template config file...");
                Console.ReadLine();
                Config.ExportConfig(configPath);
                ProcessStartInfo processStartInfo = new ProcessStartInfo(Path.GetFullPath(configPath))
                {
                    UseShellExecute = true,
                };
                Process.Start(processStartInfo);
                Program.system("pause");
                Environment.Exit(1);
            }
            var botClientBuilder = DiscordClientBuilder.CreateDefault(Config.gI().BotToken, DiscordIntents.All)
                .SetLogLevel(LogLevel.Information)
                .ConfigureEventHandlers(handler =>
                {
                    handler.HandleGuildDownloadCompleted(BotClient_GuildDownloadCompleted);
                    handler.HandleVoiceStateUpdated(BotClient_VoiceStateUpdated);
                    handler.HandleComponentInteractionCreated(BotClient_ComponentInteractionCreated);
                });

            botClientBuilder.UseCommands(cmd =>
            {
                cmd.CommandErrored += (_, args) =>
                {
                    if (args.Exception is CommandNotFoundException)
                        return Task.CompletedTask;
                    return LogException(args.Exception);
                };
                cmd.AddProcessor<SlashCommandProcessor>();
                foreach (MethodInfo method in typeof(VoiceChannelSFXSlashCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                    cmd.AddCommands(CommandBuilder.From(method));
                foreach (MethodInfo method in typeof(MusicPlayerSlashCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                    cmd.AddCommands(CommandBuilder.From(method));
                foreach (MethodInfo method in typeof(GlobalSlashCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                    cmd.AddCommands(CommandBuilder.From(method));
                if (Config.gI().EnableCommandsNext)
                {
                    cmd.AddProcessor(new TextCommandProcessor(new TextCommandConfiguration()
                    {
                        IgnoreBots = false,
                        PrefixResolver = new DefaultPrefixResolver(true, [Config.gI().DefaultPrefix]).ResolvePrefixAsync
                    }));
                    foreach (MethodInfo method in typeof(AdminBaseCommand).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                        cmd.AddCommands(CommandBuilder.From(method));
                    foreach (MethodInfo method in typeof(GlobalBaseCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                        cmd.AddCommands(CommandBuilder.From(method));
                    foreach (MethodInfo method in typeof(MusicPlayerBaseCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                        cmd.AddCommands(CommandBuilder.From(method));
                    foreach (MethodInfo method in typeof(VoiceChannelSFXBaseCommands).GetMethods().Where(m => m.GetCustomAttribute<CommandAttribute>() != null))
                        cmd.AddCommands(CommandBuilder.From(method));
                }
                cmd.AddCommands<AdminSlashCommands>(Config.gI().MainServerID);
            }, new CommandsConfiguration());


            botClientBuilder.UseVoiceNext(new VoiceNextConfiguration());

            botClientBuilder.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(5.0),
            });

            new Thread(GCThread) { IsBackground = true, Name = nameof(GCThread) }.Start();
            new Thread(DeleteTempFile) { IsBackground = true, Name = nameof(DeleteTempFile) }.Start();
            new Thread(UpdateYTDlp) { IsBackground = true, Name = nameof(UpdateYTDlp) }.Start();
            new Thread(LocalMusicChoiceProvider.UpdateCachedLocalMusic) { IsBackground = true, Name = nameof(LocalMusicChoiceProvider.UpdateCachedLocalMusic) }.Start();


            botClient = botClientBuilder.Build();
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task BotClient_ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreatedEventArgs args)
        {
            await BotServerInstance.ComponentInteractionCreated(client, args);
        }

        public static async Task MainAsync()
        {
            if (botClient == null)
                throw new NullReferenceException();
            //await botRESTClient.InitializeAsync();
            await botClient.ConnectAsync();

            await Task.Delay(Timeout.Infinite);
        }

        static void UpdateYTDlp()
        {
            while (true)
            {
                try
                {
                    Process yt_dlp = new Process()
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "yt-dlp\\yt-dlp",
                            Arguments = "-U",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            UseShellExecute = false,
                        },
                        EnableRaisingEvents = true,
                    };
                    Console.WriteLine("--------------yt-dlp Console output--------------");
                    yt_dlp.Start();
                    yt_dlp.WaitForExit();
                    Console.WriteLine("--------------End of yt-dlp Console output--------------");
                }
                catch { }
                Thread.Sleep(1000 * 60 * 60 * 24);
            }
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
                Thread.Sleep(1000 * 60 * 60);
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

        static async Task BotClient_GuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs args)
        {
            if (botClient == null)
                throw new NullReferenceException();
            await Task.Run(() =>
            {
                Config.gI().mainServer = botClient.Guilds.FirstOrDefault(g => g.Key == Config.gI().MainServerID).Value;
                Config.gI().cacheImageChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().CacheImageChannelID) ?? throw new NullReferenceException();
                Config.gI().exceptionReportChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().LogExceptionChannelID) ?? throw new NullReferenceException();
                Config.gI().debugChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().DebugChannelID) ?? throw new NullReferenceException();
            });
            await GlobalSlashCommands.GetMentionStrings();
            new Thread(async () => await ChangeStatus()) { IsBackground = true }.Start();
        }

        static async Task ChangeStatus()
        {
            if (botClient == null)
                throw new NullReferenceException();
            int count = 0;
            while (true)
            {
                if (activity == null)
                {
                    CustomDiscordActivity discordActivity = Config.gI().DefaultPresences[count];
                    count++;
                    if (count >= Config.gI().DefaultPresences.Length)
                        count = 0;
                    try
                    {
                        await botClient.UpdateStatusAsync(discordActivity, DiscordUserStatus.Online);
                    }
                    catch { }
                    for (int i = 0; i < 30; i++)
                    {
                        if (activity != null)
                            break;
                        await Task.Delay(1000);
                    }
                }
                if (activity != null)
                {
                    try
                    {
                        await botClient.UpdateStatusAsync(activity, DiscordUserStatus.Online);
                    }
                    catch { }
                    for (int i = 0; i < 30; i++)
                    {
                        if (activity == null)
                            break;
                        await Task.Delay(1000);
                    }
                }
            }
        }

        static async Task BotClient_VoiceStateUpdated(DiscordClient sender, VoiceStateUpdatedEventArgs args) => await BotServerInstance.OnVoiceStateUpdated(args);

        static Task LogException(Exception ex)
        {
            Utils.LogException(ex);
            return Task.CompletedTask;
        }
    }
}
