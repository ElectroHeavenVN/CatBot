﻿using DSharpPlus.Entities;
using SoundCloudExplode;
using SoundCloudExplode.Search;
using SoundCloudExplode.Track;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Music.SoundCloud
{
    internal class SoundCloudMusic : IMusic
    {
        internal static readonly string soundCloudLink = "https://soundcloud.com/";
        private readonly string soundCloudIconLink = "https://w.soundcloud.com/icon/assets/images/orange_white_32-94fc761.png";
        string link;
        TimeSpan duration;
        string title = "";
        string artists = "";
        string albumThumbnailLink = "";
        internal static SoundCloudClient scClient = new SoundCloudClient();
        Stream musicPCMDataStream;
        string mp3FilePath;
        TrackInformation track;
        private bool canGetStream;
        private Exception exception;
        bool _disposed;
        private string pcmFile;

        public SoundCloudMusic() { }
        public SoundCloudMusic(string linkOrKeyword)
        {
            if (!linkOrKeyword.StartsWith(soundCloudLink))
            {
                List<TrackSearchResult> result = scClient.Search.GetTracksAsync(linkOrKeyword).GetAwaiter().GetResult();
                if (result.Count == 0)
                    throw new WebException("songs not found");
                linkOrKeyword = result[0].PermalinkUrl.AbsoluteUri;
            }
            link = linkOrKeyword;
            track = scClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
            title = $"[{track.Title}]({track.PermalinkUrl})";
            artists = $"[{track.User.Username}]({track.User.PermalinkUrl})";
            if (track.ArtworkUrl != null)
                albumThumbnailLink = track.ArtworkUrl.AbsoluteUri;
            new Thread(GetDuration) { IsBackground = true }.Start();
        }
        ~SoundCloudMusic() => Dispose(false);

        async void GetDuration()
        {
            string downloadUrl = await scClient.Tracks.GetDownloadUrlAsync(track);
            try
            {
                mp3FilePath = Path.GetTempFileName();
                new WebClient().DownloadFile(downloadUrl, mp3FilePath);
                TagLib.File mp3File = TagLib.File.Create(mp3FilePath, "taglib/mp3", TagLib.ReadStyle.Average);
                duration = mp3File.Properties.Duration;
                mp3File.Dispose();
            }
            catch (WebException ex)
            {
                exception = ex;
            }
            canGetStream = true;
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
                if (musicPCMDataStream == null)
                {
                    while (!canGetStream)
                        Thread.Sleep(500);
                    if (exception != null)
                        throw exception;
                    musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3FilePath, ref pcmFile));
                    File.Delete(mp3FilePath);
                    mp3FilePath = null;
                }
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
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + "/" + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

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