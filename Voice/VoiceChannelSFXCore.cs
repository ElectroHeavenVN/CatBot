using CatBot.Extension;
using CatBot.Instance;
using CatBot.Music;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

namespace CatBot.Voice
{
    internal class VoiceChannelSFXCore
    {
        internal bool isStop;
        internal int delay = 500;
        internal double volume = 1;

        internal static async Task Speak(CommandContext ctx, params string[] fileNames)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
            if (serverInstance.voiceChannelSFX == null)
                return;
            serverInstance.voiceChannelSFX.isStop = false;
            await serverInstance.voiceChannelSFX.InternalSpeak(ctx, fileNames);
        }

        internal static async Task Reconnect(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalReconnect(ctx);
        }

        internal static async Task StopSpeaking(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalStopSpeaking(ctx);
        }

        internal static async Task Dictionary(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
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
            if (!ctx.User.IsInAdminUser())
                embeds.Last().Description += Environment.NewLine + Environment.NewLine + "Dùng lệnh " + Config.gI().DefaultPrefix + "s <tên file> để bot nói!";
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embeds[0].Build()));
            foreach (DiscordEmbedBuilder embed in embeds.Skip(1))
            {
                await Task.Delay(200);
                await serverInstance.LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()));
            }
            if (ctx.User.IsInAdminUser())
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
                    await serverInstance.LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()));
                }
            }
        }

        internal static async Task Disconnect(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalDisconnect(ctx);
        }

        internal static async Task Delay(CommandContext ctx, int delayValue)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.LastChannel = ctx.Channel;
            if (serverInstance.voiceChannelSFX == null)
                return;
            await serverInstance.voiceChannelSFX.InternalDelay(ctx, delayValue);
        }

        internal static async Task SetVolume(CommandContext ctx, long volume)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (volume == -1)
                {
                    await ctx.RespondAsync("Âm lượng SFX hiện tại: " + (int)(serverInstance.voiceChannelSFX.volume * 100));
                    return;
                }
                if (volume < 0 || volume > 250)
                {
                    await ctx.RespondAsync("Âm lượng không hợp lệ!");
                    return;
                }
                serverInstance.voiceChannelSFX.volume = volume / 100d;
                await ctx.RespondAsync("Điều chỉnh âm lượng SFX thành: " + volume + "%!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        async Task InternalSpeak(CommandContext ctx, string[] fileNames)
        {
            if (BotServerInstance.GetBotServerInstance(this).isVoicePlaying)
            {
                await ctx.RespondAsync("Có người đang dùng lệnh rồi!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            MusicPlayerCore musicPlayer = BotServerInstance.GetMusicPlayer(ctx.Guild);
            VoiceTransmitSink transmitSink = serverInstance.currentVoiceNextConnection.GetTransmitSink();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = true;
            string filesNotFound = "";
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
                if (repeatTimes > 100)
                    repeatTimes = 100;
                FileStream file;
                try
                {
                    file = File.OpenRead(Path.Combine(Config.gI().SFXFolder, fileName.TrimEnd(',') + ".pcm"));
                }
                catch (IOException)
                {
                    if (ctx.User.IsInAdminUser())
                    {
                        try
                        {
                            file = File.OpenRead(Path.Combine(Config.gI().SFXFolderSpecial, fileName.TrimEnd(',') + ".pcm"));
                        }
                        catch (IOException)
                        {
                            filesNotFound += fileName.Replace(".pcm", "") + ", ";
                            continue;
                        }
                    }
                    else
                    {
                        filesNotFound += fileName.Replace(".pcm", "") + ", ";
                        continue;
                    }
                }
                if (musicPlayer.isPlaying && !musicPlayer.isPaused)
                {
                    byte[] buffer = new byte[file.Length + file.Length % 2];
                    file.Read(buffer, 0, (int)file.Length);
                    for (int j = 0; j < buffer.Length; j += 2)
                        Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, j) * volume)), 0, buffer, j, sizeof(short));
                    for (int j = 0; j < repeatTimes - 1; j++)
                    {
                        musicPlayer.sfxData.AddRange(buffer);
                        musicPlayer.sfxData.AddRange(new byte[2 * 16 * 48000 / 8 / 1000 * delay]);
                    }
                }
                else 
                {
                    for (int j = 0; j < repeatTimes - 1; j++)
                    {
                        await TransmitData(file, transmitSink, serverInstance);
                        if (isStop)
                            break;
                        await TransmitDelayData(2 * 16 * 48000 / 8 / 1000 * delay * 3 / 2, transmitSink, serverInstance);
                    }
                    if (isStop)
                    {
                        isStop = false;
                        break;
                    }
                }
                file.Close();
            }
            while (musicPlayer.isPlaying && !musicPlayer.isPaused && musicPlayer.sfxData.Count != 0)
                await Task.Delay(100);
            filesNotFound = filesNotFound.TrimEnd(',', ' ');
            string response = "";
            if (!string.IsNullOrWhiteSpace(filesNotFound))
                response += "Không tìm thấy file " + filesNotFound + "!";
            if (!string.IsNullOrEmpty(response))
                await ctx.RespondAsync(new DiscordFollowupMessageBuilder().WithContent(response));
            else 
                await ctx.DeleteReplyAsync();
            BotServerInstance.GetBotServerInstance(this).isVoicePlaying = false;
        }
        
        async Task InternalDisconnect(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            try
            {
                if (!await serverInstance.InitializeVoiceNext(ctx))
                    return;
                serverInstance.musicPlayer.isStopped = true;
                serverInstance.musicPlayer.isCurrentSessionLocked = false;
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
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            isStop = true;
            serverInstance.isVoicePlaying = false;
            await ctx.RespondAsync($"Đã ngắt kết nối {(serverInstance.currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{serverInstance.currentVoiceNextConnection.TargetChannel.Id}>!");
            serverInstance.currentVoiceNextConnection.Disconnect();
            serverInstance.musicPlayer.playMode = new PlayMode();
            await Task.Delay(1000);
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;
        }

        async Task InternalStopSpeaking(CommandContext ctx)
        {
            isStop = true;
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
            serverInstance.isVoicePlaying = false;
            serverInstance.musicPlayer.sfxData.Clear();
            if (ctx is TextCommandContext tctx)
                await tctx.Message.CreateReactionAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:"));
            else
                await ctx.RespondAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":white_check_mark:"));

        }

        async Task InternalReconnect(CommandContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            isStop = true;
            serverInstance.suppressOnVoiceStateUpdatedEvent = true;
            bool isPaused = serverInstance.musicPlayer.isPaused;
            serverInstance.musicPlayer.isPaused = true;
            await Task.Delay(600);
            serverInstance.currentVoiceNextConnection.Disconnect();
            serverInstance.currentVoiceNextConnection?.Dispose();
            if (!await serverInstance.InitializeVoiceNext(ctx))
                return;
            serverInstance.musicPlayer.isPaused = isPaused;
            serverInstance.suppressOnVoiceStateUpdatedEvent = false;

            isStop = false;
            await ctx.RespondAsync($"Đã kết nối lại với {(serverInstance.currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage ? "sân khấu" : "kênh thoại")} <#{serverInstance.currentVoiceNextConnection.TargetChannel.Id}>!");
        }

        async Task InternalDelay(CommandContext ctx, int delayValue)
        {
            if (delayValue < 0 || delayValue > 5000)
                await ctx.RespondAsync($"Delay không hợp lệ!");
            else
            {
                delay = delayValue;
                await ctx.RespondAsync($"Đã thay đổi delay thành {delay} mili giây!");
            }
        }

        async Task TransmitData(FileStream file, VoiceTransmitSink transmitSink, BotServerInstance serverInstance)
        {
            byte[] buffer = new byte[transmitSink.SampleLength];
            while (file.Read(buffer, 0, buffer.Length) != 0)
            {
                if (isStop || (serverInstance.musicPlayer.isPlaying && !serverInstance.musicPlayer.isPaused))
                    break;
                while (!serverInstance.canSpeak)
                    await Task.Delay(500);
                for (int i = 0; i < buffer.Length; i += 2)
                    Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, i) * volume)), 0, buffer, i, sizeof(short));
                await serverInstance.WriteTransmitData(buffer);
            }
            file.Position = 0;
        }

        async Task TransmitDelayData(int length, VoiceTransmitSink transmitSink, BotServerInstance serverInstance)
        {
            MemoryStream emptyData = new MemoryStream(new byte[length]);
            byte[] buffer = new byte[transmitSink.SampleLength];
            while (emptyData.Read(buffer, 0, buffer.Length) != 0)
            {
                if (isStop || (serverInstance.musicPlayer.isPlaying && !serverInstance.musicPlayer.isPaused))
                    break;
                while (!serverInstance.canSpeak)
                    await Task.Delay(500);
                for (int i = 0; i < buffer.Length; i += 2)
                    Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, i) * volume)), 0, buffer, i, sizeof(short));
                await serverInstance.WriteTransmitData(buffer);
            }
            emptyData.Close();
        }
    }
}
