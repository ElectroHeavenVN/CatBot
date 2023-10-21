using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using SpotifyExplode;
using SpotifyExplode.Artists;
using SpotifyExplode.Search;
using SpotifyExplode.Tracks;

namespace CatBot.Music.Spotify
{
    internal class SpotifyMusic : IMusic
    {
        internal static readonly Regex regexMatchSpotifyLink = new Regex("^(?:(?:(?:spotify:|https?:\\/\\/)[a-z]*\\.?spotify\\.com(?:\\/embed)?\\/track\\/)|(?:https?:\\/\\/spotify\\.link\\/))(.[^\\?\\n]*)(\\?.*)?$", RegexOptions.Compiled);
        internal static readonly string spotifyIconLink = "https://open.spotifycdn.com/cdn/images/icons/Spotify_256.17e41e58.png";
        string link;
        TimeSpan duration;
        string title = "";
        string artists = "";
        string album = "";
        string albumThumbnailLink = "";
        string trackID = "";
        internal static SpotifyClient spClient = new SpotifyClient();
        Stream musicPCMDataStream;
        string audioFilePath;
        Track track;
        bool canGetStream;
        bool _disposed;
        string pcmFile;
        string lyric;

        static string token;
        static DateTime tokenExpireTime = DateTime.Now;

        public SpotifyMusic() { }
        public SpotifyMusic(string linkOrKeyword)
        {
            if (!regexMatchSpotifyLink.IsMatch(linkOrKeyword))
            {
                List<TrackSearchResult> result = spClient.Search.GetTracksAsync(linkOrKeyword, 0, 1).GetAwaiter().GetResult();
                if (result.Count == 0)
                    throw new MusicException("songs not found");
                linkOrKeyword = result[0].Url;
            }
            if (linkOrKeyword.Contains("spotify.link"))
                linkOrKeyword = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, linkOrKeyword), HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult().RequestMessage.RequestUri.ToString();
            link = linkOrKeyword;
            track = spClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
            title = Formatter.MaskedUrl(track.Title, new Uri(track.Url));
            foreach (Artist artist in track.Artists)
                artists += Formatter.MaskedUrl(artist.Name, new Uri("https://open.spotify.com/artist/{artist.Id}")) + ", ";
            artists = artists.TrimEnd(", ".ToCharArray());
            album = Formatter.MaskedUrl(track.Album.Name, new Uri($"https://open.spotify.com/album/{track.Album.Id}"));
            if (track.Album.Images.Count != 0)
                albumThumbnailLink = track.Album.Images[0].Url;
            trackID = regexMatchSpotifyLink.Match(link).Groups[1].Value;
        }

        ~SpotifyMusic() => Dispose(false);

