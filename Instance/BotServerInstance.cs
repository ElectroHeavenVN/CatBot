using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Music;
using CatBot.Voice;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;

namespace CatBot.Instance
{
    /// <summary>
    /// Mỗi server sẽ có 1 instance bot riêng tránh gây xung đột
    /// </summary>
    internal class BotServerInstance
    {
        DiscordChannel m_lastChannel;
        DateTime lastTimeCheckVoiceChannel = DateTime.Now;
        Thread checkVoiceChannelThread;
        internal static List<BotServerInstance> serverInstances = new List<BotServerInstance>();
        internal MusicPlayerCore musicPlayer;
        internal DiscordGuild server;
        internal VoiceNextConnection currentVoiceNextConnection;
        internal bool isDisconnect;
        internal bool canSpeak = true;
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
        internal bool suppressOnVoiceStateUpdatedEvent;
        internal bool isVoicePlaying;
        DiscordMember botMember;
        private int lastNumberOfUsersInVC;
        DiscordMessage lastNoOneInVCMessage;

        internal BotServerInstance() 
        {
            musicPlayer = new MusicPlayerCore(this);
            checkVoiceChannelThread = new Thread(() => CheckVoiceChannel(this));
        }

        internal BotServerInstance(DiscordGuild server) : this()
        {
            this.server = server;
            botMember = server.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id).GetAwaiter().GetResult();
        }

