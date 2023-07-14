//using DiscordBot.Instance;
//using DSharpPlus.Entities;
//using DSharpPlus.SlashCommands;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace DiscordBot.Obsolete
//{
//    [Obsolete("Code cũ để tham khảo")]
//    [SlashCommandGroup("offline", "Offline music commands")]
//    public class OfflineMusicPlayer : ApplicationCommandModule
//    {
//        DiscordChannel m_lastChannel;
//        internal CancellationTokenSource cts = new CancellationTokenSource();
//        internal Queue<string> musicQueue = new Queue<string>();
//        internal bool isPaused;
//        internal bool isStopped;
//        internal bool isSkipThisSong;
//        internal bool sentOutOfTrack = true;
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
//        internal string currentlyPlayingSong;
//        internal bool isPreparingNextSong;
//        internal bool isPlaying;

//        [SlashCommand("play", "Thêm nhạc vào hàng đợi")]
//        public async Task Play(InteractionContext ctx, [Option("songname", "Tên bài hát"), Autocomplete(typeof(MusicChoiceProvider))] string fileName)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc local khi đang phát nhạc từ Zing MP3!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.musicQueue.Enqueue(fileName);
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.offlineMusicPlayer.InitMainPlay();
//                await ctx.CreateResponseAsync($"Đã thêm bài \"`{Utils.GetSongTitle(fileName)}`\" vào hàng đợi!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("nextup", "Thêm nhạc vào đầu hàng đợi")]
//        public async Task PlayNextUp(InteractionContext ctx, [Option("songname", "Tên bài hát"), Autocomplete(typeof(MusicChoiceProvider))] string fileName)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc local khi đang phát nhạc từ Zing MP3!");
//                    return;
//                }
//                List<string> oldQueue = serverInstance.offlineMusicPlayer.musicQueue.ToList();
//                oldQueue.Insert(0, fileName);
//                serverInstance.offlineMusicPlayer.musicQueue = new Queue<string>(oldQueue);
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.offlineMusicPlayer.InitMainPlay();
//                await ctx.CreateResponseAsync($"Đã thêm bài \"`{Utils.GetSongTitle(fileName)}`\" vào đầu hàng đợi!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("playrandom", "Thêm ngẫu nhiên 1 bài nhạc vào hàng đợi")]
//        public async Task PlayRandom(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc local khi đang phát nhạc từ Zing MP3!");
//                    return;
//                }
//                FileInfo[] musicFiles = new DirectoryInfo(Config.musicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToArray();
//                string musicFileName = Path.GetFileNameWithoutExtension(musicFiles[new Random().Next(0, musicFiles.Length)].Name);
//                serverInstance.offlineMusicPlayer.musicQueue.Enqueue(musicFileName);
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.offlineMusicPlayer.InitMainPlay();
//                await ctx.CreateResponseAsync($"Đã thêm bài \"`{Utils.GetSongTitle(musicFileName)}`\" vào hàng đợi!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("playall", "Thêm toàn bộ nhạc vào hàng đợi")]
//        public async Task PlayAll(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.zingMP3Player.isPlaying)
//                {
//                    await ctx.CreateResponseAsync("Không thể phát nhạc local khi đang phát nhạc từ Zing MP3!");
//                    return;
//                }
//                List<FileInfo> musicFiles2 = new DirectoryInfo(Config.musicFolder).GetFiles().Where(f => f.Extension == ".mp3").ToList();
//                musicFiles2.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
//                foreach (FileInfo musicFile in musicFiles2)
//                    serverInstance.offlineMusicPlayer.musicQueue.Enqueue(Path.GetFileNameWithoutExtension(musicFile.Name));
//                serverInstance.offlineMusicPlayer.isPaused = false;
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                serverInstance.isDisconnect = false;
//                serverInstance.offlineMusicPlayer.InitMainPlay();
//                await ctx.CreateResponseAsync($"Đã thêm {musicFiles2.Count} bài vào hàng đợi! Hiện tại hàng đợi có {serverInstance.offlineMusicPlayer.musicQueue.Count} bài!");
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("nowplaying", "Xem thông tin bài nhạc đang phát")]
//        public async Task NowPlaying(InteractionContext ctx)
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrWhiteSpace(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                    await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithTitle("Không có bài nào đang phát!").WithColor(DiscordColor.Red).Build());
//                else
//                {
//                    TagLib.File musicFile = TagLib.File.Create(Path.Combine(Config.musicFolder, serverInstance.offlineMusicPlayer.currentlyPlayingSong + ".mp3"));
//                    string musicDesc = string.IsNullOrWhiteSpace(musicFile.Tag.Title) ? serverInstance.offlineMusicPlayer.currentlyPlayingSong : ("Bài hát: " + musicFile.Tag.Title) + Environment.NewLine;
//                    string artists = string.Join(", ", musicFile.Tag.Performers);
//                    if (!string.IsNullOrWhiteSpace(artists))
//                        musicDesc += "Nghệ sĩ: " + artists + Environment.NewLine;
//                    if (!string.IsNullOrWhiteSpace(musicFile.Tag.Album))
//                        musicDesc += "Album: " + musicFile.Tag.Album + Environment.NewLine;
//                    musicDesc += new TimeSpan(musicFile.Properties.Duration.Ticks * serverInstance.offlineMusicPlayer.currentMusicStream.Position / serverInstance.offlineMusicPlayer.currentMusicStream.Length).toString() + "/" + musicFile.Properties.Duration.toString();
//                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
//                    if (musicFile.Tag.Pictures.Length > 0 && Utils.TrimStartNullBytes(musicFile.Tag.Pictures[0].Data.Data).Length > 0)
//                    {
//                        DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"image.{TagLib.Picture.GetExtensionFromMime(musicFile.Tag.Pictures[0].MimeType)}", new MemoryStream(Utils.TrimStartNullBytes(musicFile.Tag.Pictures[0].Data.Data)));
//                        DiscordMessage cacheImageMessage = await Config.cacheImageChannel.SendMessageAsync(messageBuilder);
//                        string url = cacheImageMessage.Attachments[0].Url;
//                        await ctx.CreateResponseAsync(embed.WithThumbnail(url).Build());
//                        DiscordMessage message = await ctx.GetOriginalResponseAsync();
//                        await cacheImageMessage.ModifyAsync(message.JumpLink.ToString());
//                    }
//                    else
//                        await ctx.CreateResponseAsync(embed.Build());
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrWhiteSpace(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                TagLib.File musicFile = TagLib.File.Create(Path.Combine(Config.musicFolder, serverInstance.offlineMusicPlayer.currentlyPlayingSong + ".mp3"));
//                int bytesPerSeconds = (int)(serverInstance.offlineMusicPlayer.currentMusicStream.Length / musicFile.Properties.Duration.TotalSeconds);
//                bytesPerSeconds -= bytesPerSeconds % 2;
//                int bytesToSeek = (int)Math.Max(Math.Min(bytesPerSeconds * seconds, serverInstance.offlineMusicPlayer.currentMusicStream.Length - serverInstance.offlineMusicPlayer.currentMusicStream.Position), -serverInstance.offlineMusicPlayer.currentMusicStream.Position);
//                serverInstance.offlineMusicPlayer.currentMusicStream.Position += bytesToSeek;
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Đã tua {(bytesToSeek < 0 ? "lùi " : "")}bài hiện tại {new TimeSpan(0, 0, Math.Abs(bytesToSeek / bytesPerSeconds)).toVietnameseString()}!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if(serverInstance.offlineMusicPlayer.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.musicQueue.Clear();
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.isPaused = true;
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Tạm dừng phát nhạc!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.isPaused = false;
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Tiếp tục phát nhạc!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.isPaused = false;
//                serverInstance.offlineMusicPlayer.isStopped = false;
//                serverInstance.offlineMusicPlayer.isSkipThisSong = true;
//                count = Math.Min(count, serverInstance.offlineMusicPlayer.musicQueue.Count);
//                for (int i = 0; i < count - 1; i++)
//                    serverInstance.offlineMusicPlayer.musicQueue.Dequeue();
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Đã bỏ qua {(count > 1 ? (count.ToString() + " bài nhạc") : "bài nhạc hiện tại")}!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (startIndex >= serverInstance.offlineMusicPlayer.musicQueue.Count)
//                {
//                    await ctx.CreateResponseAsync($"Hàng đợi chỉ có {serverInstance.offlineMusicPlayer.musicQueue.Count} bài!");
//                    return;
//                }
//                List<string> oldQueue = serverInstance.offlineMusicPlayer.musicQueue.ToList();
//                count = Math.Min(count, serverInstance.offlineMusicPlayer.musicQueue.Count - startIndex);
//                for (int i = 0; i < count; i++)
//                    oldQueue.RemoveAt((int)startIndex);
//                serverInstance.offlineMusicPlayer.musicQueue = new Queue<string>(oldQueue);
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"Đã xóa {count} bài nhạc khỏi hàng đợi!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (string.IsNullOrEmpty(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                {
//                    await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                    return;
//                }
//                serverInstance.offlineMusicPlayer.isPaused = false;
//                serverInstance.offlineMusicPlayer.isStopped = true;
//                string response = "Dừng phát nhạc";
//                if (clearQueue)
//                {
//                    serverInstance.offlineMusicPlayer.musicQueue.Clear();
//                    response += " và xóa hàng đợi";
//                }
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(response + "!"));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.offlineMusicPlayer.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
//                {
//                    Title = $"{Math.Min(10, serverInstance.offlineMusicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.offlineMusicPlayer.musicQueue.Count})",
//                };
//                for (int i = 0; i < Math.Min(10, serverInstance.offlineMusicPlayer.musicQueue.Count); i++)
//                    embed.Description += i + 1 + ". " + Utils.GetSongTitle(serverInstance.offlineMusicPlayer.musicQueue.ElementAt(i)) + Environment.NewLine;
//                embed.Build();
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed));
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
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                if (serverInstance.offlineMusicPlayer.musicQueue.Count == 0)
//                {
//                    await ctx.CreateResponseAsync("Không có nhạc trong hàng đợi!");
//                    return;
//                }
//                Queue<string> newMusicQueue = new Queue<string>();
//                List<string> oldMusicQueue = serverInstance.offlineMusicPlayer.musicQueue.ToList();
//                Random random = new Random();
//                int count = oldMusicQueue.Count;
//                for (int i = 0; i < count; i++)
//                {
//                    int index = random.Next(0, oldMusicQueue.Count);
//                    newMusicQueue.Enqueue(oldMusicQueue.ElementAt(index));
//                    oldMusicQueue.RemoveAt(index);
//                }
//                serverInstance.offlineMusicPlayer.musicQueue = newMusicQueue;
//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
//                {
//                    Title = $"{Math.Min(10, serverInstance.offlineMusicPlayer.musicQueue.Count)} bài hát tiếp theo trong hàng đợi (tổng số: {serverInstance.offlineMusicPlayer.musicQueue.Count})",
//                };
//                for (int i = 0; i < Math.Min(10, serverInstance.offlineMusicPlayer.musicQueue.Count); i++)
//                    embed.Description += i + 1 + ". " + Utils.GetSongTitle(serverInstance.offlineMusicPlayer.musicQueue.ElementAt(i)) + Environment.NewLine;
//                embed.Build();
//                await ctx.CreateResponseAsync(DSharpPlus.InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Đã trộn danh sách nhạc trong hàng đợi!").AddEmbed(embed));
//            }
//            catch (Exception ex) { Utils.LogException(ex); }
//        }

