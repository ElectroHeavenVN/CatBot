using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using SpotifyExplode.Albums;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;

namespace CatBot.Music.YouTube
{
    internal class YouTubePlaylist : IPlaylist
    {
        internal static Regex regexMatchYTPlaylistLink = new Regex("^((?:https?:)?\\/\\/)?((?:www|m|music)\\.)?((?:youtube\\.com|youtu\\.be))(\\/(?:@|playlist\\?list=|channel\\/))([\\w\\-]+)(\\S+)?$", RegexOptions.Compiled);

        string thumbnailLink;
        string title;
        string description;
        bool isYouTubeMusicPlaylist;
        string author;
        //string subCount;
        int hiddenVideos;
        SponsorBlockOptions sponsorBlockOptions;
        MusicQueue musicQueue;

        public YouTubePlaylist() { }
        public YouTubePlaylist(string link, MusicQueue queue) 
        {
            if (regexMatchYTPlaylistLink.IsMatch(link))
            {
                musicQueue = queue;
                isYouTubeMusicPlaylist = link.Contains("music.youtube.com");
                if (link.Contains('@') || link.Contains("channel/"))
                {
                    try
                    {
                        Channel channel;
                        if (link.Contains('@'))
                        {
                            channel = YouTubeMusic.ytClient.Channels.GetByHandleAsync(link).GetAwaiter().GetResult();
                            link = channel.Url;
                        }
                        else
                            channel = YouTubeMusic.ytClient.Channels.GetAsync(link).GetAwaiter().GetResult();                        
                        title = $"Video tải lên của " + Formatter.MaskedUrl(channel.Title, new Uri(channel.Url));
                        author = Formatter.MaskedUrl(channel.Title, new Uri(channel.Url));
                        thumbnailLink = channel.Thumbnails.TryGetWithHighestResolution().Url;
                    }
                    catch (Exception) { throw new MusicException(MusicType.YouTube, "channel not found"); }
                }
                else if (link.Contains("playlist?list="))
                {
                    try
                    {
                        Playlist playlist = YouTubeMusic.ytClient.Playlists.GetAsync(link).GetAwaiter().GetResult();
                        title = Formatter.MaskedUrl(playlist.Title, new Uri(playlist.Url));
                        description = playlist.Description;
                        if (playlist.Author != null)
                            author = Formatter.MaskedUrl(playlist.Author.ChannelTitle, new Uri(playlist.Author.ChannelUrl));
                        thumbnailLink = playlist.Thumbnails.TryGetWithHighestResolution().Url;
                    }
                    catch (Exception) { throw new MusicException("playlist not found"); }
                }
                new Thread(() => AddVideos(link)) { IsBackground = true }.Start();
            }
            else
                throw new NotAPlaylistException();
        }

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public long TracksCount => 0;

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by YouTube" + (isYouTubeMusicPlaylist ? " Music" : ""), isYouTubeMusicPlaylist ? YouTubeMusic.youTubeMusicIconLink : YouTubeMusic.youTubeIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = $"Danh sách phát: {title} ";
            playlistDesc += hiddenVideos > 0 ? $"({hiddenVideos} video không xem được)" : "" + Environment.NewLine;
            playlistDesc += $"Tải lên bởi: {author} " + Environment.NewLine;
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        async void AddVideos(string link)
        {
            IEnumerable<PlaylistVideo> videos = null;
            if (link.Contains('@') || link.Contains("channel/"))
                videos = await YouTubeMusic.ytClient.Channels.GetUploadsAsync(link);
            else if (link.Contains("playlist?list="))
                videos = await YouTubeMusic.ytClient.Playlists.GetVideosAsync(link);
            foreach (var video in videos)
            {
                try
                {
                    musicQueue.Add(new YouTubeMusic(video.Url) { SponsorBlockOptions = sponsorBlockOptions });
                }
                catch (MusicException ex)
                {
                    if (ex.Message == "video not found")
                        hiddenVideos++;
                }
            }
        }

        public void SetSponsorBlockOptions(SponsorBlockOptions options) => sponsorBlockOptions = options;

        public bool isLinkMatch(string link) => regexMatchYTPlaylistLink.IsMatch(link);
    }
}
