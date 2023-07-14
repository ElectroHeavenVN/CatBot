//using DiscordBot.Instance;
//using DSharpPlus.Entities;
//using DSharpPlus.SlashCommands;
//using Leaf.xNet;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;

//namespace DiscordBot.Obsolete
//{
//    [Obsolete("Code cũ để tham khảo")]
//    [SlashCommandGroup("zing", "Zing MP3 commands")]
//    public class ZingMP3Player : ApplicationCommandModule
//    {
//        static readonly string zingMP3Link = "https://zingmp3.vn/";
//        static readonly string mainMinJSRegex = "https://zjs.zmdcdn.me/zmp3-desktop/releases/(.*?)/static/js/main.min.js";
//        static readonly string zingMP3IconLink = "https://static-zmp3.zmdcdn.me/skins/zmp3-v5.2/images/icon_zing_mp3_60.png";

//        DiscordChannel m_lastChannel;
//        internal CancellationTokenSource cts = new CancellationTokenSource();
//        internal Queue<string> musicQueue = new Queue<string>();
//        internal bool isPaused;
//        internal bool isStopped;
//        internal bool isSkipThisSong;
//        internal bool sentOutOfTrack = true;
//        internal TimeSpan currentSongDuration;
//        internal DiscordChannel lastChannel
//        {
//            get => m_lastChannel;
//            set
//            {
//                lastTimeSetLastChannel = DateTime.Now.Ticks;
//                m_lastChannel = value;
//            }
//        }
//        internal long lastTimeSetLastChannel;
//        internal bool isThreadAlive;
//        internal MemoryStream currentMusicStream = new MemoryStream();
//        internal MemoryStream nextMusicStream = new MemoryStream();
//        internal string currentlyPlayingSongLink;
//        internal bool isPreparingNextSong;
//        internal bool isPlaying;

//        [SlashCommand("play", "Thêm nhạc từ Zing MP3 vào hàng đợi")]
//        public async Task Play(InteractionContext ctx, [Option("songname", "Tên bài hát hoặc link")] string linkZingMP3)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (serverInstance.offlineMusicPlayer.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc từ Zing MP3 khi đang phát nhạc local!");
//                    return;
//                }
//                await ctx.DeferAsync();
//                if (!linkZingMP3.StartsWith(zingMP3Link))
//                    linkZingMP3 = zingMP3Link.TrimEnd('/') + GetSongInfo(FindSongID(linkZingMP3))["link"].ToString();
//                serverInstance.zingMP3Player.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.zingMP3Player.InitMainPlay();
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {GetSongName(linkZingMP3)} vào hàng đợi!").WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                embed.Build();
//                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
//                serverInstance.zingMP3Player.musicQueue.Enqueue(linkZingMP3);
//            }
//            catch (Exception ex) 
//            {
//                if (ex is WebException webException && webException.Message == "songs not found")
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Không tìm thấy bài `{linkZingMP3}`!"));
//                Utils.LogException(ex); 
//            }
//        }

//        [SlashCommand("nextup", "Thêm nhạc từ Zing MP3 vào đầu hàng đợi")]
//        public async Task PlayNextUp(InteractionContext ctx, [Option("songname", "Tên bài hát hoặc link")] string linkZingMP3)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (serverInstance.offlineMusicPlayer.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc từ Zing MP3 khi đang phát nhạc local!");
//                    return;
//                }
//                await ctx.DeferAsync();
//                if (linkZingMP3.StartsWith(zingMP3Link))
//                    linkZingMP3 = GetSongInfo(FindSongID(linkZingMP3))["link"].ToString();
//                List<string> oldQueue = serverInstance.zingMP3Player.musicQueue.ToList();
//                oldQueue.Insert(0, linkZingMP3);
//                serverInstance.zingMP3Player.musicQueue = new Queue<string>(oldQueue);
//                serverInstance.zingMP3Player.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.zingMP3Player.InitMainPlay();
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithDescription($"Đã thêm bài {GetSongName(linkZingMP3)} vào đầu hàng đợi!").WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                embed.Build();
//                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
//            }
//            catch (Exception ex)
//            {
//                if (ex is WebException webException && webException.Message == "songs not found")
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Không tìm thấy bài `{linkZingMP3}`!"));
//                Utils.LogException(ex);
//            }
//        }

