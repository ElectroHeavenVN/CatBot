using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using AngleSharp.Text;
using DiscordBot.Extension;
using DSharpPlus.Entities;
using HtmlAgilityPack;
using SpotifyExplode.Albums;
using SpotifyExplode.Artists;
using SpotifyExplode.Common;
using SpotifyExplode.Playlists;
using SpotifyExplode.Tracks;

namespace DiscordBot.Music.Spotify
{
    internal class SpotifyPlaylist : IPlaylist
    {
        internal static readonly Regex regexMatchSpotifyPlaylist = new Regex("^(?:spotify:|(?:http(?:s)?:\\/\\/)?open\\.spotify\\.com\\/)(album|playlist|artist)\\/([\\w0-9_\\-]+)\\??.*", RegexOptions.Compiled);
        string title;
        string description;
        string author;
        string thumbnailLink;
        string type;
        List<IMusic> songList = new List<IMusic>();

        public SpotifyPlaylist() { }
        public SpotifyPlaylist(string link)
        {
            if (regexMatchSpotifyPlaylist.IsMatch(link))
            {
                Match match = regexMatchSpotifyPlaylist.Match(link);
                type = match.Groups[1].Value;
                string id = match.Groups[2].Value;
                List<Track> tracks = new List<Track>();
                if (type == "album")
                {
                    try
                    {
                        tracks = SpotifyMusic.spClient.Albums.GetAllTracksAsync(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new WebException("Ex: album not found"); }
                    Album album = SpotifyMusic.spClient.Albums.GetAsync(id).GetAwaiter().GetResult();
                    title = $"[{album.Name}]({album.Url})";
                    author = string.Join(", ", album.Artists.Select(artist => $"[{artist.Name}](https://open.spotify.com/artist/{artist.Id})"));
                    thumbnailLink = album.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url;
                }
                else if (type == "playlist")
                {
                    try
                    {
                        tracks = SpotifyMusic.spClient.Playlists.GetAllTracksAsync(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new WebException("Ex: playlist not found"); }
                    Playlist playlist = SpotifyMusic.spClient.Playlists.GetAsync(id).GetAwaiter().GetResult();
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
                }
                else if (type == "artist")
                {
                    try
                    {
                        tracks = SpotifyMusic.spClient.Artists.GetTopTracks(id).GetAwaiter().GetResult();
                    }
                    catch (Exception) { throw new WebException("Ex: artist not found"); }
                    Artist artist = SpotifyMusic.spClient.Artists.GetAsync(id).GetAwaiter().GetResult();
                    title = $"Nhạc phổ biến của [{artist.Name}](https://open.spotify.com/artist/{artist.Id})";
                    author = $"[{artist.Name}](https://open.spotify.com/artist/{artist.Id})";
                    thumbnailLink = artist.Images.Aggregate((i1, i2) => i1.Width * i1.Height > i2.Width * i2.Height ? i1 : i2).Url;
                }
                foreach (Track track in tracks)
                    songList.Add(new SpotifyMusic(track.Url));
            }
            else
                throw new NotAPlaylistException();
        }

        public List<IMusic> Tracks => songList;

        public string Title => title;

        public string Description => description;

        public string Author => author;

        public string ThumbnailLink => thumbnailLink;

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Spotify", SpotifyMusic.spotifyIconLink);

        public string GetPlaylistDesc()
        {
            string playlistDesc = "";
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
            else
            {
                playlistDesc += $"Danh sách phát: {title}" + Environment.NewLine;
                playlistDesc += $"Nghệ sĩ: {author} " + Environment.NewLine;
            }
            playlistDesc += $"Số bài nhạc: {songList.Count}" + Environment.NewLine;
            playlistDesc += description + Environment.NewLine;
            return playlistDesc;
        }

        public bool isLinkMatch(string link) => regexMatchSpotifyPlaylist.IsMatch(link);
    }
}
