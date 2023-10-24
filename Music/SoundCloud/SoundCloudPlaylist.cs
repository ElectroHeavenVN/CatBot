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
    internal class SoundCloudPlaylist : IPlaylist
    {
        internal static readonly Regex regexMatchSoundCloudPlaylistLink = new Regex("^(?:https?:\\/\\/)?((?:(?:m|on)\\.)?soundcloud\\.com)\\/([\\w-]*)\\/?(?:(?:\\/?(?:sets\\/)((?:[\\w-]*))\\??.*)|(?:(likes|tracks|popular-tracks|reposts)))?$", RegexOptions.Compiled);
        string title;
        string description;
        string author;
        string thumbnailLink;
        string type;
        long songsCount;
        MusicQueue musicQueue;
        CancellationTokenSource addSongsInPlaylistCTS;

        public SoundCloudPlaylist() { }
        public SoundCloudPlaylist(string link, MusicQueue queue) 
        {
            if (regexMatchSoundCloudPlaylistLink.IsMatch(link))
            {
                musicQueue = queue;
                Match match = regexMatchSoundCloudPlaylistLink.Match(link);
                string domain = match.Groups[1].Value;
                //string artist = match.Groups[2].Value;
                //string playlist = match.Groups[3].Value;
                type = match.Groups[4].Value;
                Playlist playlist;
                User user;
                if (domain == "on.soundcloud.com")
                    link = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, link), HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult().RequestMessage.RequestUri.ToString();
                if (link.Contains("/sets/"))
                {
                    type = "set";
                    try
                    {
                        playlist = SoundCloudMusic.scClient.Playlists.GetAsync(link).GetAwaiter().GetResult();
                        title = Formatter.MaskedUrl(playlist.Title, playlist.PermalinkUrl);
                        author = Formatter.MaskedUrl(playlist.User.Username, playlist.User.PermalinkUrl);
                        description = playlist.Description;
                        if (playlist.ArtworkUrl != null)
                            thumbnailLink = playlist.ArtworkUrl.AbsoluteUri;
                        songsCount = playlist.TrackCount.Value;
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
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = "Nhạc của " + Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        author = Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "popular-tracks")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = Formatter.MaskedUrl("Nhạc nổi bật", new Uri(link + "/" + type)) + " của " + Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        author = Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "likes")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = Formatter.MaskedUrl("Nhạc đã thích", new Uri(link + "/" + type)) + " của " + Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        author = Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "reposts")
                    {
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = Formatter.MaskedUrl("Nhạc repost", new Uri(link + "/" + type)) + " của " + Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        author = Formatter.MaskedUrl(user.Username, user.PermalinkUrl);
                        if (user.AvatarUrl != null)
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

        public CancellationTokenSource AddSongsInPlaylistCTS
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
            IEnumerable<Track> tracks = null;
            if (type == "tracks")
                tracks = await SoundCloudMusic.scClient.Users.GetTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "popular-tracks")
                tracks = await SoundCloudMusic.scClient.Users.GetPopularTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "set")
                tracks = await SoundCloudMusic.scClient.Playlists.GetTracksAsync(link, 0, 200).ToListAsync();
            else if (type == "likes")
                tracks = (await SoundCloudMusic.scClient.Users.GetLikedItemsAsync(link, 0, 200)).Select(i => i.Track);
            else if (type == "reposts")
                tracks = (await SoundCloudMusic.scClient.Users.GetRepostItemsAsync(link, 0, 200)).Select(i => i.Track);
            await Task.Run(() =>
            {
                foreach (Track track in tracks)
                    musicQueue.Add(new SoundCloudMusic(track.PermalinkUrl.AbsoluteUri));
            });
        }

        public void SetSponsorBlockOptions(SponsorBlockOptions sponsorBlockOptions) { }

        public bool isLinkMatch(string link) => regexMatchSoundCloudPlaylistLink.IsMatch(link) && (SoundCloudMusic.scClient.Playlists.IsUrlValidAsync(link).GetAwaiter().GetResult() || SoundCloudMusic.scClient.Users.IsUrlValid(link.ReplaceFirst("popular-tracks", "").ReplaceFirst("tracks", "").ReplaceFirst("likes", "").ReplaceFirst("reposts", "")));
    }
}
