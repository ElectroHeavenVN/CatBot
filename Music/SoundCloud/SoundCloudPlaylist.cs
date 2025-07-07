using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Music.SponsorBlock;
using CatBot.SoundCloudExplodeExtension;
using DSharpPlus;
using DSharpPlus.Entities;
using SoundCloudExplode.Playlists;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace CatBot.Music.SoundCloud
{
    internal partial class SoundCloudPlaylist : IPlaylist
    {
        [GeneratedRegex("^(?:https?:\\/\\/)?((?:(?:m|on)\\.)?soundcloud\\.com)\\/([\\w-]*)\\/?(?:(?:\\/?(?:sets\\/)((?:[\\w-]*))\\??.*)|(?:(likes|tracks|popular-tracks|reposts)))?$", RegexOptions.Compiled)]
        internal static partial Regex GetRegexMatchSoundCloudPlaylistLink();

        string title = "";
        string description = "";
        string author = "";
        string thumbnailLink = "";
        string type = "";
        long songsCount;
        MusicQueue? musicQueue;
        CancellationTokenSource? addSongsInPlaylistCTS;

        public SoundCloudPlaylist() { }
        public SoundCloudPlaylist(string link, MusicQueue queue) 
        {
            if (GetRegexMatchSoundCloudPlaylistLink().IsMatch(link))
            {
                musicQueue = queue;
                Match match = GetRegexMatchSoundCloudPlaylistLink().Match(link);
                string domain = match.Groups[1].Value;
                //string artist = match.Groups[2].Value;
                //string playlist = match.Groups[3].Value;
                type = match.Groups[4].Value;
                Playlist playlist;
                User user;
                if (domain == "on.soundcloud.com")
                    link = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, link), HttpCompletionOption.ResponseHeadersRead).Result?.RequestMessage?.RequestUri?.ToString() ?? link;
                if (link.Contains("/sets/"))
                {
                    type = "set";
                    try
                    {
                        playlist = SoundCloudMusic.scClient.Playlists.GetAsync(link).Result;
                        title = $"[{playlist.Title}]({playlist.PermalinkUrl})";
                        author = $"[{playlist.User?.Username}]({playlist.User?.PermalinkUrl})";
                        description = playlist.Description ?? "";
                        if (playlist.ArtworkUrl is not null)
                            thumbnailLink = playlist.ArtworkUrl.AbsoluteUri;
                        songsCount = playlist.TrackCount ?? 0;
                    }
                    catch (Exception) { throw new MusicException("playlist not found"); }
                }
                else
                {
                    if (string.IsNullOrEmpty(type))
                        type = "tracks";
                    link = link.ReplaceFirst(type, "").TrimEnd('/');
                    if (type == "tracks")
                    {
                        link = link.ReplaceFirst(type, "");
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).Result;
                        title = $"Nhạc của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl is not null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "popular-tracks")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).Result;
                        title = $"Nhạc nổi bật của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl is not null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "likes")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).Result;
                        title = $"Nhạc [{user.Username}]({user.PermalinkUrl}) đã thích";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl is not null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "reposts")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).Result;
                        title = $"Nhạc [{user.Username}]({user.PermalinkUrl}) đã repost";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl is not null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                }
                new Thread(() => AddTracks(link)) { IsBackground = true }.Start();
            }
            else
                throw new NotAPlaylistException();
        }

        public long TracksCount => songsCount;

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public CancellationTokenSource? AddSongsInPlaylistCTS
        {
            get => addSongsInPlaylistCTS;
            set => addSongsInPlaylistCTS = value;
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by SoundCloud", SoundCloudMusic.soundCloudIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = $"Danh sách phát: {title}" + Environment.NewLine;
            playlistDesc += $"Tạo bởi: {author} " + Environment.NewLine;
            if (type == "set")
                playlistDesc += $"Số bài nhạc: {songsCount}" + Environment.NewLine;
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        async void AddTracks(string link)
        {
            IEnumerable<Track> tracks;
            if (type == "tracks")
                tracks = await SoundCloudMusic.scClient.Users.GetTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "popular-tracks")
                tracks = await SoundCloudMusic.scClient.Users.GetPopularTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "set")
                tracks = await SoundCloudMusic.scClient.Playlists.GetTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "likes")
                tracks = await SoundCloudMusic.scClient.Users.GetLikedTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "reposts")
                tracks = await SoundCloudMusic.scClient.Users.GetRepostedTracksAsync(link, 0, 200).ToListAsync();
            else 
                throw new MusicException("playlist not found");
            await Task.Run(() =>
            {
                foreach (Track track in tracks)
                    musicQueue?.Add(new SoundCloudMusic(track.PermalinkUrl?.AbsoluteUri ?? ""));
            });
        }

        public void SetSponsorBlockOptions(SponsorBlockOptions sponsorBlockOptions) { }

        public bool isLinkMatch(string link) => GetRegexMatchSoundCloudPlaylistLink().IsMatch(link) && (SoundCloudMusic.scClient.Playlists.IsUrlValidAsync(link).Result || SoundCloudMusic.scClient.Users.IsUrlValid(link.ReplaceFirst("popular-tracks", "").ReplaceFirst("tracks", "").ReplaceFirst("likes", "").ReplaceFirst("reposts", "")));
    }
}
