using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Music.SoundCloud;
using DiscordBot.Music.SponsorBlock;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using SpotifyExplode;
using SpotifyExplode.Artists;
using SpotifyExplode.Search;
using SpotifyExplode.Tracks;

namespace DiscordBot.Music.Spotify
{
    internal class SpotifyMusic : IMusic
    {
        internal static readonly Regex regexMatchSpotifyLink = new Regex("^(?:spotify:|https:\\/\\/[a-z]+\\.spotify\\.com\\/track\\/)(.[^\\?]*)(\\?.*)?$", RegexOptions.Compiled);
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
        string mp3OrWEBMFilePath;
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
                    throw new MusicException("Ex: songs not found");
                linkOrKeyword = result[0].Url;
            }
            link = linkOrKeyword;
            track = spClient.Tracks.GetAsync(linkOrKeyword).GetAwaiter().GetResult();
            title = $"[{track.Title}]({track.Url})";
            foreach (Artist artist in track.Artists)
                artists += $"[{artist.Name}](https://open.spotify.com/artist/{artist.Id}), ";
            artists = artists.TrimEnd(", ".ToCharArray());
            album = $"[{track.Album.Name}](https://open.spotify.com/album/{track.Album.Id})";
            if (track.Album.Images.Count != 0)
                albumThumbnailLink = track.Album.Images[0].Url;
            trackID = regexMatchSpotifyLink.Match(link).Groups[1].Value;
        }

        ~SpotifyMusic() => Dispose(false);

        public void Download()
        {
            string mimeType;
            mp3OrWEBMFilePath = Path.GetTempFileName();
            try
            {
                GetTrackFromYtDlp();
                mimeType = "taglib/webm";
            }
            catch
            {
                try
                {
                    GetTrackFromSoundCloud();
                    mimeType = "taglib/mp3";
                }
                catch 
                { 
                    try
                    {
                        new WebClient().DownloadFile(GetLinkFromSpotifyDown(), mp3OrWEBMFilePath);
                        mimeType = "taglib/mp3";
                    }
                    catch { throw new MusicException("Sp: not found"); }
                }
            }
            TagLib.File mp3OrWEBMFile = TagLib.File.Create(mp3OrWEBMFilePath, mimeType, TagLib.ReadStyle.Average);
            duration = mp3OrWEBMFile.Properties.Duration;
            mp3OrWEBMFile.Dispose();
            if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) > 15000)
                throw new MusicException("Sp: not found");
            canGetStream = true;
            musicPCMDataStream = File.OpenRead(MusicUtils.GetPCMFile(mp3OrWEBMFilePath, ref pcmFile));
            File.Delete(mp3OrWEBMFilePath);
            mp3OrWEBMFilePath = null;
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
            if (string.IsNullOrWhiteSpace(Config.SpotifyCookie))
                return new LyricData("Không tìm thấy cookie của Spotify!");
            if (string.IsNullOrEmpty(token) || tokenExpireTime < DateTime.Now)
            {
                string url = "https://open.spotify.com/get_access_token";
                Leaf.xNet.HttpRequest httpClient = new Leaf.xNet.HttpRequest();
                httpClient.AddHeader("User-Agent", Config.UserAgent);
                httpClient.AddHeader("Cookie", Config.SpotifyCookie);
                MusicUtils.SetCookie(httpClient, Config.SpotifyCookie);
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

        public string[] GetFilesInUse() => new string[] { mp3OrWEBMFilePath, pcmFile };

        public string GetIcon() => Config.SpotifyIcon;

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

        void GetTrackFromYtDlp()
        {
            MusicUtils.DownloadWEBMFromYouTube($"\"ytsearch: {MusicUtils.RemoveEmbedLink(title).ToLower()} {MusicUtils.RemoveEmbedLink(artists).ToLower()}\"", ref mp3OrWEBMFilePath);
            TagLib.File mp3OrWEBMFile = TagLib.File.Create(mp3OrWEBMFilePath, "taglib/webm", TagLib.ReadStyle.Average);
            TimeSpan duration = mp3OrWEBMFile.Properties.Duration;
            mp3OrWEBMFile.Dispose();
            if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) > 15000)
                throw new MusicException("Wrong track");
        }

        void GetTrackFromSoundCloud()
        {
            List<SoundCloudExplode.Search.TrackSearchResult> results = SoundCloudMusic.scClient.Search.GetTracksAsync($"{MusicUtils.RemoveEmbedLink(title).ToLower()} {MusicUtils.RemoveEmbedLink(artists).ToLower()}").ToListAsync().GetAwaiter().GetResult();
            if (results.Count == 0)
                throw new MusicException("not found");
            int i = 0;
            int count = Math.Min(5, results.Count);
            for (; i < count; i++)
            {
                SoundCloudExplode.Search.TrackSearchResult result = results[i];
                string mp3Link = SoundCloudMusic.scClient.Tracks.GetDownloadUrlAsync(result.PermalinkUrl.AbsoluteUri).GetAwaiter().GetResult();
                new WebClient().DownloadFile(mp3Link, mp3OrWEBMFilePath);
                TagLib.File mp3OrWEBMFile = TagLib.File.Create(mp3OrWEBMFilePath, "taglib/mp3", TagLib.ReadStyle.Average);
                TimeSpan duration = mp3OrWEBMFile.Properties.Duration;
                mp3OrWEBMFile.Dispose();
                if (Math.Abs(duration.TotalMilliseconds - track.DurationMs) <= 15000)
                    break;
            }
            if (i == count)
                throw new MusicException("Wrong track");
        }

        string GetLinkFromSpotifyDown()
        {
            string url = $"https://api.spotifydown.com/download/{trackID}";
            HttpClient httpClient = new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            httpClient.DefaultRequestHeaders.Add("origin", "https://spotifydown.com");
            httpClient.DefaultRequestHeaders.Add("referer", "https://spotifydown.com/");
            HttpResponseMessage response;
            try
            {
                response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                throw new MusicException("Sp: music download timeout");
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
                    File.Delete(mp3OrWEBMFilePath);
                }
                catch (Exception) { }
            }
            musicPCMDataStream = null;
        }
    }
}