//        [SlashCommand("nowplaying", "Xem thông tin bài nhạc đang phát")]
//        public async Task NowPlaying(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrWhiteSpace(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                    await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
//                else
//                {
//                    await ctx.DeferAsync();
//                    JObject songDesc = GetSongInfo(serverInstance.zingMP3Player.currentlyPlayingSongLink);
//                    string musicDesc = GetSongDesc(serverInstance.zingMP3Player.currentlyPlayingSongLink);
//                    musicDesc += new TimeSpan(long.Parse(songDesc["duration"].ToString()) * 10000000 * serverInstance.zingMP3Player.currentMusicStream.Position / serverInstance.zingMP3Player.currentMusicStream.Length).toString() + "/" + new TimeSpan(long.Parse(songDesc["duration"].ToString()) * 10000000).toString();
//                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green).WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.WithThumbnail(songDesc["thumbnail"].ToString()).Build()));
//                }
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("seek", "Tua bài hiện tại")]
//        public async Task Seek(InteractionContext ctx, [Option("seconds", "số giây để tua (mặc định: 10)"), Minimum(int.MinValue), Maximum(int.MaxValue)] long seconds = 10)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrWhiteSpace(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                await ctx.DeferAsync();
//                int bytesPerSeconds = (int)(serverInstance.zingMP3Player.currentMusicStream.Length / serverInstance.zingMP3Player.currentSongDuration.TotalSeconds);
//                bytesPerSeconds -= bytesPerSeconds % 2;
//                int bytesToSeek = (int)Math.Max(Math.Min(bytesPerSeconds * seconds, serverInstance.zingMP3Player.currentMusicStream.Length - serverInstance.zingMP3Player.currentMusicStream.Position), -serverInstance.zingMP3Player.currentMusicStream.Position);
//                serverInstance.zingMP3Player.currentMusicStream.Position += bytesToSeek;
//                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Đã tua {(bytesToSeek < 0 ? "lùi " : "")}bài hiện tại {new TimeSpan(0, 0, Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!"));
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("clear", "Xóa hết nhạc trong hàng đợi")]
//        public async Task Clear(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                serverInstance.zingMP3Player.musicQueue.Clear();
//                await ctx.CreateResponseAsync("Đã xóa hết nhạc trong hàng đợi!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("pause", "Tạm dừng nhạc")]
//        public async Task Pause(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.zingMP3Player.isPaused = true;
//                await ctx.CreateResponseAsync("Tạm dừng phát nhạc!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("resume", "Tiếp tục phát nhạc")]
//        public async Task Resume(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.zingMP3Player.isPaused = false;
//                serverInstance.zingMP3Player.isStopped = false;
//                await ctx.CreateResponseAsync("Tiếp tục phát nhạc!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("skip", "Bỏ qua bài hát")]
//        public async Task Skip(InteractionContext ctx, [Option("count", "Số bài bỏ qua (mặc định: 1)"), Minimum(1), Maximum(int.MaxValue)] long count = 1)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.zingMP3Player.isPaused = false;
//                serverInstance.zingMP3Player.isStopped = false;
//                serverInstance.zingMP3Player.isSkipThisSong = true;
//                count = Math.Min(count, serverInstance.zingMP3Player.musicQueue.Count);
//                for (int i = 0; i < count - 1; i++)
//                    serverInstance.zingMP3Player.musicQueue.Dequeue();
//                await ctx.CreateResponseAsync($"Đã bỏ qua {(count > 1 ? (count.ToString() + " bài nhạc") : "bài nhạc hiện tại")}!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("remove", "Xóa nhạc trong hàng đợi")]
//        public async Task Remove(InteractionContext ctx, [Option("index", "Vị trí xóa bài hát"), Minimum(0), Maximum(int.MaxValue)] long startIndex = 0, [Option("count", "Số lượng bài hát"), Minimum(1), Maximum(int.MaxValue)] long count = 1)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (startIndex >= serverInstance.zingMP3Player.musicQueue.Count)
//                {
//                    await ctx.CreateResponseAsync($"Hàng đợi chỉ có {serverInstance.zingMP3Player.musicQueue.Count} bài!");
//                    return;
//                }
//                List<string> oldQueue = serverInstance.zingMP3Player.musicQueue.ToList();
//                count = Math.Min(count, serverInstance.zingMP3Player.musicQueue.Count - startIndex);
//                for (int i = 0; i < count; i++)
//                    oldQueue.RemoveAt((int)startIndex);
//                serverInstance.zingMP3Player.musicQueue = new Queue<string>(oldQueue);
//                await ctx.CreateResponseAsync($"Đã xóa {count} bài nhạc khỏi hàng đợi!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("stop", "Dừng phát nhạc")]
//        public async Task Stop(InteractionContext ctx, [Option("clearQueue", "Xóa nhạc trong hàng đợi"), Choice("Có", "true"), Choice("Không", "false")] string clearQueueStr = "true")
//        {
//            try
//            {
//                if (!bool.TryParse(clearQueueStr, out bool clearQueue))
//                    return;
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.zingMP3Player.isPaused = false;
//                serverInstance.zingMP3Player.isStopped = true;
//                string response = "Dừng phát nhạc";
//                if (clearQueue)
//                {
//                    serverInstance.zingMP3Player.musicQueue.Clear();
//                    response += " và xóa hàng đợi";
//                }
//                await ctx.CreateResponseAsync(response + "!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("queue", "Xem hàng đợi nhạc")]
//        public async Task Queue(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                else
//                {
//                    await ctx.DeferAsync();
//                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
//                    {
//                        Title = $"{Math.Min(10, serverInstance.zingMP3Player.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.zingMP3Player.musicQueue.Count})",
//                    }.WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                    for (int i = 0; i < Math.Min(10, serverInstance.zingMP3Player.musicQueue.Count); i++)
//                        embed.Description += i + 1 + ". <:ZingMP3:1124356310503276634> " + GetSongName(serverInstance.zingMP3Player.musicQueue.ElementAt(i)) + Environment.NewLine;
//                    embed.Build();
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
//                }
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("shuffle", "Trộn danh sách nhạc trong hàng đợi")]
//        public async Task ShuffleQueue(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                Queue<string> newMusicQueue = new Queue<string>();
//                List<string> oldMusicQueue = serverInstance.zingMP3Player.musicQueue.ToList();
//                Random random = new Random();
//                int count = oldMusicQueue.Count;
//                for (int i = 0; i < count; i++)
//                {
//                    int index = random.Next(0, oldMusicQueue.Count);
//                    newMusicQueue.Enqueue(oldMusicQueue.ElementAt(index));
//                    oldMusicQueue.RemoveAt(index);
//                }
//                serverInstance.zingMP3Player.musicQueue = newMusicQueue;
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
//                {
//                    Title = $"{Math.Min(10, serverInstance.zingMP3Player.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.zingMP3Player.musicQueue.Count})",
//                }.WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                for (int i = 0; i < Math.Min(10, serverInstance.zingMP3Player.musicQueue.Count); i++)
//                    embed.Description += i + 1 + ". " + GetSongName(serverInstance.zingMP3Player.musicQueue.ElementAt(i)) + Environment.NewLine;
//                embed.Build();
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!").AddEmbed(embed));
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("lyric", "Xem lời bài hát đang phát")]
//        public async Task Lyric(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.zingMP3Player.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.zingMP3Player.currentlyPlayingSongLink))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                await ctx.DeferAsync();
//                JObject lyricData = GetSongLyricInfo(serverInstance.zingMP3Player.currentlyPlayingSongLink);
//                if (!lyricData.ContainsKey("file"))
//                {
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bài hát này không có lời trên server của Zing MP3!"));
//                    return;
//                }
//                JObject songDesc = GetSongInfo(serverInstance.zingMP3Player.currentlyPlayingSongLink);
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle($"Lời bài hát {songDesc["title"]} - {songDesc["artistsNames"]}").WithDescription(RemoveLyricTimestamps(new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(lyricData["file"].ToString()))).WithThumbnail(songDesc["thumbnail"].ToString()).WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                embed.Build();
//                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        async void MainPlay(CancellationToken token)
//        {
//            try
//            {
//                while (true)
//                {
//                    BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(this);
//                    if (musicQueue.Count > 0)
//                    {
//                        isPlaying = true;
//                        sentOutOfTrack = false;
//                        isPreparingNextSong = false;
//                        currentlyPlayingSongLink = musicQueue.Dequeue();
//                        try
//                        {
//                            JObject songDesc = GetSongInfo(currentlyPlayingSongLink);
//                            string musicDesc = GetSongDesc(currentlyPlayingSongLink);
//                            musicDesc += "Thời lượng: " + new TimeSpan(long.Parse(songDesc["duration"].ToString()) * 10000000).toString();
//                            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green).WithFooter("Powered by Zing MP3", zingMP3IconLink);
//                            DiscordMessage message = await serverInstance.zingMP3Player.lastChannel.SendMessageAsync(embed.WithThumbnail(songDesc["thumbnail"].ToString()).Build());
//                            ResetCurrentMusicStream();
//                            if (nextMusicStream.Capacity != 0)
//                                await nextMusicStream.CopyToAsync(currentMusicStream);
//                            else
//                            {
//                                string tempFilePath = Path.GetTempFileName();
//                                FileStream file = File.OpenWrite(tempFilePath);
//                                byte[] data = new WebClient().DownloadData(GetSongLink(currentlyPlayingSongLink));
//                                await file.WriteAsync(data, 0, data.Length);
//                                await file.FlushAsync();
//                                file.Close();
//                                TagLib.File f = TagLib.File.Create(tempFilePath, "taglib/mp3", TagLib.ReadStyle.Average);
//                                currentSongDuration = f.Properties.Duration;
//                                f.Dispose();
//                                Stream fileStream = Utils.GetPCMStream(tempFilePath);
//                                await fileStream.CopyToAsync(currentMusicStream);
//                                File.Delete(tempFilePath);
//                                fileStream.Dispose();
//                            }
//                            ResetNextMusicStream();
//                            currentMusicStream.Position = 0;
//                        }
//                        catch (Exception ex)
//                        {
//                            if (ex.Message.StartsWith("-1110"))
//                            {
//                                await serverInstance.zingMP3Player.lastChannel.SendMessageAsync($"Bỏ qua bài này vì bài này bị chặn ở quốc gia đặt máy chủ của bot!");
//                                continue;
//                            }
//                            Utils.LogException(ex);
//                            await serverInstance.zingMP3Player.lastChannel.SendMessageAsync($"Có lỗi xảy ra!");
//                            continue;
//                        }
//                        byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
//                        while (currentMusicStream.Read(buffer, 0, buffer.Length) != 0)
//                        {
//                            if (token.IsCancellationRequested)
//                                goto exit;
//                            if (isStopped || isSkipThisSong || serverInstance.currentVoiceNextConnection.isDisposed())
//                                break;
//                            if (!isPreparingNextSong && currentSongDuration.Ticks * (1 - serverInstance.zingMP3Player.currentMusicStream.Position / (float)serverInstance.zingMP3Player.currentMusicStream.Length) <= 100000000) //10s
//                                new Thread(PrepareNextMusicStream) { IsBackground = true }.Start();
//                            while (isPaused)
//                                await Task.Delay(500);
//                            tryagain:;
//                            try
//                            {
//                                if (serverInstance.currentVoiceNextConnection.isDisposed())
//                                {
//                                    await Task.Delay(500);
//                                    continue;
//                                }
//                                await serverInstance.currentVoiceNextConnection.GetTransmitSink().WriteAsync(new ReadOnlyMemory<byte>(buffer));
//                            }
//                            catch (Exception ex)
//                            {
//                                Utils.LogException(ex);
//                                goto tryagain;
//                            }
//                        }
//                        if (token.IsCancellationRequested)
//                            goto exit;
//                        if (isSkipThisSong)
//                            isSkipThisSong = false;
//                        if (isStopped)
//                            await Task.Delay(500);
//                        if (serverInstance.currentVoiceNextConnection.isDisposed())
//                            sentOutOfTrack = true;
//                    }
//                    else
//                    {
//                        isPlaying = false;
//                        currentlyPlayingSongLink = "";
//                        if (!sentOutOfTrack)
//                        {
//                            ResetCurrentMusicStream();
//                            if (token.IsCancellationRequested)
//                                goto exit;
//                            sentOutOfTrack = true;
//                            await serverInstance.zingMP3Player.lastChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build());
//                        }
//                    }
//                    if (token.IsCancellationRequested)
//                        goto exit;
//                    await Task.Delay(1000);
//                }
//            }
//            catch (Exception ex)
//            {
//                Utils.LogException(ex);
//            }
//        exit:;
//            ResetCurrentMusicStream();
//            ResetNextMusicStream();
//            isPlaying = false;
//            isThreadAlive = false;
//        }