        static void CheckVoiceChannel(BotServerInstance self)
        {
            while(true)
            {
                if (self.currentVoiceNextConnection != null && !self.currentVoiceNextConnection.isDisposed())
                {
                    if (self.currentVoiceNextConnection.TargetChannel.Users.Any(m => !m.IsBot || m.IsBotExcluded()))
                        self.lastTimeCheckVoiceChannel = DateTime.Now;
                    else if ((DateTime.Now - self.lastTimeCheckVoiceChannel).TotalMinutes > 30)
                    {
                        self.suppressOnVoiceStateUpdatedEvent = true;
                        string content = "Bot tự động rời kênh thoại do không có ai trong kênh thoại trong 30 phút!";
                        if (self.currentVoiceNextConnection.TargetChannel.Type == ChannelType.Stage)
                            content = "Bot tự động rời sân khấu do không có người nghe trong 30 phút!";
                        self.GetLastChannel().SendMessageAsync(new DiscordEmbedBuilder().WithTitle(content).WithColor(DiscordColor.Red));
                        if (self.musicPlayer.isPlaying)
						{
							self.musicPlayer.isPaused = false;
							self.musicPlayer.isStopped = true;
						}
                        Thread.Sleep(500);
						for (int i = self.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
							self.musicPlayer.musicQueue.ElementAt(i).Dispose();
						self.musicPlayer.musicQueue.Clear();
						self.musicPlayer.isPlaying = false;
						self.isDisconnect = true;
						self.musicPlayer.isMainPlayRunning = false;
						self.musicPlayer.sentOutOfTrack = true;
						self.musicPlayer.cts.Cancel();
						self.musicPlayer.cts = new CancellationTokenSource();
						self.isVoicePlaying = false;
						self.currentVoiceNextConnection.Disconnect();
						self.lastNumberOfUsersInVC = int.MaxValue;
						self.musicPlayer.playMode = new PlayMode();
						self.lastTimeCheckVoiceChannel = DateTime.Now;
                        self.canSpeak = true;
						Thread.Sleep(3000);
						self.suppressOnVoiceStateUpdatedEvent = false;
                    }
                }
                Thread.Sleep(10000);
            }
        }

        internal async Task<bool> InitializeVoiceNext(SnowflakeObject obj)
        {
            VoiceNextConnection connection = await GetVoiceConnection(obj);
            if (connection == null)
                return false;
            currentVoiceNextConnection = connection;
            return true;
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
                if (Utils.IsBotOwner(member.Id))
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
                    DiscordChannel voiceChannel = member.VoiceState.Channel;
                    Permissions permissions = voiceChannel.PermissionsFor(serverInstance.botMember);
                    if (permissions.HasPermission(Permissions.AccessChannels | Permissions.UseVoice))
                    {
                        if (voiceChannel.Type == ChannelType.Stage)
                            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                        voiceNextConnection = await voiceChannel.ConnectAsync();
                        if (voiceNextConnection.TargetChannel.Type == ChannelType.Stage)
                        {
                            if (permissions.HasPermission(Permissions.MoveMembers) && serverInstance.botMember.VoiceState.IsSuppressed)
                                await serverInstance.botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                        }
                        voiceNextConnection.SetVolume(volume);
                        serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                        serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                        AddEvent(voiceNextConnection);
                    }
                    else
                    {
                        await obj.TryRespondAsync($"Bot bị thiếu quyền để kết nối tới {(member.VoiceState.Channel.Type == ChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{member.VoiceState.Channel.Id}>!");
                        return null;
                    }
                }
                return voiceNextConnection;
            }
            else if (channel != null)
            {
                if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && bSI.currentVoiceNextConnection.TargetChannel.Id == channel.Id))
                    return serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.isDisposed() && bSI.currentVoiceNextConnection.TargetChannel.Id == channel.Id).currentVoiceNextConnection;
                Permissions permissions = channel.PermissionsFor(serverInstance.botMember);
                if (permissions.HasPermission(Permissions.AccessChannels | Permissions.UseVoice))
                {
                    if (channel.Type == ChannelType.Stage)
                        serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                    voiceNextConnection = await channel.ConnectAsync();
                    if (voiceNextConnection.TargetChannel.Type == ChannelType.Stage)
                    {
                        if (permissions.HasPermission(Permissions.MoveMembers) && serverInstance.botMember.VoiceState.IsSuppressed)
                            await serverInstance.botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                        serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                    }
                    voiceNextConnection.SetVolume(volume);
                    serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                    serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                    AddEvent(voiceNextConnection);
                    return voiceNextConnection;
                }
                else
                {
                    await obj.TryRespondAsync($"Bot bị thiếu quyền để kết nối tới {(member.VoiceState.Channel.Type == ChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{member.VoiceState.Channel.Id}>!");
                    return null;
                }
            }
            return null;
        }

        internal static BotServerInstance GetBotServerInstance(DiscordGuild server)
        {
            if (serverInstances.Any(s => s.server != null && s.server.Id == server.Id))
                return serverInstances.First(s => s.server.Id == server.Id);
            serverInstances.Add(new BotServerInstance(server));
            serverInstances.Last().checkVoiceChannelThread.Start();
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
                    try
                    {
                        if (!serverInstances[i].currentVoiceNextConnection.isDisposed())
                        {
                            serverInstances[i].suppressOnVoiceStateUpdatedEvent = true;
                            serverInstances[i].currentVoiceNextConnection.Disconnect();
                            serverInstances[i].musicPlayer.playMode = new PlayMode();
                            serverInstances[i].checkVoiceChannelThread.Abort();
                            await Task.Delay(1000);
                        }
                        serverInstances[i] = null;
                        serverInstances.RemoveAt(i);
                        return;
                    }
                    catch { }
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
        
        internal static BotServerInstance GetBotServerInstance(VoiceNextConnection voiceNextConnection)
        {
            if (voiceNextConnection == null)
                return null;
            if (serverInstances.Any(s => s.currentVoiceNextConnection == voiceNextConnection))
                return serverInstances.First(s => s.currentVoiceNextConnection == voiceNextConnection);
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
                foreach (DiscordChannel voiceChannel in server.Channels.Values.Where(c => c.Type == ChannelType.Voice))
                    if (voiceChannel.Id == channelID)
                    {
                        channel = voiceChannel;
                        BotServerInstance serverInstance = GetBotServerInstance(channel.Guild);
                        if (serverInstance.currentVoiceNextConnection != null && !serverInstance.currentVoiceNextConnection.isDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == channel.GuildId)
                        {
                            serverInstance.currentVoiceNextConnection.Disconnect();
                            serverInstance.musicPlayer.playMode = new PlayMode();
                            serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                            await Task.Delay(300);
                        }
                        Permissions permissions = channel.PermissionsFor(serverInstance.botMember);
                        if (permissions.HasPermission(Permissions.AccessChannels | Permissions.UseVoice))
                        {
                            voiceNextConnection = await channel.ConnectAsync();
                            if (voiceNextConnection.TargetChannel.Type == ChannelType.Stage)
                            {
                                if (permissions.HasPermission(Permissions.MoveMembers) && serverInstance.botMember.VoiceState.IsSuppressed)
                                    await serverInstance.botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                            }
                            serverInstance.currentVoiceNextConnection = voiceNextConnection;
                            serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                            serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                            AddEvent(voiceNextConnection);
                        }
                        else
                        {
                            return new KeyValuePair<DiscordChannel, VoiceNextConnection>();
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
                    serverInstance.musicPlayer.playMode = new PlayMode();
                    serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                    await Task.Delay(200);
                    return new KeyValuePair<DiscordChannel, bool>(channel, true);
                }
            }
            return new KeyValuePair<DiscordChannel, bool>(channel, false);
        }

        internal static async Task SetVolume(SnowflakeObject obj, long volume)
        {
            BotServerInstance serverInstance = GetBotServerInstance(obj.TryGetChannel().Guild);
            if (!await serverInstance.InitializeVoiceNext(obj))
                return;
            if (volume == -1)
            {
                await obj.TryRespondAsync("Âm lượng hiện tại: " + (int)(serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier * 100) + Environment.NewLine + 
                    "Âm lượng TTS hiện tại: " + (int)(serverInstance.textToSpeech.volume * 100) + Environment.NewLine + 
                    "Âm lượng SFX hiện tại: " + (int)(serverInstance.voiceChannelSFX.volume * 100) + Environment.NewLine +
                    "Âm lượng nhạc hiện tại: " + (int)(serverInstance.musicPlayer.volume * 100));
                return;
            }
            if (volume < 0 || volume > 250)
            {
                await obj.TryRespondAsync("Âm lượng không hợp lệ!");
                return;
            }
            serverInstance.currentVoiceNextConnection.SetVolume(volume / 100d);
            await obj.TryRespondAsync("Điều chỉnh âm lượng thành: " + volume + "%!");
        }

        internal static async Task OnVoiceStateUpdated(VoiceStateUpdateEventArgs args)
        {
            BotServerInstance serverInstance = GetBotServerInstance(args.Guild);
            if (args.User.Id != DiscordBotMain.botClient.CurrentUser.Id)
            {
                if (serverInstance.currentVoiceNextConnection == null || serverInstance.currentVoiceNextConnection.isDisposed())
                    return;
                DiscordChannel voiceChannel = null;
                if (args.Before != null && args.Before.Channel != null)
                    voiceChannel = args.Before.Channel;
                else if (args.After != null && args.After.Channel != null)
                    voiceChannel = args.After.Channel;
                if (voiceChannel != null && voiceChannel == serverInstance.currentVoiceNextConnection.TargetChannel)
                    await serverInstance.CheckPeopleInVC();
                return;
            }
            if (serverInstance.suppressOnVoiceStateUpdatedEvent)
                return;
            try
            {
                if (args.After == null || args.After.Channel == null)
                {
                    //leave
                    if (!serverInstance.isDisconnect)
                    {
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
                        serverInstance.isVoicePlaying = false;
                        serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                        DiscordChannel channel = serverInstance.GetLastChannel();
                        bool isDelete = false;
                        DiscordMessage discordMessage = null;
                        string message = $"Bot đã bị kick khỏi kênh thoại <#{args.Before.Channel.Id}>!";
                        if (args.Before.Channel.Type == ChannelType.Stage)
                            message = $"Bot đã bị kick khỏi sân khấu <#{args.Before.Channel.Id}>!";
                        if (channel != null)
                        {
                            discordMessage = await channel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(message).WithColor(DiscordColor.Red).Build());
                        }
                        else
                        {
                            isDelete = true;
                            foreach (DiscordChannel ch in args.Guild.Channels.Values)
                            {
                                try
                                {
                                    discordMessage = await ch.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(message).WithColor(DiscordColor.Red).Build());
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
                        serverInstance.musicPlayer.isStopped = false;
                    }
                }
                else if (args.Before == null || args.Before.Channel == null)
                {
                    //join
                    serverInstance.isDisconnect = false;
                    await VoiceNextConnection_UserChange(serverInstance.currentVoiceNextConnection, null);
                }
                else if (args.Before.Channel.Id != args.After.Channel.Id)
                {
                    //move
                    serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                    try
                    {
                        serverInstance.isDisconnect = false;
                        bool isPaused = serverInstance.musicPlayer.isPaused;
                        serverInstance.musicPlayer.isPaused = true;
                        await Task.Delay(600);
                        VoiceNextConnection connection = serverInstance.currentVoiceNextConnection ?? await GetVoiceConnection(args.After.Channel);
                        connection.Disconnect();
                        serverInstance.currentVoiceNextConnection = await GetVoiceConnection(args.After.Channel);
                        serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                        serverInstance.musicPlayer.isPaused = isPaused;
                    }
                    catch (Exception ex) { Utils.LogException(ex); }
                    serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                    await VoiceNextConnection_UserChange(serverInstance.currentVoiceNextConnection, null);
                }
                else if (args.After != null)
                {
                    if (args.After.IsServerMuted || args.After.IsSuppressed)
                    {
                        if (!serverInstance.suppressOnVoiceStateUpdatedEvent)
                        {
                            //mute
                            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                            DiscordChannel channel = serverInstance.GetLastChannel();
                            bool isDelete = false;
                            DiscordMessage discordMessage = null;
                            string message = "Bot bị tắt tiếng! Nhạc sẽ được tạm dừng đến khi bot được bật tiếng!";
                            DiscordChannel targetChannel;
                            if (args.After.Channel != null)
                                targetChannel = args.After.Channel;
                            else
                                targetChannel = serverInstance.currentVoiceNextConnection.TargetChannel;
                            if (targetChannel.Type == ChannelType.Stage && args.After.IsSuppressed)
                                message = "Bot đang là người nghe! Nhạc sẽ được tạm dừng đến khi bot được chuyển thành người nói!";
                            if (channel != null)
                            {
                                discordMessage = await channel.SendMessageAsync(new DiscordEmbedBuilder().WithDescription(message).WithColor(DiscordColor.Orange).Build());
                            }
                            else
                            {
                                isDelete = true;
                                foreach (DiscordChannel ch in args.Guild.Channels.Values)
                                {
                                    try
                                    {
                                        discordMessage = await ch.SendMessageAsync(new DiscordEmbedBuilder().WithDescription(message).WithColor(DiscordColor.Orange).Build());
                                        break;
                                    }
                                    catch (Exception) { }
                                }
                            }
                            serverInstance.canSpeak = false;
                            if (isDelete)
                            {
                                await Task.Delay(3000);
                                await discordMessage?.DeleteAsync();
                            }
                            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                        }
                    }
                    else if (!args.After.IsServerMuted || !args.After.IsSuppressed)
                    {
                        //unmute
                        serverInstance.canSpeak = true;
                    }
                }
            }
            catch (Exception ex)
            {
                serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                Utils.LogException(ex);
            }
        }

        internal async Task WriteTransmitData(byte[] data)
        {
            if (currentVoiceNextConnection == null)
                return;
            await currentVoiceNextConnection.GetTransmitSink().WriteAsync(new ReadOnlyMemory<byte>(data));
        }

        internal static void AddEvent(VoiceNextConnection voiceNextConnection)
        {
            //fck vps
            //voiceNextConnection.UserJoined += VoiceNextConnection_UserChange;
            //voiceNextConnection.UserLeft += VoiceNextConnection_UserChange; 
        }

        private static async Task VoiceNextConnection_UserChange(VoiceNextConnection sender, DiscordEventArgs args)
        {
            BotServerInstance serverInstance = GetBotServerInstance(sender);
            if (serverInstance != null)
                await serverInstance.CheckPeopleInVC();
        }

        async Task CheckPeopleInVC()
        {
            if (currentVoiceNextConnection == null)
                return;
            if (currentVoiceNextConnection.isDisposed())
                return;
            int userCount = currentVoiceNextConnection.TargetChannel.Users.Where(u => !u.IsBot || u.IsBotExcluded()).Count() + 1;
            bool result = userCount >= 2;
            canSpeak = result;
            if (!result && lastNumberOfUsersInVC >= 2 && (musicPlayer.isPlaying || isVoicePlaying))
            {
                if (lastChannel != null)
                {
                    IReadOnlyList<DiscordMessage> lastMessage = await lastChannel.GetMessagesAsync(1);
                    if (lastNoOneInVCMessage != null && lastNoOneInVCMessage == lastMessage[0])
                        return;
                    if (lastNoOneInVCMessage != null)
                        await lastNoOneInVCMessage.DeleteAsync();
                }
                string message = "Không có người trong kênh thoại! Nhạc sẽ được tạm dừng cho đến khi có người khác vào kênh thoại!";
                if (currentVoiceNextConnection.TargetChannel.Type == ChannelType.Stage)
                    message = "Không có người trong sân khấu! Nhạc sẽ được tạm dừng cho đến khi có người khác vào sân khấu!";
                DiscordChannel discordChannel = GetLastChannel();
                if (discordChannel != null)
                    lastNoOneInVCMessage = await discordChannel.SendMessageAsync(new DiscordEmbedBuilder().WithDescription(message).WithColor(DiscordColor.Orange).Build());
            }
            lastNumberOfUsersInVC = userCount;
        }
    }
}
