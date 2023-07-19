using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Diagnostics;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Newtonsoft.Json.Linq;
using DSharpPlus;
using DiscordBot.Instance;
using DiscordBot.Music;
using DSharpPlus.SlashCommands;

namespace DiscordBot.Voice
{
    internal class VoiceChannelSFXCore
    {
        internal bool isStop;
        internal int delay = 500;

        internal static async Task Speak(SnowflakeObject message, params string[] fileNames)
        {
            if (fileNames.Length == 1)
            {
                if (fileNames[0] == "dict")
                {
                    await Dictionary(message);
                    return;
                }
                else if (fileNames[0] == "stop")
                {
                    await StopSpeaking(message);
                    return;
                } 
                else if (fileNames[0] == "leave" || fileNames[0] == "disconnect")
                {
                    await Disconnect(message);
                    return;
                } 
                else if (fileNames[0] == "reconnect")
                {
                    await Reconnect(message);
                    return;
                }
            }
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(message.TryGetChannel().Guild);
            serverInstance.lastChannel = message.TryGetChannel();
            if (serverInstance.voiceChannelSFX == null)
                return;
            serverInstance.voiceChannelSFX.isStop = false;
            new Thread(async () =>
            {
                await serverInstance.voiceChannelSFX.InternalSpeak(message, fileNames);
            }) { IsBackground = true }.Start();
        }

        internal static async Task Reconnect(SnowflakeObject messageToReact)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            serverInstance.lastChannel = messageToReact.TryGetChannel();
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalReconnect(messageToReact);
        }

