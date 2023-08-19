using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Instance;
using DiscordBot.Music.Local;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Music
{
    internal class MusicPlayerCore
    {
        DiscordChannel m_lastChannel;
        internal CancellationTokenSource cts = new CancellationTokenSource();
        internal MusicQueue musicQueue = new MusicQueue();
        internal bool isPaused;
        internal bool isStopped;
        internal bool isSkipThisSong;
        internal bool sentOutOfTrack = true;
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
        internal bool isThreadAlive;
        internal IMusic currentlyPlayingSong;
        internal bool isPreparingNextSong;
        internal bool isPlaying;
        internal SponsorBlockOptions sponsorBlockOptions = new SponsorBlockOptions();
        internal List<byte> sfxData = new List<byte>();
        Thread prepareNextMusicStreamThread;
        internal double volume = 1;
        internal PlayMode playMode = new PlayMode();
        bool isDownloading;
        int currentIndex;
        int nextIndex = -1;
        Random random = new Random();
        bool isFirstTimeDequeue;

        internal static async Task Play(InteractionContext ctx, string input, MusicType musicType)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;

                DiscordEmbedBuilder embed;
                await ctx.DeferAsync();
                if (string.IsNullOrWhiteSpace(input))
                {
                    if (serverInstance.musicPlayer.musicQueue.Count == 0)
                    {
                        if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                        {
                            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Không có nhạc trong hàng đợi! Hãy thêm 1 bài vào hàng đợi bằng các lệnh phát nhạc!"));
                            return;
                        }
                        if (!serverInstance.musicPlayer.isStopped || !serverInstance.musicPlayer.isPaused)
                        {
                            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Hãy thêm 1 bài vào hàng đợi bằng các lệnh phát nhạc!"));
                            return;
                        }
                    }
                    serverInstance.musicPlayer.isStopped = false;
                    serverInstance.isDisconnect = false;
                    serverInstance.musicPlayer.isPaused = false;
                    serverInstance.musicPlayer.InitMainPlay();
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bắt đầu phát nhạc!"));
                    return;
                }
                try
                {
                    if (MusicUtils.TryCreateMusicPlaylistInstance(input, out IPlaylist playlist))
                    {
                        foreach (IMusic track in playlist.Tracks)
                            track.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                        serverInstance.musicPlayer.musicQueue.AddRange(playlist.Tracks);
                        serverInstance.musicPlayer.isStopped = false;
                        serverInstance.isDisconnect = false;
                        serverInstance.musicPlayer.InitMainPlay();
                        embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm {playlist.Tracks.Count} bài từ danh sách phát {playlist.Title} vào hàng đợi!");
                        DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                        playlist.AddFooter(embed2);
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                        return;
                    }
                }
                catch (WebException ex)
                { 
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                IMusic music = null;
                try
                {
                    if (!MusicUtils.TryCreateMusicInstance(input, out music))
                        music = MusicUtils.CreateMusicInstance(input, musicType);
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                serverInstance.musicPlayer.musicQueue.Enqueue(music);
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.isDisconnect = false;
                serverInstance.musicPlayer.InitMainPlay();
                embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!");
                music.AddFooter(embed);
                embed.Build();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Enqueue(InteractionContext ctx, string input, MusicType musicType)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;

                DiscordEmbedBuilder embed;
                await ctx.DeferAsync();
                try 
                { 
                    if (MusicUtils.TryCreateMusicPlaylistInstance(input, out IPlaylist playlist))
                    {
                        foreach (IMusic track in playlist.Tracks)
                            track.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                        serverInstance.musicPlayer.musicQueue.AddRange(playlist.Tracks);
                        embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm {playlist.Tracks.Count} bài từ danh sách phát {playlist.Title} vào hàng đợi!");
                        DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                        playlist.AddFooter(embed2);
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                        return;
                    }
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                IMusic music = null;
                try
                {
                    if (!MusicUtils.TryCreateMusicInstance(input, out music))
                        music = MusicUtils.CreateMusicInstance(input, musicType);
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                serverInstance.musicPlayer.musicQueue.Enqueue(music);
                embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!");
                music.AddFooter(embed);
                embed.Build();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task PlayNextUp(InteractionContext ctx, string input, MusicType musicType)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;

                DiscordEmbedBuilder embed;
                await ctx.DeferAsync();
                try 
                { 
                    if (MusicUtils.TryCreateMusicPlaylistInstance(input, out IPlaylist playlist))
                    {
                        foreach (IMusic track in playlist.Tracks)
                            track.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                        serverInstance.musicPlayer.musicQueue.InsertRange(0, playlist.Tracks);
                        serverInstance.musicPlayer.isStopped = false;
                        serverInstance.isDisconnect = false;
                        serverInstance.musicPlayer.InitMainPlay();
                        embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm {playlist.Tracks.Count} bài từ danh sách phát {playlist.Title} vào hàng đợi!");
                        DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                        playlist.AddFooter(embed2);
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                        return;
                    }
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                IMusic music = null;
                try
                {
                    if (!MusicUtils.TryCreateMusicInstance(input, out music))
                        music = MusicUtils.CreateMusicInstance(input, musicType);
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), input)));
                    return;
                }
                music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
                serverInstance.musicPlayer.musicQueue.Peek().DeletePCMFile();
                serverInstance.musicPlayer.musicQueue.Insert(0, music);
                serverInstance.musicPlayer.isPreparingNextSong = false;
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.isDisconnect = false;
                serverInstance.musicPlayer.InitMainPlay();
                embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào đầu hàng đợi!");
                music.AddFooter(embed);
                embed.Build();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task PlayRandomLocalMusic(InteractionContext ctx, long count)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;

                await ctx.DeferAsync();
                Random random = new Random();
                FileInfo[] musicFiles = new DirectoryInfo(Config.MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToArray();
                IMusic music = null;
                for (int i = 0; i < count; i++)
                {
                    string musicFileName = Path.GetFileNameWithoutExtension(musicFiles[random.Next(0, musicFiles.Length)].Name);
                        music = MusicUtils.CreateMusicInstance(musicFileName, MusicType.Local);
                    serverInstance.musicPlayer.musicQueue.Enqueue(music);
                }
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.isDisconnect = false;
                serverInstance.musicPlayer.InitMainPlay();
                if (count == 1)
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!").Build()));
                else 
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithDescription($"Đã thêm {count} bài vào hàng đợi!").Build()));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task PlayAllLocalMusic(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                await ctx.DeferAsync();
                List<FileInfo> musicFiles2 = new DirectoryInfo(Config.MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
                musicFiles2.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
                foreach (FileInfo musicFile in musicFiles2)
                    serverInstance.musicPlayer.musicQueue.Enqueue(MusicUtils.CreateMusicInstance(musicFile.Name, MusicType.Local));
                serverInstance.musicPlayer.isPaused = false;
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.isDisconnect = false;
                serverInstance.musicPlayer.InitMainPlay();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle($"Đã thêm {musicFiles2.Count} bài vào hàng đợi! Hiện tại hàng đợi có {serverInstance.musicPlayer.musicQueue.Count} bài!").Build()));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task NowPlaying(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                    await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
                else
                {
                    await ctx.DeferAsync();
                    string musicDesc = serverInstance.musicPlayer.currentlyPlayingSong.GetSongDesc(true);
                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
                    serverInstance.musicPlayer.currentlyPlayingSong.AddFooter(embed);
                    string albumThumbnailLink = serverInstance.musicPlayer.currentlyPlayingSong.AlbumThumbnailLink;
                    if (!string.IsNullOrEmpty(albumThumbnailLink))
                    {
                        DiscordMessage message = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.WithThumbnail(albumThumbnailLink).Build()));
                        if (serverInstance.musicPlayer.currentlyPlayingSong is LocalMusic localMusic)
                            await localMusic.lastCacheImageMessage.ModifyAsync(message.JumpLink.ToString());
                    }
                    else
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()));
                }
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Seek(InteractionContext ctx, long seconds)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                {
                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                    return;
                }
                try
                {
                    int bytesPerSeconds = 2 * 16 * 48000 / 8;
                    int bytesToSeek = (int)Math.Max(Math.Min(bytesPerSeconds * seconds, serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Length - serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position), -serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position);
                    bytesToSeek -= bytesToSeek % 2;
                    serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position += bytesToSeek;
                    await ctx.CreateResponseAsync($"Đã tua {(bytesToSeek < 0 ? "lùi " : "")}bài hiện tại {new TimeSpan(0, 0, Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!");
                }
                catch (WebException ex)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(string.Format(GetErrorMessage(ex), serverInstance.musicPlayer.currentlyPlayingSong.Title)));
                    return;
                }
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Clear(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.musicQueue.Count == 0)
                {
                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                    return;
                }
                for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                {
                    IMusic music = serverInstance.musicPlayer.musicQueue.ElementAt(i);
                    if (serverInstance.musicPlayer.currentlyPlayingSong != music)
                        music.Dispose();
                }
                serverInstance.musicPlayer.musicQueue.Clear();
                if (serverInstance.musicPlayer.playMode.isLoopQueue || serverInstance.musicPlayer.playMode.isLoopASong)
                    serverInstance.musicPlayer.musicQueue.Add(serverInstance.musicPlayer.currentlyPlayingSong);
                serverInstance.musicPlayer.isPreparingNextSong = false;
                if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                    serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
                serverInstance.musicPlayer.isPreparingNextSong = false;
                await ctx.CreateResponseAsync("Đã xóa hết nhạc trong hàng đợi!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Pause(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                {
                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                    return;
                }
                serverInstance.musicPlayer.isPaused = true;
                await ctx.CreateResponseAsync("Tạm dừng phát nhạc!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Resume(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                {
                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                    return;
                }
                serverInstance.musicPlayer.isPaused = false;
                serverInstance.musicPlayer.isStopped = false;
                await ctx.CreateResponseAsync("Tiếp tục phát nhạc!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }
 
        internal static async Task Skip(InteractionContext ctx, long count)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                {
                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                    return;
                }
                if (serverInstance.musicPlayer.playMode.isRandom && count > 1)
                {
                    await ctx.CreateResponseAsync($"Không thể bỏ qua {count} bài nhạc khi đang phát ngẫu nhiên!");
                    return;
                }
                serverInstance.musicPlayer.isPaused = false;
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.musicPlayer.isSkipThisSong = true;
                count = Math.Min(count, serverInstance.musicPlayer.musicQueue.Count - serverInstance.musicPlayer.currentIndex);
                if (count > 1)
                {
                    serverInstance.musicPlayer.isPreparingNextSong = false;
                    if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                        serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
                }
                if (serverInstance.musicPlayer.playMode.isRandom)
                    serverInstance.musicPlayer.RandomIndex();
                else if (serverInstance.musicPlayer.playMode.isLoopQueue)
                {
                    serverInstance.musicPlayer.currentIndex += (int)count - 1;
                    if (serverInstance.musicPlayer.playMode.isLoopASong)
                    {
                        serverInstance.musicPlayer.currentIndex++;
                        if (serverInstance.musicPlayer.currentIndex >= serverInstance.musicPlayer.musicQueue.Count)
                            serverInstance.musicPlayer.currentIndex = 0;
                    }
                }
                else 
                {
                    for (int i = 0; i < count - 1; i++)
                    {
                        int index = serverInstance.musicPlayer.currentIndex;
                        if (index >= serverInstance.musicPlayer.musicQueue.Count)
                        {
                            serverInstance.musicPlayer.currentIndex = 0;
                            break;
                        }
                        IMusic music = serverInstance.musicPlayer.musicQueue.DequeueAt(index);
                        if (music != serverInstance.musicPlayer.currentlyPlayingSong)
                            music.Dispose();
                    }
                }
                await ctx.CreateResponseAsync($"Đã bỏ qua {(count > 1 ? (count.ToString() + " bài nhạc") : "bài nhạc hiện tại")}!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Remove(InteractionContext ctx,long startIndex, long count)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (startIndex >= serverInstance.musicPlayer.musicQueue.Count)
                {
                    await ctx.CreateResponseAsync($"Hàng đợi chỉ có {serverInstance.musicPlayer.musicQueue.Count} bài!");
                    return;
                }
                count = Math.Min(count, serverInstance.musicPlayer.musicQueue.Count - startIndex);
                int countRemoved = 0;
                bool hasCurrentSong = false;
                for (int i = 0; i < count; i++)
                {
                    int index = (int)startIndex + (hasCurrentSong ? 1 : 0);
                    if (index >= serverInstance.musicPlayer.musicQueue.Count)
                        break;
                    IMusic music = serverInstance.musicPlayer.musicQueue[index];
                    if (music != serverInstance.musicPlayer.currentlyPlayingSong)
                    {
                        music.Dispose();
                        serverInstance.musicPlayer.musicQueue.RemoveAt(index);
                        countRemoved++;
                    }
                    else
                    {
                        hasCurrentSong = true;
                        count++;
                    }
                }
                if (startIndex == serverInstance.musicPlayer.currentIndex + 1 || hasCurrentSong)
                {
                    serverInstance.musicPlayer.isPreparingNextSong = false;
                    if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                        serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
                }
                await ctx.CreateResponseAsync($"Đã xóa {countRemoved} bài nhạc khỏi hàng đợi!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Stop(InteractionContext ctx, string clearQueueStr)
        {
            try
            {
                if (!bool.TryParse(clearQueueStr, out bool clearQueue))
                    return;
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                {
                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                    return;
                }
                serverInstance.musicPlayer.isPaused = false;
                serverInstance.musicPlayer.isStopped = true;
                await Task.Delay(500);
                string response = "Dừng phát nhạc";
                if (clearQueue)
                {
                    serverInstance.musicPlayer.isPreparingNextSong = false;
                    if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                        serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
                    for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                        serverInstance.musicPlayer.musicQueue.ElementAt(i)?.Dispose();
                    serverInstance.musicPlayer.musicQueue.Clear();
                    serverInstance.musicPlayer.isPreparingNextSong = false;
                    response += " và xóa hàng đợi";
                }
                await ctx.CreateResponseAsync(response + "!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task SetVolume(SnowflakeObject obj, long volume)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(obj.TryGetChannel().Guild);
                if (volume == -1)
                {
                    await obj.TryRespondAsync("Âm lượng nhạc hiện tại: " + (int)(serverInstance.musicPlayer.volume * 100));
                    return;
                }
                if (volume < 0 || volume > 250)
                {
                    await obj.TryRespondAsync("Âm lượng không hợp lệ!");
                    return;
                }
                serverInstance.musicPlayer.volume = volume / 100d;
                await obj.TryRespondAsync("Điều chỉnh âm lượng nhạc thành: " + volume + "%!");
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Queue(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.musicQueue.Count == 0)
                {
                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                    return;
                }
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = $"{Math.Min(10, serverInstance.musicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.musicPlayer.musicQueue.Count})",
                };
                for (int i = 0; i < Math.Min(10, serverInstance.musicPlayer.musicQueue.Count); i++)
                    embed.Description += $"{i + 1}. {serverInstance.musicPlayer.musicQueue.ElementAt(i).GetIcon()} {serverInstance.musicPlayer.musicQueue.ElementAt(i).Title} - {serverInstance.musicPlayer.musicQueue.ElementAt(i).Artists}{Environment.NewLine}";
                embed.Build();
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task SetPlayMode(InteractionContext ctx, PlayModeChoice playMode)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                switch(playMode)
                {
                    case PlayModeChoice.Queue:
                        serverInstance.musicPlayer.playMode.isLoopQueue = false;
                        break;
                    case PlayModeChoice.LoopQueue:
                        if (serverInstance.musicPlayer.currentlyPlayingSong != null && !serverInstance.musicPlayer.musicQueue.Contains(serverInstance.musicPlayer.currentlyPlayingSong))
                            serverInstance.musicPlayer.musicQueue.Insert(0, serverInstance.musicPlayer.currentlyPlayingSong);
                        serverInstance.musicPlayer.playMode.isLoopQueue = true;
                        break;
                    case PlayModeChoice.Incremental:
                        serverInstance.musicPlayer.playMode.isRandom = false;
                        break;
                    case PlayModeChoice.Random:
                        serverInstance.musicPlayer.playMode.isRandom = true;
                        break;
                    case PlayModeChoice.DontLoopSong:
                        serverInstance.musicPlayer.playMode.isLoopASong = false;
                        break;
                    case PlayModeChoice.LoopASong:
                        if (serverInstance.musicPlayer.currentlyPlayingSong != null && !serverInstance.musicPlayer.musicQueue.Contains(serverInstance.musicPlayer.currentlyPlayingSong))
                            serverInstance.musicPlayer.musicQueue.Insert(0, serverInstance.musicPlayer.currentlyPlayingSong);
                        serverInstance.musicPlayer.playMode.isLoopASong = true;
                        break;
                }
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Thay đổi chế độ phát thành: " + playMode.GetName()));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task ShuffleQueue(InteractionContext ctx)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                if (serverInstance.musicPlayer.musicQueue.Count == 0)
                {
                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                    return;
                }
                serverInstance.musicPlayer.musicQueue.Shuffle();
                serverInstance.musicPlayer.isPreparingNextSong = false;
                if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                    serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = $"{Math.Min(10, serverInstance.musicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.musicPlayer.musicQueue.Count})",
                };
                for (int i = 0; i < Math.Min(10, serverInstance.musicPlayer.musicQueue.Count); i++)
                    embed.Description += $"{i + 1}. {serverInstance.musicPlayer.musicQueue.ElementAt(i).GetIcon()} {serverInstance.musicPlayer.musicQueue.ElementAt(i).Title} - {serverInstance.musicPlayer.musicQueue.ElementAt(i).Artists}{Environment.NewLine}";
                embed.Build();
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!").AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task Lyric(InteractionContext ctx, string songName, string artistsName)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                await ctx.DeferAsync();
                string jsonLyric = "";
                LyricData lyricData = null;
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
                if (string.IsNullOrWhiteSpace(songName))
                {
                    if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Không có bài nào đang phát!"));
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(serverInstance.musicPlayer.currentlyPlayingSong.Title))
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bài hát đang phát không có tiêu đề!"));
                        return;
                    }
                    lyricData = serverInstance.musicPlayer.currentlyPlayingSong.GetLyric();
                    if (!string.IsNullOrEmpty(lyricData.NotFoundMessage))
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(lyricData.NotFoundMessage));
                        return;
                    }
                    embed = serverInstance.musicPlayer.currentlyPlayingSong.AddFooter(embed);
                    if (serverInstance.musicPlayer.currentlyPlayingSong is LocalMusic)
                        embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");    //You may need to change this
                }
                else
                {
                    string apiEndpoint = Config.LyricAPI + songName;
                    if (!string.IsNullOrWhiteSpace(artistsName))
                        apiEndpoint += "/" + artistsName;
                    jsonLyric = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(apiEndpoint));
                    JObject jsonLyricData = JObject.Parse(jsonLyric);
                    if (!jsonLyricData.ContainsKey("lyrics"))
                    {
                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Không tìm thấy lời bài hát!"));
                        return;
                    }
                    lyricData = new LyricData(jsonLyricData["title"].ToString(), jsonLyricData["artist"].ToString(), jsonLyricData["lyrics"].ToString(), jsonLyricData["image"].ToString());
                    embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");    //You may need to change this
                }
                embed = embed.WithTitle($"Lời bài hát {lyricData.Title} - {lyricData.Artists}").WithDescription(lyricData.Lyric).WithThumbnail(lyricData.AlbumThumbnailLink);
                embed.Build();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        internal static async Task AddOrRemoveSponsorBlockOption(InteractionContext ctx, SponsorBlockSectionType type)
        {
            try
            {
                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
                if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                    return;
                serverInstance.musicPlayer.lastChannel = ctx.Channel;
                string str = "";
                if (type == 0)
                {
                    if (!serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                        str = "Chức năng bỏ qua phân đoạn SponsorBlock đang bị tắt!";
                    else 
                        str = "Các phân đoạn thuộc loại sau sẽ bị bỏ qua: " + serverInstance.musicPlayer.sponsorBlockOptions.GetName();
                }
                else 
                {
                    if (type == SponsorBlockSectionType.All)
                    {
                        if (serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                            serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(type);
                        else 
                            serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(0);
                    }
                    serverInstance.musicPlayer.sponsorBlockOptions.AddOrRemoveOptions(type);
                    str = $"Đã {(serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) ? "thêm" : "xóa")} {(type == SponsorBlockSectionType.All ? "tất cả loại phân đoạn" : $"loại phân đoạn \"{type.GetName()}\"")} {(serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) ? "vào" : "khỏi")} danh sách bỏ qua!";
                    if (!serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) && !serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                        str += Environment.NewLine + $"Không có loại phân đoạn nào để bỏ qua, tắt chức năng bỏ qua phân đoạn SponsorBlock!";
                }
                await ctx.CreateResponseAsync(str);
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        async Task MainPlay(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
                    if (musicQueue.Count > 0)
                    {
                        isPlaying = true;
                        sentOutOfTrack = false;
                        isPreparingNextSong = false;
                        try
                        {
                            if (!playMode.isLoopASong && !playMode.isLoopQueue && !musicQueue.Contains(currentlyPlayingSong))
                            {
                                currentlyPlayingSong?.Dispose();
                                currentlyPlayingSong = null;
                            }
                            currentlyPlayingSong = GetCurrentSong();
                            nextIndex = -1;
                            if (currentlyPlayingSong.MusicPCMDataStream == null && !isDownloading)
                                currentlyPlayingSong.Download();
                            while (isDownloading)
                                await Task.Delay(200);
                            currentlyPlayingSong.MusicPCMDataStream.Position = 0;
                            string musicDesc = currentlyPlayingSong.GetSongDesc();
                            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
                            currentlyPlayingSong.AddFooter(embed);
                            string albumThumbnailLink = currentlyPlayingSong.AlbumThumbnailLink;
                            if (!string.IsNullOrEmpty(albumThumbnailLink))
                            {
                                DiscordMessage message = await lastChannel.SendMessageAsync(embed.WithThumbnail(albumThumbnailLink).Build());
                                if (currentlyPlayingSong is LocalMusic localMusic)
                                    await localMusic.lastCacheImageMessage.ModifyAsync(message.JumpLink.ToString());
                            }
                            else
                                await lastChannel.SendMessageAsync(embed.Build());
                        }
                        catch (WebException ex)
                        {
                            await lastChannel.SendMessageAsync(GetErrorMessage(ex));
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Utils.LogException(ex);
                            await lastChannel.SendMessageAsync($"Có lỗi xảy ra!");
                            continue;
                        }
                        byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
                        while (currentlyPlayingSong.MusicPCMDataStream.Read(buffer, 0, buffer.Length) != 0)
                        {
                            if (token.IsCancellationRequested)
                                goto exit;
                            if (isStopped || isSkipThisSong || serverInstance.currentVoiceNextConnection.isDisposed())
                                break;
                            if (!isPreparingNextSong && musicQueue.Count > 0 && currentlyPlayingSong.Duration.Ticks * (1 - currentlyPlayingSong.MusicPCMDataStream.Position / (float)currentlyPlayingSong.MusicPCMDataStream.Length) <= 300000000)
                            {
                                isPreparingNextSong = true;
                                prepareNextMusicStreamThread = new Thread(PrepareNextSong) { IsBackground = true };
                                prepareNextMusicStreamThread.Start();
                            }
                            while (isPaused)
                                await Task.Delay(500);
                            tryagain:;
                            try
                            {
                                if (serverInstance.currentVoiceNextConnection.isDisposed())
                                {
                                    await Task.Delay(500);
                                    continue;
                                }
                                int i = 0;
                                if (sfxData.Count > 0)
                                {
                                    byte[] data = sfxData.ToArray();
                                    for (; i < buffer.Length && i < data.Length; i += 2)
                                    {
                                        int a = (int)(BitConverter.ToInt16(buffer, i) * volume) + 32768;
                                        int b = BitConverter.ToInt16(data, i) + 32768;
                                        int m = 0;
                                        if (a < 32768 || b < 32768)
                                            m = a * b / 32768;
                                        else
                                            m = 2 * (a + b) - a * b / 32768 - 65536;
                                        if (m == 65536) 
                                            m = 65535;
                                        m -= 32768;
                                        Array.Copy(BitConverter.GetBytes((short)m), 0, buffer, i, sizeof(short));
                                    }
                                    sfxData.RemoveRange(0, Math.Min(buffer.Length, data.Length));
                                }
                                for (; i < buffer.Length; i += 2)
                                        Array.Copy(BitConverter.GetBytes((short)(BitConverter.ToInt16(buffer, i) * volume)), 0, buffer, i, sizeof(short));
                                await serverInstance.currentVoiceNextConnection.GetTransmitSink().WriteAsync(new ReadOnlyMemory<byte>(buffer));
                            }
                            catch (Exception ex)
                            {
                                Utils.LogException(ex);
                                goto tryagain;
                            }
                        }
                        if (token.IsCancellationRequested)
                            goto exit;
                        if (isSkipThisSong)
                        {
                            isSkipThisSong = false;
                            await Task.Delay(500);
                        }
                        if (isStopped)
                            await Task.Delay(500);
                        if (serverInstance.currentVoiceNextConnection.isDisposed())
                            sentOutOfTrack = true;
                    }
                    else
                    {
                        isPlaying = false;
                        currentlyPlayingSong?.Dispose();
                        currentlyPlayingSong = null;
                        if (!sentOutOfTrack)
                        {
                            if (token.IsCancellationRequested)
                                goto exit;
                            sentOutOfTrack = true;
                            await lastChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build());
                        }
                    }
                    if (token.IsCancellationRequested)
                        goto exit;
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
            }
        exit:;
            currentlyPlayingSong.Dispose();
            isPlaying = false;
            isThreadAlive = false;
        }

        void PrepareNextSong()
        {
            if (musicQueue.Count == 0)
                return;
            if (playMode.isLoopASong)
                return;
            if (musicQueue.Count <= 1 && playMode.isLoopQueue)
                return;
            try
            {
                IMusic nextSong = GetNextSong();
                isDownloading = true;
                nextSong.Download();
                while (nextSong.MusicPCMDataStream == null)
                    Thread.Sleep(200);
                nextSong.MusicPCMDataStream.Position = 0;
            }
            catch (Exception ex) { Utils.LogException(ex); }
            isDownloading = false;
            GC.Collect();
        }

        void InitMainPlay()
        {
            if (!isThreadAlive)
            {
                isThreadAlive = true;
                new Thread(async() => await MainPlay(cts.Token)) { IsBackground = true }.Start();
            }
        }

        static string GetErrorMessage(WebException ex)
        {
            string content;
            if (ex.Message.StartsWith("-1110"))
                content = $"Bài này bị Zing MP3 chặn ở quốc gia đặt máy chủ của bot!";
            if (ex.Message == "Ex: songs not found")
                content = "Không tìm thấy bài \"{0}\"!";
            else if (ex.Message == "Ex: not found")
                content = "Không tìm thấy bài này!";
            else if (ex.Message == "NCT: not available")
                content = "Bài này bị NhacCuaTui chặn ở quốc gia đặt máy chủ của bot!";
            else if (ex.Message == "YT: video not found")
                content = "Không tìm thấy video này!";
            else if (ex.Message == "Ex: playlist not found")
                content = "Không tìm thấy danh sách phát này!";
            else if (ex.Message == "YT: channel not found")
                content = "Không tìm thấy kênh này!";
            else if (ex.Message == "Ex: artist not found")
                content = "Không tìm thấy nghệ sĩ này!";
            else if (ex.Message == "SC: invalid short link")
                content = "Link SoundCloud không hợp lệ!";
            else
                content = ex.ToString();
            return content;
        }

        void RandomIndex() => currentIndex = random.Next(0, musicQueue.Count);

        IMusic GetCurrentSong()
        {
            if (nextIndex != -1)
                currentIndex = nextIndex;
            else
            {
                if (playMode.isRandom)
                    RandomIndex();
                else if (playMode.isLoopQueue && !playMode.isLoopASong && !isFirstTimeDequeue)
                    currentIndex++;
            }
            isFirstTimeDequeue = false;
            if (currentIndex >= musicQueue.Count)
                currentIndex = 0;
            if (!playMode.isLoopQueue && !playMode.isLoopASong)
                return musicQueue.DequeueAt(currentIndex);
            return musicQueue[currentIndex];
        }

        IMusic GetNextSong()
        {
            if (playMode.isRandom)
                nextIndex = random.Next(0, musicQueue.Count);
            else if (playMode.isLoopQueue)
                nextIndex = currentIndex + 1;
            else if (playMode.isLoopASong)
                nextIndex = currentIndex;
            if (nextIndex >= musicQueue.Count)
                nextIndex = 0;
            if (nextIndex < 0)
                nextIndex = 0;
            return musicQueue[nextIndex];
        }
    }
}
