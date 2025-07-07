using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Extensions;
using CatBot.Instance;
using CatBot.Music.Dummy;
using CatBot.Music.Local;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.VoiceNext;

namespace CatBot.Music
{
    internal class MusicPlayerCore
    {
        internal bool isPaused;
        internal bool isStopped;
        internal bool isSkipThisSong;
        internal bool sentOutOfTrack = true;
        internal bool isMainPlayRunning;
        internal bool isPreparingNextSong;
        internal bool isPlaying;
        internal bool isCurrentSessionLocked;
        internal long lastTimeSetLastChannel;
        internal double volume = .75;
        internal List<byte> sfxData = new List<byte>();
        internal IMusic? currentlyPlayingSong;
        internal PlayMode playMode = new PlayMode();
        internal MusicQueue musicQueue = new MusicQueue();
        internal SponsorBlockOptions sponsorBlockOptions = new SponsorBlockOptions();
        internal CancellationTokenSource cts = new CancellationTokenSource();
        internal CancellationTokenSource ctsPrepareNextSong = new CancellationTokenSource();
        internal DiscordChannel? LastChannel
        {
            get => lastChannel;
            set
            {
                lastTimeSetLastChannel = DateTime.Now.Ticks;
                lastChannel = value;
            }
        }
        internal DiscordMessage? lastNowPlayingMessage;
        
        bool isPrevious;
        bool isDownloading;
        bool isFirstTimeDequeue;
        bool lyricsShown;
        bool isDeleteBrowseQueueButtonThreadRunning;
        bool isSetVCStatus;
        bool isSongDownloaded;
        int currentIndex;
        int nextIndex = -1;
        int currentQueuePage = 1;
        long lastStreamPosition;
        string uniqueID = Utils.RandomString(10);
        DateTime lastTimeRefresh = DateTime.Now;
        DateTime lastTimeChangePageQueue = DateTime.MinValue;
        Random random = new Random();
        Thread? prepareNextMusicStreamThread;
        BotServerInstance serverInstance;
        LyricData? lyricsFromLRCLIB;
        CancellationTokenSource addSongsInPlaylistCTS = new CancellationTokenSource();
        DiscordEmbedBuilder? lastCurrentlyPlayingEmbed;
        DiscordChannel? lastChannel;
        DiscordMessage? browseQueueMessage;
        DiscordMessage? viewLyricsMessage;
        List<DiscordMessage?> lyricsMessages = new List<DiscordMessage?>();

        internal MusicPlayerCore(BotServerInstance serverInstance)
        {
            this.serverInstance = serverInstance;
        }

