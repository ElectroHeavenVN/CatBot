using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using SoundCloudExplode;
using SoundCloudExplode.Search;
using SoundCloudExplode.Tracks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CatBot.Music.SoundCloud
{
    internal class SoundCloudMusic : IMusic
    {
        internal static readonly Regex regexMatchSoundCloudLink = new Regex("^(?:https?:\\/\\/)?((?:(?:(?:m|on)\\.)?soundcloud\\.com)|(?:snd\\.sc))\\/([\\w-]*)\\/?([\\w-]*)\\??.*$", RegexOptions.Compiled);
        internal static readonly string soundCloudIconLink = "https://cdn.discordapp.com/emojis/1137041961669378241.webp?quality=lossless";
        string link;
        TimeSpan duration;
        string title = "";
        string artists = "";
        string albumThumbnailLink = "";
        internal static SoundCloudClient scClient = new SoundCloudClient();
        Stream musicPCMDataStream;
        string mp3FilePath;
        Track track;
        private bool canGetStream;
        bool _disposed;
        private string pcmFile;

        public SoundCloudMusic() { }
        public SoundCloudMusic(string linkOrKeyword)
        {
            if (!regexMatchSoundCloudLink.IsMatch(linkOrKeyword))
            {
                List<TrackSearchResult> result = scClient.Search.GetTracksAsync(linkOrKeyword).ToListAsync().GetAwaiter().GetResult();
                if (result.Count == 0)
                    throw new MusicException("Ex: songs not found");
                linkOrKeyword = result[0].PermalinkUrl.AbsoluteUri;
            }
            link = linkOrKeyword;
            track = scClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
            title = $"[{track.Title}]({track.PermalinkUrl})";
            artists = $"[{track.User.Username}]({track.User.PermalinkUrl})";
            if (track.ArtworkUrl != null)
                albumThumbnailLink = track.ArtworkUrl.AbsoluteUri;
        }
        ~SoundCloudMusic() => Dispose(false);

        public void Download()
        {
            string downloadUrl = scClient.Tracks.GetDownloadUrlAsync(track).GetAwaiter().GetResult();
            mp3FilePath = Path.GetTempFileName();
            new WebClient().DownloadFile(downloadUrl, mp3FilePath);
            TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            duration = mp3File.Properties.Duration;
            mp3File.Dispose();
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
            File.Delete(mp3FilePath);
            mp3FilePath = null;
        }

        public MusicType MusicType => MusicType.SoundCloud;

        public string PathOrLink => link;

        public TimeSpan Duration => duration;

        public string Title => title;

        public string Artists => artists;

        public string Album => "";

        public string AlbumThumbnailLink => albumThumbnailLink;

        public SponsorBlockOptions SponsorBlockOptions
        {
            get => null;
            set { }
        }

        public Stream MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                return musicPCMDataStream;
            }
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by SoundCloud", soundCloudIconLink);

        public string[] GetFilesInUse() => new string[] { mp3FilePath, pcmFile };

        public string GetIcon() => Config.SoundCloudIcon;

        public LyricData GetLyric() => new LyricData("SoundCloud không lưu trữ lời bài hát!");

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {title}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {artists}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public bool isLinkMatch(string link) => regexMatchSoundCloudLink.IsMatch(link);

        public string GetPCMFilePath() => pcmFile;

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