//        void ResetCurrentMusicStream()
//        {
//            currentMusicStream.Dispose();
//            currentMusicStream = new MemoryStream();
//        }

//        void ResetNextMusicStream()
//        {
//            nextMusicStream.Dispose();
//            nextMusicStream = new MemoryStream();
//        }

//        void PrepareNextMusicStream()
//        {
//            if (isPreparingNextSong)
//                return;
//            if (musicQueue.Count == 0)
//                return;
//            isPreparingNextSong = true;
//            try
//            {
//                string nextSong = musicQueue.Peek();
//                string tempFilePath = Path.GetTempFileName();
//                FileStream file = File.OpenWrite(tempFilePath);
//                byte[] data = new WebClient().DownloadData(GetSongLink(nextSong));
//                file.Write(data, 0, data.Length);
//                file.Flush();
//                file.Close();
//                Stream fileStream = Utils.GetPCMStream(tempFilePath);
//                fileStream.CopyToAsync(currentMusicStream);
//                File.Delete(tempFilePath);
//                fileStream.Dispose();
//            }
//            catch (Exception ex)
//            {
//                Utils.LogException(ex);
//                ResetNextMusicStream();
//            }
//            GC.Collect();
//        }

//        void InitMainPlay()
//        {
//            if (!isThreadAlive)
//            {
//                isThreadAlive = true;
//                new Thread(() => MainPlay(cts.Token)) { IsBackground = true }.Start();
//            }
//        }