        internal static async Task StopSpeaking(SnowflakeObject messageToReact)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            serverInstance.lastChannel = messageToReact.TryGetChannel();
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalStopSpeaking(messageToReact);
        }

        internal static async Task Dictionary(SnowflakeObject messageToReply)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReply.TryGetChannel().Guild);
            serverInstance.lastChannel = messageToReply.TryGetChannel();
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            embed.Title = "Danh sách file";
            FileInfo[] sfxs = new DirectoryInfo(Config.SFXFolder).GetFiles();
            FileInfo[] sfxSpecials = new DirectoryInfo(Config.SFXFolderSpecial).GetFiles();
            embed.Title += " (tổng số file: " + (sfxs.Length + sfxSpecials.Length) + ")";
            embed.Description += "**Các file thông thường (có thể được mọi người sử dụng):**" + Environment.NewLine;
            for (int i = 0; i < sfxs.Length; i++)
                embed.Description += Path.GetFileNameWithoutExtension(sfxs[i].Name) + ", ";
            embed.Description = embed.Description.Trim(',', ' ');
            if (((DiscordMember)messageToReply.TryGetUser()).isInAdminServer())
            {
                DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder();
                embed2.Description = "**Các file đặc biệt (chỉ có người có quyền mới được sử dụng):**" + Environment.NewLine;
                for (int i = 0; i < sfxSpecials.Length; i++)
                    embed2.Description += Path.GetFileNameWithoutExtension(sfxSpecials[i].Name) + ", ";
                embed2.Description = embed2.Description.Trim(',', ' ') + Environment.NewLine + Environment.NewLine + "Dùng lệnh " + Config.Prefix + "s <tên file> để bot nói!";
                if (messageToReply is DiscordInteraction interaction)
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                else if (messageToReply is DiscordMessage message) 
                    await message.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
            }
            else
            {
                embed.Description += Environment.NewLine + Environment.NewLine + "Dùng lệnh " + Config.Prefix + "s <tên file> để bot nói!";
                await messageToReply.TryRespondAsync(embed.Build());
            }
        }

        internal static async Task Disconnect(SnowflakeObject messageToReact)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            serverInstance.lastChannel = messageToReact.TryGetChannel();
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalDisconnect(messageToReact);
        }

        internal static async Task Delay(SnowflakeObject messageToReact, int delayValue)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            serverInstance.lastChannel = messageToReact.TryGetChannel();
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalDelay(messageToReact, delayValue);
        }

        internal static async Task SetVolume(SnowflakeObject obj, long volume)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(obj.TryGetChannel().Guild);
            if (!await serverInstance.InitializeVoiceNext(obj))
                return;
            if (volume < 0 || volume > 250)
            {
                await obj.TryRespondAsync("Âm lượng không hợp lệ!");
                return;
            }
            serverInstance.currentVoiceNextConnection.SetVolume(volume / 100d);
            await obj.TryRespondAsync("Điều chỉnh âm lượng thành: " + volume + "%!");
        }
      
        async Task InternalSpeak(SnowflakeObject message, string[] fileNames)
        {
            if (BotServerInstance.GetBotServerInstance(this).isVoicePlaying)
            {
                await message.TryRespondAsync("Có người đang dùng lệnh rồi!");
                return;
            }
            if (message is DiscordInteraction interaction)
                await interaction.DeferAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            if (!await serverInstance.InitializeVoiceNext(message))
                return;
            MusicPlayerCore musicPlayer = BotServerInstance.GetMusicPlayer(message.TryGetChannel().Guild);
            bool isPaused = musicPlayer.isPaused;
            musicPlayer.isPaused = true;
            VoiceTransmitSink transmitSink = serverInstance.currentVoiceNextConnection.GetTransmitSink();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = true;
            string filesNotFound = "";
            string permissions = "";
            byte[] buffer = new byte[transmitSink.SampleLength];
            string previousFileName = "";
            for (int i = 0; i < fileNames.Length; i++)
            {
                string fileName = fileNames[i];
                uint repeatTimes = 0;
                if (fileName.ToLower().StartsWith("x") && uint.TryParse(fileName.Remove(0, 1), out repeatTimes))
                    fileName = previousFileName;
                else
                    previousFileName = fileName;
                if (repeatTimes == 0)
                    repeatTimes = 2;
                for (int j = 0; j < repeatTimes - 1; j++)
                {
                    try
                    {
                        FileStream file = File.OpenRead(Path.Combine(Config.SFXFolder, fileName.TrimEnd(',') + ".pcm"));
                        while (file.Read(buffer, 0, buffer.Length) != 0)
                        {
                            if (isStop)
                                break;
                            await transmitSink.WriteAsync(new ReadOnlyMemory<byte>(buffer));
                        }
                        file.Close();
                        if (isStop)
                            break;
                    }
                    catch (FileNotFoundException ex)
                    {
                        if (message.TryGetUser() is DiscordMember member && member.isInAdminServer())
                        {
                            try
                            {
                                FileStream file = File.OpenRead(Path.Combine(Config.SFXFolderSpecial, fileName.TrimEnd(',') + ".pcm"));
                                while (file.Read(buffer, 0, buffer.Length) != 0)
                                {
                                    if (isStop)
                                        break;
                                    await transmitSink.WriteAsync(new ReadOnlyMemory<byte>(buffer));
                                }
                                file.Close();
                                if (isStop)
                                    break;
                            }
                            catch (FileNotFoundException ex2)
                            {
                                filesNotFound += Path.GetFileNameWithoutExtension(ex2.FileName) + ", ";
                            }
                        }
                        else if (File.Exists(Path.Combine(Config.SFXFolderSpecial, fileName.TrimEnd(',') + ".pcm")))
                            permissions += fileName.TrimEnd(',') + ", ";
                        else
                            filesNotFound += Path.GetFileNameWithoutExtension(ex.FileName) + ", ";
                    }
                    await Task.Delay(delay);
                }
                if (isStop)
                {
                    isStop = false;
                    break;
                }
            }
            musicPlayer.isPaused = isPaused;
            filesNotFound = filesNotFound.TrimEnd(',', ' ');
            permissions = permissions.TrimEnd(',', ' ');
            string response = "";
            if (!string.IsNullOrWhiteSpace(filesNotFound))
                response += "Không tìm thấy file " + filesNotFound + "!";
            if (!string.IsNullOrWhiteSpace(permissions))
                response += Environment.NewLine + "Bạn không có quyền sử dụng file " + permissions + "!";
            if (!string.IsNullOrEmpty(response))
            {
                if (message is DiscordInteraction interaction2)
                    await interaction2.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent(response));
                else
                    await message.TryRespondAsync(response);
            }
            else if (message is DiscordInteraction interaction2)
                await interaction2.DeleteOriginalResponseAsync();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
        }
        
        async Task InternalDisconnect(SnowflakeObject messageToReact)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            try
            {
                if (!await serverInstance.InitializeVoiceNext(messageToReact))
                    return;
                for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                    serverInstance.musicPlayer.musicQueue.ElementAt(i).Dispose();
                serverInstance.musicPlayer.musicQueue.Clear();
                serverInstance.isDisconnect = true;
                serverInstance.musicPlayer.isThreadAlive = false;
                serverInstance.musicPlayer.sentOutOfTrack = true;
                serverInstance.musicPlayer.cts.Cancel();
                serverInstance.musicPlayer.cts = new CancellationTokenSource();
            }
            catch (Exception ex) { Utils.LogException(ex); }
            if (!await serverInstance.InitializeVoiceNext(messageToReact))
                return;
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            isStop = true;
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
            serverInstance.currentVoiceNextConnection.Disconnect();
            if (messageToReact is DiscordMessage message)
                await message.CreateReactionAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:"));
            else if (messageToReact is DiscordInteraction interaction)
                await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:")));
            await Task.Delay(1000);
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        async Task InternalStopSpeaking(SnowflakeObject messageToReact)
        {
            isStop = true;
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
            if (messageToReact is DiscordMessage message)
                await message.CreateReactionAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:"));
            else if (messageToReact is DiscordInteraction interaction)
                await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:")));
        }

        async Task InternalReconnect(SnowflakeObject messageToReact)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReact.TryGetChannel().Guild);
            if (!await serverInstance.InitializeVoiceNext(messageToReact))
                return;
            isStop = true;
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            bool isPaused = serverInstance.musicPlayer.isPaused;
            serverInstance.musicPlayer.isPaused = true;
            await Task.Delay(600);
            serverInstance.currentVoiceNextConnection.Disconnect();
            serverInstance.currentVoiceNextConnection?.Dispose();
            if (!await serverInstance.InitializeVoiceNext(messageToReact))
                return;
            serverInstance.musicPlayer.isPaused = isPaused;
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;

            isStop = false;
            if (messageToReact is DiscordMessage message)
                await message.CreateReactionAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:"));
            else if (messageToReact is DiscordInteraction interaction)
                await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:")));
        }

        async Task InternalDelay(SnowflakeObject messageToReact, int delayValue)
        {
            if (delayValue < 0 || delayValue > 5000)
                await messageToReact.TryRespondAsync($"Delay không hợp lệ!");
            else
            {
                delay = delayValue;
                await messageToReact.TryRespondAsync($"Đã thay đổi delay thành {delay} mili giây!");
            }
        }
    }
}
