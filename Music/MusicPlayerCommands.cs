using System.ComponentModel;
using CatBot.Music.Local;
using CatBot.Music.NhacCuaTui;
using CatBot.Music.SoundCloud;
using CatBot.Music.SponsorBlock;
using CatBot.Music.Spotify;
using CatBot.Music.YouTube;
using CatBot.Music.ZingMP3;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;

namespace CatBot.Music
{
    public class MusicPlayerCommands
    {
        #region Slash-only commands
        [Command("youtube"), Description("Thêm video YouTube vào hàng đợi")]
        public async Task PlayYouTubeVideo(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(YouTubeMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.YouTube);

        [Command("nhaccuatui"), Description("Thêm nhạc từ NhacCuaTui vào hàng đợi")]
        public async Task PlayNhacCuaTuiMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(NhacCuaTuiMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.NhacCuaTui);

        [Command("zingmp3"), Description("Thêm nhạc từ ZingMP3 vào hàng đợi")]
        public async Task PlayZingMP3Music(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(ZingMP3MusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.ZingMP3);

        [Command("soundcloud"), Description("Thêm nhạc từ SoundCloud vào hàng đợi")]
        public async Task PlaySoundCloudMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(SoundCloudMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.SoundCloud);

        [Command("spotify"), Description("Thêm nhạc từ Spotify vào hàng đợi")]
        public async Task PlaySpotifyMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(SpotifyMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.Spotify);

        [Command("nextup-yt"), Description("Thêm video YouTube vào đầu hàng đợi")]
        public async Task PlayNextUpYouTubeVideo(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(YouTubeMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.YouTube);

        [Command("nextup-nct"), Description("Thêm nhạc từ NhacCuaTui vào đầu hàng đợi")]
        public async Task PlayNextUpNhacCuaTuiMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(NhacCuaTuiMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.NhacCuaTui);

        [Command("nextup-zing"), Description("Thêm nhạc từ ZingMP3 vào đầu hàng đợi")]
        public async Task PlayNextUpZingMP3Music(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(ZingMP3MusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.ZingMP3);

        [Command("nextup-sc"), Description("Thêm nhạc từ SoundCloud vào đầu hàng đợi")]
        public async Task PlayNextUpSoundCloudMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(SoundCloudMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.SoundCloud);

        [Command("nextup-sp"), Description("Thêm nhạc từ Spotify vào đầu hàng đợi")]
        public async Task PlayNextUpSpotifyMusic(SlashCommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link"), SlashAutoCompleteProvider(typeof(SpotifyMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.Spotify);

        [Command("play-local"), Description("Thêm nhạc local vào hàng đợi")]
        public async Task PlayLocalMusic(SlashCommandContext ctx, [Parameter("name"), Description("Tên bài hát"), SlashAutoCompleteProvider(typeof(LocalMusicChoiceProvider))] string name) => await MusicPlayerCore.Play(ctx, name, MusicType.Local);

        [Command("nextup-local"), Description("Thêm nhạc vào đầu hàng đợi")]
        public async Task PlayNextUpLocalMusic(SlashCommandContext ctx, [Parameter("name"), Description("Tên bài hát"), SlashAutoCompleteProvider(typeof(LocalMusicChoiceProvider))] string name) => await MusicPlayerCore.PlayNextUp(ctx, name, MusicType.Local);
        #endregion

        [Command("play"), Description("Bắt đầu phát nhạc")]
        public async Task Play(CommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link")] string input = "", [Parameter("type"), Description("Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào không phải là link)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.Play(ctx, input, musicType);

        [Command("enqueue"), Description("Thêm nhạc vào hàng đợi")]
        public async Task Enqueue(CommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link")] string input, [Parameter("type"), Description("Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào không phải là link)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.Play(ctx, input, musicType);

        [Command("nextup"), Description("Thêm nhạc vào đầu hàng đợi")]
        public async Task PlayNextUp(CommandContext ctx, [Parameter("input"), Description("Từ khóa hoặc link")] string input, [Parameter("type"), Description("Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào là tên bài hát)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.PlayNextUp(ctx, input, musicType);

        [Command("play-local-random"), Description("Thêm ngẫu nhiên 1 bài nhạc local vào hàng đợi")]
        public async Task PlayRandomLocalMusic(CommandContext ctx, [Parameter("count"), Description("Số lượng bài nhạc"), MinMaxValue((long)1, (long)int.MaxValue)] long count = 1) => await MusicPlayerCore.PlayRandomLocalMusic(ctx, count);

        [Command("play-local-all"), Description("Thêm toàn bộ nhạc local vào hàng đợi")]
        public async Task PlayAllLocalMusic(CommandContext ctx, [Parameter("search"), Description("Từ khóa lọc các bài hát")] string search = "") => await MusicPlayerCore.PlayAllLocalMusic(ctx, search);

        [Command("now-playing"), Description("Xem thông tin bài nhạc đang phát")]
        public async Task NowPlaying(CommandContext ctx) => await MusicPlayerCore.NowPlaying(ctx);

        [Command("seek"), Description("Tua bài hiện tại")]
        public async Task Seek(CommandContext ctx, [Parameter("seconds"), Description("số giây để tua (mặc định: 10)"), MinMaxValue((long)int.MinValue, (long)int.MaxValue)] long seconds = 10) => await MusicPlayerCore.Seek(ctx, seconds);

        [Command("seek-to"), Description("Tua bài hiện tại đến vị trí chỉ định")]
        public async Task SeekTo(CommandContext ctx, [Parameter("seconds"), Description("số giây tính từ đầu (mặc định: 0)"), MinMaxValue((long)int.MinValue, (long)int.MaxValue)] long seconds = 0) => await MusicPlayerCore.SeekTo(ctx, seconds);

        [Command("clear"), Description("Xóa hết nhạc trong hàng đợi")]
        public async Task Clear(CommandContext ctx) => await MusicPlayerCore.Clear(ctx);

        [Command("pause"), Description("Tạm dừng nhạc")]
        public async Task Pause(CommandContext ctx) => await MusicPlayerCore.Pause(ctx);

        [Command("resume"), Description("Tiếp tục phát nhạc")]
        public async Task Resume(CommandContext ctx) => await MusicPlayerCore.Resume(ctx);

        [Command("skip"), Description("Bỏ qua bài hát")]
        public async Task Skip(CommandContext ctx, [Parameter("count"), Description("Số bài bỏ qua (mặc định: 1)"), MinMaxValue((long)1, (long)int.MaxValue)] long count = 1) => await MusicPlayerCore.Skip(ctx, count);
        
        [Command("next"), Description("Bỏ qua bài hát hiện tại")]
        public async Task NextSong(CommandContext ctx) => await MusicPlayerCore.Skip(ctx, 1);

        [Command("remove"), Description("Xóa nhạc trong hàng đợi")]
        public async Task Remove(CommandContext ctx, [Parameter("index"), Description("Vị trí xóa bài hát"), MinMaxValue((long)0, (long)int.MaxValue)] long startIndex = 0, [Parameter("count"), Description("Số lượng bài hát"), MinMaxValue((long)1, (long)int.MaxValue)] long count = 1) => await MusicPlayerCore.Remove(ctx, startIndex, count);

        [Command("stop-music"), Description("Dừng phát nhạc")]
        // [Option("clearQueue", "Xóa nhạc trong hàng đợi"), Choice("Có", "true"), Choice("Không", "false")]
        public async Task Stop(CommandContext ctx, [Parameter("clearQueue"), Description("Xóa nhạc trong hàng đợi")] bool clearQueue = true) => await MusicPlayerCore.Stop(ctx, clearQueue);

        [Command("queue"), Description("Xem hàng đợi nhạc")]
        public async Task Queue(CommandContext ctx) => await MusicPlayerCore.Queue(ctx);

        [Command("mode"), Description("Thay đổi chế độ phát nhạc")]
        public async Task SetPlayMode(CommandContext ctx, [Parameter("playMode"), Description("Chế độ phát nhạc")] PlayModeChoice playMode) => await MusicPlayerCore.SetPlayMode(ctx, playMode);

        [Command("shuffle"), Description("Trộn danh sách nhạc trong hàng đợi")]
        public async Task ShuffleQueue(CommandContext ctx) => await MusicPlayerCore.ShuffleQueue(ctx);

        [Command("reverse"), Description("Đảo danh sách nhạc trong hàng đợi")]
        public async Task ReverseQueue(CommandContext ctx) => await MusicPlayerCore.ReverseQueue(ctx);

        [Command("lyric"), Description("Tìm lời bài hát (mặc định tìm lời bài hát đang phát)")]
        public async Task Lyric(CommandContext ctx, [Parameter("name"), Description("Tên bài hát")] string name = "", [Parameter("artists"), Description("Tên nghệ sĩ")] string artistsName = "") => await MusicPlayerCore.Lyric(ctx, name, artistsName);

        [Command("sponsorblock"), Description("Thêm vào hoặc xóa loại phân đoạn SponsorBlock khỏi danh sách phân đoạn bỏ qua")]
        public async Task AddOrRemoveSponsorBlockOption(CommandContext ctx, [Parameter("type"), Description("Loại phân đoạn")] SponsorBlockCategory type = 0) => await MusicPlayerCore.AddOrRemoveSponsorBlockOption(ctx, type);

        [Command("musicvolume"), TextAlias("mvol"), Description("Xem hoặc chỉnh âm lượng nhạc của bot")]
        public async Task SetSFXVolume(CommandContext ctx, [Parameter("volume"), Description("Âm lượng"), MinMaxValue((long)-1, (long)250)] long volume = -1) => await MusicPlayerCore.SetVolume(ctx, volume);

        [Command("artwork"), Description("Xem ảnh album của bài đang phát")]
        public async Task ViewAlbumArtwork(CommandContext ctx) => await MusicPlayerCore.ViewAlbumArtwork(ctx);

        [Command("setvcstatus"), Description("Bật/tắt tự động đặt trạng thái kênh thoại thành bài hát đang phát")]
        public async Task SetAutoSetVoiceChannelStatus(CommandContext ctx) => await MusicPlayerCore.SetAutoSetVoiceChannelStatus(ctx);

        [Command("download"), Description("Tải bài hát hiện tại xuống")]
        public async Task Download(CommandContext ctx) => await MusicPlayerCore.Download(ctx);

        [Command("locksession"), Description("Khoá phiên phát nhạc (chỉ quản trị viên có quyền điểu khiển nhạc)")]
        public async Task LockSession(CommandContext ctx, [Parameter(nameof(value)), Description("Khoá hay mở?")] bool value = true) => await MusicPlayerCore.LockSession(ctx, value);
    }
}
