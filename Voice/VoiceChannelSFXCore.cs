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
using CatBot.Instance;
using CatBot.Music;
using DSharpPlus.SlashCommands;

namespace CatBot.Voice
{
    internal class VoiceChannelSFXCore
    {
        internal bool isStop;
        internal int delay = 500;
        internal double volume = 1;

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
            await serverInstance.voiceChannelSFX.InternalSpeak(message, fileNames);
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
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(messageToReply.TryGetChannel().Guild);
                serverInstance.lastChannel = messageToReply.TryGetChannel();
                FileInfo[] sfxs = new DirectoryInfo(Config.gI().SFXFolder).GetFiles();
                List<DiscordEmbedBuilder> embeds = new List<DiscordEmbedBuilder> { new DiscordEmbedBuilder() };
                string totalSize = Utils.GetMemorySize((ulong)sfxs.Select(f => f.Length).Sum());
                embeds[0].Title = $"Danh sách file ({sfxs.Length} file, {totalSize})";
                for (int i = 0; i < sfxs.Length; i++)
                {
                    string description = embeds.Last().Description + Path.GetFileNameWithoutExtension(sfxs[i].Name) + ", ";
                    if (description.Length > 2048 - 38 + Config.gI().DefaultPrefix.Length)
                    {
                        embeds.Last().Description = embeds.Last().Description.Trim(',', ' ');
                        embeds.Add(new DiscordEmbedBuilder());
                        description = Path.GetFileNameWithoutExtension(sfxs[i].Name) + ", ";
                    }
                    embeds.Last().Description = description;
                }
                embeds.Last().Description = embeds.Last().Description.Trim(',', ' ');
                if (!((DiscordMember)messageToReply.TryGetUser()).isInAdminServer())
                    embeds.Last().Description += Environment.NewLine + Environment.NewLine + "Dùng lệnh " + Config.gI().DefaultPrefix + "s <tên file> để bot nói!";
                if (messageToReply is DiscordMessage message)
                    await message.RespondAsync(new DiscordMessageBuilder().AddEmbed(embeds[0].Build()));
                else if (messageToReply is DiscordInteraction interaction)
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embeds[0].Build()));
                foreach (DiscordEmbedBuilder embed in embeds.Skip(1))
                {
                    await Task.Delay(200);
                    await serverInstance.lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()));
                }
                if (((DiscordMember)messageToReply.TryGetUser()).isInAdminServer())
                {
                    List<FileInfo> sfxSpecials = new DirectoryInfo(Config.gI().SFXFolderSpecial).GetFiles().ToList();
                    List<DiscordEmbedBuilder> embeds2 = new List<DiscordEmbedBuilder> { new DiscordEmbedBuilder() };
                    sfxSpecials.Sort((f1, f2) => f1.CreationTime.CompareTo(f2.CreationTime));
                    string totalSizeSpecial = Utils.GetMemorySize((ulong)sfxSpecials.Select(f => f.Length).Sum());
                    embeds2[0].Title = $"Danh sách file đặc biệt ({sfxSpecials.Count} file, {totalSizeSpecial})";
                    for (int i = 0; i < sfxSpecials.Count; i++)
                    {
                        string description = embeds2.Last().Description + Path.GetFileNameWithoutExtension(sfxSpecials[i].Name) + ", ";
                        if (description.Length > 2048 - 38 + Config.gI().DefaultPrefix.Length)
                        {
                            embeds2.Last().Description = embeds2.Last().Description.Trim(',', ' ');
                            embeds2.Add(new DiscordEmbedBuilder());
                            description = Path.GetFileNameWithoutExtension(sfxSpecials[i].Name) + ", ";
                        }
                        embeds2.Last().Description = description;
                    }
                    embeds2.Last().Description = embeds2.Last().Description.Trim(',', ' ') + Environment.NewLine + Environment.NewLine + "Dùng lệnh " + Config.gI().DefaultPrefix + "s <tên file> để bot nói!";
                    foreach (DiscordEmbedBuilder embed in embeds2)
                    {
                        await Task.Delay(200);
                        await serverInstance.lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()));
                    }
                }
            }
            catch (Exception ex) { Utils.LogException(ex); }
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
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(obj.TryGetChannel().Guild);
                if (volume == -1)
                {
                    await obj.TryRespondAsync("Âm lượng SFX hiện tại: " + (int)(serverInstance.voiceChannelSFX.volume * 100));
                    return;
                }
                if (volume < 0 || volume > 250)
                {
                    await obj.TryRespondAsync("Âm lượng không hợp lệ!");
                    return;
                }
                serverInstance.voiceChannelSFX.volume = volume / 100d;
                await obj.TryRespondAsync("Điều chỉnh âm lượng SFX thành: " + volume + "%!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
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
            VoiceTransmitSink transmitSink = serverInstance.currentVoiceNextConnection.GetTransmitSink();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = true;
            string filesNotFound = "";
            string previousFileName = "";
            List<byte> sfxData = new List<byte>();
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
                if (repeatTimes > 100)
                    repeatTimes = 100;
                FileStream file;
                try
                {
                    file = File.OpenRead(Path.Combine(Config.gI().SFXFolder, fileName.TrimEnd(',') + ".pcm"));
                }
                catch (FileNotFoundException ex)
                {
                    if (message.TryGetUser() is DiscordMember member && member.isInAdminServer())
                    {
                        try
                        {
                            file = File.OpenRead(Path.Combine(Config.gI().SFXFolderSpecial, fileName.TrimEnd(',') + ".pcm"));
                        }
                        catch (FileNotFoundException ex2)
                        {
                            filesNotFound += Path.GetFileNameWithoutExtension(ex2.FileName) + ", ";
                            continue;
                        }
                    }
                    else
                    {
                        filesNotFound += Path.GetFileNameWithoutExtension(ex.FileName) + ", ";
                        continue;
                    }
                }
                if (musicPlayer.isPlaying)
                {
                    byte[] buffer = new byte[file.Length + file.Length % 2];
                    file.Read(buffer, 0, (int)file.Length);
                    for (int j = 0; j < buffer.Length; j += 2)
                        Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, j) * volume)), 0, buffer, j, sizeof(short));
                    for (int j = 0; j < repeatTimes - 1; j++)
                        sfxData.AddRange(buffer);
                    sfxData.AddRange(new byte[2 * 16 * 48000 / 8 / 1000 * delay]);
                }
                else 
                {
                    for (int j = 0; j < repeatTimes - 1; j++)
                    {
                        await TransmitData(file, transmitSink, serverInstance);
                        if (isStop)
                            break;
                        await Task.Delay(delay);
                    }
                    if (isStop)
                    {
                        isStop = false;
                        break;
                    }
                }
                file.Close();
            }
            musicPlayer.sfxData.AddRange(sfxData);
            while (musicPlayer.sfxData.Count != 0)
                await Task.Delay(100);
            filesNotFound = filesNotFound.TrimEnd(',', ' ');
            string response = "";
            if (!string.IsNullOrWhiteSpace(filesNotFound))
                response += "Không tìm thấy file " + filesNotFound + "!";
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
                serverInstance.musicPlayer.isStopped = true;
                await Task.Delay(500);
                for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                    serverInstance.musicPlayer.musicQueue.ElementAt(i).Dispose();
                serverInstance.musicPlayer.musicQueue.Clear();
                serverInstance.isDisconnect = true;
                serverInstance.musicPlayer.isMainPlayRunning = false;
                serverInstance.musicPlayer.sentOutOfTrack = true;
                serverInstance.musicPlayer.cts.Cancel();
                serverInstance.musicPlayer.cts = new CancellationTokenSource();
                serverInstance.musicPlayer.isStopped = false;
            }
            catch (Exception ex) { Utils.LogException(ex); }
            if (!await serverInstance.InitializeVoiceNext(messageToReact))
                return;
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            isStop = true;
            serverInstance.isVoicePlaying = false;
            await messageToReact.TryRespondAsync($"Đã ngắt kết nối {(serverInstance.currentVoiceNextConnection.TargetChannel.Type == ChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{serverInstance.currentVoiceNextConnection.TargetChannel.Id}>!");
            serverInstance.currentVoiceNextConnection.Disconnect();
            serverInstance.musicPlayer.playMode = new PlayMode();
            await Task.Delay(1000);
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        async Task InternalStopSpeaking(SnowflakeObject messageToReact)
        {
            isStop = true;
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            serverInstance.isVoicePlaying = false;
            serverInstance.musicPlayer.sfxData.Clear();
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
            await messageToReact.TryRespondAsync($"Đã kết nối lại với {(serverInstance.currentVoiceNextConnection.TargetChannel.Type == ChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{serverInstance.currentVoiceNextConnection.TargetChannel.Id}>!");
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

        async Task TransmitData(FileStream file, VoiceTransmitSink transmitSink, BotServerInstance serverInstance)
        {
            byte[] buffer = new byte[transmitSink.SampleLength];
            while (file.Read(buffer, 0, buffer.Length) != 0)
            {
                if (isStop)
                    break;
                while (!serverInstance.canSpeak)
                    await Task.Delay(500);
                for (int i = 0; i < buffer.Length; i += 2)
                    Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, i) * volume)), 0, buffer, i, sizeof(short));
                await serverInstance.WriteTransmitData(buffer);
            }
            file.Position = 0;
        }
    }
}