//        static JObject GetSongInfo(string linkOrID)
//        {
//            string apiEndpoint = "/api/v2/page/get/song";
//            HttpRequest http = Utils.InitHttpRequestWithCookie();
//            string id = GetSongID(linkOrID);
//            string ctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
//            string zingMP3Web = http.Get(zingMP3Link, null).ToString();
//            string version = Regex.Match(zingMP3Web, mainMinJSRegex).Groups[1].Value.Replace("v", "");
//            string secretKey = Config.zingMP3SecretKey;
//            string apiKey = Config.zingMP3APIKey;
//            string hash = apiEndpoint + Utils.ToSHA256($"ctime={ctime}id={id}version={version}");
//            string sig = Utils.SHA512_ComputeHash(hash, secretKey);
//            string getSongInfoUrl = $"{zingMP3Link.TrimEnd('/')}{apiEndpoint}?id={id}&ctime={ctime}&version={version}&sig={sig}&apiKey={apiKey}";
//            string str = http.Get(getSongInfoUrl, null).ToString();
//            JObject obj = JObject.Parse(str);
//            if (obj["err"].ToString() == "0")
//                return (JObject)obj["data"];
//            throw new WebException(obj["err"] + ": " + obj["msg"]);
//        }

//        static JObject GetSongLyricInfo(string linkOrID)
//        {
//            string apiEndpoint = "/api/v2/lyric/get/lyric";
//            HttpRequest http = Utils.InitHttpRequestWithCookie();
//            string id = GetSongID(linkOrID);
//            string ctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
//            string zingMP3Web = http.Get(zingMP3Link, null).ToString();
//            string version = Regex.Match(zingMP3Web, mainMinJSRegex).Groups[1].Value.Replace("v", "");
//            string secretKey = Config.zingMP3SecretKey;
//            string apiKey = Config.zingMP3APIKey;
//            string hash = apiEndpoint + Utils.ToSHA256($"ctime={ctime}id={id}version={version}");
//            string sig = Utils.SHA512_ComputeHash(hash, secretKey);
//            string getSongInfoUrl = $"{zingMP3Link.TrimEnd('/')}{apiEndpoint}?id={id}&BGId=0&ctime={ctime}&version={version}&sig={sig}&apiKey={apiKey}";
//            string str = http.Get(getSongInfoUrl, null).ToString();
//            JObject obj = JObject.Parse(str);
//            if (obj["err"].ToString() == "0")
//                return (JObject)obj["data"];
//            throw new WebException(obj["err"] + ": " + obj["msg"]);
//        }

