using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using HtmlAgilityPack;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

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
        CancellationTokenSource? addSongsInPlaylistCTS;

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
                    FullAlbum album;
                    try
                    {
                        album = SpotifyMusic.SPClient.Albums.Get(id).Result;
                    }
                    catch (Exception) { throw new MusicException("album not found"); }
                    title = Formatter.MaskedUrl(album.Name, new Uri(album.Uri));
                    author = string.Join(", ", album.Artists.Select(artist => Formatter.MaskedUrl(artist.Name, new Uri($"https://open.spotify.com/artist/{artist.Id}"))));
                    thumbnailLink = album.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url;
                    songCount = album.TotalTracks;
                }
                else if (type == "playlist")
                {
                    FullPlaylist playlist;
                    try
                    {
                        playlist = SpotifyMusic.SPClient.Playlists.Get(id).Result;
                    }
                    catch (Exception) { throw new MusicException("playlist not found"); }
                    //string[] imageLinks = SpotifyMusic.SPClient.Playlists.GetImagesAsync(id).Result;
                    title = Formatter.MaskedUrl(playlist.Name ?? "", new Uri($"https://open.spotify.com/playlist/{playlist.Id}"));
                    description = playlist.Description ?? "";
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(description);
                    var links = doc.DocumentNode.SelectNodes("//a");
                    if (links is not null)
                        foreach (HtmlNode tb in links)
                            description = description.ReplaceFirst(tb.OuterHtml, tb.InnerText);
                    author = Formatter.MaskedUrl(playlist.Owner?.DisplayName ?? "", new Uri($"https://open.spotify.com/user/{playlist.Owner.Id}"));
                    //thumbnailLink = imageLinks.First(s => !string.IsNullOrWhiteSpace(s));
                    thumbnailLink = playlist.Images?.MaxBy(i => i.Width * i.Height)?.Url ?? "";
                }
                else if (type == "artist")
                {
                    FullArtist artist; 
                    try
                    {
                        artist = SpotifyMusic.SPClient.Artists.Get(id).Result;
                    }
                    catch (Exception) { throw new MusicException("artist not found"); }
                    title = "Nhạc phổ biến của " + Formatter.MaskedUrl(artist.Name, new Uri($"https://open.spotify.com/artist/{artist.Id}"));
                    author = Formatter.MaskedUrl(artist.Name, new Uri($"https://open.spotify.com/artist/{artist.Id}"));
                    thumbnailLink = artist.Images.MaxBy(i => i.Width * i.Height)?.Url ?? "";
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

        public CancellationTokenSource? AddSongsInPlaylistCTS
        {
            get => addSongsInPlaylistCTS;
            set => addSongsInPlaylistCTS = value;
        }
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
            IEnumerable<object> tracks;
            if (type == "album")
                tracks = (await SpotifyMusic.SPClient.Albums.GetTracks(id)).Items ?? [];
            else if (type == "playlist")
                tracks = (await SpotifyMusic.SPClient.Playlists.GetItems(id)).Items?.Select(i => i.Track)?.OfType<SimpleTrack>() ?? [];
            else if (type == "artist")
                tracks = (await SpotifyMusic.SPClient.Artists.GetTopTracks(id, new ArtistsTopTracksRequest("from_token"))).Tracks ?? [];
            else
                return;
            songCount = tracks.Count();
            foreach (object obj in tracks)
            {
                if (obj is SimpleTrack simpleTrack)
                    musicQueue.Add(new SpotifyMusic(simpleTrack.Uri));
                else if (obj is FullTrack fullTrack)
                    musicQueue.Add(new SpotifyMusic(fullTrack.Uri));
            }
        }

        public void SetSponsorBlockOptions(SponsorBlockOptions sponsorBlockOptions) { }

        public bool isLinkMatch(string link) => regexMatchSpotifyPlaylist.IsMatch(link);
    }
}
