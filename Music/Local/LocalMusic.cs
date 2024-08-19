using System.Reflection;
using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace CatBot.Music.Local
{
    internal class LocalMusic : IMusic
    {
        string path = "";
        TimeSpan duration = TimeSpan.Zero;
        string title = "";
        string[] artists = [];
        string album = "";
        byte[] albumThumbnailData = [];
        string albumThumbnailExt = "";
        string pcmFile = "";
        FileStream? musicPCMDataStream;
        bool _disposed;
        LyricData? lyric;

        public LocalMusic() { }

        public LocalMusic(string path)
        {
            path = Path.Combine(Config.gI().MusicFolder, path.EndsWith(".mp3") ? path : (path + ".mp3"));
            if (!File.Exists(path))
                path = new DirectoryInfo(Config.gI().MusicFolder).GetFiles(path + "*")[0].FullName;
            this.path = path;
            try
            {
                TagLib.File musicFile = TagLib.File.Create(path);
                duration = musicFile.Properties.Duration;
                title = string.IsNullOrWhiteSpace(musicFile.Tag.Title) ? Path.GetFileNameWithoutExtension(path) : musicFile.Tag.Title;
                artists = musicFile.Tag.Performers;
                album = musicFile.Tag.Album ?? "";
                if (musicFile.Tag.Pictures.Length != 0 && musicFile.Tag.Pictures[0].Data.Data.Length != 0)
                {
                    albumThumbnailData = MusicUtils.TrimStartNullBytes(musicFile.Tag.Pictures[0].Data.Data);
                    albumThumbnailExt = TagLib.Picture.GetExtensionFromMime(musicFile.Tag.Pictures[0].MimeType);
                }
            }
            catch (Exception) { throw new MusicException(MusicType.Local, "file not found"); }
        }

        ~LocalMusic() => Dispose(false);

        public void Download() => musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(path, ref pcmFile));

        public MusicType MusicType => MusicType.Local;
        public string PathOrLink => path;
        public TimeSpan Duration => duration;
        public string Title => title;
        public string TitleWithLink => title;
        public string AllArtists => string.Join(", ", artists);
        public string AllArtistsWithLinks => string.Join(", ", artists);
        public string[] Artists => artists;
        public string[] ArtistsWithLinks => artists;
        public string Album => album;
        public string AlbumWithLink => album;
        public string AlbumThumbnailLink
        {
            get
            {
                if (albumThumbnailData == null)
                    return "";
                string hash = Utils.ComputeSHA256Hash(albumThumbnailData);
                if (Data.gI().CachedLocalSongAlbumArtworks.TryGetValue(hash, out string? cachedLink))
                    return cachedLink;
                DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"image.{albumThumbnailExt}", new MemoryStream(albumThumbnailData));
                DiscordMessage message = Config.gI().cacheImageChannel.SendMessageAsync(messageBuilder).Result;
                string url = message.Attachments[0].Url ?? "";
                if (string.IsNullOrWhiteSpace(url))
                    return "";
                Data.gI().CachedLocalSongAlbumArtworks.Add(hash, url);
                return url;
            }
        }
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

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed;

        public LyricData? GetLyric()
        {
            if (lyric != null)
                return lyric;
            if (this.TryGetLyricsFromLRCLIB(out LyricData? result))
                return lyric = result;
            return new LyricData("Không tìm thấy lời bài hát!");
        }

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            string musicDesc = "Bài hát: " + TitleWithLink + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(AllArtistsWithLinks))
                musicDesc += "Nghệ sĩ: " + AllArtistsWithLinks + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(AlbumWithLink))
                musicDesc += "Album: " + AlbumWithLink + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public bool isLinkMatch(string link) => false;

        public string GetPCMFilePath() => pcmFile;

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload(Path.GetExtension(path), new FileStream(path, FileMode.Open, FileAccess.Read));

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

        public string[] GetFilesInUse() => [pcmFile, path];

        public string GetIcon() => "\ud83d\udcc1";

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
            }
            musicPCMDataStream = null;
        }
    }
}
