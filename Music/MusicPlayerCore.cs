using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Instance;
using CatBot.Music.Local;
using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;
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
        Thread addSongsThread;

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
                    serverInstance.musicPlayer.addSongsThread = playlist.AddSongsInPlaylistThread;
                    serverInstance.musicPlayer.isStopped = false;
                    serverInstance.isDisconnect = false;
                    serverInstance.musicPlayer.InitMainPlay();
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    return;
                }
            }
            catch (MusicException ex)
            { 
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
                return;
            }
            IMusic music = null;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
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
                    serverInstance.musicPlayer.addSongsThread = playlist.AddSongsInPlaylistThread;
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    return;
                }
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
                return;
            }
            IMusic music = null;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
                return;
            }
            music.SponsorBlockOptions = serverInstance.musicPlayer.sponsorBlockOptions;
            serverInstance.musicPlayer.musicQueue.Enqueue(music);
            embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {music.Title} - {music.Artists} vào hàng đợi!");
            music.AddFooter(embed);
            embed.Build();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
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
                    serverInstance.musicPlayer.addSongsThread = playlist.AddSongsInPlaylistThread;
                    serverInstance.musicPlayer.isStopped = false;
                    serverInstance.isDisconnect = false;
                    serverInstance.musicPlayer.InitMainPlay();
                    embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm danh sách phát {playlist.Title} vào hàng đợi!");
                    DiscordEmbedBuilder embed2 = new DiscordEmbedBuilder().WithTitle("Thêm danh sách phát").WithDescription(playlist.GetPlaylistDesc()).WithThumbnail(playlist.ThumbnailLink).WithColor(DiscordColor.Green);
                    playlist.AddFooter(embed2);
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()).AddEmbed(embed2.Build()));
                    return;
                }
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
                return;
            }
            IMusic music = null;
            try
            {
                if (!MusicUtils.TryCreateMusicInstance(input, out music))
                    music = MusicUtils.CreateMusicInstance(input, musicType);
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), input)).WithColor(DiscordColor.Red).Build()));
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
        }

        internal static async Task PlayAllLocalMusic(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            await ctx.DeferAsync();
            List<FileInfo> musicFiles2 = new DirectoryInfo(Config.gI().MusicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
            musicFiles2.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
            foreach (FileInfo musicFile in musicFiles2)
                serverInstance.musicPlayer.musicQueue.Enqueue(MusicUtils.CreateMusicInstance(musicFile.Name, MusicType.Local));
            serverInstance.musicPlayer.isPaused = false;
            serverInstance.musicPlayer.isStopped = false;
            serverInstance.isDisconnect = false;
            serverInstance.musicPlayer.InitMainPlay();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle($"Đã thêm {musicFiles2.Count} bài vào hàng đợi! Hiện tại hàng đợi có {serverInstance.musicPlayer.musicQueue.Count} bài!").Build()));
        }

        internal static async Task NowPlaying(InteractionContext ctx)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
            if (serverInstance.musicPlayer.currentlyPlayingSong == null)
                await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
            else
            {
                await ctx.DeferAsync();
                string musicDesc = serverInstance.musicPlayer.currentlyPlayingSong.GetSongDesc(true);
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
                serverInstance.musicPlayer.currentlyPlayingSong.AddFooter(embed);
                string albumThumbnailLink = serverInstance.musicPlayer.currentlyPlayingSong.AlbumThumbnailLink;
                DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"waveform.png", MusicUtils.GetMusicWaveform(serverInstance.musicPlayer.currentlyPlayingSong));
                DiscordMessage cacheWaveformMessage = await Config.gI().cacheImageChannel.SendMessageAsync(messageBuilder);
                embed = embed.WithImageUrl(cacheWaveformMessage.Attachments[0].Url);
                IReadOnlyList<DiscordMessage> lastMessage = await serverInstance.musicPlayer.lastChannel.GetMessagesAsync(1);
                if (serverInstance.musicPlayer.lastNowPlayingMessage != null && lastMessage[0] != serverInstance.musicPlayer.lastNowPlayingMessage)
                    await serverInstance.musicPlayer.lastNowPlayingMessage.DeleteAsync();
                if (!string.IsNullOrEmpty(albumThumbnailLink))
                    embed = embed.WithThumbnail(albumThumbnailLink);
                serverInstance.musicPlayer.lastNowPlayingMessage = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()));
            }
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
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), serverInstance.musicPlayer.currentlyPlayingSong.Title)).WithColor(DiscordColor.Red).Build()));
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
            }
            catch (MusicException ex)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder().WithTitle(string.Format(ex.GetErrorMessage(), serverInstance.musicPlayer.currentlyPlayingSong.Title)).WithColor(DiscordColor.Red).Build()));
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
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            {
                Title = $"{Math.Min(10, serverInstance.musicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.musicPlayer.musicQueue.Count})",
            };
            for (int i = 0; i < Math.Min(10, serverInstance.musicPlayer.musicQueue.Count); i++)
                embed.Description += $"{i + 1}. {serverInstance.musicPlayer.musicQueue.ElementAt(i).GetIcon()} {serverInstance.musicPlayer.musicQueue.ElementAt(i).Title} - {serverInstance.musicPlayer.musicQueue.ElementAt(i).Artists}{Environment.NewLine}";
            embed.Build();
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed));
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
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            {
                Title = $"{Math.Min(10, serverInstance.musicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.musicPlayer.musicQueue.Count})",
            };
            for (int i = 0; i < Math.Min(10, serverInstance.musicPlayer.musicQueue.Count); i++)
                embed.Description += $"{i + 1}. {serverInstance.musicPlayer.musicQueue.ElementAt(i).GetIcon()} {serverInstance.musicPlayer.musicQueue.ElementAt(i).Title} - {serverInstance.musicPlayer.musicQueue.ElementAt(i).Artists}{Environment.NewLine}";
            embed.Build();
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!").AddEmbed(embed));
        }

        internal static async Task Lyric(InteractionContext ctx, string songName, string artistsName)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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
                    embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");    
            }
            else
            {
                string apiEndpoint = Config.gI().LyricAPI + songName;
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
                embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");    
            }
            embed = embed.WithTitle($"Lời bài hát {lyricData.Title} - {lyricData.Artists}").WithDescription(lyricData.Lyric).WithThumbnail(lyricData.AlbumThumbnailLink);
            embed.Build();
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
        }

        internal static async Task AddOrRemoveSponsorBlockOption(InteractionContext ctx, SponsorBlockCategory type)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
            serverInstance.musicPlayer.lastChannel = ctx.Channel;
            if (!await serverInstance.InitializeVoiceNext(ctx.Interaction))
                return;
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
                if (type == SponsorBlockCategory.All)
                {
                    if (serverInstance.musicPlayer.sponsorBlockOptions.Enabled)
                        serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(type);
                    else 
                        serverInstance.musicPlayer.sponsorBlockOptions.SetOptions(0);
                }
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

        async Task MainPlay(CancellationToken token)
        {
            BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
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
                        await SendCurrentlyPlayingSong(serverInstance);
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
                            while (isPaused || !serverInstance.canSpeak)
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
                                await serverInstance.WriteTransmitData(buffer);
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
                        {
                            if (addSongsThread != null && !addSongsThread.IsAlive)
                                addSongsThread.Abort();
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
                        if (!sentOutOfTrack)
                        {
                            if (token.IsCancellationRequested)
                                goto exit;
                            sentOutOfTrack = true;
                            IReadOnlyList<DiscordMessage> lastMessage = await lastChannel.GetMessagesAsync(1);
                            DiscordEmbed embed = new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build();
                            if (lastNowPlayingMessage == null || lastMessage[0] != lastNowPlayingMessage)
                            {
                                if (lastNowPlayingMessage != null)
                                    await lastNowPlayingMessage.DeleteAsync();
                                lastNowPlayingMessage = await lastChannel.SendMessageAsync(embed);
                            }
                            else
                                lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(embed);
                        }
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
            currentlyPlayingSong?.Dispose();
            isPlaying = false;
            isMainPlayRunning = false;
        }

        async Task SendCurrentlyPlayingSong(BotServerInstance serverInstance)
        {
            string musicDesc = currentlyPlayingSong.GetSongDesc();
            string albumThumbnailLink = currentlyPlayingSong.AlbumThumbnailLink;
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
            currentlyPlayingSong.AddFooter(embed);
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"waveform.png", MusicUtils.GetMusicWaveform(serverInstance.musicPlayer.currentlyPlayingSong, true));
            DiscordMessage cacheWaveformMessage = await Config.gI().cacheImageChannel.SendMessageAsync(messageBuilder);
            embed = embed.WithImageUrl(cacheWaveformMessage.Attachments[0].Url);
            DiscordEmbed messageEmbed = string.IsNullOrEmpty(albumThumbnailLink) ? embed.Build() : embed.WithThumbnail(albumThumbnailLink).Build();
            IReadOnlyList<DiscordMessage> lastMessage = await lastChannel.GetMessagesAsync(1);
            if (lastNowPlayingMessage == null || lastMessage[0] != lastNowPlayingMessage)
            {
                if (lastNowPlayingMessage != null)
                    await lastNowPlayingMessage.DeleteAsync();
                lastNowPlayingMessage = await lastChannel.SendMessageAsync(messageEmbed);
            }
            else
                lastNowPlayingMessage = await lastNowPlayingMessage.ModifyAsync(messageEmbed);
            //if (currentlyPlayingSong is LocalMusic localMusic)
            //    await localMusic.lastCacheImageMessage.ModifyAsync(lastNowPlayingMessage.JumpLink.AbsoluteUri);
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