//        [SlashCommand("lyric", "Tìm lời bài hát (mặc định tìm lời bài hát đang phát)")]
//        public async Task Lyric(InteractionContext ctx, [Option("name", "Tên bài hát")] string songName = "", [Option("artists", "Tên nghệ sĩ")] string artistsName = "")
//        {
//            try
//            {
//                BotServerInstance serverInstance = BotServerInstance.GetBotServerInstance(ctx.Guild);
//                //await serverInstance.InitializeVoiceNext(ctx.Interaction);
//                serverInstance.offlineMusicPlayer.lastChannel = ctx.Channel;
//                await ctx.DeferAsync();
//                string jsonLyric = "";
//                JObject lyricData = new JObject();
//                if (string.IsNullOrWhiteSpace(songName))
//                {
//                    if (string.IsNullOrEmpty(serverInstance.offlineMusicPlayer.currentlyPlayingSong))
//                    {
//                        await ctx.CreateResponseAsync("Không có bài nào đang phát!");
//                        return;
//                    }
//                    TagLib.File musicFile = TagLib.File.Create(Path.Combine(Config.musicFolder, serverInstance.offlineMusicPlayer.currentlyPlayingSong + ".mp3"));
//                    if (string.IsNullOrWhiteSpace(musicFile.Tag.Title))
//                    {
//                        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Bài hát đang phát không có siêu dữ liệu!"));
//                        return;
//                    }
//                    jsonLyric = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(Config.lyricAPI + musicFile.Tag.Title + "/" + string.Join(", ", musicFile.Tag.Performers)));
//                    lyricData = JObject.Parse(jsonLyric);
//                    if (!lyricData.ContainsKey("lyrics"))
//                    {
//                        string jsonLyricWithoutArtists = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(Config.lyricAPI + musicFile.Tag.Title));
//                        lyricData = JObject.Parse(jsonLyricWithoutArtists);
//                    }
//                }
//                else
//                {
//                    string apiEndpoint = Config.lyricAPI + songName;
//                    if (!string.IsNullOrWhiteSpace(artistsName))
//                        apiEndpoint += "/" + artistsName;
//                    jsonLyric = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(apiEndpoint));
//                    lyricData = JObject.Parse(jsonLyric);
//                }
//                if (!lyricData.ContainsKey("lyrics"))
//                {
//                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Không tìm thấy lời bài hát!"));
//                    return;
//                }

