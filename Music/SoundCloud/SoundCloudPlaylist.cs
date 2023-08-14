using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Text;
using DiscordBot.SoundCloudExplodeExtension;
using DSharpPlus.Entities;
using SoundCloudExplode.Playlists;
using SoundCloudExplode.Tracks;
using SoundCloudExplode.Users;

namespace DiscordBot.Music.SoundCloud
{
    internal class SoundCloudPlaylist : IPlaylist
    {
        internal static readonly Regex regexMatchSoundCloudPlaylistLink = new Regex("^(?:https?:\\/\\/)?((?:(?:(?:m|on)\\.)?soundcloud\\.com)|(?:snd\\.sc))\\/([\\w-]*)(?:(?:\\/?(?:sets\\/)((?:[\\w-]*)|)\\??.*)|(?:\\/(likes|tracks|popular-tracks|reposts)))?$", RegexOptions.Compiled);
        string title;
        string description;
        string author;
        string thumbnailLink;
        string type;
        List<IMusic> songList = new List<IMusic>();

        public SoundCloudPlaylist() { }
        public SoundCloudPlaylist(string link) 
        {
            if (regexMatchSoundCloudPlaylistLink.IsMatch(link))
            {
                Match match = regexMatchSoundCloudPlaylistLink.Match(link);
                string domain = match.Groups[1].Value;
                //string artist = match.Groups[2].Value;
                //string playlist = match.Groups[3].Value;
                type = match.Groups[4].Value;
                List<Track> tracks = new List<Track>();
                Playlist playlist;
                User user;
                if (domain == "snd.sc" || domain == "on.soundcloud.com")
                {
                    try
                    {
                        if (!SoundCloudMusic.scClient.Playlists.IsUrlValidAsync(link).GetAwaiter().GetResult() && domain != "snd.sc")
                            throw new Exception();
                        type = "set";
                        tracks = SoundCloudMusic.scClient.Playlists.GetTracksAsync(link, 0, 200).GetAwaiter().GetResult();
                        playlist = SoundCloudMusic.scClient.Playlists.GetAsync(link).GetAwaiter().GetResult();
                        title = $"[{playlist.Title}]({playlist.PermalinkUrl})";
                        author = $"[{playlist.User.Username}]({playlist.User.PermalinkUrl})";
                        description = playlist.Description;
                        if (playlist.ArtworkUrl != null)
                            thumbnailLink = playlist.ArtworkUrl.AbsoluteUri;
                    }
                    catch
                    {
                        type = "tracks";
                        tracks = SoundCloudMusic.scClient.Users.GetTracksAsync(link, 0, 200).GetAwaiter().GetResult();
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = $"Nhạc của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                }
                else if (link.Contains("/sets/"))
                {
                    type = "set";
                    tracks = SoundCloudMusic.scClient.Playlists.GetTracksAsync(link, 0, 200).GetAwaiter().GetResult();
                    playlist = SoundCloudMusic.scClient.Playlists.GetAsync(link).GetAwaiter().GetResult();
                    title = $"[{playlist.Title}]({playlist.PermalinkUrl})";
                    author = $"[{playlist.User.Username}]({playlist.User.PermalinkUrl})";
                    description = playlist.Description;
                    if (playlist.ArtworkUrl != null)
                        thumbnailLink = playlist.ArtworkUrl.AbsoluteUri;
                }
                else
                {
                    if (string.IsNullOrEmpty(type))
                        type = "tracks";
                    link = link.ReplaceFirst(type, "").TrimEnd('/');
                    if (type == "tracks")
                    {
                        link = link.ReplaceFirst(type, "");
                        tracks = SoundCloudMusic.scClient.Users.GetPopularTracksAsync(link, 0, 200).GetAwaiter().GetResult();   //WHY?
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = $"Nhạc của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "popular-tracks")
                    {
                        tracks = SoundCloudMusic.scClient.Users.GetTracksAsync(link, 0, 200).GetAwaiter().GetResult();  //WHY?
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = $"[Nhạc nổi bật]({link}/{type}) của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "likes")
                    {
                        tracks = SoundCloudMusic.scClient.Users.GetLikedTracksAsync(link, 0, 200).GetAwaiter().GetResult();
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = $"[Nhạc đã thích]({link}/{type}) của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                    else if (type == "reposts")
                    {
                        tracks = SoundCloudMusic.scClient.Users.GetRepostTracksAsync(link, 0, 200).GetAwaiter().GetResult();
                        user = SoundCloudMusic.scClient.Users.GetAsync(link).GetAwaiter().GetResult();
                        title = $"[Nhạc repost]({link}/{type}) của [{user.Username}]({user.PermalinkUrl})";
                        author = $"[{user.Username}]({user.PermalinkUrl})";
                        if (user.AvatarUrl != null)
                            thumbnailLink = user.AvatarUrl.AbsoluteUri;
                    }
                }
                foreach (Track track in tracks)
                    songList.Add(new SoundCloudMusic(track.PermalinkUrl.AbsoluteUri));
            }
            else
                throw new NotAPlaylistException();
        }

        public List<IMusic> Tracks => songList;

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by SoundCloud", SoundCloudMusic.soundCloudIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = $"Danh sách phát: {title}" + Environment.NewLine;
            playlistDesc += $"Tạo bởi: {author} " + Environment.NewLine;
            playlistDesc += $"Số bài nhạc: {songList.Count}" + Environment.NewLine;
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        public bool isLinkMatch(string link) => regexMatchSoundCloudPlaylistLink.IsMatch(link) && (SoundCloudMusic.scClient.Playlists.IsUrlValidAsync(link).GetAwaiter().GetResult() || SoundCloudMusic.scClient.Users.IsUrlValid(link.ReplaceFirst("popular-tracks", "").ReplaceFirst("tracks", "").ReplaceFirst("likes", "").ReplaceFirst("reposts", "")));

    }
}
