using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using SoundCloudExplode;
using SoundCloudExplode.Search;
using SoundCloudExplode.Tracks;

namespace CatBot.Music.SoundCloud
{
    internal partial class SoundCloudMusic : IMusic
    {
        [GeneratedRegex("^(?:https?:\\/\\/)?((?:(?:(?:m|on)\\.)?soundcloud\\.com)|(?:snd\\.sc))\\/([\\w-]*)\\/?([\\w-]*)\\??.*$", RegexOptions.Compiled)]
        internal static partial Regex GetRegexMatchSoundCloudLink();
        internal static readonly string soundCloudIconLink = "https://cdn.discordapp.com/emojis/1137041961669378241.webp?quality=lossless";
        internal static SoundCloudClient scClient;
        Track? track;
        string link = "";
        TimeSpan duration;
        string title = "";
        string[] artists = [];
        string[] artistsWithLinks = [];
        string albumThumbnailLink = "";
        string mp3FilePath = "";
        string pcmFile = "";
        FileStream? musicPCMDataStream;
        bool canGetStream;
        bool _disposed;

        static SoundCloudMusic()
        {
            if (!string.IsNullOrEmpty(Config.gI().SoundCloudClientID))
                scClient = new SoundCloudClient(Config.gI().SoundCloudClientID);
            else
            {
                scClient = new SoundCloudClient();
                scClient.InitializeAsync().Wait();
            }
        }

        public SoundCloudMusic() { }
        public SoundCloudMusic(string linkOrKeyword)
        {
            if (!GetRegexMatchSoundCloudLink().IsMatch(linkOrKeyword))
            {
                List<TrackSearchResult> result = scClient.Search.GetTracksAsync(linkOrKeyword).ToListAsync().Result;
                if (result.Count == 0)
                    throw new MusicException("songs not found");
                linkOrKeyword = result[0].PermalinkUrl?.AbsoluteUri ?? "";
            }
            ValueTask<Track?> valueTask = scClient.Tracks.GetAsync(linkOrKeyword);
            while (!valueTask.IsCompleted)
                Thread.Sleep(100);
            track = valueTask.Result;
            if (track is null)
                throw new MusicException("not found");
            link = track.PermalinkUrl?.AbsoluteUri ?? "";
            title = track.Title ?? "";
            artists = [track.User?.Username ?? ""];
            artistsWithLinks = [Formatter.MaskedUrl(track.User?.Username ?? "", new Uri(track.User?.PermalinkUrl ?? ""))];
            if (track.ArtworkUrl is not null)
                albumThumbnailLink = track.ArtworkUrl.AbsoluteUri;
        }
        ~SoundCloudMusic() => Dispose(false);

        public void Download()
        {
            ValueTask<string?> valueTask = scClient.Tracks.GetDownloadUrlAsync(track);
            while (!valueTask.IsCompleted)
                Thread.Sleep(100);
            string? downloadUrl = valueTask.Result;
            if (string.IsNullOrEmpty(downloadUrl))
                throw new MusicException("Download link not found");
            mp3FilePath = Path.GetTempFileName();
            HttpClient httpClient = new HttpClient();
            byte[] data = httpClient.GetByteArrayAsync(downloadUrl).Result;
            File.WriteAllBytes(mp3FilePath, data);
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            //File.Delete(mp3FilePath);
            //mp3FilePath = null;
        }

        public MusicType MusicType => MusicType.SoundCloud;
        public string PathOrLink => link;
        public TimeSpan Duration => duration;
        public string Title => title;
        public string TitleWithLink => Formatter.MaskedUrl(title, new Uri(link));
        public string[] Artists => artists;
        public string[] ArtistsWithLinks => artistsWithLinks;
        public string AllArtists => string.Join(", ", artists);
        public string AllArtistsWithLinks => string.Join(", ", artistsWithLinks);
        public string Album => "";
        public string AlbumWithLink => "";
        public string AlbumThumbnailLink => albumThumbnailLink;
        public SponsorBlockOptions? SponsorBlockOptions
        {
            get => null;
            set { }
        }

        public Stream? MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                return musicPCMDataStream;
            }
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by SoundCloud", soundCloudIconLink);

        public string[] GetFilesInUse() => [mp3FilePath, pcmFile];

        public string GetIcon() => Config.gI().SoundCloudIcon;

        public LyricData? GetLyric() => new LyricData("SoundCloud không lưu trữ lời bài hát!");

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {TitleWithLink}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {AllArtistsWithLinks}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public bool isLinkMatch(string link) => GetRegexMatchSoundCloudLink().IsMatch(link);

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload(".mp3", new FileStream(mp3FilePath, FileMode.Open, FileAccess.Read));

        public string GetPCMFilePath() => pcmFile;

        public void DeletePCMFile()
        {
            try
            {
                File.Delete(pcmFile);
                pcmFile = "";
                musicPCMDataStream?.Dispose();
                musicPCMDataStream = null;
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (disposing)
            {
                musicPCMDataStream?.Dispose();
                DeletePCMFile();
                try
                {
                    File.Delete(mp3FilePath);
                }
                catch (Exception) { }
            }
            musicPCMDataStream = null;
        }
    }
}