//        static string GetSongDesc(string linkOrID)
//        {
//            JObject songDesc = GetSongInfo(linkOrID);
//            string musicDesc = "Bài hát: [" + songDesc["title"] + "](" + linkOrID + ")" + Environment.NewLine;
//            musicDesc += "Nghệ sĩ: ";
//            if (songDesc["artists"] != null)
//            {
//                foreach (JToken artist in songDesc["artists"])
//                    musicDesc += $"[{artist["name"]}]({zingMP3Link.TrimEnd('/') + artist["link"]}), ";
//            }
//            else
//                musicDesc += songDesc["artistsNames"];
//            musicDesc = musicDesc.TrimEnd(" ,".ToCharArray()) + Environment.NewLine;
//            if (songDesc["album"] != null)
//                musicDesc += $"Album: [{songDesc["album"]["title"]}]({zingMP3Link.TrimEnd('/') + songDesc["album"]["link"]})" + Environment.NewLine;
//            return musicDesc;
//        }

//        static string GetSongLink(string zingMP3Link)
//        {
//            HttpRequest http = Utils.InitHttpRequestWithCookie();
//            string id = GetSongID(zingMP3Link);
//            string apiEndpoint = "/api/v2/song/get/streaming";
//            string ctime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
//            string zingMP3Web = http.Get(ZingMP3Player.zingMP3Link, null).ToString();
//            string version = Regex.Match(zingMP3Web, mainMinJSRegex).Groups[1].Value.Replace("v", "");
//            string secretKey = Config.zingMP3SecretKey;
//            string apiKey = Config.zingMP3APIKey;
//            string hash = apiEndpoint + Utils.ToSHA256($"ctime={ctime}id={id}version={version}");
//            string sig = Utils.SHA512_ComputeHash(hash, secretKey);
//            string getSongLinkUrl = $"{ZingMP3Player.zingMP3Link.TrimEnd('/')}{apiEndpoint}?id={id}&ctime={ctime}&version={version}&sig={sig}&apiKey={apiKey}";
//            JObject obj = JObject.Parse(http.Get(getSongLinkUrl, null).ToString());
//            if (obj["err"].ToString() == "0")
//                return obj["data"]["128"].ToString();
//            throw new WebException(obj["err"] + ": " + obj["msg"]);
//        }