        public void Download()
        {
            string mimeType;
            audioFilePath = Path.GetTempFileName();
            try
            {
                DownloadTrackUsingZotify();
                mimeType = "taglib/ogg";
            }
            catch
            {
                try
                {
                    GetTrackFromSpotdl();
                    mimeType = "taglib/mp3";
                }
                catch
                {
                    try
                    {
                        new WebClient().DownloadFile(GetLinkFromSpotifyDown(), audioFilePath);
                        mimeType = "taglib/mp3";
                    }
                    catch { throw new MusicException(MusicType.Spotify, "not found"); }
                }
            }
            TagLib.File mp3OrWEBMFile = TagLib.File.Create(audioFilePath, mimeType, TagLib.ReadStyle.Average);
            duration = mp3OrWEBMFile.Properties.Duration;
            mp3OrWEBMFile.Dispose();
            if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) > 15000)
                throw new MusicException("not found");
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(audioFilePath, ref pcmFile));
            File.Delete(audioFilePath);
            audioFilePath = null;
        }

        public MusicType MusicType => MusicType.Spotify;

        public string PathOrLink => link;

        public TimeSpan Duration => duration;

        public string Title => title;

        public string Artists => artists;

        public string Album => album;

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

        public LyricData GetLyric()
        {
            if (string.IsNullOrWhiteSpace(Config.gI().SpotifyCookie))
                return new LyricData("Không tìm thấy cookie của Spotify!");
            if (string.IsNullOrEmpty(token) || tokenExpireTime < DateTime.Now)
            {
                string url = "https://open.spotify.com/get_access_token";
                Leaf.xNet.HttpRequest httpClient = new Leaf.xNet.HttpRequest();
                httpClient.AddHeader("User-Agent", Config.gI().UserAgent);
                httpClient.AddHeader("Cookie", Config.gI().SpotifyCookie);
                MusicUtils.SetCookie(httpClient, Config.gI().SpotifyCookie);
                JObject responseContent =JObject.Parse(httpClient.Get(url).ToString());
                token = responseContent["accessToken"].ToString();
                tokenExpireTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(double.Parse(responseContent["accessTokenExpirationTimestampMs"].ToString())).ToLocalTime();
            }
            if (string.IsNullOrWhiteSpace(lyric))
            {
                string url = $"https://spclient.wg.spotify.com/color-lyrics/v2/track/{trackID}?format=json&vocalRemoval=false&market=from_token";
                Leaf.xNet.HttpRequest httpClient = new Leaf.xNet.HttpRequest();
                httpClient.AddHeader("app-platform", "WebPlayer");
                httpClient.Authorization = $"Bearer {token}";
                try
                {
                    JObject lyricData = JObject.Parse(httpClient.Get(url).ToString());
                    foreach (var item in lyricData["lyrics"]["lines"])
                        lyric += item["words"] + Environment.NewLine;
                    lyric = lyric.TrimEnd(Environment.NewLine.ToCharArray()).Trim();
                }
                catch (Leaf.xNet.HttpException ex) 
                {
                    if (ex.HttpStatusCode == Leaf.xNet.HttpStatusCode.NotFound)
                        return new LyricData("Không tìm thấy lời bài hát này trên Spotify!");
                    throw;
                }
            }
            return new LyricData(MusicUtils.RemoveEmbedLink(title), MusicUtils.RemoveEmbedLink(artists), lyric, albumThumbnailLink);
        }

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Spotify", spotifyIconLink);

        public string[] GetFilesInUse() => new string[] { audioFilePath, pcmFile };

        public string GetIcon() => Config.gI().SpotifyIcon;

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {title}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {artists}" + Environment.NewLine;
            musicDesc += $"Album: {album}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        void DownloadTrackUsingZotify()
        {
            MusicUtils.DownloadOGGFromSpotify(link, ref audioFilePath);
            TagLib.File oggFile = TagLib.File.Create(audioFilePath, "taglib/ogg", TagLib.ReadStyle.Average);
            TimeSpan duration = oggFile.Properties.Duration;
            oggFile.Dispose();
            if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) > 15000)
                throw new MusicException("Wrong track");
        }

        void GetTrackFromSpotdl()
        {
            MusicUtils.DownloadTrackUsingSpotdl(link, ref audioFilePath);
            TagLib.File mp3OrWEBMFile = TagLib.File.Create(audioFilePath, "taglib/mp3", TagLib.ReadStyle.Average);
            TimeSpan duration = mp3OrWEBMFile.Properties.Duration;
            mp3OrWEBMFile.Dispose();
            if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) > 15000)
                throw new MusicException("Wrong track");
        }

        string GetLinkFromSpotifyDown()
        {
            string url = $"https://api.spotifydown.com/download/{trackID}";
            HttpClient httpClient = new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", Config.gI().UserAgent);
            httpClient.DefaultRequestHeaders.Add("origin", "https://spotifydown.com");
            httpClient.DefaultRequestHeaders.Add("referer", "https://spotifydown.com/");
            HttpResponseMessage response;
            try
            {
                response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                throw new MusicException(MusicType.Spotify, "music download timeout");
            }
            JObject responseData = JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            string downloadUrl = responseData["link"].ToString();
            return downloadUrl;
        }

        public bool isLinkMatch(string link) => regexMatchSpotifyLink.IsMatch(link);

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
                    File.Delete(audioFilePath);
                }
                catch (Exception) { }
            }
            musicPCMDataStream = null;
        }
    }
}
