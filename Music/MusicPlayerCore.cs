using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CatBot.Extension;
using CatBot.Instance;
using CatBot.Music.Local;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json.Linq;

namespace CatBot.Music
{
    internal class MusicPlayerCore
    {
        DiscordChannel m_lastChannel;
        internal CancellationTokenSource cts = new CancellationTokenSource();
        internal MusicQueue musicQueue = new MusicQueue();
        internal bool isPaused;
        internal bool isStopped;
        bool isPrevious;
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
        internal bool isMainPlayRunning;
        internal IMusic currentlyPlayingSong;
        internal bool isPreparingNextSong;
        internal bool isPlaying;
        internal SponsorBlockOptions sponsorBlockOptions = new SponsorBlockOptions();
        internal List<byte> sfxData = new List<byte>();
        Thread prepareNextMusicStreamThread;
        internal double volume = .75;
        internal PlayMode playMode = new PlayMode();
        bool isDownloading;
        int currentIndex;
        int nextIndex = -1;
        Random random = new Random();
        bool isFirstTimeDequeue;
        DiscordMessage lastNowPlayingMessage;
        CancellationTokenSource addSongsInPlaylistCTS = new CancellationTokenSource();
        BotServerInstance serverInstance;
        DiscordEmbedBuilder lastCurrentlyPlayingEmbed;
        long lastStreamPosition;
        string uniqueID = Utils.RandomString(10);
        DateTime lastTimeRefresh = DateTime.Now;
        bool lyricsShown;
        int currentQueuePage = 1;
        DateTime lastTimeChangePageQueue;
        DiscordMessage browseQueueMessage;
        bool isDeleteBrowseQueueButtonThreadRunning;
        bool isSetVCStatus;
        bool isSongDownloaded;

        internal MusicPlayerCore() 
        {
            DiscordBotMain.botClient.ComponentInteractionCreated += ButtonPressed;
        }

        internal MusicPlayerCore(BotServerInstance serverInstance) : this()
        {
            this.serverInstance = serverInstance;
        }

        internal static async Task Play(InteractionContext ctx, string input, MusicType musicType)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;

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
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
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
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task Enqueue(InteractionContext ctx, string input, MusicType musicType)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;

            DiscordEmbedBuilder embed;
            await ctx.DeferAsync();
            try 
            { 
                if (MusicUtils.TryCreateMusicPlaylistInstance(input, serverInstance.musicPlayer.musicQueue, out IPlaylist playlist))
                {
                    playlist.SetSponsorBlockOptions(serverInstance.musicPlayer.sponsorBlockOptions);
                    playlist.AddSongsInPlaylistCTS = serverInstance.musicPlayer.addSongsInPlaylistCTS;
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
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
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayNextUp(InteractionContext ctx, string input, MusicType musicType)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;

            DiscordEmbedBuilder embed;
            await ctx.DeferAsync();
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
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
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
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào đầu hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayRandomLocalMusic(InteractionContext ctx, long count)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;

            await ctx.DeferAsync();
            Random random = new Random();
            FileInfo[] musicFiles = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToArray();
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
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task PlayAllLocalMusic(InteractionContext ctx, string search = "")
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            await ctx.DeferAsync();
            search = search.ToLower();  
            List<FileInfo> musicFiles2 = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            musicFiles2.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            int count = 0;
            foreach (FileInfo musicFile in musicFiles2)
            {
                IMusic music = MusicUtils.CreateMusicInstance(musicFile.Name, MusicType.Local);
                if (!string.IsNullOrEmpty(search) && !music.Title.ToLower().Contains(search) && !music.Artists.ToLower().Contains(search) && !music.Album.ToLower().Contains(search))
                    continue;
                serverInstance.musicPlayer.musicQueue.Enqueue(music);
                count++;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.isDisconnect = false;
            serverInstance.musicPlayer.InitMainPlay();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithDescription($"Đã thêm {count} bài vào hàng đợi! Hiện tại hàng đợi có {serverInstance.musicPlayer.musicQueue.Count} bài!").Build()));
            await serverInstance.musicPlayer.UpdateCurrentlyPlayingButtons();
        }

