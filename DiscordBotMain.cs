﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Admin;
using CatBot.Extension;
using CatBot.Instance;
using CatBot.Music;
using CatBot.Music.Local;
using CatBot.Voice;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace CatBot
{
    internal class DiscordBotMain
    {
        internal static DiscordClient botClient;

        //internal static DiscordRestClient botRESTClient = new DiscordRestClient(new DiscordConfiguration()
        //{
        //    TokenType = TokenType.Bot,
        //    Token = Config.gI().BotToken,
        //    Intents = DiscordIntents.All,
        //    MinimumLogLevel = LogLevel.Information
        //});

        internal static CustomDiscordActivity activity;

        internal static void Main()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(delegate { return true; });
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            string configPath = $"{Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName)}_config.json";
            try
            {
                Config.ImportConfig(configPath);
            }
            catch
            {
                if (!File.Exists(configPath))
                    Console.WriteLine("Config file not found, creating one...");
                else
                    Console.WriteLine("Config file corrupted, creating new one...");
                Config.ExportConfig(configPath);
                Process.Start(Path.GetFullPath(configPath));
                Program.system("pause");
                Environment.Exit(1);
            }
            botClient = new DiscordClient(new DiscordConfiguration()
            {
                TokenType = TokenType.Bot,
                Token = Config.gI().BotToken,
                Intents = DiscordIntents.All,
                MinimumLogLevel = LogLevel.Information,
            });
            botClient.GuildDownloadCompleted += BotClient_GuildDownloadCompleted;
            botClient.VoiceStateUpdated += BotClient_VoiceStateUpdated;
            try
            {
                new Harmony("Hook").PatchAll();
            }
            catch (Exception ex) { Utils.LogException(ex, false); }
            new Thread(GCThread) { IsBackground = true, Name = nameof(GCThread) }.Start();
            new Thread(DeleteTempFile) { IsBackground = true, Name = nameof(DeleteTempFile) }.Start();
            new Thread(UpdateYTDlp) { IsBackground = true, Name = nameof(UpdateYTDlp) }.Start();
            new Thread(LocalMusicChoiceProvider.UpdateCachedLocalMusic) { IsBackground = true, Name = nameof(LocalMusicChoiceProvider.UpdateCachedLocalMusic) }.Start();
            MainAsync().GetAwaiter().GetResult();
        }

        public static async Task MainAsync()
        {
            if (Config.gI().EnableCommandsNext)
            {
                CommandsNextExtension commandNext = botClient.UseCommandsNext(new CommandsNextConfiguration()
                {
                    StringPrefixes = new string[] { Config.gI().DefaultPrefix },
                    CaseSensitive = true,
                    EnableDms = false,
                    UseDefaultCommandHandler = false,
                });
                botClient.MessageCreated += commandNext.HandleCommandsAsync;
                commandNext.CommandErrored += (_, args) => 
                {
                    if (args.Exception is CommandNotFoundException)
                        return Task.CompletedTask;
                    return LogException(args.Exception);
                };
                commandNext.RegisterCommands<AdminBaseCommand>();
                commandNext.RegisterCommands<GlobalBaseCommands>();
                commandNext.RegisterCommands<MusicPlayerBaseCommands>();
                commandNext.RegisterCommands<VoiceChannelSFXBaseCommands>();
                //commandNext.RegisterCommands<TTSBaseCommands>();
                //commandNext.RegisterCommands<EmojiReplyBaseCommands>();
                commandNext.SetHelpFormatter<HelpFormatter>();
            }
            SlashCommandsExtension slashCommand = botClient.UseSlashCommands(new SlashCommandsConfiguration());
            slashCommand.SlashCommandErrored += (_, args) =>
            {
                if (args.Exception is CommandNotFoundException)
                    return Task.CompletedTask;
                return LogException(args.Exception);
            };
            slashCommand.RegisterCommands<VoiceChannelSFXSlashCommands>();
            slashCommand.RegisterCommands<MusicPlayerSlashCommands>();
            slashCommand.RegisterCommands<GlobalSlashCommands>();
            slashCommand.RegisterCommands<AdminSlashCommands>(Config.gI().MainServerID);
            //slashCommand.RegisterCommands<EmojiReplySlashCommands>();
            //slashCommand.RegisterCommands<TTSSlashCommands>();

            botClient.UseVoiceNext(new VoiceNextConfiguration());

            botClient.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(5.0),
            });

            //await botRESTClient.InitializeAsync();
            await botClient.ConnectAsync();

            await Task.Delay(Timeout.Infinite);
        }

        static void UpdateYTDlp()
        {
            while (true)
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
            await Task.Run(() =>
            {
                Config.gI().mainServer = botClient.Guilds.FirstOrDefault(g => g.Key == Config.gI().MainServerID).Value;
                Config.gI().cacheImageChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().CacheImageChannelID);
                Config.gI().exceptionReportChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().LogExceptionChannelID);
                Config.gI().debugChannel = Config.gI().mainServer.Channels.Values.FirstOrDefault(ch => ch.Id == Config.gI().DebugChannelID);
            });
            await GlobalSlashCommands.GetMentionStrings();
            new Thread(async() => await ChangeStatus()) { IsBackground = true }.Start();
        }

        static async Task ChangeStatus()
        {
            int count = 0;
            while (true)
            {
                if (activity == null)
                {
                    CustomDiscordActivity discordActivity = new CustomDiscordActivity();
                    if (count == 0)
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.ListeningTo, "Zing MP3");
                    else if (count == 1)
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.ListeningTo, "NhacCuaTui");
                    else if (count == 2)
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.ListeningTo, "YouTube Music");
                    else if (count == 3)
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.ListeningTo, "SoundCloud");
                    else if (count == 4)
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.ListeningTo, "Spotify");
                    else if (count == 5)
                    {
                        discordActivity = new CustomDiscordActivity(botClient.CurrentApplication.Id, ActivityType.Watching, "YouTube");
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

        static async Task BotClient_VoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs args) => await BotServerInstance.OnVoiceStateUpdated(args);

        static Task LogException(Exception ex)
        {
            Utils.LogException(ex);
            return Task.CompletedTask;
        }
    }
}
