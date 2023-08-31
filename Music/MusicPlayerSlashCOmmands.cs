using DiscordBot.Instance;
using DiscordBot.Music.Local;
using DiscordBot.Music.NhacCuaTui;
using DiscordBot.Music.YouTube;
using DiscordBot.Music.ZingMP3;
using DiscordBot.Music.SoundCloud;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DiscordBot.Music.Spotify;
using DiscordBot.Voice;

namespace DiscordBot.Music
{
    public class MusicPlayerSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("play", "Bắt đầu phát nhạc")]
        public async Task Play(InteractionContext ctx, [Option("input", "Từ khóa hoặc link")] string input = "", [Option("type", "Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào không phải là link)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.Play(ctx, input, musicType);

        [SlashCommand("enqueue", "Thêm nhạc vào hàng đợi")]
        public async Task Enqueue(InteractionContext ctx, [Option("input", "Từ khóa hoặc link")] string input, [Option("type", "Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào không phải là link)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.Play(ctx, input, musicType);

        [SlashCommand("playlocal", "Thêm nhạc local vào hàng đợi")]
        public async Task PlayLocalMusic(InteractionContext ctx, [Option("name", "Tên bài hát"), Autocomplete(typeof(LocalMusicChoiceProvider))] string name) => await MusicPlayerCore.Play(ctx, name, MusicType.Local);

        [SlashCommand("youtube", "Thêm video YouTube vào hàng đợi")]
        public async Task PlayYouTubeVideo(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(YouTubeMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.YouTube);
        
        [SlashCommand("nhaccuatui", "Thêm nhạc từ NhacCuaTui vào hàng đợi")]
        public async Task PlayNhacCuaTuiMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(NhacCuaTuiMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.NhacCuaTui);

        [SlashCommand("zingmp3", "Thêm nhạc từ ZingMP3 vào hàng đợi")]
        public async Task PlayZingMP3Music(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(ZingMP3MusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.ZingMP3);
        
        [SlashCommand("soundcloud", "Thêm nhạc từ SoundCloud vào hàng đợi")]
        public async Task PlaySoundCloudMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(SoundCloudMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.SoundCloud);
        
        [SlashCommand("spotify", "Thêm nhạc từ Spotify vào hàng đợi")]
        public async Task PlaySpotifyMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(SpotifyMusicChoiceProvider))] string input) => await MusicPlayerCore.Play(ctx, input, MusicType.Spotify);

        [SlashCommand("nextup", "Thêm nhạc vào đầu hàng đợi")]
        public async Task PlayNextUp(InteractionContext ctx, [Option("input", "Từ khóa hoặc link")] string input, [Option("type", "Loại nhạc (mặc định sẽ tìm nhạc từ SoundCloud nếu dữ liệu nhập vào là tên bài hát)")] MusicType musicType = MusicType.SoundCloud) => await MusicPlayerCore.PlayNextUp(ctx, input, musicType);

        [SlashCommand("nextuplocal", "Thêm nhạc vào đầu hàng đợi")]
        public async Task PlayNextUpLocalMusic(InteractionContext ctx, [Option("name", "Tên bài hát"), Autocomplete(typeof(LocalMusicChoiceProvider))] string name) => await MusicPlayerCore.PlayNextUp(ctx, name, MusicType.Local);

        [SlashCommand("nextupyt", "Thêm video YouTube vào đầu hàng đợi")]
        public async Task PlayNextUpYouTubeVideo(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(YouTubeMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.YouTube);

        [SlashCommand("nextupnct", "Thêm nhạc từ NhacCuaTui vào đầu hàng đợi")]
        public async Task PlayNextUpNhacCuaTuiMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(NhacCuaTuiMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.NhacCuaTui);

        [SlashCommand("nextupzing", "Thêm nhạc từ ZingMP3 vào đầu hàng đợi")]
        public async Task PlayNextUpZingMP3Music(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(ZingMP3MusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.ZingMP3);
        
        [SlashCommand("nextupsc", "Thêm nhạc từ SoundCloud vào đầu hàng đợi")]
        public async Task PlayNextUpSoundCloudMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(SoundCloudMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.SoundCloud);
        
        [SlashCommand("nextupsp", "Thêm nhạc từ Spotify vào đầu hàng đợi")]
        public async Task PlayNextUpSpotifyMusic(InteractionContext ctx, [Option("input", "Từ khóa hoặc link"), Autocomplete(typeof(SpotifyMusicChoiceProvider))] string input) => await MusicPlayerCore.PlayNextUp(ctx, input, MusicType.Spotify);

        [SlashCommand("playrandom", "Thêm ngẫu nhiên 1 bài nhạc local vào hàng đợi")]
        public async Task PlayRandomLocalMusic(InteractionContext ctx, [Option("count", "Số lượng bài nhạc"), Minimum(1), Maximum(int.MaxValue)] long count = 1) => await MusicPlayerCore.PlayRandomLocalMusic(ctx, count);

        [SlashCommand("playall", "Thêm toàn bộ nhạc local vào hàng đợi")]
        public async Task PlayAllLocalMusic(InteractionContext ctx) => await MusicPlayerCore.PlayAllLocalMusic(ctx);

        [SlashCommand("nowplaying", "Xem thông tin bài nhạc đang phát")]
        public async Task NowPlaying(InteractionContext ctx) => await MusicPlayerCore.NowPlaying(ctx);

        [SlashCommand("seek", "Tua bài hiện tại")]
        public async Task Seek(InteractionContext ctx, [Option("seconds", "số giây để tua (mặc định: 10)"), Minimum(int.MinValue), Maximum(int.MaxValue)] long seconds = 10) => await MusicPlayerCore.Seek(ctx, seconds);
        
        [SlashCommand("seekto", "Tua bài hiện tại đến vị trí chỉ định")]
        public async Task SeekTo(InteractionContext ctx, [Option("seconds", "số giây tính từ đầu (mặc định: 0)"), Minimum(int.MinValue), Maximum(int.MaxValue)] long seconds = 0) => await MusicPlayerCore.SeekTo(ctx, seconds);

        [SlashCommand("clear", "Xóa hết nhạc trong hàng đợi")]
        public async Task Clear(InteractionContext ctx) => await MusicPlayerCore.Clear(ctx);

        [SlashCommand("pause", "Tạm dừng nhạc")]
        public async Task Pause(InteractionContext ctx) => await MusicPlayerCore.Pause(ctx);

        [SlashCommand("resume", "Tiếp tục phát nhạc")]
        public async Task Resume(InteractionContext ctx) => await MusicPlayerCore.Resume(ctx);

        [SlashCommand("skip", "Bỏ qua bài hát")]
        public async Task Skip(InteractionContext ctx, [Option("count", "Số bài bỏ qua (mặc định: 1)"), Minimum(1), Maximum(int.MaxValue)] long count = 1) => await MusicPlayerCore.Skip(ctx, count);

        [SlashCommand("remove", "Xóa nhạc trong hàng đợi")]
        public async Task Remove(InteractionContext ctx, [Option("index", "Vị trí xóa bài hát"), Minimum(0), Maximum(int.MaxValue)] long startIndex = 0, [Option("count", "Số lượng bài hát"), Minimum(1), Maximum(int.MaxValue)] long count = 1) => await MusicPlayerCore.Remove(ctx, startIndex, count);

        [SlashCommand("stop", "Dừng phát nhạc")]
        public async Task Stop(InteractionContext ctx, [Option("clearQueue", "Xóa nhạc trong hàng đợi"), Choice("Có", "true"), Choice("Không", "false")] string clearQueueStr = "true") => await MusicPlayerCore.Stop(ctx, clearQueueStr);

        [SlashCommand("queue", "Xem hàng đợi nhạc")]
        public async Task Queue(InteractionContext ctx) => await MusicPlayerCore.Queue(ctx);

        [SlashCommand("mode", "Thay đổi chế độ phát nhạc")]
        public async Task SetPlayMode(InteractionContext ctx, [Option("playMode", "Chế độ phát nhạc")] PlayModeChoice playMode) => await MusicPlayerCore.SetPlayMode(ctx, playMode);

        [SlashCommand("shuffle", "Trộn danh sách nhạc trong hàng đợi")]
        public async Task ShuffleQueue(InteractionContext ctx) => await MusicPlayerCore.ShuffleQueue(ctx);

        [SlashCommand("lyric", "Tìm lời bài hát (mặc định tìm lời bài hát đang phát)")]
        public async Task Lyric(InteractionContext ctx, [Option("name", "Tên bài hát")] string name = "", [Option("artists", "Tên nghệ sĩ")] string artistsName = "") => await MusicPlayerCore.Lyric(ctx, name, artistsName);

        [SlashCommand("sponsorblock", "Thêm vào hoặc xóa loại phân đoạn SponsorBlock khỏi danh sách phân đoạn bỏ qua")]
        public async Task AddOrRemoveSponsorBlockOption(InteractionContext ctx, [Option("type", "Loại phân đoạn")] SponsorBlockSectionType type = 0) => await MusicPlayerCore.AddOrRemoveSponsorBlockOption(ctx, type);

        [SlashCommand("musicvolume", "Xem hoặc chỉnh âm lượng nhạc của bot")]
        public async Task SetSFXVolume(InteractionContext ctx, [Option("volume", "Âm lượng"), Minimum(0), Maximum(250)] long volume = -1) => await MusicPlayerCore.SetVolume(ctx.Interaction, volume);

        [SlashCommand("albumartwork", "Xem ảnh album của bài đang phát")]
        public async Task ViewAlbumArtwork(InteractionContext ctx) => await MusicPlayerCore.ViewAlbumArtwork(ctx);
    }
}
