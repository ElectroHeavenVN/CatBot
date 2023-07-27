using DiscordBot.Instance;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music.Local
{
    internal class LocalMusic : IMusic
    {
        string path;
        internal DiscordMessage lastCacheImageMessage;
        TimeSpan duration;
        string title;
        string artists;
        string album;
        byte[] albumThumbnailData;
        string albumThumbnailExt;
        string pcmFile;
        FileStream musicPCMDataStream;
        bool _disposed;

        internal LocalMusic() { }

        internal LocalMusic(string path)
        {
            path = Path.Combine(Config.MusicFolder, path.EndsWith(".mp3") ? path : (path + ".mp3"));
            this.path = path;
            TagLib.File musicFile = TagLib.File.Create(path);
            duration = musicFile.Properties.Duration;
            title = string.IsNullOrWhiteSpace(musicFile.Tag.Title) ? Path.GetFileNameWithoutExtension(path) : musicFile.Tag.Title;
            artists = string.Join(", ", musicFile.Tag.Performers);
            album = musicFile.Tag.Album;
            if (musicFile.Tag.Pictures.Length != 0 && musicFile.Tag.Pictures[0].Data.Data.Length != 0)
            {
                albumThumbnailData = MusicUtils.TrimStartNullBytes(musicFile.Tag.Pictures[0].Data.Data);
                albumThumbnailExt = TagLib.Picture.GetExtensionFromMime(musicFile.Tag.Pictures[0].MimeType);
            }
        }

        ~LocalMusic() => Dispose(false);

        public MusicType MusicType => MusicType.Local;

        public string PathOrLink => path;

        public TimeSpan Duration => duration;

        public string Title => title;

        public string Artists => artists;

        public string Album => album;

        public string AlbumThumbnailLink
        {
            get
            {
                if (albumThumbnailData == null)
                    return "";
                DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder().AddFile($"image.{albumThumbnailExt}", new MemoryStream(albumThumbnailData));
                lastCacheImageMessage = Config.cacheImageChannel.SendMessageAsync(messageBuilder).GetAwaiter().GetResult();
                return lastCacheImageMessage.Attachments[0].Url;
            }
        }

        public Stream MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                if (musicPCMDataStream == null)
                    musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(path, ref pcmFile));
                return musicPCMDataStream;
            }
        }

        public SponsorBlockOptions SponsorBlockOptions
        {
            get => null;
            set { }
        }
        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed;

        public LyricData GetLyric()
        {
            if (string.IsNullOrWhiteSpace(Title))
                return null;
            string jsonLyric = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(Config.LyricAPI + Title + "/" + artists));
            JObject lyricData = JObject.Parse(jsonLyric);
            if (!lyricData.ContainsKey("lyrics"))
            {
                string jsonLyricWithoutArtists = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(Uri.EscapeUriString(Config.LyricAPI + Title));
                lyricData = JObject.Parse(jsonLyricWithoutArtists);
            }
            if (!lyricData.ContainsKey("lyrics"))
                return null;
            return new LyricData(lyricData["title"].ToString(), lyricData["artist"].ToString(), lyricData["lyrics"].ToString(), lyricData["image"].ToString());
        }

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            string musicDesc = "Bài hát: " + Title + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(Artists))
                musicDesc += "Nghệ sĩ: " + Artists + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(Album))
                musicDesc += "Album: " + Album + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public bool isLinkMatch(string link) => false;

        public void DeletePCMFile()
        {
            try
            {
                File.Delete(pcmFile);
                pcmFile = null;
                musicPCMDataStream?.Dispose();
                musicPCMDataStream = null;
            }
            catch (Exception) { }
        }

        public string[] GetFilesInUse() => new string[] { pcmFile };

        public string GetIcon() => Config.LocalMusicIcon;

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
            lastCacheImageMessage = null;
        }
    }
}
