using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AngleSharp.Text;
using CatBot.Extension;
using CatBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using HtmlAgilityPack;
using SpotifyExplode.Albums;
using SpotifyExplode.Artists;
using SpotifyExplode.Playlists;
using SpotifyExplode.Tracks;

namespace CatBot.Music.Spotify
{
    internal class SpotifyPlaylist : IPlaylist
    {
        internal static readonly Regex regexMatchSpotifyPlaylist = new Regex("^(?:spotify:|(?:http(?:s)?:\\/\\/)?open\\.spotify\\.com\\/)(album|playlist|artist)\\/([\\w0-9_\\-]+)\\??.*", RegexOptions.Compiled);
        string title;
        string description;
        string author;
        string thumbnailLink;
        string type;
        MusicQueue musicQueue;
        int songCount;

        public SpotifyPlaylist() { }
        public SpotifyPlaylist(string link, MusicQueue queue)
        {
            if (regexMatchSpotifyPlaylist.IsMatch(link))
            {
                musicQueue = queue;
                Match match = regexMatchSpotifyPlaylist.Match(link);
                type = match.Groups[1].Value;
                string id = match.Groups[2].Value;
                if (type == "album")
                {
                    Album album;
                    try
                    {
                        album = SpotifyMusic.spClient.Albums.GetAsync(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new MusicException("album not found"); }
                    title = $"[{album.Name}]({album.Url})";
                    author = string.Join(", ", album.Artists.Select(artist => $"[{artist.Name}](https://open.spotify.com/artist/{artist.Id})"));
                    thumbnailLink = album.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url;
                    songCount = album.TotalTracks;
                }
                else if (type == "playlist")
                {
                    Playlist playlist;
                    try
                    {
                        playlist = SpotifyMusic.spClient.Playlists.GetAsync(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new MusicException("playlist not found"); }
                    string[] imageLinks = SpotifyMusic.spClient.Playlists.GetImagesAsync(id).GetAwaiter().GetResult();
                    title = $"[{playlist.Name}](https://open.spotify.com/playlist/{playlist.Id})";
                    description = playlist.Description;
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(description);
                    var links = doc.DocumentNode.SelectNodes("//a");
                    if (links != null)
                        foreach (HtmlNode tb in links)
                            description = description.ReplaceFirst(tb.OuterHtml, tb.InnerText);
                    author = $"[{playlist.Owner.DisplayName}](https://open.spotify.com/user/{playlist.Owner.Id})";
                    thumbnailLink = imageLinks.First(s => !string.IsNullOrWhiteSpace(s));
                    songCount = playlist.Items.Count;
                }
                else if (type == "artist")
                {
                    Artist artist; 
                    try
                    {
                        artist = SpotifyMusic.spClient.Artists.GetAsync(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new MusicException("artist not found"); }
                    title = $"Nhạc phổ biến của [{artist.Name}](https://open.spotify.com/artist/{artist.Id})";
                    author = $"[{artist.Name}](https://open.spotify.com/artist/{artist.Id})";
                    thumbnailLink = artist.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url;
                }
                new Thread(() => AddTracks(id)) { IsBackground = true }.Start();
            }
            else
                throw new NotAPlaylistException();
        }

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public long TracksCount => songCount;

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Spotify", SpotifyMusic.spotifyIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = "";
            if (type == "artist")
            {
                playlistDesc += $"Danh sách phát: {title}" + Environment.NewLine;
                playlistDesc += $"Nghệ sĩ: {author} " + Environment.NewLine;
            }
            else 
            { 
                if (type == "playlist")
                {
                    playlistDesc += $"Danh sách phát: {title}" + Environment.NewLine;
                    playlistDesc += $"Tạo bởi: {author} " + Environment.NewLine;
                }
                else if (type == "album")
                {
                    playlistDesc += $"Album: {title}" + Environment.NewLine;
                    playlistDesc += $"Nghệ sĩ: {author} " + Environment.NewLine;
                }
                playlistDesc += $"Số bài nhạc: {songCount}" + Environment.NewLine;
            }
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        async void AddTracks(string id)
        {
            IEnumerable<Track> tracks = null;
            if (type == "album")
                tracks = await SpotifyMusic.spClient.Albums.GetAllTracksAsync(id);
            else if (type == "playlist")
                tracks = await SpotifyMusic.spClient.Playlists.GetAllTracksAsync(id);
            else if (type == "artist")
                tracks = await SpotifyMusic.spClient.Artists.GetTopTracks(id);
            foreach (Track track in tracks)
                musicQueue.Add(new SpotifyMusic(track.Url));
        }

        public void SetSponsorBlockOptions(SponsorBlockOptions sponsorBlockOptions) { }

        public bool isLinkMatch(string link) => regexMatchSpotifyPlaylist.IsMatch(link);
    }
}