        internal static async Task NowPlaying(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
                return;
            }
            await ctx.DeferAsync();
            DiscordEmbedBuilder embed = await serverInstance.musicPlayer.GetCurrentlyPlayingEmbed(true);
            IReadOnlyList<DiscordMessage> lastMessage = await serverInstance.musicPlayer.lastChannel.GetMessagesAsync(1);
            if (serverInstance.musicPlayer.lastNowPlayingMessage != null && lastMessage[0] != serverInstance.musicPlayer.lastNowPlayingMessage)
                try
                {
                    await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                }
                catch (NotFoundException) { }
            MusicPlayerCore musicPlayer = serverInstance.musicPlayer;
            musicPlayer.lastNowPlayingMessage = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddComponents(serverInstance.musicPlayer.GetMusicControlButtons(1)).AddComponents(serverInstance.musicPlayer.GetMusicControlButtons(2)).AddComponents(serverInstance.musicPlayer.GetMusicControlButtons(3)));
        }

        internal static async Task Seek(InteractionContext ctx, long seconds)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                return;
            }
            try
            {
                int bytesPerSeconds = 2 * 16 * 48000 / 8;
                long bytesToSeek = Math.Max(Math.Min(bytesPerSeconds * seconds, serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Length - serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position), -serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position);
                bytesToSeek -= bytesToSeek % 2;
                serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position += bytesToSeek;
                await ctx.CreateResponseAsync($"Đã tua {(bytesToSeek < 0 ? "lùi " : "")}bài hiện tại {new TimeSpan(0, 0, (int)Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!");
                await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, serverInstance.musicPlayer.currentlyPlayingSong.Title, ex);
                return;
            }
        }
        
        internal static async Task SeekTo(InteractionContext ctx, long seconds)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                return;
            }
            try
            {
                int bytesPerSeconds = 2 * 16 * 48000 / 8;
                long bytesToSeek = Math.Min(bytesPerSeconds * seconds, serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Length);
                bytesToSeek -= bytesToSeek % 2;
                serverInstance.musicPlayer.currentlyPlayingSong.MusicPCMDataStream.Position = bytesToSeek;
                await ctx.CreateResponseAsync($"Đã tua bài hiện tại đến vị trí {new TimeSpan(0, 0, (int)Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!");
                await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
            }
            catch (Exception ex)
            {
                await ReportMusicException(ctx, serverInstance.musicPlayer.currentlyPlayingSong.Title, ex);
                return;
            }
        }

        internal static async Task Clear(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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

        internal static async Task Pause(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                return;
            }
            serverInstance.musicPlayer.isPaused = true;
            await ctx.CreateResponseAsync("Tạm dừng phát nhạc!");
            await serverInstance.musicPlayer.SendOrEditCurrentlyPlayingSong(true);
        }

        internal static async Task Resume(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                return;
            }
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            await ctx.CreateResponseAsync("Tiếp tục phát nhạc!");
        }
 
        internal static async Task Skip(InteractionContext ctx, long count)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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
            await ctx.CreateResponseAsync($"Đã bỏ qua {(count > 1 ? (count.ToString() + " bài nhạc") : "bài nhạc hiện tại")}!");
        }

        internal static async Task Remove(InteractionContext ctx,long startIndex, long count)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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

        internal static async Task Stop(InteractionContext ctx, string clearQueueStr)
        {
            if (!bool.TryParse(clearQueueStr, out bool clearQueue))
                return;
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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

        internal static async Task SetVolume(SnowflakeObject obj, long volume)
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

        internal static async Task Queue(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            await ctx.DeferAsync();
            if (serverInstance.musicPlayer.browseQueueMessage != null)
                try
                {
                    await serverInstance.musicPlayer.browseQueueMessage.DeleteAsync();
                }
                catch (NotFoundException) { }
            serverInstance.musicPlayer.browseQueueMessage = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(serverInstance.musicPlayer.GetBrowseQueueEmbed().Build()).AddComponents(serverInstance.musicPlayer.GetBrowseQueueButtons()));
            serverInstance.musicPlayer.lastTimeChangePageQueue = DateTime.Now;
            new Thread(serverInstance.musicPlayer.DeleteBrowseQueueButton) { IsBackground = true }.Start();
        }

        internal static async Task SetPlayMode(InteractionContext ctx, PlayModeChoice playMode)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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

        internal static async Task ShuffleQueue(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            serverInstance.musicPlayer.musicQueue.Shuffle();
            serverInstance.musicPlayer.isPreparingNextSong = false;
            if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!"));
        }

        internal static async Task ReverseQueue(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.musicQueue.Count == 0)
            {
                await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
                return;
            }
            serverInstance.musicPlayer.musicQueue.Reverse();
            serverInstance.musicPlayer.isPreparingNextSong = false;
            if (serverInstance.musicPlayer.prepareNextMusicStreamThread != null && serverInstance.musicPlayer.prepareNextMusicStreamThread.IsAlive)
                serverInstance.musicPlayer.prepareNextMusicStreamThread.Abort();
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã đảo danh sách nhạc trong hàng đợi!"));
        }

        internal static async Task Lyric(InteractionContext ctx, string songName, string artistsName)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            await ctx.DeferAsync();
            try
            {
                List<DiscordEmbed> embeds = serverInstance.musicPlayer.GetLyricEmbeds(songName, artistsName);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embeds[0]));
                foreach (DiscordEmbed embed in embeds.Skip(1))
                    await ctx.Channel.SendMessageAsync(embed);
            }
            catch (LyricException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(ex.Message));
            }
        }

        internal static async Task AddOrRemoveSponsorBlockOption(InteractionContext ctx, SponsorBlockCategory type)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
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
            await ctx.CreateResponseAsync(str);
        }

        internal static async Task ViewAlbumArtwork(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (string.IsNullOrEmpty(serverInstance.musicPlayer.currentlyPlayingSong.AlbumThumbnailLink))
            {
                await ctx.CreateResponseAsync("Bài đang phát không có ảnh album!");
                return;
            }
            await ctx.CreateResponseAsync(serverInstance.musicPlayer.currentlyPlayingSong.AlbumThumbnailLink);
        }

        internal static async Task SetAutoSetVoiceChannelStatus(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            serverInstance.musicPlayer.isSetVCStatus = !serverInstance.musicPlayer.isSetVCStatus;
            if (serverInstance.musicPlayer.isSetVCStatus)
            {
                await ctx.CreateResponseAsync("Tự động đặt trạng thái kênh thoại thành bài hát đang phát!");
                await serverInstance.musicPlayer.SetCurrentVCStatus();
            }
            else
                await ctx.CreateResponseAsync("Không đặt trạng thái kênh thoại thành bài hát đang phát!");
        }

        internal static async Task Download(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
            {
                await ctx.CreateResponseAsync("Không có bài nào đang phát!");
                return;
            }
            if (serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Stream.Length > 25 * 1024 * 1024)
            {
                await ctx.CreateResponseAsync("Kích thước bài hát hiện tại lớn hơn 25MB!");
                return;
            }
            await ctx.DeferAsync();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddFile(MusicUtils.RemoveEmbedLink(serverInstance.musicPlayer.currentlyPlayingSong.Artists) + " - " + MusicUtils.RemoveEmbedLink(serverInstance.musicPlayer.currentlyPlayingSong.Title) + serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Extension, serverInstance.musicPlayer.currentlyPlayingSong.GetDownloadFile().Stream));
        }

        async Task MainPlay(CancellationToken token)
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
                            lyricsShown = false;
                            isSongDownloaded = false;
                        }
                        catch (MusicException ex)
                        {
                            await lastChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Title))).WithColor(DiscordColor.Red).Build());
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Utils.LogException(ex);
                            await lastChannel.SendMessageAsync($"Có lỗi xảy ra!");
                            continue;
                        }
                        await SendOrEditCurrentlyPlayingSong();
                        await SetCurrentVCStatus();
                        byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
                        try
                        {
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
                                    await serverInstance.WriteTransmitData(buffer);
                                }
                                catch (Exception ex)
                                {
                                    Utils.LogException(ex);
                                    goto tryagain;
                                }
                            }
                        }
                        catch(ObjectDisposedException) { }
                        if (token.IsCancellationRequested)
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
                        if (serverInstance.currentVoiceNextConnection.isDisposed())
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
                            if (token.IsCancellationRequested)
                                goto exit;
                            sentOutOfTrack = true;
                            await SetCurrentVCStatus();
                            IReadOnlyList<DiscordMessage> lastMessage = await lastChannel.GetMessagesAsync(1);
                            DiscordEmbed embed = new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build();
                            if (lastNowPlayingMessage == null || lastMessage[0] != lastNowPlayingMessage)
                            {
                                if (lastNowPlayingMessage != null)
                                    try
                                    {
                                        await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                                    }
                                    catch (NotFoundException) { }
                                lastNowPlayingMessage = await lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                            }
                            else
                                try
                                {
                                    lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(embed));
                                }
                                catch (NotFoundException)
                                {
                                    lastNowPlayingMessage = await lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                                }
                        }
                        if (addSongsInPlaylistCTS.IsCancellationRequested)
                            addSongsInPlaylistCTS = new CancellationTokenSource();
                        while (sfxData.Count != 0)
                        {
                            byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
                            sfxData.CopyTo(0, buffer, 0, Math.Min(buffer.Length, sfxData.Count));
                            sfxData.RemoveRange(0, Math.Min(buffer.Length, sfxData.Count));
                            await serverInstance.WriteTransmitData(buffer);
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
            await SetCurrentVCStatus();
            currentlyPlayingSong?.Dispose();
            isPlaying = false;
            isMainPlayRunning = false;
        }

        async Task SendOrEditCurrentlyPlayingSong(bool hasTimeStamp = false)
        {
            DiscordEmbedBuilder embed = await GetCurrentlyPlayingEmbed(hasTimeStamp);
            IReadOnlyList<DiscordMessage> lastMessage = await lastChannel.GetMessagesAsync(1);
            if (lastNowPlayingMessage == null || lastMessage[0] != lastNowPlayingMessage)
            {
                if (lastNowPlayingMessage != null)
                    try
                    {
                        await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                    }
                    catch (NotFoundException) { }
                lastNowPlayingMessage = await lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
            }
            else
                try
                {
                    lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
                }
                catch (NotFoundException)
                {
                    lastNowPlayingMessage = await lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
                }
        }

        async Task SetCurrentVCStatus()
        {
            if (!isSetVCStatus)
                return;
            if (serverInstance.currentVoiceNextConnection.TargetChannel?.Type == ChannelType.Stage)
                return;
            try
            {
                if (currentlyPlayingSong != null)
                {
                    if (isPaused)
                        await serverInstance.currentVoiceNextConnection.TargetChannel?.ModifyVoiceStatusAsync(DiscordEmoji.FromName(DiscordBotMain.botClient, ":pause_button:") + currentlyPlayingSong.GetIcon() + " " + MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Artists) + " - " + MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Title));
                    else
                        await serverInstance.currentVoiceNextConnection.TargetChannel?.ModifyVoiceStatusAsync(currentlyPlayingSong.GetIcon() + " " + MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Artists) + " - " + MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Title));
                }
                else
                    await serverInstance.currentVoiceNextConnection.TargetChannel?.ModifyVoiceStatusAsync("");
            }
            catch { }
        }

        async Task UpdateCurrentlyPlayingButtons()
        {
            if (lastNowPlayingMessage == null || lastCurrentlyPlayingEmbed == null)
                return;
            if (musicQueue.Count == 0 && currentlyPlayingSong == null)
                return;
            try
            {
                lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
            }
            catch (NotFoundException)
            {
                lastNowPlayingMessage = await lastChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
            }
        }

        async Task<DiscordEmbedBuilder> GetCurrentlyPlayingEmbed(bool hasTimeStamp = false)
        {
            while (currentlyPlayingSong.MusicPCMDataStream == null)
                await Task.Delay(500);
            if (currentlyPlayingSong.MusicPCMDataStream.Position - lastStreamPosition == 0 && hasTimeStamp && lastCurrentlyPlayingEmbed != null)
                return lastCurrentlyPlayingEmbed;
            string musicDesc = currentlyPlayingSong.GetSongDesc(hasTimeStamp);
            DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle(string.Concat(DiscordEmoji.FromName(DiscordBotMain.botClient, ":dvd:"), " Hiện đang phát")).WithDescription(musicDesc).WithColor(DiscordColor.Green);
            currentlyPlayingSong.AddFooter(embed2);
            string albumThumbnailLink = currentlyPlayingSong.AlbumThumbnailLink;
            if (!string.IsNullOrEmpty(albumThumbnailLink))
                embed2 = embed2.WithThumbnail(albumThumbnailLink);
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile("waveform.png", MusicUtils.GetMusicWaveform(currentlyPlayingSong, !hasTimeStamp));
            embed2 = lastCurrentlyPlayingEmbed = embed2.WithImageUrl((await Config.gI().cacheImageChannel.SendMessageAsync(messageBuilder)).Attachments[0].Url);
            lastStreamPosition = currentlyPlayingSong.MusicPCMDataStream.Position;
            return embed2;
        }

        List<DiscordEmbed> GetLyricEmbeds(string songName = "", string artistsName = "")
        {
            LyricData lyricData = null;
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            if (string.IsNullOrWhiteSpace(songName))
            {
                if (currentlyPlayingSong == null)
                    throw new LyricException("Không có bài nào đang phát!");
                if (string.IsNullOrWhiteSpace(currentlyPlayingSong.Title))
                    throw new LyricException("Bài hát đang phát không có tiêu đề!");
                lyricData = currentlyPlayingSong.GetLyric();
                if (!string.IsNullOrEmpty(lyricData.NotFoundMessage))
                    throw new LyricException(lyricData.NotFoundMessage);
                if (currentlyPlayingSong is LocalMusic)
                    embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");
                else 
                    embed = currentlyPlayingSong.AddFooter(embed);
            }
            else
            {
                string apiEndpoint = Config.gI().LyricAPI + songName;
                if (!string.IsNullOrWhiteSpace(artistsName))
                apiEndpoint = apiEndpoint + "/" + artistsName;
                string jsonLyric = new WebClient { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(apiEndpoint));
                JObject jsonLyricData = JObject.Parse(jsonLyric);
                if (!jsonLyricData.ContainsKey("lyrics"))
                throw new LyricException("Không tìm thấy lời bài hát!");
                lyricData = new LyricData(jsonLyricData["title"].ToString(), jsonLyricData["artist"].ToString(), jsonLyricData["lyrics"].ToString(), jsonLyricData["image"].ToString());
                embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");
            }
            embed = embed.WithTitle("Lời bài hát " + lyricData.Title + " - " + lyricData.Artists).WithDescription(lyricData.Lyric).WithThumbnail(lyricData.AlbumThumbnailLink);
            return embed.SplitLongEmbed().Select(e => e.Build()).ToList();
        }

        async Task ButtonPressed(DiscordClient sender, ComponentInteractionCreateEventArgs args)
        {
            try
            {
                string id = args.Id.Substring(0, args.Id.IndexOf('_'));
                if (args.Id.EndsWith(uniqueID + "_player_controls_" + serverInstance.server.Id))
                {
                    if (!serverInstance.currentVoiceNextConnection.TargetChannel.Users.Any((DiscordMember u) => args.User == u) && !Utils.IsBotOwner(args.User.Id))
                    {
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Bạn không ở cùng kênh thoại với bot!").AsEphemeral());
                        return;
                    }
                    //row 1
                    if (id == "previous")
                    {
                        if (playMode.isLoopQueue)
                        {
                            isPrevious = true;
                            isSkipThisSong = true;
                        }
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        return;
                    }
                    else if (id == "rewind")
                    {
                        int bytesPerSeconds = 192000;
                        long bytesToSeek = bytesPerSeconds * 10;
                        bytesToSeek -= bytesToSeek % 2;
                        if (bytesToSeek > currentlyPlayingSong.MusicPCMDataStream.Position)
                            currentlyPlayingSong.MusicPCMDataStream.Position = 0L;
                        else
                            currentlyPlayingSong.MusicPCMDataStream.Position -= bytesToSeek;
                    }
                    else if (id == "pauseplay")
                    {
                        isPaused = !isPaused;
                        if (!isPaused)
                        {
                            if (lastCurrentlyPlayingEmbed != null)
                                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(lastCurrentlyPlayingEmbed.Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
                            else 
                                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed((await GetCurrentlyPlayingEmbed()).Build()).AddComponents(GetMusicControlButtons(1)).AddComponents(GetMusicControlButtons(2)).AddComponents(GetMusicControlButtons(3)));
                            return;
                        }
                    }
                    else if (id == "fastforward")
                    {
                        long bytesToSeek = 192000 * 10;
                        bytesToSeek -= bytesToSeek % 2;
                        if (bytesToSeek + currentlyPlayingSong.MusicPCMDataStream.Position > currentlyPlayingSong.MusicPCMDataStream.Length)
                            currentlyPlayingSong.MusicPCMDataStream.Position = currentlyPlayingSong.MusicPCMDataStream.Length;
                        else
                            currentlyPlayingSong.MusicPCMDataStream.Position += bytesToSeek;
                    }
                    else if (id == "next")
                    {
                        isPaused = false;
                        isSkipThisSong = true;
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
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
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "refresh")
                    {
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        if ((DateTime.Now - lastTimeRefresh).TotalSeconds > 5.0)
                        {
                            lastTimeRefresh = DateTime.Now;
                            await SendOrEditCurrentlyPlayingSong(true);
                        }
                        return;
                    }
                    else if (id == "stop")
                    {
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        isStopped = true;
                        isPreparingNextSong = false;
                        if (prepareNextMusicStreamThread != null && prepareNextMusicStreamThread.IsAlive)
                            prepareNextMusicStreamThread.Abort();
                        for (int i = musicQueue.Count - 1; i >= 0; i--)
                            musicQueue.ElementAt(i)?.Dispose();
                        musicQueue.Clear();
                        isPreparingNextSong = false;
                        return;
                    }
                    else if (id == "download")
                    {
                        if (currentlyPlayingSong.GetDownloadFile().Stream.Length > 25 * 1024 * 1024)
                        {
                            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Kích thước bài hát hiện tại lớn hơn 25MB!"));
                            return;
                        }
                        await args.Interaction.DeferAsync();
                        await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AddFile(MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Artists) + " - " + MusicUtils.RemoveEmbedLink(currentlyPlayingSong.Title) + currentlyPlayingSong.GetDownloadFile().Extension, currentlyPlayingSong.GetDownloadFile().Stream));
                        isSongDownloaded = true;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "volume+")
                    {
                        volume += 0.1;
                        if (volume > 2.5)
                            volume = 2.5;
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    //row 3
                    else if (id == "lyric")
                    {
                        try
                        {
                            List<DiscordEmbed> embeds = GetLyricEmbeds();
                            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embeds[0]));
                            foreach (DiscordEmbed embed in embeds.Skip(1))
                                await args.Channel.SendMessageAsync(embed);
                        }
                        catch (LyricException ex)
                        {
                            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(ex.Message));
                        }
                        lyricsShown = true;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    else if (id == "repeat")
                    {
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
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
                        await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                        playMode.isRandom = !playMode.isRandom;
                        await UpdateCurrentlyPlayingButtons();
                        return;
                    }
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                    await SendOrEditCurrentlyPlayingSong(true);
                }
                if (args.Id.EndsWith(uniqueID + "_browse_music_queue_" + serverInstance.server.Id))
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
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(GetBrowseQueueEmbed().Build()).AddComponents(GetBrowseQueueButtons()));
                    lastTimeChangePageQueue = DateTime.Now;
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
                embed.Description += $"{i + 1}. {musicQueue.ElementAt(i).GetIcon()} {musicQueue.ElementAt(i).Title} - {musicQueue.ElementAt(i).Artists}{Environment.NewLine}";
            }
            return embed;
        }

        DiscordButtonComponent[] GetMusicControlButtons(int rows)
        {
            if (rows == 1)
                return new DiscordButtonComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Primary, "previous_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", !playMode.isLoopQueue, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":track_previous:"))),
                    new DiscordButtonComponent(ButtonStyle.Primary, "rewind_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":rewind:"))),
                    new DiscordButtonComponent(ButtonStyle.Primary, "pauseplay_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, isPaused ? ":arrow_forward:" : ":pause_button:"))),
                    new DiscordButtonComponent(ButtonStyle.Primary, "fastforward_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":fast_forward:"))),
                    new DiscordButtonComponent(ButtonStyle.Primary, "next_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", musicQueue.Count <= 0 && !playMode.isLoopQueue, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":track_next:")))
                };
            if (rows == 2)
                return new DiscordButtonComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Secondary, "volume-_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", volume <= 0.0, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":sound:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "refresh_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":arrows_counterclockwise:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "stop_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":stop_button:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "download_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", isSongDownloaded, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":arrow_down:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "volume+_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", volume >= 2.5, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":loud_sound:")))
                };
            if (rows == 3)
                return new DiscordButtonComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Secondary, "unused1_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", true, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":black_small_square:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "lyric_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", lyricsShown, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":page_facing_up:"))),
                    new DiscordButtonComponent((playMode.isLoopASong || playMode.isLoopQueue) ? ButtonStyle.Success : ButtonStyle.Secondary, "repeat_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, (playMode.isLoopASong ? ":repeat_one:" : ":repeat:")))),
                    new DiscordButtonComponent(playMode.isRandom ? ButtonStyle.Success : ButtonStyle.Secondary, "shuffle_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":twisted_rightwards_arrows:"))),
                    new DiscordButtonComponent(ButtonStyle.Secondary, "unused2_" + uniqueID + "_player_controls_" + serverInstance.server.Id, "", true, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":black_small_square:"))),
                };
            return new DiscordButtonComponent[0];
        }

        DiscordButtonComponent[] GetBrowseQueueButtons()
        {
            return new DiscordButtonComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Primary, "firstpage_" + uniqueID + "_browse_music_queue_" + serverInstance.server.Id, "", currentQueuePage == 1, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":rewind:"))),
                new DiscordButtonComponent(ButtonStyle.Primary, "previouspage_" + uniqueID + "_browse_music_queue_" + serverInstance.server.Id, "", currentQueuePage == 1, new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":arrow_backward:"))),
                new DiscordButtonComponent(ButtonStyle.Secondary, "pagenumbers_" + uniqueID + "_browse_music_queue_" + serverInstance.server.Id, currentQueuePage + "/" + (int)Math.Ceiling(musicQueue.Count / 10d), true),
                new DiscordButtonComponent(ButtonStyle.Primary, "nextpage_" + uniqueID + "_browse_music_queue_" + serverInstance.server.Id, "", currentQueuePage == Math.Ceiling(musicQueue.Count / 10d), new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":arrow_forward:"))),
                new DiscordButtonComponent(ButtonStyle.Primary, "lastpage_" + uniqueID + "_browse_music_queue_" + serverInstance.server.Id, "", currentQueuePage == Math.Ceiling(musicQueue.Count / 10d), new DiscordComponentEmoji(DiscordEmoji.FromName(DiscordBotMain.botClient, ":fast_forward:"))),
            };
        }

        static async Task ReportMusicException(InteractionContext ctx, string input, Exception ex)
        {
            MusicException mEx = null;
            if (ex is MusicException)
                mEx = ex as MusicException;
            if (ex is TargetInvocationException)
                mEx = ex.InnerException as MusicException;
            if (mEx == null)
                throw ex;
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(mEx.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
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
                browseQueueMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbeds(browseQueueMessage.Embeds));
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
            if (!isMainPlayRunning)
            {
                isMainPlayRunning = true;
                new Thread(async() => await MainPlay(cts.Token)) { IsBackground = true }.Start();
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
