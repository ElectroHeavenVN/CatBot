using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Extension;
using CatBot.Music;
using CatBot.Voice;
using DSharpPlus;
using DSharpPlus.Commands;
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
        DiscordChannel? lastChannel;
        DateTime lastTimeCheckVoiceChannel = DateTime.Now;
        Thread checkVoiceChannelThread;
        internal static List<BotServerInstance> serverInstances = new List<BotServerInstance>();
        internal MusicPlayerCore musicPlayer;
        internal DiscordGuild server;
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
        internal DiscordMember BotMember => botMember;
        DiscordMember botMember;
        private int lastNumberOfUsersInVC;
        DiscordMessage? lastNoOneInVCMessage;
        CancellationTokenSource ctsCheckVC = new CancellationTokenSource();

        internal BotServerInstance(DiscordGuild server)
        {
            this.server = server;
            botMember = server.GetMemberAsync(DiscordBotMain.botClient.CurrentUser.Id).Result;
            musicPlayer = new MusicPlayerCore(this);
            checkVoiceChannelThread = new Thread(CheckVoiceChannel);
        }

        async void CheckVoiceChannel()
        {
            while(true)
            {
                if (ctsCheckVC.IsCancellationRequested)
                    return;
                if (currentVoiceNextConnection != null && !currentVoiceNextConnection.IsDisposed())
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
                        Thread.Sleep(500);
						for (int i = musicPlayer.musicQueue.Count - 1; i >= 0; i--)
							musicPlayer.musicQueue.ElementAt(i).Dispose();
						musicPlayer.musicQueue.Clear();
						musicPlayer.isPlaying = false;
						isDisconnect = true;
						musicPlayer.isMainPlayRunning = false;
                        if (musicPlayer.lastNowPlayingMessage is not null)
                            await musicPlayer.lastNowPlayingMessage.DeleteAsync();
                        musicPlayer.lastNowPlayingMessage = null;
                        musicPlayer.sentOutOfTrack = true;
						musicPlayer.cts.Cancel();
						musicPlayer.cts = new CancellationTokenSource();
						isVoicePlaying = false;
						currentVoiceNextConnection.Disconnect();
						lastNumberOfUsersInVC = int.MaxValue;
						musicPlayer.playMode = new PlayMode();
						lastTimeCheckVoiceChannel = DateTime.Now;
                        canSpeak = true;
						Thread.Sleep(3000);
						suppressOnVoiceStateUpdatedEvent = false;
                    }
                }
                await Task.Delay(1000);
            }
        }

        internal async Task<bool> InitializeVoiceNext(CommandContext ctx, bool isInteractionDeferred = false)
        {
            VoiceNextConnection? connection = await GetVoiceConnection(ctx, isInteractionDeferred);
            if (connection == null)
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

        static async Task<VoiceNextConnection?> GetVoiceConnection(CommandContext ctx, bool isInteractionDeferred)
        {
            DiscordMember? member = ctx.Member;
            if (member is null)
                throw new Exception("Can't find member");
            BotServerInstance serverInstance = GetBotServerInstance(ctx.Guild);
            VoiceNextConnection? voiceNextConnection = null;
            double volume = serverInstance.currentVoiceNextConnection == null ? 1 : serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier;
            if (Utils.IsBotOwner(member.Id))
            {
                if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                    return serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
                else if (member.VoiceState == null || member.VoiceState.Channel is null)
                {
                    await ctx.ReplyAsync("Bot không ở trong kênh thoại nào trong server này!", isInteractionDeferred);
                    return null;
                }
            }
            if (member.VoiceState == null || member.VoiceState.Channel is null)
            {
                await ctx.ReplyAsync("Bạn không ở trong kênh thoại nào trong server này!", isInteractionDeferred);
                return null;
            }
            if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild))
                voiceNextConnection = serverInstances.First(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.IsDisposed() && ctx.Guild! == bSI.currentVoiceNextConnection.TargetChannel.Guild).currentVoiceNextConnection;
            else if (serverInstances.Any(bSI => bSI.currentVoiceNextConnection != null && !bSI.currentVoiceNextConnection.IsDisposed() && member.VoiceState.Guild is not null && member.VoiceState.Guild == bSI.currentVoiceNextConnection.TargetChannel.Guild))
            {
                await ctx.ReplyAsync("Bạn đang ở kênh thoại khác!", isInteractionDeferred);
                return null;
            }
            else
            {
                DiscordChannel voiceChannel = member.VoiceState.Channel;
                DiscordPermissions permissions = voiceChannel.PermissionsFor(serverInstance.botMember);
                if (permissions.HasPermission(DiscordPermissions.AccessChannels | DiscordPermissions.UseVoice))
                {
                    if (voiceChannel.Type == DiscordChannelType.Stage)
                        serverInstance.suppressOnVoiceStateUpdatedEvent = true;
                    voiceNextConnection = await voiceChannel.ConnectAsync();
                    if (voiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                    {
                        if (permissions.HasPermission(DiscordPermissions.MoveMembers) && serverInstance.botMember.VoiceState.IsSuppressed)
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
                    await ctx.ReplyAsync($"Bot bị thiếu quyền để kết nối tới {(member.VoiceState.Channel.Type == DiscordChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{member.VoiceState.Channel.Id}>!", isInteractionDeferred);
                    return null;
                }
            }
            return voiceNextConnection;
        }
    
        async Task<VoiceNextConnection?> GetVoiceConnection(DiscordChannel? voiceChannel)
        {
            if (voiceChannel is null)
                return null;
            if (voiceChannel.Type != DiscordChannelType.Voice || voiceChannel.Type != DiscordChannelType.Stage)
                return null;
            VoiceNextConnection? result = null;
            DiscordPermissions permissions = voiceChannel.PermissionsFor(botMember);
            if (permissions.HasPermission(DiscordPermissions.AccessChannels | DiscordPermissions.UseVoice))
            {
                if (voiceChannel.Type == DiscordChannelType.Stage)
                    suppressOnVoiceStateUpdatedEvent = true;
                result = await voiceChannel.ConnectAsync();
                if (result.TargetChannel.Type == DiscordChannelType.Stage)
                {
                    if (permissions.HasPermission(DiscordPermissions.MoveMembers) && botMember.VoiceState.IsSuppressed)
                        await botMember.UpdateVoiceStateAsync(result.TargetChannel, false);
                    suppressOnVoiceStateUpdatedEvent = false;
                }
                double volume = currentVoiceNextConnection == null ? 1 : currentVoiceNextConnection.GetTransmitSink().VolumeModifier;
                result.SetVolume(volume);
                lastTimeCheckVoiceChannel = DateTime.Now;
                lastNumberOfUsersInVC = int.MaxValue;
                AddEvent(result);
            }
            return result;
        }

        internal static BotServerInstance GetBotServerInstance(DiscordGuild? server)
        {
            if (server is null)
                throw new ArgumentNullException(nameof(server));
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.server?.Id == server.Id);
            if (botServerInstance == null)
            {
                botServerInstance = new BotServerInstance(server);
                botServerInstance.checkVoiceChannelThread.Start();
                serverInstances.Add(botServerInstance);
            }
            return botServerInstance;
        }

        internal static MusicPlayerCore? GetMusicPlayer(DiscordGuild server)
        {
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.server?.Id == server.Id);
            if (botServerInstance == null)
            {
                botServerInstance = new BotServerInstance(server);
                botServerInstance.checkVoiceChannelThread.Start();
                serverInstances.Add(botServerInstance);
            }
            return botServerInstance.musicPlayer;
        }

        internal static async Task RemoveBotServerInstance(ulong serverID)
        {
            for (int i = 0; i < serverInstances.Count; i++)
                if (serverInstances[i].server.Id == serverID)
                {
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
            if (voiceChannelSFX == null)
                throw new ArgumentNullException(nameof(voiceChannelSFX));
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.voiceChannelSFX == voiceChannelSFX);
            //if (botServerInstance == null)
            //{
            //    botServerInstance = new BotServerInstance();
            //    botServerInstance.voiceChannelSFX = voiceChannelSFX;
            //    serverInstances.Add(botServerInstance);
            //}
            if (botServerInstance == null)
                throw new ArgumentNullException(nameof(botServerInstance));
            return botServerInstance;
        }

        internal static BotServerInstance? GetBotServerInstance(VoiceNextConnection? voiceNextConnection)
        {
            if (voiceNextConnection == null)
                return null;
            BotServerInstance? botServerInstance = serverInstances.FirstOrDefault(s => s.currentVoiceNextConnection == voiceNextConnection);
            //if (botServerInstance == null)
            //{
            //    botServerInstance = new BotServerInstance();
            //    botServerInstance.currentVoiceNextConnection = voiceNextConnection;
            //    serverInstances.Add(botServerInstance);
            //}
            if (botServerInstance == null)
                throw new ArgumentNullException(nameof(botServerInstance));
            return botServerInstance;
        }

        internal static async Task<KeyValuePair<DiscordChannel?, VoiceNextConnection?>> JoinVoiceChannel(ulong channelID)
        {
            DiscordChannel? channel = null;
            VoiceNextConnection? voiceNextConnection = null;
            foreach (DiscordGuild server in DiscordBotMain.botClient.Guilds.Values)
                foreach (DiscordChannel voiceChannel in server.Channels.Values.Where(c => c.Type == DiscordChannelType.Voice))
                    if (voiceChannel.Id == channelID)
                    {
                        channel = voiceChannel;
                        BotServerInstance serverInstance = GetBotServerInstance(channel.Guild);
                        if (serverInstance.currentVoiceNextConnection != null && !serverInstance.currentVoiceNextConnection.IsDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == channel.GuildId)
                        {
                            serverInstance.currentVoiceNextConnection.Disconnect();
                            serverInstance.musicPlayer.playMode = new PlayMode();
                            serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                            await Task.Delay(300);
                        }
                        DiscordPermissions DiscordPermissions = channel.PermissionsFor(serverInstance.botMember);
                        if (DiscordPermissions.HasPermission(DiscordPermissions.AccessChannels | DiscordPermissions.UseVoice))
                        {
                            voiceNextConnection = await channel.ConnectAsync();
                            if (voiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage)
                            {
                                if (DiscordPermissions.HasPermission(DiscordPermissions.MoveMembers) && serverInstance.botMember.VoiceState.IsSuppressed)
                                    await serverInstance.botMember.UpdateVoiceStateAsync(voiceNextConnection.TargetChannel, false);
                            }
                            serverInstance.currentVoiceNextConnection = voiceNextConnection;
                            serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                            serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                            AddEvent(voiceNextConnection);
                        }
                        else
                        {
                            return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>();
                        }
                        return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>(channel, voiceNextConnection);
                    }
            return new KeyValuePair<DiscordChannel?, VoiceNextConnection?>(channel, voiceNextConnection);
        }

        internal static async Task<KeyValuePair<DiscordChannel?, bool>> LeaveVoiceChannel(ulong serverID)
        {
            DiscordChannel? channel = null;
            foreach (BotServerInstance serverInstance in serverInstances)
            {
                if (serverInstance.currentVoiceNextConnection != null && !serverInstance.currentVoiceNextConnection.IsDisposed() && serverInstance.currentVoiceNextConnection.TargetChannel.GuildId == serverID)
                {
                    channel = serverInstance.currentVoiceNextConnection.TargetChannel;
                    serverInstance.currentVoiceNextConnection.Disconnect();
                    serverInstance.musicPlayer.playMode = new PlayMode();
                    serverInstance.lastTimeCheckVoiceChannel = DateTime.Now;
                    await Task.Delay(200);
                    return new KeyValuePair<DiscordChannel?, bool>(channel, true);
                }
            }
            return new KeyValuePair<DiscordChannel?, bool>(channel, false);
        }

        internal static async Task SetVolume(CommandContext ctx, long volume)
        {
            BotServerInstance serverInstance = GetBotServerInstance(ctx.Guild);
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            if (serverInstance.currentVoiceNextConnection is null)
            {
                await ctx.RespondAsync("Bot không kết nối với kênh thoại nào!");
                return;
            }
            if (volume == -1)
            {
                string nl = Environment.NewLine;
                await ctx.RespondAsync($"Âm lượng hiện tại: {(int)(serverInstance.currentVoiceNextConnection.GetTransmitSink().VolumeModifier * 100)}{nl}Âm lượng SFX hiện tại: {(int)(serverInstance.voiceChannelSFX.volume * 100)}{nl}Âm lượng nhạc hiện tại: {(int)(serverInstance.musicPlayer.volume * 100)}");
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
            BotServerInstance serverInstance = GetBotServerInstance(args.Guild);
            if (args.User.Id != DiscordBotMain.botClient.CurrentUser.Id)
            {
                if (serverInstance.currentVoiceNextConnection == null || serverInstance.currentVoiceNextConnection.IsDisposed())
                    return;
                DiscordChannel? voiceChannel = null;
                if (args.Before != null && args.Before.Channel is not null)
                    voiceChannel = args.Before.Channel;
                else if (args.After != null && args.After.Channel is not null)
                    voiceChannel = args.After.Channel;
                if (voiceChannel is not null && voiceChannel == serverInstance.currentVoiceNextConnection.TargetChannel)
                    await serverInstance.CheckPeopleInVC();
                return;
            }
            if (serverInstance.suppressOnVoiceStateUpdatedEvent)
                return;
            try
            {
                if (args.After == null || args.After.Channel is null)
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
                        serverInstance.musicPlayer.sentOutOfTrack = true;
                        if (serverInstance.musicPlayer.lastNowPlayingMessage is not null)
                            await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                        serverInstance.musicPlayer.lastNowPlayingMessage = null;
                        serverInstance.musicPlayer.cts.Cancel();
                        serverInstance.musicPlayer.cts = new CancellationTokenSource();
                        serverInstance.isVoicePlaying = false;
                        serverInstance.lastNumberOfUsersInVC = int.MaxValue;
                        DiscordChannel? channel = serverInstance.GetLastChannel();
                        //bool isDelete = false;
                        DiscordMessage? discordMessage = null;
                        string message = $"Bot đã bị kick khỏi kênh thoại <#{args.Before.Channel?.Id}>!";
                        if (args.Before.Channel?.Type == DiscordChannelType.Stage)
                            message = $"Bot đã bị kick khỏi sân khấu <#{args.Before.Channel?.Id}>!";
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
                else if (args.Before == null || args.Before.Channel is null)
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
                        VoiceNextConnection? connection = serverInstance.currentVoiceNextConnection ?? await serverInstance.GetVoiceConnection(args.After.Channel);
                        connection?.Disconnect();
                        serverInstance.currentVoiceNextConnection = await serverInstance.GetVoiceConnection(args.After.Channel);
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
                            DiscordChannel? channel = serverInstance.GetLastChannel();
                            bool isDelete = false;
                            DiscordMessage? discordMessage = null;
                            string message = "Bot bị tắt tiếng! Nhạc sẽ được tạm dừng đến khi bot được bật tiếng!";
                            DiscordChannel? targetChannel = null;
                            if (args.After.Channel is not null)
                                targetChannel = args.After.Channel;
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

        private static async Task VoiceNextConnection_UserChange(VoiceNextConnection? sender, DiscordEventArgs? args)
        {
            BotServerInstance? serverInstance = GetBotServerInstance(sender);
            if (serverInstance != null)
                await serverInstance.CheckPeopleInVC();
        }

        async Task CheckPeopleInVC()
        {
            if (currentVoiceNextConnection == null)
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
                if (instance.musicPlayer == null)
                    continue;
                await instance.musicPlayer.ButtonPressed(client, args);
            }
        }
    }
}
