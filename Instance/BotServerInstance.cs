using DiscordBot.Music;
using DiscordBot.Voice;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Instance
{
    /// <summary>
    /// Mỗi server sẽ có 1 instance bot riêng tránh gây xung đột
    /// </summary>
    internal class BotServerInstance
    {
        DiscordChannel m_lastChannel;
        internal static List<BotServerInstance> serverInstances = new List<BotServerInstance>();
        //internal OfflineMusicPlayer offlineMusicPlayer = new OfflineMusicPlayer();
        //internal ZingMP3Player zingMP3Player = new ZingMP3Player();
        internal MusicPlayerCore musicPlayer = new MusicPlayerCore();
        internal DiscordGuild server;
        internal VoiceNextConnection currentVoiceNextConnection;
        internal bool isDisconnect;
        internal DiscordChannel lastChannel
        {
            get => m_lastChannel;
            set
            {
                lastTimeSetLastChannel = DateTime.Now.Ticks;
                m_lastChannel = value;
            }
        }
        internal long lastTimeSetLastChannel;
        internal VoiceChannelSFXCore voiceChannelSFX = new VoiceChannelSFXCore();
        internal TTSCore textToSpeech = new TTSCore();
        internal bool supressOnVoiceStateUpdatedEvent;
        internal bool isVoicePlaying;

        internal async Task<bool> InitializeVoiceNext(SnowflakeObject obj)
        {
            VoiceNextConnection connection = await GetVoiceConnection(obj);
            if (connection == null)
                return false;
            currentVoiceNextConnection = connection;
            return true;
        }

        static async Task<VoiceNextConnection> GetVoiceConnection(SnowflakeObject obj)
        {
            DiscordMember member = null;
            DiscordChannel channel = null;
            if (obj is DiscordMessage message)
            {
                if (message.Author is DiscordMember mem)
                    member = mem;
                channel = message.Channel;
            }
            else if (obj is DiscordInteraction interaction)
            {
                if (interaction.User is DiscordMember mem2)
                    member = mem2;
                channel = interaction.Channel;
            }
            else if (obj is DiscordChannel ch)
                channel = ch;
            BotServerInstance serverInstance = GetBotServerInstance(channel.Guild);
            VoiceNextConnection voiceNextConnection = null;
            double volume = serverInstance.currentVoiceNextConnection == null ? 1 : serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier;
            if (member != null)
            {
                if (Config.BotAuthorsID.Contains(member.Id))
                {
                    if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && channel.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                        return serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && channel.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
                    else if (member.VoiceState == null || member.VoiceState.Channel == null)
                    {
                        await obj.TryRespondAsync("Bot không ở trong kênh thoại nào trong server này!");
                        return null;
                    }
                }
                if (member.VoiceState == null || member.VoiceState.Channel == null)
                {
                    await obj.TryRespondAsync("Bạn không ở trong kênh thoại nào trong server này!");
                    return null;
                }
                if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && channel.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                    voiceNextConnection = serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && channel.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
                else if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && member.VoiceState.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                {
                    await obj.TryRespondAsync("Bạn đang ở kênh thoại khác!");
                    return null;
                }
                else
                {
                    voiceNextConnection = await member.VoiceState.Channel.ConnectAsync();
                    voiceNextConnection.SetVolume(volume);
                    serverInstance.currentVoiceNextConnection = voiceNextConnection;
                }
                return voiceNextConnection;
            }
            else if (channel != null)
            {
                if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && bSI.currentVoiceNextConnection.TargetChannel.Id == channel.Id))
                    return serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && bSI.currentVoiceNextConnection.TargetChannel.Id == channel.Id).currentVoiceNextConnection;
                voiceNextConnection = await channel.ConnectAsync();
                voiceNextConnection.SetVolume(volume);
                serverInstance.currentVoiceNextConnection = voiceNextConnection;
                return voiceNextConnection;
            }
            return null;
        }

        internal DiscordChannel GetLastChannel()
        {
            if (lastTimeSetLastChannel > musicPlayer.lastTimeSetLastChannel && m_lastChannel != null)
                return lastChannel;
            else if (musicPlayer.lastChannel != null)
                return musicPlayer.lastChannel;
            else
                return null;
        }

        internal static BotServerInstance GetBotServerInstance(DiscordGuild server)
        {
            if (serverInstances.Any(s => s.server != null && s.server.Id == server.Id))
                return serverInstances.First(s => s.server.Id == server.Id);
            serverInstances.Add(new BotServerInstance() { server = server });
            return serverInstances.Last();
        }

        internal static BotServerInstance GetBotServerInstance(MusicPlayerCore musicPlayer)
        {
            if (serverInstances.Any(s => s.server != null && s.musicPlayer == musicPlayer))
                return serverInstances.First(s => s.musicPlayer == musicPlayer);
            serverInstances.Add(new BotServerInstance());
            return serverInstances.Last();
        }

        internal static MusicPlayerCore GetMusicPlayer(DiscordGuild server)
        {
            if (serverInstances.Any(s => s.server == server))
                return serverInstances.First(s => s.server == server).musicPlayer;
            serverInstances.Add(new BotServerInstance());
            return serverInstances.Last().musicPlayer;
        }

        internal static async Task RemoveBotServerInstance(ulong serverID)
        {
            for (int i = 0; i < serverInstances.Count; i++)
                if (serverInstances[i].server.Id == serverID)
                {
                    if (!serverInstances[i].currentVoiceNextConnection.isDisposed())
                    {
                        serverInstances[i].supressOnVoiceStateUpdatedEvent = true;
                        serverInstances[i].currentVoiceNextConnection.Disconnect();
                        await Task.Delay(1000);
                    }
                    serverInstances[i] = null;
                    serverInstances.RemoveAt(i);
                    return;
                }                
        }

        internal static BotServerInstance GetBotServerInstance(VoiceChannelSFXCore voiceChannelSFX)
        {
            if (voiceChannelSFX == null)
                return null;
            if (serverInstances.Any(s => s.voiceChannelSFX == voiceChannelSFX))
                return serverInstances.First(s => s.voiceChannelSFX == voiceChannelSFX);
            return null;
        }

        internal static BotServerInstance GetBotServerInstance(TTSCore textToSpeech)
        {
            if (textToSpeech == null)
                return null;
            if (serverInstances.Any(s => s.textToSpeech == textToSpeech))
                return serverInstances.First(s => s.textToSpeech == textToSpeech);
            return null;
        }
        
        internal static VoiceChannelSFXCore GetVoiceChannelSFXCore(DiscordGuild server)
        {
            if (serverInstances.Any(s => s.server == server))
                return serverInstances.First(s => s.server == server).voiceChannelSFX;
            serverInstances.Add(new BotServerInstance());
            return serverInstances.Last().voiceChannelSFX;
        }

        internal static async Task<KeyValuePair<DiscordChannel, VoiceNextConnection>> JoinVoiceChannel(ulong channelID)
        {
            DiscordChannel channel = null;
            VoiceNextConnection voiceNextConnection = null;
            foreach (DiscordGuild server in DiscordBotMain.botClient.Guilds.Values)
                foreach (DiscordChannel voiceChannel in server.Channels.Values.Where(c => c.Type == DSharpPlus.ChannelType.Voice))
                    if (voiceChannel.Id == channelID)
                    {
                        channel = voiceChannel;
                        foreach (BotServerInstance serverInstance in serverInstances)
                        {
                            if (serverInstance.currentVoiceNextConnection != null && !serverInstance.currentVoiceNextConnection.isDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == channel.GuildId)
                            {
                                serverInstance.currentVoiceNextConnection.Disconnect();
                                await Task.Delay(300);
                                break;
                            }
                        }
                        if (channel.PermissionsFor(channel.Guild.CurrentMember).HasPermission(Permissions.AccessChannels | Permissions.UseVoice))
                        {
                            voiceNextConnection = await channel.ConnectAsync();
                            GetBotServerInstance(channel.Guild).currentVoiceNextConnection = voiceNextConnection;
                        }
                        return new KeyValuePair<DiscordChannel, VoiceNextConnection>(channel, voiceNextConnection);
                    }
            return new KeyValuePair<DiscordChannel, VoiceNextConnection>(channel, voiceNextConnection);
        }

        internal static async Task<KeyValuePair<DiscordChannel, bool>> LeaveVoiceChannel(ulong serverID)
        {
            DiscordChannel channel = null;
            foreach (BotServerInstance serverInstance in serverInstances)
            {
                if (serverInstance.currentVoiceNextConnection != null && !serverInstance.currentVoiceNextConnection.isDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == serverID)
                {
                    channel = serverInstance.currentVoiceNextConnection.TargetChannel;
                    serverInstance.currentVoiceNextConnection.Disconnect();
                    await Task.Delay(200);
                    return new KeyValuePair<DiscordChannel, bool>(channel, true);
                }
            }
            return new KeyValuePair<DiscordChannel, bool>(channel, false);
        }

        internal static async Task onVoiceStateUpdated(VoiceStateUpdateEventArgs args)
        {
            if (args.User.Id == DiscordBotMain.botClient.CurrentUser.Id)
            {
                BotServerInstance serverInstance = GetBotServerInstance(args.Guild);
                if (serverInstance.supressOnVoiceStateUpdatedEvent)
                    return;
                try
                {
                    if (args.After == null || args.After.Channel == null)
                    {
                        //leave
                        if (!serverInstance.isDisconnect)
                        {
                            for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                                serverInstance.musicPlayer.musicQueue.ElementAt(i).Dispose();
                            serverInstance.musicPlayer.musicQueue.Clear();
                            serverInstance.isDisconnect = true;
                            serverInstance.musicPlayer.isThreadAlive = false;
                            serverInstance.musicPlayer.sentOutOfTrack = true;
                            serverInstance.musicPlayer.cts.Cancel();
                            serverInstance.musicPlayer.cts = new CancellationTokenSource();
                            serverInstance.isVoicePlaying = false;
                            DiscordChannel channel = serverInstance.GetLastChannel();
                            bool isDelete = false;
                            DiscordMessage discordMessage = null;
                            if (channel != null)
                            {
                                discordMessage = await channel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle($"Bot đã bị kick khỏi kênh thoại <#{args.Before.Channel.Id}>!").WithColor(DiscordColor.Red).Build());
                            }
                            else
                            {
                                isDelete = true;
                                foreach (DiscordChannel ch in args.Guild.Channels.Values)
                                {
                                    try
                                    {
                                        discordMessage = await ch.SendMessageAsync(new DiscordEmbedBuilder().WithTitle($"Bot đã bị kick khỏi kênh thoại <#{args.Before.Channel.Id}>!").WithColor(DiscordColor.Red).Build());
                                        break;
                                    }
                                    catch (Exception) { }
                                }
                            }
                            if (isDelete)
                            {
                                await Task.Delay(3000);
                                await discordMessage?.DeleteAsync();
                            }
                        }
                    }
                    else if (args.Before == null || args.Before.Channel == null)
                    {
                        //join
                        serverInstance.isDisconnect = false;
                    }
                    else if (args.Before.Channel.Id != args.After.Channel.Id)
                    {
                        //move
                        serverInstance.supressOnVoiceStateUpdatedEvent = true;
                        try
                        {
                            serverInstance.isDisconnect = false;
                            bool isPaused = serverInstance.musicPlayer.isPaused;
                            serverInstance.musicPlayer.isPaused = true;
                            await Task.Delay(600);
                            VoiceNextConnection connection = serverInstance.currentVoiceNextConnection ?? await GetVoiceConnection(args.After.Channel);
                            connection.Disconnect();
                            serverInstance.currentVoiceNextConnection = await GetVoiceConnection(args.After.Channel);
                            serverInstance.musicPlayer.isPaused = isPaused;
                        }
                        catch (Exception ex) { Utils.LogException(ex); }
                        serverInstance.supressOnVoiceStateUpdatedEvent = false;
                    }
                }
                catch (Exception ex)
                {
                    serverInstance.supressOnVoiceStateUpdatedEvent = false;
                    Utils.LogException(ex);
                }
            }
        }

        //internal static BotServerInstance GetBotServerInstance(OfflineMusicPlayer musicPlayer)
        //{
        //    if (musicPlayer == null)
        //        return null;
        //    if (serverInstances.Any(s => s.offlineMusicPlayer == musicPlayer))
        //        return serverInstances.First(s => s.offlineMusicPlayer == musicPlayer);
        //    return null;
        //}

        //internal static OfflineMusicPlayer GetOfflineMusicPlayer(DiscordGuild server)
        //{
        //    if (serverInstances.Any(s => s.server == server))
        //        return serverInstances.First(s => s.server == server).offlineMusicPlayer;
        //    serverInstances.Add(new BotServerInstance());
        //    return serverInstances.Last().offlineMusicPlayer;
        //}

        //internal static ZingMP3Player GetZingMP3Player(DiscordGuild server)
        //{
        //    if (serverInstances.Any(s => s.server == server))
        //        return serverInstances.First(s => s.server == server).zingMP3Player;
        //    serverInstances.Add(new BotServerInstance());
        //    return serverInstances.Last().zingMP3Player;
        //}

        //internal static BotServerInstance GetBotServerInstance(ZingMP3Player zingMP3Player)
        //{
        //    if (serverInstances.Any(s => s.server != null && s.zingMP3Player == zingMP3Player))
        //        return serverInstances.First(s => s.zingMP3Player == zingMP3Player);
        //    serverInstances.Add(new BotServerInstance());
        //    return serverInstances.Last();
        //}
    }
}