//                DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle($"Lời bài hát {lyricData["title"]} - {lyricData["artist"]}").WithDescription(lyricData["lyrics"].ToString()).WithThumbnail(lyricData["image"].ToString());
//                embed = embed.WithFooter("Powered by lyrist.vercel.app", "https://cdn.discordapp.com/emojis/1124407257787019276.webp?quality=lossless");
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
//                        currentlyPlayingSong = musicQueue.Dequeue();
//                        TagLib.File musicFile;
//                        try
//                        {
//                            musicFile = TagLib.File.Create(Path.Combine(Config.musicFolder, currentlyPlayingSong + ".mp3"));
//                            string musicDesc = string.IsNullOrWhiteSpace(musicFile.Tag.Title) ? currentlyPlayingSong : ("Bài hát: " + musicFile.Tag.Title) + Environment.NewLine;
//                            string artists = string.Join(", ", musicFile.Tag.Performers);
//                            if (!string.IsNullOrWhiteSpace(artists))
//                                musicDesc += "Nghệ sĩ: " + artists + Environment.NewLine;
//                            if (!string.IsNullOrWhiteSpace(musicFile.Tag.Album))
//                                musicDesc += "Album: " + musicFile.Tag.Album + Environment.NewLine;
//                            musicDesc += "Thời lượng: " + musicFile.Properties.Duration.toString();
//                            DiscordEmbedBuilder embed = new DiscordEmbedBuilder().WithTitle("Hiện đang phát").WithDescription(musicDesc).WithColor(DiscordColor.Green);
//                            if (musicFile.Tag.Pictures.Length > 0)
//                            {
//                                DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"image.{TagLib.Picture.GetExtensionFromMime(musicFile.Tag.Pictures[0].MimeType)}", new MemoryStream(Utils.TrimStartNullBytes(musicFile.Tag.Pictures[0].Data.Data)));
//                                DiscordMessage cacheImageMessage = await Config.cacheImageChannel.SendMessageAsync(messageBuilder);
//                                string url = cacheImageMessage.Attachments[0].Url;
//                                DiscordMessage message = await serverInstance.offlineMusicPlayer.lastChannel.SendMessageAsync(embed.WithThumbnail(url).Build());
//                                await cacheImageMessage.ModifyAsync(message.JumpLink.ToString());
//                            }
//                            else
//                                await serverInstance.offlineMusicPlayer.lastChannel.SendMessageAsync(embed.Build());
//                            ResetCurrentMusicStream();
//                            if (nextMusicStream.Capacity != 0)
//                                await nextMusicStream.CopyToAsync(currentMusicStream);
//                            else 
//                            {
//                                Stream fileStream = Utils.GetPCMStream(Path.Combine(Config.musicFolder, currentlyPlayingSong + ".mp3"));
//                                await fileStream.CopyToAsync(currentMusicStream);
//                                fileStream.Dispose();
//                            }
//                            ResetNextMusicStream();
//                            currentMusicStream.Position = 0;
//                        }
//                        catch (Exception ex)
//                        {
//                            Utils.LogException(ex);
//                            await serverInstance.offlineMusicPlayer.lastChannel.SendMessageAsync($"Không tìm thấy bài \"{currentlyPlayingSong}\"!");
//                            continue;
//                        }
//                        byte[] buffer = new byte[serverInstance.currentVoiceNextConnection.GetTransmitSink().SampleLength];
//                        while (currentMusicStream.Read(buffer, 0, buffer.Length) != 0)
//                        {
//                            if (token.IsCancellationRequested)
//                                goto exit;
//                            if (isStopped || isSkipThisSong || serverInstance.currentVoiceNextConnection.isDisposed())
//                                break;
//                            if (!isPreparingNextSong && musicFile.Properties.Duration.Ticks * (1 - serverInstance.offlineMusicPlayer.currentMusicStream.Position / (float)serverInstance.offlineMusicPlayer.currentMusicStream.Length) <= 100000000) //10s
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
//                        currentlyPlayingSong = "";
//                        if (!sentOutOfTrack)
//                        {
//                            ResetCurrentMusicStream();
//                            if (token.IsCancellationRequested)
//                                goto exit;
//                            sentOutOfTrack = true;
//                            await serverInstance.offlineMusicPlayer.lastChannel.SendMessageAsync(new DiscordEmbedBuilder().WithTitle("Đã hết nhạc trong hàng đợi").WithDescription("Vui lòng thêm nhạc vào hàng đợi để nghe tiếp!").WithColor(DiscordColor.Red).Build());
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
//                Stream fileStream = Utils.GetPCMStream(Path.Combine(Config.musicFolder, nextSong + ".mp3"));
//                fileStream.CopyTo(nextMusicStream);
//                fileStream.Dispose();
//                nextMusicStream.Position = 0;
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
//    }

//    internal class MusicChoiceProvider : IAutocompleteProvider
//    {
//        public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
//        {
//            List<DiscordAutoCompleteChoice> choices = new List<DiscordAutoCompleteChoice>();
//            List<FileInfo> musicFiles = new DirectoryInfo(Config.musicFolder).GetFiles().ToList();
//            musicFiles.Sort((f1, f2) => -f1.LastWriteTime.Ticks.CompareTo(f2.LastWriteTime.Ticks));
//            foreach (FileInfo musicFile in musicFiles.Where(f => f.Extension == ".mp3"))
//            {
//                string musicFileName = Utils.GetSongTitle(Path.GetFileNameWithoutExtension(musicFile.Name));
//                if (musicFileName.ToLower().Contains(ctx.FocusedOption.Value.ToString().ToLower()))
//                    choices.Add(new DiscordAutoCompleteChoice(musicFileName, Path.GetFileNameWithoutExtension(musicFile.Name)));
//                if (choices.Count >= 25)
//                    break;
//            }
//            return Task.FromResult(choices.AsEnumerable());
//        }
//    }
//}