        internal static async Task Play(CommandContext ctx, string input, MusicType musicType)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            DiscordEmbedBuilder embed;
            if (string.IsNullOrWhiteSpace(input))
            {
                if (serverInstance.musicPlayer.musicQueue.Count == 0)
                {
                    if (serverInstance.musicPlayer.currentlyPlayingSong is null)
                    {
                        await ctx.FollowupAsync("Không có nhạc trong hàng đợi! Hãy thêm 1 bài vào hàng đợi bằng các lệnh phát nhạc!");
                        return;
                    }
                    if (!serverInstance.musicPlayer.isStopped || !serverInstance.musicPlayer.isPaused)
                    {
                        await ctx.FollowupAsync("Hãy thêm 1 bài vào hàng đợi bằng các lệnh phát nhạc!");
                        return;
                    }
                }
                serverInstance.musicPlayer.isStopped = false;
                serverInstance.isDisconnect = false;
                serverInstance.musicPlayer.isPaused = false;
                serverInstance.musicPlayer.InitMainPlay();
                await ctx.FollowupAsync("Bắt đầu phát nhạc!");
                return;
            }
            try
            {
                if (MusicUtils.TryCreateMusicPlaylistInstance(input, serverInstance.musicPlayer.musicQueue, out IPlaylist playlist))
                {
                    playlist.SetSponsorBlockOptions(serverInstance.musicPlayer.sponsorBlockOptions);
                    playlist.AddSongsInPlaylistCTS = serverInstance.musicPlayer.addSongsInPlaylistCTS;
                    serverInstance.musicPlayer.isStopped = false;
                    serverInstance.isDisconnect = false;
                    serverInstance.musicPlayer.InitMainPlay();
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
                    return;
                }
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            IMusic music;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
            serverInstance.musicPlayer.musicQueue.Enqueue(music);
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.isDisconnect = false;
            serverInstance.musicPlayer.InitMainPlay();
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.TitleWithLink} - {music.AllArtistsWithLinks} vào hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowupAsync(embed);
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task Enqueue(CommandContext ctx, string input, MusicType musicType)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            DiscordEmbedBuilder embed;
            try
            {
                if (MusicUtils.TryCreateMusicPlaylistInstance(input, serverInstance.musicPlayer.musicQueue, out IPlaylist playlist))
                {
                    playlist.SetSponsorBlockOptions(serverInstance.musicPlayer.sponsorBlockOptions);
                    playlist.AddSongsInPlaylistCTS = serverInstance.musicPlayer.addSongsInPlaylistCTS;
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
                    return;
                }
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            IMusic music;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
            serverInstance.musicPlayer.musicQueue.Enqueue(music);
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.TitleWithLink} - {music.AllArtistsWithLinks} vào hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowupAsync(embed);
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayNextUp(CommandContext ctx, string input, MusicType musicType)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            DiscordEmbedBuilder embed;
            try
            {
                if (MusicUtils.TryCreateMusicPlaylistInstance(input, serverInstance.musicPlayer.musicQueue, out IPlaylist playlist))
                {
                    playlist.SetSponsorBlockOptions(serverInstance.musicPlayer.sponsorBlockOptions);
                    playlist.AddSongsInPlaylistCTS = serverInstance.musicPlayer.addSongsInPlaylistCTS;
                    serverInstance.musicPlayer.isStopped = false;
                    serverInstance.isDisconnect = false;
                    serverInstance.musicPlayer.InitMainPlay();
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
                    return;
                }
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            IMusic music;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, input, ex);
                return;
            }
            music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
            serverInstance.musicPlayer.musicQueue.Insert(0, music);
            serverInstance.musicPlayer.isPreparingNextSong = false;
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.isDisconnect = false;
            serverInstance.musicPlayer.InitMainPlay();
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.TitleWithLink} - {music.AllArtistsWithLinks} vào đầu hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowupAsync(embed);
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayRandomLocalMusic(CommandContext ctx, long count)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            Random random = new Random();
            FileInfo[] musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToArray();
            IMusic? music = null;
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
                await ctx.FollowupAsync(new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music?.TitleWithLink}  -  {music?.AllArtistsWithLinks} vào hàng đợi!").Build());
            else
                await ctx.FollowupAsync(new DiscordEmbedBuilder().WithDescription($"Đã thêm {count} bài vào hàng đợi!").Build());
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayAllLocalMusic(CommandContext ctx, string search = "")
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            search = search.ToLower();
            List<FileInfo> musicFiles2 = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            musicFiles2.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            int count = 0;
            foreach (FileInfo musicFile in musicFiles2)
            {
                IMusic music = MusicUtils.CreateMusicInstance(musicFile.Name, MusicType.Local);
                if (!string.IsNullOrEmpty(search) && !music.Title.ToLower().Contains(search) && !music.AllArtists.ToLower().Contains(search) && !music.Album.ToLower().Contains(search))
                    continue;
                serverInstance.musicPlayer.musicQueue.Enqueue(music);
                count++;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.isDisconnect = false;
            serverInstance.musicPlayer.InitMainPlay();
            await ctx.FollowupAsync(new DiscordEmbedBuilder().WithDescription($"Đã thêm {count} bài vào hàng đợi! Hiện tại hàng đợi có {serverInstance.musicPlayer.musicQueue.Count} bài!").Build());
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task NowPlaying(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
                return;
            }
            DiscordEmbedBuilder embed = await serverInstance.musicPlayer.GetCurrentlyPlayingEmbed(true);
            IAsyncEnumerable<DiscordMessage> lastMessage = serverInstance.musicPlayer.LastChannel.GetMessagesAsync(1);
            if (serverInstance.musicPlayer.lastNowPlayingMessage is not null && await lastMessage.ElementAtAsync(0) != serverInstance.musicPlayer.lastNowPlayingMessage)
                try
                {
                    await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                }
                catch (NotFoundException) { }
            MusicPlayerCore musicPlayer = serverInstance.musicPlayer;
            musicPlayer.lastNowPlayingMessage = await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddActionRowComponent(serverInstance.musicPlayer.GetMusicControlButtons(1)).AddActionRowComponent(serverInstance.musicPlayer.GetMusicControlButtons(2)).AddActionRowComponent(serverInstance.musicPlayer.GetMusicControlButtons(3)));
        }

        internal static async Task Seek(CommandContext ctx, long seconds)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            try
            {
                int bytesPerSeconds = 2 * 16 * 48000 / 8;
                Stream? musicPCMDataStream = serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream;
                if (musicPCMDataStream is null)
                    throw new NullReferenceException(nameof(musicPCMDataStream));
                long bytesToSeek = Math.Max(Math.Min(bytesPerSeconds * seconds, musicPCMDataStream.Length - musicPCMDataStream.Position), -musicPCMDataStream.Position);
                bytesToSeek -= bytesToSeek % 2;
                musicPCMDataStream.Position += bytesToSeek;
                await ctx.FollowupAsync($"Đã tua {(bytesToSeek < 0 ? "lùi " : "")}bài hiện tại {new TimeSpan(0, 0, (int)Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!");
                await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, serverInstance.musicPlayer.currentlyPlayingSong.Title, ex);
                return;
            }
        }

        internal static async Task SeekTo(CommandContext ctx, long seconds)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            try
            {
                int bytesPerSeconds = 2 * 16 * 48000 / 8;
                Stream? musicPCMDataStream = serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream;
                if (musicPCMDataStream is null)
                    throw new NullReferenceException(nameof(musicPCMDataStream));
                long bytesToSeek = Math.Min(bytesPerSeconds * seconds, musicPCMDataStream.Length);
                bytesToSeek -= bytesToSeek % 2;
                musicPCMDataStream.Position = bytesToSeek;
                await ctx.FollowupAsync($"Đã tua bài hiện tại đến vị trí {new TimeSpan(0, 0, (int)Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!");
                await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, serverInstance.musicPlayer.currentlyPlayingSong.Title, ex);
                return;
            }
        }

        internal static async Task Clear(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.FollowupAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
            {
                IMusic music = serverInstance.musicPlayer.musicQueue.ElementAt(i);
                if (serverInstance.musicPlayer.currentlyPlayingSong != music)
                    music.Dispose();
            }
            serverInstance.musicPlayer.musicQueue.Clear();
            if (serverInstance.musicPlayer.currentlyPlayingSong is not null && (serverInstance.musicPlayer.playMode.isLoopQueue || serverInstance.musicPlayer.playMode.isLoopASong))
                serverInstance.musicPlayer.musicQueue.Add(serverInstance.musicPlayer.currentlyPlayingSong);
            serverInstance.musicPlayer.isPreparingNextSong = false;
            if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
            serverInstance.musicPlayer.isPreparingNextSong = false;
            await ctx.FollowupAsync("Đã xóa hết nhạc trong hàng đợi!");
        }

        internal static async Task Pause(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            serverInstance.musicPlayer.isPaused = true;
            await ctx.FollowupAsync("Tạm dừng phát nhạc!");
            await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
        }

        internal static async Task Resume(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            await ctx.FollowupAsync("Tiếp tục phát nhạc!");
        }

        internal static async Task Skip(CommandContext ctx, long count)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            if (serverInstance.musicPlayer.playMode.isRandom && count > 1)
            {
                await ctx.FollowupAsync($"Không thể bỏ qua {count} bài nhạc khi đang phát ngẫu nhiên!");
                return;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.musicPlayer.isSkipThisSong = true;
            count = Math.Min(count, serverInstance.musicPlayer.musicQueue.Count - serverInstance.musicPlayer.currentIndex);
            if (count > 1)
            {
                serverInstance.musicPlayer.isPreparingNextSong = false;
                if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                    serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
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
            else for (int i = 0; i < count - 1; i++)
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
            await ctx.FollowupAsync($"Đã bỏ qua {(count > 1 ? (count.ToString() + " bài nhạc") : "bài nhạc hiện tại")}!");
        }

        internal static async Task Remove(CommandContext ctx, long startIndex, long count)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (startIndex >= serverInstance.musicPlayer.musicQueue.Count)
            {
                await ctx.FollowupAsync($"Hàng đợi chỉ có {serverInstance.musicPlayer.musicQueue.Count} bài!");
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
                if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                    serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
            }
            await ctx.FollowupAsync($"Đã xóa {countRemoved} bài nhạc khỏi hàng đợi!");
        }

        internal static async Task Stop(CommandContext ctx, bool clearQueue)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = true;
            await Task.Delay(500);
            string response = "Dừng phát nhạc";
            if (clearQueue)
            {
                serverInstance.musicPlayer.isPreparingNextSong = false;
                if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                    serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
                for (int i = serverInstance.musicPlayer.musicQueue.Count - 1; i >= 0; i--)
                    serverInstance.musicPlayer.musicQueue.ElementAt(i)?.Dispose();
                serverInstance.musicPlayer.musicQueue.Clear();
                serverInstance.musicPlayer.isPreparingNextSong = false;
                response += " và xóa hàng đợi";
            }
            await ctx.FollowupAsync(response + "!");
        }

        internal static async Task SetVolume(CommandContext ctx, long volume)
        {
            DiscordChannel? discordChannel = ctx.Channel;
            if (discordChannel is null || discordChannel.GuildId is null)
                return;
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(discordChannel.GuildId.Value);
            if (volume == -1)
            {
                await ctx.FollowupAsync("Âm lượng nhạc hiện tại: " + (int)(serverInstance.musicPlayer.volume * 100));
                return;
            }
            if (volume < 0 || volume > 250)
            {
                await ctx.FollowupAsync("Âm lượng không hợp lệ!");
                return;
            }
            serverInstance.musicPlayer.volume = volume / 100d;
            await ctx.FollowupAsync("Điều chỉnh âm lượng nhạc thành: " + volume + "%!");
        }

        internal static async Task Queue(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            //if (!await CheckPermissions(ctx, serverInstance))
            //    return;

            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.FollowupAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            if (serverInstance.musicPlayer.browseQueueMessage is not null)
                try
                {
                    await serverInstance.musicPlayer.browseQueueMessage.DeleteAsync();
                }
                catch (NotFoundException) { }
            serverInstance.musicPlayer.browseQueueMessage = await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(serverInstance.musicPlayer.GetBrowseQueueEmbed().Build()).AddActionRowComponent(serverInstance.musicPlayer.GetBrowseQueueButtons()));
            serverInstance.musicPlayer.lastTimeChangePageQueue = DateTime.Now;
            new Thread(serverInstance.musicPlayer.DeleteBrowseQueueButton) { IsBackground = true }.Start();
        }

        internal static async Task SetPlayMode(CommandContext ctx, PlayModeChoice playMode)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            switch (playMode)
            {
                case PlayModeChoice.Queue:
                    serverInstance.musicPlayer.playMode.isLoopQueue = false;
                    break;
                case PlayModeChoice.LoopQueue:
                    if (serverInstance.musicPlayer.currentlyPlayingSong is not null && !serverInstance.musicPlayer.musicQueue.Contains(serverInstance.musicPlayer.currentlyPlayingSong))
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
                    if (serverInstance.musicPlayer.currentlyPlayingSong is not null && !serverInstance.musicPlayer.musicQueue.Contains(serverInstance.musicPlayer.currentlyPlayingSong))
                        serverInstance.musicPlayer.musicQueue.Insert(0, serverInstance.musicPlayer.currentlyPlayingSong);
                    serverInstance.musicPlayer.playMode.isLoopASong = true;
                    break;
            }
            await ctx.FollowupAsync(new DiscordInteractionResponseBuilder().WithContent("Thay đổi chế độ phát thành: " + playMode.GetName()));
        }

        internal static async Task ShuffleQueue(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.FollowupAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            serverInstance.musicPlayer.musicQueue.Shuffle();
            serverInstance.musicPlayer.isPreparingNextSong = false;
            if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
            await ctx.FollowupAsync(new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!"));
        }

        internal static async Task ReverseQueue(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.FollowupAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            serverInstance.musicPlayer.musicQueue.Reverse();
            serverInstance.musicPlayer.isPreparingNextSong = false;
            if (serverInstance.musicPlayer.prepareNextMusicStreamThread is not null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                serverInstance.musicPlayer.ctsPrepareNextSong.Cancel();
            await ctx.FollowupAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đảo danh sách nhạc trong hàng đợi!"));
        }

        internal static async Task Lyric(CommandContext ctx, string songName, string artistsName)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            //if (!await CheckPermissions(ctx, serverInstance))
            //    return;

            try
            {
                IMusic music;
                if (string.IsNullOrEmpty(songName))
                {
                    if (serverInstance.musicPlayer.currentlyPlayingSong is not null)
                        music = serverInstance.musicPlayer.currentlyPlayingSong;
                    else
                    {
                        await ctx.FollowupAsync("Không có bài nào đang phát!");
                        return;
                    }
                }
                else
                    music = new DummyMusic()
                    {
                        Title = songName.Trim(),
                        Artists = artistsName.Split([','], StringSplitOptions.RemoveEmptyEntries)
                    };
                List<DiscordEmbed> embeds = GetLyricEmbeds(music);
                if (serverInstance.musicPlayer.lyricsMessages.Count > 0)
                    serverInstance.musicPlayer.lyricsMessages.ForEach(async m =>
                    {
                        if (m is not null)
                            await m.DeleteAsync();
                    });
                serverInstance.musicPlayer.lyricsMessages.Clear();
                if (embeds.Count > 1)
                {
                    serverInstance.musicPlayer.lyricsMessages.Add(await ctx.FollowupAsync(embeds[0]));
                    foreach (DiscordEmbed embed in embeds.Skip(1).Take(embeds.Count - 2))
                        serverInstance.musicPlayer.lyricsMessages.Add(await ctx.Channel.SendMessageAsync(embed));
                    DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder();
                    foreach (DiscordButtonComponent button in serverInstance.musicPlayer.GetLyricButtons(music))
                    {
                        messageBuilder = messageBuilder.AddActionRowComponent(button);
                    }
                    serverInstance.musicPlayer.lyricsMessages.Add(await ctx.Channel.SendMessageAsync(messageBuilder.AddEmbed(embeds.Last())));
                }
                else
                {
                    DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().AddEmbed(embeds[0]);
                    foreach (DiscordButtonComponent button in serverInstance.musicPlayer.GetLyricButtons(music))
                    {
                        builder = builder.AddActionRowComponent(button);
                    }
                    serverInstance.musicPlayer.lyricsMessages.Add(await ctx.FollowupAsync(builder));
                }
                serverInstance.musicPlayer.lyricsFromLRCLIB = music.GetLyric();
            }
            catch (LyricException ex)
            {
                serverInstance.musicPlayer.viewLyricsMessage = await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent(ex.Message).AddActionRowComponent(serverInstance.musicPlayer.GetLRCLIBButton()));
            }
        }

        internal static async Task AddOrRemoveSponsorBlockOption(CommandContext ctx, SponsorBlockCategory type)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            string str = "";
            if (type == 0)
                if (!serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                    str = "Chức năng bỏ qua phân đoạn SponsorBlock đang bị tắt!";
                else
                    str = "Các phân đoạn thuộc loại sau sẽ bị bỏ qua: " + serverInstance.musicPlayer.sponsorBlockOptions.GetName();
            else
            {
                if (type == SponsorBlockCategory.All)
                    if (serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                        serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(type);
                    else
                        serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(0);
                serverInstance.musicPlayer.sponsorBlockOptions.AddOrRemoveOptions(type);
                str = $"Đã {(serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) ? "thêm" : "xóa")} {(type == SponsorBlockCategory.All ? "tất cả loại phân đoạn" : $"loại phân đoạn \"{type.GetName()}\"")} {(serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) ? "vào" : "khỏi")} danh sách bỏ qua!";
                if (!serverInstance.musicPlayer.sponsorBlockOptions.HasOption(type) && !serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                    str += Environment.NewLine + $"Không có loại phân đoạn nào để bỏ qua, tắt chức năng bỏ qua phân đoạn SponsorBlock!";
            }
            await ctx.FollowupAsync(str);
        }

        internal static async Task ViewAlbumArtwork(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            //if (!await CheckPermissions(ctx, serverInstance))
            //    return;

            if (string.IsNullOrEmpty(serverInstance.musicPlayer.currentlyPlayingSong?.AlbumThumbnailLink))
            {
                await ctx.FollowupAsync("Bài đang phát không có ảnh album!");
                return;
            }
            await ctx.FollowupAsync(serverInstance.musicPlayer.currentlyPlayingSong.AlbumThumbnailLink);
        }

        internal static async Task SetAutoSetVoiceChannelStatus(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            if (!await CheckPermissions(ctx, serverInstance))
                return;

            serverInstance.musicPlayer.isSetVCStatus = !serverInstance.musicPlayer.isSetVCStatus;
            if (serverInstance.musicPlayer.isSetVCStatus)
            {
                await ctx.FollowupAsync("Tự động đặt trạng thái kênh thoại thành bài hát đang phát!");
                await serverInstance.musicPlayer.SetCurrentVCStatus();
            }
            else
            {
                await ctx.FollowupAsync("Không đặt trạng thái kênh thoại thành bài hát đang phát!");
                VoiceNextConnection? currentVoiceNextConnection = serverInstance.currentVoiceNextConnection;
                if (currentVoiceNextConnection is not null)
                    await currentVoiceNextConnection.TargetChannel.ModifyVoiceStatusAsync("");
            }
        }

        internal static async Task Download(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }
            //if (!await CheckPermissions(ctx, serverInstance))
            //    return;

            if (serverInstance.musicPlayer.currentlyPlayingSong is null)
            {
                await ctx.FollowupAsync("Không có bài nào đang phát!");
                return;
            }
            if (serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Stream.Length > 25 * 1024 * 1024)
            {
                await ctx.FollowupAsync("Kích thước bài hát hiện tại lớn hơn 25MB!");
                return;
            }
            await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddFile(serverInstance.musicPlayer.currentlyPlayingSong.AllArtists + " - " + serverInstance.musicPlayer.currentlyPlayingSong.Title + serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Extension, serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Stream));
        }
        
        internal static async Task LockSession(CommandContext ctx, bool value)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Lệnh này chỉ có thể sử dụng trong máy chủ!");
                return;
            }
            await ctx.DeferResponseAsync();
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild.Id);
            serverInstance.musicPlayer.LastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx))
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            if (!value && ctx.Member.Permissions.HasPermission(DiscordPermission.MoveMembers))
            {
                await ctx.FollowupAsync("Bạn không có quyền mở khoá phiên phát nhạc!");
                return;
            }
            serverInstance.musicPlayer.isCurrentSessionLocked = value;
            await ctx.FollowupAsync($"Đã {(value ? "khóa" : "mở khóa")} phiên phát nhạc hiện tại!");
        }

        async Task MainPlay()
        {
            try
            {
                while (true)
                {
                    if (musicQueue.Count > 0)
                    {
                        isPlaying = true;
                        sentOutOfTrack = false;
                        isPreparingNextSong = false;
                        try
                        {
                            if (currentlyPlayingSong is not null && !playMode.isLoopASong && !playMode.isLoopQueue && !musicQueue.Contains(currentlyPlayingSong))
                            {
                                currentlyPlayingSong?.Dispose();
                                currentlyPlayingSong = null;
                            }
                            currentlyPlayingSong = GetCurrentSong();
                            nextIndex = -1;
                            if (currentlyPlayingSong.MusicPCMDataStream is null && !isDownloading)
                                currentlyPlayingSong.Download();
                            while (isDownloading)
                                await Task.Delay(200);
                            if (currentlyPlayingSong.MusicPCMDataStream is not null)
                                currentlyPlayingSong.MusicPCMDataStream.Position = 0;
                            lyricsShown = false;
                            isSongDownloaded = false;
                        }
                        catch (MusicException ex)
                        {
                            if (LastChannel is not null)
                                await LastChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), currentlyPlayingSong?.Title)).WithColor(DiscordColor.Red).Build());
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Utils.LogException(ex);
                            if (LastChannel is not null)
                                await LastChannel.SendMessageAsync($"Có lỗi xảy ra!");
                            continue;
                        }
                        await SendOrEditCurrentlyPlayingSong();
                        await SetCurrentVCStatus();
                        VoiceNextConnection? currentVoiceNextConnection = serverInstance.currentVoiceNextConnection;
                        if (currentVoiceNextConnection is null)
                            continue;
                        byte[] buffer = new byte[currentVoiceNextConnection.GetTransmitSink().SampleLength];
                        try
                        {
                            if (currentlyPlayingSong.MusicPCMDataStream is not null)
                                while (currentlyPlayingSong.MusicPCMDataStream.Read(buffer, 0, buffer.Length) != 0)
                                {
                                    if (cts.IsCancellationRequested)
                                        goto exit;
                                    if (isStopped || isSkipThisSong || currentVoiceNextConnection.IsDisposed())
                                        break;
                                    if (!isPreparingNextSong && musicQueue.Count > 0 && currentlyPlayingSong.Duration.Ticks * (1 - currentlyPlayingSong.MusicPCMDataStream.Position / (float)currentlyPlayingSong.MusicPCMDataStream.Length) <= 300000000)
                                    {
                                        isPreparingNextSong = true;
                                        prepareNextMusicStreamThread = new Thread(PrepareNextSong) { IsBackground = true };
                                        prepareNextMusicStreamThread.Start();
                                    }
                                    bool lastPaused = isPaused || !serverInstance.canSpeak;
                                    if (lastPaused)
                                        await SetCurrentVCStatus();
                                    while (isPaused || !serverInstance.canSpeak)
                                        await Task.Delay(500);
                                    if (lastPaused)
                                        await SetCurrentVCStatus();
                                    tryagain:;
                                    try
                                    {
                                        if (currentVoiceNextConnection.IsDisposed())
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
                                        await serverInstance.WriteTransmitData(buffer);
                                    }
                                    catch (Exception ex)
                                    {
                                        Utils.LogException(ex);
                                        goto tryagain;
                                    }
                                }
                        }
                        catch (ObjectDisposedException) { }
                        if (cts.IsCancellationRequested)
                            goto exit;
                        if (isSkipThisSong)
                        {
                            isSkipThisSong = false;
                            await Task.Delay(500);
                        }
                        if (isStopped)
                        {
                            addSongsInPlaylistCTS.Cancel();
                            await Task.Delay(500);
                        }
                        if (currentVoiceNextConnection.IsDisposed())
                            sentOutOfTrack = true;
                    }
                    else
                    {
                        isPlaying = false;
                        currentlyPlayingSong?.Dispose();
                        currentlyPlayingSong = null;
                        lastCurrentlyPlayingEmbed = null;
                        if (!sentOutOfTrack)
                        {
                            if (cts.IsCancellationRequested)
                                goto exit;
                            sentOutOfTrack = true;
                            await SetCurrentVCStatus();
                            if (LastChannel is not null) 
                            {
                                DiscordMessage? lastMessage = await LastChannel.GetMessagesAsync(1).ElementAtAsync(0);
                                DiscordEmbed embed = new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build();
                                if (lastNowPlayingMessage is null || lastMessage != lastNowPlayingMessage)
                                {
                                    if (lastNowPlayingMessage is not null && serverInstance.musicPlayer.lastNowPlayingMessage is not null)
                                        try
                                        {
                                            await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                                        }
                                        catch (NotFoundException) { }
                                    lastNowPlayingMessage = await LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                                }
                                else
                                {
                                    try
                                    {
                                        lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(embed));
                                    }
                                    catch (NotFoundException)
                                    {
                                        lastNowPlayingMessage = await LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                                    }
                                }
                            }
                        }
                        if (sentOutOfTrack && lastNowPlayingMessage?.CreationTimestamp.AddMinutes(1) < DateTimeOffset.Now)
                        {
                            try
                            {
                                await lastNowPlayingMessage.DeleteAsync();
                            }
                            catch { }
                            lastNowPlayingMessage = null;
                        }
                        if (addSongsInPlaylistCTS.IsCancellationRequested)
                            addSongsInPlaylistCTS = new CancellationTokenSource();
                        while (sfxData.Count != 0 && serverInstance.currentVoiceNextConnection is not null)
                        {
                            byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
                            sfxData.CopyTo(0, buffer, 0, Math.Min(buffer.Length, sfxData.Count));
                            sfxData.RemoveRange(0, Math.Min(buffer.Length, sfxData.Count));
                            await serverInstance.WriteTransmitData(buffer);
                        }
                    }
                    if (viewLyricsMessage is not null)
                    {
                        await viewLyricsMessage.ModifyAsync(new DiscordMessageBuilder().WithContent(viewLyricsMessage.Content).AddEmbeds(viewLyricsMessage.Embeds));
                        viewLyricsMessage = null;
                    }
                    if (lyricsMessages.Count > 0)
                    {
                        DiscordMessage? lyricMessage = lyricsMessages.Last();
                        if (lyricMessage is not null)
                            await lyricMessage.ModifyAsync(new DiscordMessageBuilder().WithContent(lyricMessage.Content).AddEmbeds(lyricMessage.Embeds));
                        lyricsMessages.Clear();
                    }
                    if (cts.IsCancellationRequested)
                        goto exit;
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
            }
        exit:;
            await SetCurrentVCStatus();
            if (viewLyricsMessage is not null)
            {
                await viewLyricsMessage.ModifyAsync(new DiscordMessageBuilder().WithContent(viewLyricsMessage.Content).AddEmbeds(viewLyricsMessage.Embeds));
                viewLyricsMessage = null;
            }
            if (lyricsMessages.Count > 0)
            {
                DiscordMessage? lyricMessage = lyricsMessages.Last();
                if (lyricMessage is not null)
                    await lyricMessage.ModifyAsync(new DiscordMessageBuilder().WithContent(lyricMessage.Content).AddEmbeds(lyricMessage.Embeds));
                lyricsMessages.Clear();
            }
            currentlyPlayingSong?.Dispose();
            isPlaying = false;
            isMainPlayRunning = false;
            cts = new CancellationTokenSource();
        }

        async Task SendOrEditCurrentlyPlayingSong(bool hasTimeStamp = false)
        {
            if (LastChannel is null)
                return;
            DiscordEmbedBuilder embed = await GetCurrentlyPlayingEmbed(hasTimeStamp);
            IAsyncEnumerable<DiscordMessage> lastMessage = LastChannel.GetMessagesAsync(1);
            if (lastNowPlayingMessage is null || await lastMessage.ElementAtAsync(0) != lastNowPlayingMessage)
            {
                if (lastNowPlayingMessage is not null)
                    try
                    {
                        await lastNowPlayingMessage.DeleteAsync();
                    }
                    catch (NotFoundException) { }
                lastNowPlayingMessage = await LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
            }
            else
                try
                {
                    lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
                }
                catch (NotFoundException)
                {
                    lastNowPlayingMessage = await LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
                }
        }

        async Task SetCurrentVCStatus()
        {
            if (!isSetVCStatus)
                return;
            if (serverInstance.currentVoiceNextConnection?.TargetChannel?.Type == DiscordChannelType.Stage)
                return;
            if (serverInstance.currentVoiceNextConnection?.TargetChannel is null)
                return;
            try
            {
                if (currentlyPlayingSong is not null)
                {
                    if (isPaused)
                        await serverInstance.currentVoiceNextConnection.TargetChannel.ModifyVoiceStatusAsync(":pause_button:" + currentlyPlayingSong.GetIcon() + " " + currentlyPlayingSong.AllArtists + " - " + currentlyPlayingSong.Title);
                    else
                        await serverInstance.currentVoiceNextConnection.TargetChannel.ModifyVoiceStatusAsync(currentlyPlayingSong.GetIcon() + " " + currentlyPlayingSong.AllArtists + " - " + currentlyPlayingSong.Title);
                }
                else
                    await serverInstance.currentVoiceNextConnection.TargetChannel.ModifyVoiceStatusAsync("");
            }
            catch { }
        }

        async Task UpdateCurrentlyPlayingButtons()
        {
            if (lastNowPlayingMessage is null || lastCurrentlyPlayingEmbed is null)
                return;
            if (musicQueue.Count == 0 && currentlyPlayingSong is null)
                return;
            try
            {
                lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
            }
            catch (NotFoundException)
            {
                if (LastChannel is not null)
                    lastNowPlayingMessage = await LastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
            }
        }

        async Task<DiscordEmbedBuilder> GetCurrentlyPlayingEmbed(bool hasTimeStamp = false)
        {
            while (currentlyPlayingSong?.MusicPCMDataStream is null)
                await Task.Delay(500);
            if (currentlyPlayingSong.MusicPCMDataStream.Position - lastStreamPosition == 0 && hasTimeStamp && lastCurrentlyPlayingEmbed is not null)
                return lastCurrentlyPlayingEmbed;
            string musicDesc = currentlyPlayingSong.GetSongDesc(hasTimeStamp);
            DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle(":dvd: Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
            currentlyPlayingSong.AddFooter(embed2);
            string albumThumbnailLink = currentlyPlayingSong.AlbumThumbnailLink;
            if (!string.IsNullOrEmpty(albumThumbnailLink))
                embed2 = embed2.WithThumbnail(albumThumbnailLink);
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile("waveform.png", MusicUtils.GetMusicWaveform(currentlyPlayingSong, !hasTimeStamp));
            embed2 = lastCurrentlyPlayingEmbed = embed2.WithImageUrl((await Config.gI().cacheImageChannel.SendMessageAsync(messageBuilder)).Attachments[0].Url ?? "");
            lastStreamPosition = currentlyPlayingSong.MusicPCMDataStream.Position;
            return embed2;
        }

        List<DiscordEmbed> GetLyricEmbeds()
        {
            LyricData? lyricData = null;
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            if (currentlyPlayingSong is null)
                throw new LyricException("Không có bài nào đang phát!");
            if (string.IsNullOrWhiteSpace(currentlyPlayingSong.Title))
                throw new LyricException("Bài hát đang phát không có tiêu đề!");
            lyricData = currentlyPlayingSong.GetLyric();
            if (lyricData is null)
                throw new LyricException("Không tìm thấy lời bài hát!");
            if (!string.IsNullOrEmpty(lyricData.NotFoundMessage))
                throw new LyricException(lyricData.NotFoundMessage);
            if (currentlyPlayingSong is LocalMusic)
                embed = embed.WithFooter("Powered by LRCLIB", "https://cdn.discordapp.com/emojis/1274659676688224307.webp");
            else
                embed = currentlyPlayingSong.AddFooter(embed);
            embed = embed.WithTitle("Lời bài hát " + lyricData.Title + " - " + lyricData.Artists).WithDescription(lyricData.PlainLyrics).WithThumbnail(lyricData.AlbumThumbnail);
            return embed.SplitLongEmbed().Select(e => e.Build()).ToList();
        }

        static List<DiscordEmbed> GetLyricEmbeds(IMusic music)
        {
            LyricData? lyricData = null;
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            lyricData = music.GetLyric();
            if (lyricData is null)
                throw new LyricException("Không tìm thấy lời bài hát!");
            if (!string.IsNullOrEmpty(lyricData.NotFoundMessage))
                throw new LyricException(lyricData.NotFoundMessage);
            if (music is LocalMusic || music is DummyMusic)
                embed = embed.WithFooter("Powered by LRCLIB", "https://cdn.discordapp.com/emojis/1274659676688224307.webp");
            else
                embed = music.AddFooter(embed);
            embed = embed.WithTitle("Lời bài hát " + lyricData.Title + " - " + lyricData.Artists).WithDescription(lyricData.PlainLyrics).WithThumbnail(lyricData.AlbumThumbnail);
            return embed.SplitLongEmbed().Select(e => e.Build()).ToList();
        }

        List<DiscordButtonComponent> GetLyricButtons(IMusic track)
        {
            List<DiscordButtonComponent> buttons = new List<DiscordButtonComponent>();
            LyricData? lyricData = track.GetLyric();
            if (track is null || lyricData is null || !string.IsNullOrEmpty(lyricData.NotFoundMessage))
                return buttons;
            if (!string.IsNullOrEmpty(lyricData.PlainLyrics))
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Primary, "view-plain_" + uniqueID + "_lyrics_" + serverInstance.serverID, "Xem lời không đồng bộ"));
            if (!string.IsNullOrEmpty(lyricData.SyncedLyrics))
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Primary, "view-synced_" + uniqueID + "_lyrics_" + serverInstance.serverID, "Xem lời đồng bộ"));
            if (!string.IsNullOrEmpty(lyricData.EnhancedLyrics))
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Primary, "view-enhanced_" + uniqueID + "_lyrics_" + serverInstance.serverID, "Xem lời đồng bộ từ"));
            return buttons;
        }

        DiscordButtonComponent GetLRCLIBButton()
        {
            return new DiscordButtonComponent(DiscordButtonStyle.Primary, "view-lrclib_" + uniqueID + "_lyrics_" + serverInstance.serverID, "Tìm lời bài hát trên LRCLIB");
        }

        internal async Task ButtonPressed(DiscordClient sender, ComponentInteractionCreatedEventArgs args)
        {
            try
            {
                string id = args.Id.Substring(0, args.Id.IndexOf('_'));
                if (args.Id.EndsWith(uniqueID + "_player_controls_" + serverInstance.serverID))
                {
                    if (serverInstance.currentVoiceNextConnection is not null && !serverInstance.currentVoiceNextConnection.TargetChannel.Users.Any((DiscordMember u) => args.User == u) && !Utils.IsBotOwner(args.User.Id))
                    {
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Bạn không ở cùng kênh thoại với bot!").AsEphemeral());
                        return;
                    }
                    if (id != "download" && id != "lyric" && !await CheckPermissions(args.Interaction, serverInstance))
                        return;
                    //row 1
                    if (id == "previous")
                    {
                        if (playMode.isLoopQueue)
                        {
                            isPrevious = true;
                            isSkipThisSong = true;
                        }
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        return;
                    }
                    else if (id == "rewind")
                    {
                        int bytesPerSeconds = 192000;
                        long bytesToSeek = bytesPerSeconds * 10;
                        bytesToSeek -= bytesToSeek % 2;
                        Stream? musicPCMDataStream = currentlyPlayingSong?.MusicPCMDataStream;
                        if (musicPCMDataStream is not null)
                        {
                            if (bytesToSeek > musicPCMDataStream.Position)
                                musicPCMDataStream.Position = 0L;
                            else
                                musicPCMDataStream.Position -= bytesToSeek;
                        }
                    }
                    else if (id == "pauseplay")
                    {
                        isPaused = !isPaused;
                        if (!isPaused)
                        {
                            if (lastCurrentlyPlayingEmbed is not null)
                                await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
                            else
                                await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed((await GetCurrentlyPlayingEmbed()).Build()).AddActionRowComponent(GetMusicControlButtons(1)).AddActionRowComponent(GetMusicControlButtons(2)).AddActionRowComponent(GetMusicControlButtons(3)));
                            return;
                        }
                    }
                    else if (id == "fastforward")
                    {
                        long bytesToSeek = 192000 * 10;
                        bytesToSeek -= bytesToSeek % 2;
                        Stream? musicPCMDataStream = currentlyPlayingSong?.MusicPCMDataStream;
                        if (musicPCMDataStream is not null)
                        {
                            if (bytesToSeek + musicPCMDataStream.Position > musicPCMDataStream.Length)
                                musicPCMDataStream.Position = musicPCMDataStream.Length;
                            else
                                musicPCMDataStream.Position += bytesToSeek;
                        }
                    }
                    else if (id == "next")
                    {
                        isPaused = false;
                        isSkipThisSong = true;
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        return;
                    }
                    //row 2
                    else if (id == "volume-")
                    {
                        if (volume <= 0.1)
                            volume -= 0.01;
                        else
                            volume -= 0.1;
                        if (volume < 0.0)
                            volume = 0.0;
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "refresh")
                    {
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        if ((DateTime.Now - lastTimeRefresh).TotalSeconds > 5.0)
                        {
                            lastTimeRefresh = DateTime.Now;
                            await SendOrEditCurrentlyPlayingSong(true);
                        }
                        return;
                    }
                    else if (id == "stop")
                    {
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        isStopped = true;
                        isPreparingNextSong = false;
                        if (prepareNextMusicStreamThread is not null && prepareNextMusicStreamThread.IsAlive)
                            ctsPrepareNextSong.Cancel();
                        for (int i = musicQueue.Count - 1; i >= 0; i--)
                            musicQueue.ElementAt(i)?.Dispose();
                        musicQueue.Clear();
                        isPreparingNextSong = false;
                        return;
                    }
                    else if (id == "download")
                    {
                        if (currentlyPlayingSong is null)
                            return;
                        if (currentlyPlayingSong.GetDownloadFile().Stream.Length > 25 * 1024 * 1024)
                        {
                            await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Kích thước bài hát hiện tại lớn hơn 25MB!"));
                            return;
                        }
                        await args.Interaction.DeferAsync();
                        await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddFile(currentlyPlayingSong.AllArtists + " - " + currentlyPlayingSong.Title + currentlyPlayingSong.GetDownloadFile().Extension, currentlyPlayingSong.GetDownloadFile().Stream));
                        isSongDownloaded = true;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "volume+")
                    {
                        volume += 0.1;
                        if (volume > 2.5)
                            volume = 2.5;
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    //row 3
                    else if (id == "lyric")
                    {
                        try
                        {
                            await args.Interaction.DeferAsync();
                            List<DiscordEmbed> embeds = GetLyricEmbeds();
                            lyricsMessages.Clear();
                            lyricsMessages.Add(await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddEmbed(embeds[0])));
                            foreach (DiscordEmbed embed in embeds.Skip(1).Take(embeds.Count - 2))
                                lyricsMessages.Add(await args.Channel.SendMessageAsync(embed));
                            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder();
                            if (currentlyPlayingSong is not null)
                                foreach (DiscordButtonComponent button in GetLyricButtons(currentlyPlayingSong))
                                {
                                    messageBuilder = messageBuilder.AddActionRowComponent(button);
                                }
                            lyricsMessages.Add(await args.Channel.SendMessageAsync(messageBuilder.AddEmbed(embeds.Last())));
                        }
                        catch (LyricException ex)
                        {
                            viewLyricsMessage = await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent(ex.Message).AddActionRowComponent(GetLRCLIBButton()));
                        }
                        lyricsShown = true;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "repeat")
                    {
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        if (!playMode.isLoopASong && !playMode.isLoopQueue)
                            playMode.isLoopQueue = true;
                        else if (!playMode.isLoopASong && playMode.isLoopQueue)
                        {
                            playMode.isLoopQueue = false;
                            playMode.isLoopASong = true;
                        }
                        else //if (playMode.isLoopASong && !playMode.isLoopQueue)
                            playMode.isLoopASong = playMode.isLoopQueue = false;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "shuffle")
                    {
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                        playMode.isRandom = !playMode.isRandom;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage);
                    await SendOrEditCurrentlyPlayingSong(true);
                }
                if (args.Id.EndsWith(uniqueID + "_browse_music_queue_" + serverInstance.serverID))
                {
                    if (id == "firstpage")
                    {
                        currentQueuePage = 1;
                    }
                    if (id == "previouspage")
                    {
                        currentQueuePage--;
                        if (currentQueuePage < 1)
                            currentQueuePage = 1;
                    }
                    else if (id == "nextpage")
                    {
                        currentQueuePage++;
                        int maxPage = (int)Math.Ceiling(musicQueue.Count / 10d);
                        if (currentQueuePage > maxPage)
                            currentIndex = maxPage;
                    }
                    if (id == "lastpage")
                    {
                        currentQueuePage = (int)Math.Ceiling(musicQueue.Count / 10d);
                    }
                    await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(GetBrowseQueueEmbed().Build()).AddActionRowComponent(GetBrowseQueueButtons()));
                    lastTimeChangePageQueue = DateTime.Now;
                }
                if (args.Id.EndsWith(uniqueID + "_lyrics_" + serverInstance.serverID))
                {
                    await args.Interaction.DeferAsync();
                    if (id == "view-lrclib")
                    {
                        if (currentlyPlayingSong is null)
                            return;
                        try
                        {
                            DummyMusic music = new DummyMusic()
                            {
                                Title = currentlyPlayingSong.Title.Trim(),
                                Artists = currentlyPlayingSong.AllArtists.Split([','], StringSplitOptions.RemoveEmptyEntries)
                            };
                            List<DiscordEmbed> embeds;
                            try
                            {
                                embeds = GetLyricEmbeds(music);
                            }
                            catch
                            {
                                foreach (string artist in music.Artists)
                                    music.Title = music.Title.Replace(artist + " - ", "").Replace(" - " + artist, "").Trim();
                                embeds = GetLyricEmbeds(music);
                            }
                            lyricsMessages.Clear();
                            if (embeds.Count > 1)
                            {
                                lyricsMessages.Add(await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddEmbed(embeds[0])));
                                foreach (DiscordEmbed embed in embeds.Skip(1).Take(embeds.Count - 2))
                                    lyricsMessages.Add(await args.Channel.SendMessageAsync(embed));
                                DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder();
                                foreach (DiscordButtonComponent button in GetLyricButtons(music))
                                {
                                    messageBuilder = messageBuilder.AddActionRowComponent(button);
                                }
                                lyricsMessages.Add(await args.Channel.SendMessageAsync(messageBuilder.AddEmbed(embeds.Last())));
                            }
                            else
                            {
                                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().AddEmbed(embeds[0]);
                                foreach (DiscordButtonComponent button in GetLyricButtons(music))
                                {
                                    builder = builder.AddActionRowComponent(button);
                                }
                                lyricsMessages.Add(await args.Interaction.CreateFollowupMessageAsync(builder));
                            }
                            lyricsFromLRCLIB = music.GetLyric();
                        }
                        catch (LyricException ex)
                        {
                            await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent(ex.Message));
                        }
                        if (viewLyricsMessage is not null)
                        {
                            await viewLyricsMessage.ModifyAsync(new DiscordMessageBuilder().WithContent(viewLyricsMessage.Content).AddEmbeds(viewLyricsMessage.Embeds));
                            viewLyricsMessage = null;
                        }
                        return;
                    }
                    LyricData? lyrics = currentlyPlayingSong?.GetLyric();
                    if (!string.IsNullOrEmpty(lyrics?.NotFoundMessage))
                        lyrics = lyricsFromLRCLIB;
                    if (lyrics is null)
                        return;
                    if (id == "view-plain")
                    {
                        await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddFile(currentlyPlayingSong?.AllArtists + " - " + currentlyPlayingSong?.Title + ".txt", new MemoryStream(Encoding.UTF8.GetBytes(lyrics.PlainLyrics))));
                        return;
                    }
                    if (id == "view-synced")
                    {
                        await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddFile(currentlyPlayingSong?.AllArtists + " - " + currentlyPlayingSong?.Title + ".lrc", new MemoryStream(Encoding.UTF8.GetBytes(lyrics.SyncedLyrics))));
                        return;
                    }
                    if (id == "view-enhanced")
                    {
                        await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddFile(currentlyPlayingSong?.AllArtists + " - " + currentlyPlayingSong?.Title + ".lrc", new MemoryStream(Encoding.UTF8.GetBytes(lyrics.EnhancedLyrics))));
                        return;
                    }
                }
            }
            catch (Exception ex) { Utils.LogException(ex); }
        }

        DiscordEmbedBuilder GetBrowseQueueEmbed()
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle($"Danh sách nhạc trong hàng đợi (tổng số: {musicQueue.Count})");
            for (int i = (currentQueuePage - 1) * 10; i < currentQueuePage * 10; i++)
            {
                if (musicQueue.Count == i)
                    break;
                embed.Description += $"{i + 1}. {musicQueue.ElementAt(i).GetIcon()} {musicQueue.ElementAt(i).TitleWithLink} - {musicQueue.ElementAt(i).AllArtistsWithLinks}{Environment.NewLine}";
            }
            return embed;
        }

        DiscordButtonComponent[] GetMusicControlButtons(int rows)
        {
            if (rows == 1)
                return
                [
                    new DiscordButtonComponent(DiscordButtonStyle.Primary, "previous_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", !playMode.isLoopQueue, new DiscordComponentEmoji(":track_previous:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Primary, "rewind_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(":rewind:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Primary, "pauseplay_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(isPaused ? ":arrow_forward:" : ":pause_button:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Primary, "fastforward_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(":fast_forward:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Primary, "next_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", musicQueue.Count <= 0 && !playMode.isLoopQueue, new DiscordComponentEmoji(":track_next:"))
                ];
            if (rows == 2)
                return
                [
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "volume-_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", volume <= 0.0, new DiscordComponentEmoji(":sound:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "refresh_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(":arrows_counterclockwise:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "stop_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(":stop_button:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "download_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", isSongDownloaded, new DiscordComponentEmoji(":arrow_down:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "volume+_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", volume >= 2.5, new DiscordComponentEmoji(":loud_sound:"))
                ];
            if (rows == 3)
                return
                [
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "unused1_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", true, new DiscordComponentEmoji(":black_small_square:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "lyric_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", lyricsShown, new DiscordComponentEmoji(":page_facing_up:")),
                    new DiscordButtonComponent((playMode.isLoopASong || playMode.isLoopQueue) ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary, "repeat_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(playMode.isLoopASong ? ":repeat_one:" : ":repeat:")),
                    new DiscordButtonComponent(playMode.isRandom ? DiscordButtonStyle.Success : DiscordButtonStyle.Secondary, "shuffle_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", false, new DiscordComponentEmoji(":twisted_rightwards_arrows:")),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, "unused2_" + uniqueID + "_player_controls_" + serverInstance.serverID, "", true, new DiscordComponentEmoji(":black_small_square:")),
                ];
            return [];
        }

        DiscordButtonComponent[] GetBrowseQueueButtons()
        {
            return new DiscordButtonComponent[]
            {
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "firstpage_" + uniqueID + "_browse_music_queue_" + serverInstance.serverID, "", currentQueuePage == 1, new DiscordComponentEmoji(":rewind:")),
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "previouspage_" + uniqueID + "_browse_music_queue_" + serverInstance.serverID, "", currentQueuePage == 1, new DiscordComponentEmoji(":arrow_backward:")),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, "pagenumbers_" + uniqueID + "_browse_music_queue_" + serverInstance.serverID, currentQueuePage + "/" + (int)Math.Ceiling(musicQueue.Count / 10d), true),
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "nextpage_" + uniqueID + "_browse_music_queue_" + serverInstance.serverID, "", currentQueuePage == Math.Ceiling(musicQueue.Count / 10d), new DiscordComponentEmoji(":arrow_forward:")),
                new DiscordButtonComponent(DiscordButtonStyle.Primary, "lastpage_" + uniqueID + "_browse_music_queue_" + serverInstance.serverID, "", currentQueuePage == Math.Ceiling(musicQueue.Count / 10d), new DiscordComponentEmoji(":fast_forward:")),
            };
        }

        static async Task ReportMusicException(CommandContext ctx, string input, Exception ex)
        {
            MusicException? mEx = null;
            if (ex is MusicException)
                mEx = ex as MusicException;
            if (ex is TargetInvocationException)
                mEx = ex.InnerException as MusicException;
            if (mEx is null)
                throw ex;
            await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(mEx.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
        }

        static async Task<bool> CheckPermissions(CommandContext ctx, BotServerInstance serverInstance)
        {
            if (serverInstance.currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage && (ctx.Member.VoiceState.IsSuppressed || ctx.Member.VoiceState.IsServerMuted))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Bạn không phải là người nói trên sân khấu!"));
                return false;
            }
            if (serverInstance.musicPlayer.isCurrentSessionLocked && !ctx.Channel.PermissionsFor(ctx.Member).HasPermission(DiscordPermission.MoveMembers))
            {
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().WithContent("Phiên phát nhạc hiện tại đang bị khóa!"));
                return false;
            }
            return true;
        }

        static async Task<bool> CheckPermissions(DiscordInteraction interaction, BotServerInstance serverInstance)
        {
            if (interaction.User is not DiscordMember member)
                return false;
            if (serverInstance.currentVoiceNextConnection.TargetChannel.Type == DiscordChannelType.Stage && (member.VoiceState.IsSuppressed || member.VoiceState.IsServerMuted))
            {
                await interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Bạn không phải là người nói trên sân khấu!").AsEphemeral());
                return false;
            }
            if (serverInstance.musicPlayer.isCurrentSessionLocked && !interaction.Channel.PermissionsFor(member).HasPermission(DiscordPermission.MoveMembers))
            {
                await interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Phiên phát nhạc hiện tại đang bị khóa!").AsEphemeral());
                return false;
            }
            return true;
        }

        void DeleteBrowseQueueButton()
        {
            if (isDeleteBrowseQueueButtonThreadRunning)
                return;
            isDeleteBrowseQueueButtonThreadRunning = true;
            while ((DateTime.Now - lastTimeChangePageQueue).TotalMinutes < 1)
                Thread.Sleep(5000);
            try
            {
                browseQueueMessage?.ModifyAsync(new DiscordMessageBuilder().AddEmbeds(browseQueueMessage.Embeds));
            }
            catch (NotFoundException) { }
            isDeleteBrowseQueueButtonThreadRunning = false;
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
                while (nextSong.MusicPCMDataStream is null)
                {
                    if (ctsPrepareNextSong.IsCancellationRequested)
                        return;
                    Thread.Sleep(200);
                }
                nextSong.MusicPCMDataStream.Position = 0;
            }
            catch (Exception ex) { Utils.LogException(ex); }
            isDownloading = false;
            GC.Collect();
        }

        void InitMainPlay()
        {
            if (!isMainPlayRunning)
            {
                isMainPlayRunning = true;
                new Thread(async () => await MainPlay()) { IsBackground = true }.Start();
            }
        }

        void RandomIndex() => currentIndex = random.Next(0, musicQueue.Count);

        IMusic GetCurrentSong()
        {
            if (nextIndex != -1 && !isPrevious)
                currentIndex = nextIndex;
            else if (playMode.isRandom)
                RandomIndex();
            else if (playMode.isLoopQueue && !playMode.isLoopASong && !isFirstTimeDequeue)
            {
                if (isPrevious)
                    currentIndex--;
                else
                    currentIndex++;
            }
            isPrevious = false;
            isFirstTimeDequeue = false;
            if (currentIndex >= musicQueue.Count)
                currentIndex = 0;
            if (currentIndex < 0)
                currentIndex = musicQueue.Count - 1;
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