//        static string FindSongID(string name)
//        {
//            JObject obj = JObject.Parse(new WebClient().DownloadString("http://ac.mp3.zing.vn/complete?type=song&num=1&query=" + Uri.EscapeUriString(name)));
//            if (bool.Parse(obj["result"].ToString()) && ((JArray)obj["data"]).Count > 0)
//            {
//                string str = obj["data"][0]["song"][0]["id"].ToString();
//                return str;
//            }
//            Console.WriteLine(obj.ToString());
//            throw new WebException("songs not found");
//        }

//        static string GetSongID(string link)
//        {
//            if (link.StartsWith(zingMP3Link))
//                link = Regex.Match(link, "/([a-zA-Z0-9]+).html").Groups[1].Value;
//            return link;
//        }

//        static string GetSongName(string linkOrID)
//        {
//            JObject obj = GetSongInfo(linkOrID);
//            return $"[{obj["title"]}]({zingMP3Link.TrimEnd('/')}{obj["link"]})";
//        }

//        static string RemoveLyricTimestamps(string lyrics)
//        {
//            string result = "";
//            foreach (string sentence in lyrics.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
//            {
//                string lyric = sentence;
//                if (sentence.Contains("]"))
//                    lyric = sentence.Remove(sentence.IndexOf('['), sentence.LastIndexOf(']') - sentence.IndexOf('[') + 1);
//                result += lyric + Environment.NewLine;
//            }
//            result = result.Trim(Environment.NewLine.ToCharArray());
//            return result;
//        }
//    }
//}
