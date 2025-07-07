using CatBot.Music;
using CatBot.Voice;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CatBot.Instance
{
    /// <summary>
    /// Mỗi server sẽ có 1 instance bot riêng tránh gây xung đột
    /// </summary>
    internal class BotServerInstance
    {
        DiscordChannel? lastChannel;
        DateTime lastTimeCheckVoiceChannel = DateTime.Now;
        Thread checkVoiceChannelThread;
        internal static List<BotServerInstance> serverInstances = new List<BotServerInstance>();
        internal MusicPlayerCore musicPlayer;
        internal ulong serverID;
        internal VoiceNextConnection? currentVoiceNextConnection;
        internal bool isDisconnect;
        internal bool canSpeak = true;
        internal DiscordChannel? LastChannel
        {
            get => lastChannel;
            set
            {
                lastTimeSetLastChannel = DateTime.Now.Ticks;
                lastChannel = value;
            }
        }
        internal long lastTimeSetLastChannel;
        internal VoiceChannelSFXCore voiceChannelSFX = new VoiceChannelSFXCore();
        internal bool suppressOnVoiceStateUpdatedEvent;
        internal bool isVoicePlaying;
        private int lastNumberOfUsersInVC;
        DiscordMessage? lastNoOneInVCMessage;
        CancellationTokenSource ctsCheckVC = new CancellationTokenSource();

        internal BotServerInstance(ulong serverID)
        {
            this.serverID = serverID;
            musicPlayer = new MusicPlayerCore(this);
            checkVoiceChannelThread = new Thread(CheckVoiceChannel);
        }

        async void CheckVoiceChannel()
        {
            while(true)
            {
                if (ctsCheckVC.IsCancellationRequested)
                    return;
                if (currentVoiceNextConnection is not null && !currentVoiceNextConnection.IsDisposed())
                {
                    if (currentVoiceNextConnection.TargetChannel.Users.Any(m => !m.IsBot || m.IsBotExcluded()))
                        lastTimeCheckVoiceChannel = DateTime.Now;
                    else if ((DateTime.Now - lastTimeCheckVoiceChannel).TotalMinutes > 30)
                    {
                        suppressOnVoiceStateUpdatedEvent = true;
                        string content = "Bot tự động rời kênh thoại do không có ai trong kênh thoại trong 30 phút!";
                        if (currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                            content = "Bot tự động rời sân khấu do không có người nghe trong 30 phút!";
                        DiscordChannel? discordChannel = GetLastChannel();
                        if (discordChannel is not null)
                            await discordChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(content).WithColor(DiscordColor.Red));
                        if (musicPlayer.isPlaying)
						{
							musicPlayer.isPaused = false;
							musicPlayer.isStopped = true;
						}
                        musicPlayer.isCurrentSessionLocked = false;
                        await Task.Delay(500);
                        for (int i = musicPlayer.musicQueue.Count - 1; i >= 0; i--)
							musicPlayer.musicQueue.ElementAt(i).Dispose();
						musicPlayer.musicQueue.Clear();
						musicPlayer.isPlaying = false;
						isDisconnect = true;
						musicPlayer.isMainPlayRunning = false;
						musicPlayer.cts.Cancel();
                        if (musicPlayer.lastNowPlayingMessage is not null)
                            await musicPlayer.lastNowPlayingMessage.DeleteAsync();
                        musicPlayer.lastNowPlayingMessage = null;
                        musicPlayer.sentOutOfTrack = true;
						isVoicePlaying = false;
						currentVoiceNextConnection.Disconnect();
						lastNumberOfUsersInVC = int.MaxValue;
						musicPlayer.playMode = new PlayMode();
						lastTimeCheckVoiceChannel = DateTime.Now;
                        canSpeak = true;
                        await Task.Delay(3000);
                        suppressOnVoiceStateUpdatedEvent = false;
                    }
                }
                await Task.Delay(1000);
            }
        }

        internal async Task<bool> InitializeVoiceNext(CommandContext ctx)
        {
            VoiceNextConnection? connection = await GetVoiceConnection(ctx);
            if (connection is null)
                return false;
            currentVoiceNextConnection = connection;
            return true;
        }

        internal DiscordChannel? GetLastChannel()
        {
            if (lastTimeSetLastChannel > musicPlayer.lastTimeSetLastChannel && lastChannel is not null)
                return LastChannel;
            else if (musicPlayer.LastChannel is not null)
                return musicPlayer.LastChannel;
            else
                return null;
        }

        static async Task<VoiceNextConnection?> GetVoiceConnection(CommandContext ctx)
        {
            DiscordMember? member = ctx.Member;
            if (member is null)
                throw new Exception("Can't find member");
            if (ctx.Guild is null)
                throw new Exception("Can't find guild");
            BotServerInstance serverInstance = GetBotServerInstance(ctx.Guild.Id);
            VoiceNextConnection? voiceNextConnection = null;
            double volume = serverInstance.currentVoiceNextConnection is null ? 1 : serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier;
            if (Utils.IsBotOwner(member.Id))
            {
                if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection is not null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                    return serverInstances.First(bSI => bSI.currentVoiceNextConnection is not null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
                else if (member.VoiceState is null || member.VoiceState.ChannelId is null)
                {
                    await ctx.RespondAsync("Bot không ở trong kênh thoại nào trong server này!");
                    return null;
                }
            }
            if (member.VoiceState is null || member.VoiceState.ChannelId is null)
            {
                await ctx.RespondAsync("Bạn không ở trong kênh thoại nào trong server này!");
                return null;
            }
            if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection is not null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                voiceNextConnection = serverInstances.First(bSI => bSI.currentVoiceNextConnection is not null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
            else if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection is not null && !bSI.currentVoiceNextConnection.IsDisposed() && member.VoiceState.GuildId is not null && member.VoiceState.GuildId == bSI.currentVoiceNextConnection.TargetChannel.GuildId))
            {
                await ctx.RespondAsync("Bạn đang ở kênh thoại khác!");
                return null;
            }
            else
            {
                DiscordChannel? voiceChannel = await member.VoiceState.GetChannelAsync();
                if (voiceChannel is null)
                {
                    await ctx.RespondAsync("Bạn không ở trong kênh thoại nào trong server này!");
                    return null;
                }
                DiscordMember botMember = await ctx.Guild.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id);
                DiscordPermissions permissions = voiceChannel.PermissionsFor(botMember);
                if (permissions.HasAllPermissions(new DiscordPermissions(DiscordPermission.ViewChannel, DiscordPermission.Connect)))
                {
                    if (voiceChannel.Type == DiscordChannelType.Stage)
                        serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                    if (!permissions.HasPermission(DiscordPermission.MoveMembers) && voiceChannel.Users.Count >= voiceChannel.UserLimit)
                    {
                        await ctx.RespondAsync($"Kênh thoại <#{voiceChannel.Id}> đã đầy!");
                        return null;
                    }
                    voiceNextConnection = await voiceChannel.ConnectAsync();
                    if (voiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                    {
                        if (permissions.HasPermission(DiscordPermission.MoveMembers) && botMember.VoiceState.IsSuppressed)
                            await botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                        serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                    }
                    voiceNextConnection.SetVolume(volume);
                    serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                    serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                    AddEvent(voiceNextConnection);
                }
                else
                {
                    await ctx.RespondAsync($"Bot bị thiếu quyền để kết nối tới {(voiceChannel.Type == DiscordChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{voiceChannel.Id}>!");
                    return null;
                }
            }
            return voiceNextConnection;
        }
    
        async Task<VoiceNextConnection?> GetVoiceConnection(DiscordChannel voiceChannel)
        {
            if (voiceChannel.Type != DiscordChannelType.Voice || voiceChannel.Type != DiscordChannelType.Stage)
                return null;
            VoiceNextConnection? result = null;
            DiscordMember botMember = await voiceChannel.Guild.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id);
            DiscordPermissions permissions = voiceChannel.PermissionsFor(botMember);
            if (permissions.HasAllPermissions(new DiscordPermissions(DiscordPermission.ViewChannel, DiscordPermission.Connect)))
            {
                if (voiceChannel.Type == DiscordChannelType.Stage)
                    suppressOnVoiceStateUpdatedEvent = true;
                result = await voiceChannel.ConnectAsync();
                if (result.TargetChannel.Type == DiscordChannelType.Stage)
                {
                    if (permissions.HasPermission(DiscordPermission.MoveMembers) && botMember.VoiceState.IsSuppressed)
                        await botMember.UpdateVoiceStateAsync(result.TargetChannel, false);
                    suppressOnVoiceStateUpdatedEvent = false;
                }
                double volume = currentVoiceNextConnection is null ? 1 : currentVoiceNextConnection.GetTransmitSink().VolumeModifier;
                result.SetVolume(volume);
                lastTimeCheckVoiceChannel = DateTime.Now;
                lastNumberOfUsersInVC = int.MaxValue;
                AddEvent(result);
            }
            return result;
        }

        internal static BotServerInstance GetBotServerInstance(ulong serverID)
        {
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.serverID == serverID);
            if (botServerInstance is null)
            {
                botServerInstance = new BotServerInstance(serverID);
                botServerInstance.checkVoiceChannelThread.Start();
                serverInstances.Add(botServerInstance);
            }
            return botServerInstance;
        }

        internal static MusicPlayerCore? GetMusicPlayer(ulong serverID)
        {
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.serverID == serverID);
            if (botServerInstance is null)
            {
                botServerInstance = new BotServerInstance(serverID);
                botServerInstance.checkVoiceChannelThread.Start();
                serverInstances.Add(botServerInstance);
            }
            return botServerInstance.musicPlayer;
        }

        internal static async Task RemoveBotServerInstance(ulong serverID)
        {
            for (int i = 0; i < serverInstances.Count; i++)
            {
                if (serverInstances[i].serverID != serverID)
                    continue;
                try
                {
                    if (serverInstances[i].currentVoiceNextConnection is not null && !serverInstances[i].currentVoiceNextConnection.IsDisposed())
                    {
                        serverInstances[i].suppressOnVoiceStateUpdatedEvent = true;
                        serverInstances[i].currentVoiceNextConnection?.Disconnect();
                        serverInstances[i].musicPlayer.playMode = new PlayMode();
                        serverInstances[i].ctsCheckVC.Cancel();
                        await Task.Delay(1000);
                    }
                    serverInstances.RemoveAt(i);
                    return;
                }
                catch { }
            }
        }

        internal static BotServerInstance GetBotServerInstance(VoiceChannelSFXCore voiceChannelSFX)
        {
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.voiceChannelSFX == voiceChannelSFX);
            //if (botServerInstance is null)
            //{
            //    botServerInstance = new BotServerInstance();
            //    botServerInstance.voiceChannelSFX = voiceChannelSFX;
            //    serverInstances.Add(botServerInstance);
            //}
            if (botServerInstance is null)
                throw new ArgumentNullException(nameof(botServerInstance));
            return botServerInstance;
        }

        internal static BotServerInstance GetBotServerInstance(VoiceNextConnection? voiceNextConnection)
        {
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.currentVoiceNextConnection == voiceNextConnection);
            //if (botServerInstance is null)
            //{
            //    botServerInstance = new BotServerInstance();
            //    botServerInstance.currentVoiceNextConnection = voiceNextConnection;
            //    serverInstances.Add(botServerInstance);
            //}
            if (botServerInstance is null)
                throw new ArgumentNullException(nameof(botServerInstance));
            return botServerInstance;
        }

        internal static async Task<KeyValuePair<DiscordChannel?, VoiceNextConnection?>> JoinVoiceChannel(ulong channelID)
        {
            DiscordChannel? channel = null;
            VoiceNextConnection? voiceNextConnection = null;
            foreach (DiscordGuild server in DiscordBotMain.botClient.Guilds.Values)
            {
                foreach (DiscordChannel voiceChannel in server.Channels.Values.Where(c => c.Type == DiscordChannelType.Voice))
                {
                    if (voiceChannel.Id != channelID)
                        continue;
                    channel = voiceChannel;
                    BotServerInstance serverInstance = GetBotServerInstance(channel.GuildId ?? 0);
                    if (serverInstance.currentVoiceNextConnection is not null && !serverInstance.currentVoiceNextConnection.IsDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == channel.GuildId)
                    {
                        serverInstance.currentVoiceNextConnection.Disconnect();
                        serverInstance.musicPlayer.playMode = new PlayMode();
                        serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                        await Task.Delay(300);
                    }
                    DiscordMember botMember = await server.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id);
                    DiscordPermissions perm = channel.PermissionsFor(botMember);
                    if (!perm.HasAllPermissions(new DiscordPermissions(DiscordPermission.ViewChannel, DiscordPermission.Connect)))
                        return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>();
                    voiceNextConnection = await channel.ConnectAsync();
                    if (voiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                    {
                        if (perm.HasPermission(DiscordPermission.MoveMembers) && botMember.VoiceState.IsSuppressed)
                            await botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                    }
                    serverInstance.currentVoiceNextConnection = voiceNextConnection;
                    serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                    serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                    AddEvent(voiceNextConnection);
                    return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>(channel, voiceNextConnection);
                }
            }
            return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>(channel, voiceNextConnection);
        }

        internal static async Task<KeyValuePair<DiscordChannel?, bool>> LeaveVoiceChannel(ulong serverID)
        {
            DiscordChannel? channel = null;
            foreach (BotServerInstance serverInstance in serverInstances)
            {
                if (serverInstance.currentVoiceNextConnection is null || serverInstance.currentVoiceNextConnection.IsDisposed() || serverInstance.currentVoiceNextConnection.TargetChannel.GuildId != serverID)
                    continue;
                channel = serverInstance.currentVoiceNextConnection.TargetChannel;
                serverInstance.currentVoiceNextConnection.Disconnect();
                serverInstance.musicPlayer.playMode = new PlayMode();
                serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                await Task.Delay(200);
                return new KeyValuePair<DiscordChannel?, bool>(channel, true);
            }
            return new KeyValuePair<DiscordChannel?, bool>(channel, false);
        }

        internal static async Task SetVolume(CommandContext ctx, long volume)
        {
            if (ctx.Guild is null)
                return;
            BotServerInstance serverInstance = GetBotServerInstance(ctx.Guild.Id);
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            if (serverInstance.currentVoiceNextConnection is null)
            {
                await ctx.RespondAsync("Bot không kết nối với kênh thoại nào!");
                return;
            }
            if (volume == -1)
            {
                await ctx.RespondAsync(
                    $"""
                    Âm lượng hiện tại: {serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier * 100:00}
                    Âm lượng SFX hiện tại: {serverInstance.voiceChannelSFX.volume * 100:00}
                    Âm lượng nhạc hiện tại: {serverInstance.musicPlayer.volume * 100:00}
                    """);
                return;
            }
            if (volume < 0 || volume > 250)
            {
                await ctx.RespondAsync("Âm lượng không hợp lệ!");
                return;
            }
            serverInstance.currentVoiceNextConnection.SetVolume(volume / 100d);
            await ctx.RespondAsync("Điều chỉnh âm lượng thành: " + volume + "%!");
        }

        internal static async Task OnVoiceStateUpdated(VoiceStateUpdatedEventArgs args)
        {
            if (args.GuildId is null)
                return;
            BotServerInstance serverInstance = GetBotServerInstance(args.GuildId.Value);
            if (args.UserId != DiscordBotMain.botClient.CurrentUser.Id)
            {
                if (serverInstance.currentVoiceNextConnection is null || serverInstance.currentVoiceNextConnection.IsDisposed())
                    return;
                ulong? voiceChannelId = null;
                if (args.Before.ChannelId is not null)
                    voiceChannelId = args.Before.ChannelId;
                else if (args.After.ChannelId is not null)
                    voiceChannelId = args.After.ChannelId;
                if (voiceChannelId is not null && voiceChannelId == serverInstance.currentVoiceNextConnection.TargetChannel.Id)
                    await serverInstance.CheckPeopleInVC();
                return;
            }
            if (serverInstance.suppressOnVoiceStateUpdatedEvent)
                return;
            try
            {
                if (args.After.ChannelId is null)
                {
                    //leave
                    if (!serverInstance.isDisconnect)
                    {
                        serverInstance.musicPlayer.isStopped = true;
                        serverInstance.musicPlayer.isCurrentSessionLocked = false;
                        await Task.Delay(500);
                        for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                            serverInstance.musicPlayer.musicQueue.ElementAt(i).Dispose();
                        serverInstance.musicPlayer.musicQueue.Clear();
                        serverInstance.isDisconnect = true;
                        serverInstance.musicPlayer.isMainPlayRunning = false;
                        serverInstance.musicPlayer.cts.Cancel();
                        serverInstance.musicPlayer.sentOutOfTrack = true;
                        if (serverInstance.musicPlayer.lastNowPlayingMessage is not null)
                            await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                        serverInstance.musicPlayer.lastNowPlayingMessage = null;
                        serverInstance.isVoicePlaying = false;
                        serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                        DiscordChannel? channel = serverInstance.GetLastChannel();
                        //bool isDelete = false;
                        DiscordMessage? discordMessage = null;
                        string message = $"Bot đã bị kick khỏi kênh thoại <#{args.Before.ChannelId}>!";
                        DiscordChannel? ch = await args.GetChannelAsync();
                        if (ch is null || ch.Type == DiscordChannelType.Stage)
                            message = $"Bot đã bị kick khỏi sân khấu <#{args.Before.ChannelId}>!";
                        if (channel is not null)
                            discordMessage = await channel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(message).WithColor(DiscordColor.Red).Build());
                        //else
                        //{
                        //    isDelete = true;
                        //    foreach (DiscordChannel ch in args.Guild.Channels.Values)
                        //    {
                        //        try
                        //        {
                        //            discordMessage = await ch.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(message).WithColor(DiscordColor.Red).Build());
                        //            break;
                        //        }
                        //        catch (Exception) { }
                        //    }
                        //}
                        //if (isDelete)
                        //{
                        //    await Task.Delay(3000);
                        //    if (discordMessage is not null)
                        //        await discordMessage.DeleteAsync();
                        //}
                        serverInstance.musicPlayer.isStopped = false;
                    }
                }
                else if (args.Before.ChannelId is null)
                {
                    //join
                    serverInstance.isDisconnect = false;
                    await VoiceNextConnection_UserChange(serverInstance.currentVoiceNextConnection, null);
                }
                else if (args.Before.ChannelId != args.After.ChannelId)
                {
                    //move
                    serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                    try
                    {
                        serverInstance.isDisconnect = false;
                        bool isPaused = serverInstance.musicPlayer.isPaused;
                        serverInstance.musicPlayer.isPaused = true;
                        await Task.Delay(600);
                        DiscordChannel? ch = await args.After.GetChannelAsync();
                        if (ch is not null)
                        {
                            VoiceNextConnection? connection = serverInstance.currentVoiceNextConnection ?? await serverInstance.GetVoiceConnection(ch);
                            connection?.Disconnect();
                            serverInstance.currentVoiceNextConnection = await serverInstance.GetVoiceConnection(ch);
                            serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                            serverInstance.musicPlayer.isPaused = isPaused;
                        }
                    }
                    catch (Exception ex) { Utils.LogException(ex); }
                    serverInstance.suppressOnVoiceStateUpdatedEvent = false;
                    await VoiceNextConnection_UserChange(serverInstance.currentVoiceNextConnection, null);
                }
                else if (args.After is not null)
                {
                    if (args.After.IsServerMuted || args.After.IsSuppressed)
                    {
                        if (!serverInstance.suppressOnVoiceStateUpdatedEvent)
                        {
                            //mute
                            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                            DiscordChannel? channel = serverInstance.GetLastChannel();
                            bool isDelete = false;
                            DiscordMessage? discordMessage = null;
                            string message = "Bot bị tắt tiếng! Nhạc sẽ được tạm dừng đến khi bot được bật tiếng!";
                            DiscordChannel? targetChannel = null;
                            if (args.After.ChannelId is not null)
                                targetChannel = await args.After.GetChannelAsync();
                            else if (serverInstance.currentVoiceNextConnection is not null)
                                targetChannel = serverInstance.currentVoiceNextConnection.TargetChannel;
                            if (targetChannel is not null && targetChannel.Type == DiscordChannelType.Stage && args.After.IsSuppressed)
                                message = "Bot đang là người nghe! Nhạc sẽ được tạm dừng đến khi bot được chuyển thành người nói!";
                            if (channel is not null)
                            {
                                discordMessage = await channel.SendMessageAsync(new DiscordEmbedBuilder().WithDescription(message).WithColor(DiscordColor.Orange).Build());
                            }
                            else
                            {
                                isDelete = true;
                                DiscordGuild? guild = await args.GetGuildAsync();
                                foreach (DiscordChannel ch in guild?.Channels.Values ?? [])
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
                                if (discordMessage is not null)
                                    await discordMessage.DeleteAsync();
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
            if (currentVoiceNextConnection is null)
                return;
            await currentVoiceNextConnection.GetTransmitSink().WriteAsync(new ReadOnlyMemory<byte>(data));
        }

        internal static void AddEvent(VoiceNextConnection voiceNextConnection)
        {
            //fck vps
            //voiceNextConnection.UserJoined += VoiceNextConnection_UserChange;
            //voiceNextConnection.UserLeft += VoiceNextConnection_UserChange; 
        }

        private static async Task VoiceNextConnection_UserChange(VoiceNextConnection? sender, DiscordEventArgs? args)
        {
            BotServerInstance? serverInstance = GetBotServerInstance(sender);
            if (serverInstance is not null)
                await serverInstance.CheckPeopleInVC();
        }

        async Task CheckPeopleInVC()
        {
            if (currentVoiceNextConnection is null)
                return;
            if (currentVoiceNextConnection.IsDisposed())
                return;
            int userCount = currentVoiceNextConnection.TargetChannel.Users.Where(u => !u.IsBot || u.IsBotExcluded()).Count() + 1;
            bool result = userCount >= 2;
            canSpeak = result;
            if (!result && lastNumberOfUsersInVC >= 2 && (musicPlayer.isPlaying || isVoicePlaying))
            {
                if (LastChannel is not null)
                {
                    IAsyncEnumerable<DiscordMessage> lastMessage = LastChannel.GetMessagesAsync(1);
                    if (lastNoOneInVCMessage is not null && lastNoOneInVCMessage == await lastMessage.ElementAtAsync(0))
                        return;
                    if (lastNoOneInVCMessage is not null)
                        await lastNoOneInVCMessage.DeleteAsync();
                }
                string message = "Không có người trong kênh thoại! Nhạc sẽ được tạm dừng cho đến khi có người khác vào kênh thoại!";
                if (currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                    message = "Không có người trong sân khấu! Nhạc sẽ được tạm dừng cho đến khi có người khác vào sân khấu!";
                DiscordChannel? discordChannel = GetLastChannel();
                if (discordChannel is not null)
                    lastNoOneInVCMessage = await discordChannel.SendMessageAsync(new DiscordEmbedBuilder().WithDescription(message).WithColor(DiscordColor.Orange).Build());
            }
            lastNumberOfUsersInVC = userCount;
        }

        internal static async Task ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreatedEventArgs args)
        {
            foreach (BotServerInstance instance in serverInstances)
            {
                if (instance.musicPlayer is null)
                    continue;
                await instance.musicPlayer.ButtonPressed(client, args);
            }
        }
    }
}
