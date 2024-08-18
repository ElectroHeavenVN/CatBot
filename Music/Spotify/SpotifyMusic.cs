using System.Net;
using System.Text.RegularExpressions;
using CatBot.Music.SponsorBlock;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using SpotifyExplode;
using SpotifyExplode.Search;
using SpotifyExplode.Tracks;

namespace CatBot.Music.Spotify
{
    internal partial class SpotifyMusic : IMusic
    {
        [GeneratedRegex("^(?:(?:(?:spotify:|https?:\\/\\/)[a-z]*\\.?spotify\\.com(?:\\/embed)?\\/track\\/)|(?:https?:\\/\\/spotify\\.link\\/))(.[^\\?\\n]*)(\\?.*)?$", RegexOptions.Compiled)]
        internal static partial Regex GetRegexMatchSpotifyLink();

        internal static readonly string spotifyIconLink = "https://open.spotifycdn.com/cdn/images/icons/Spotify_256.17e41e58.png";
        string trackID = "";
        static SpotifyClient spClient;
        internal static SpotifyClient SPClient
        {
            get
            {
                if (spClient == null)
                {
                    if (!string.IsNullOrEmpty(Config.gI().SpotifyCookie))
                        spClient = new SpotifyClient(new HttpClient(new HttpClientHandler() { CookieContainer = MusicUtils.GetCookie(".spotify.com", Config.gI().SpotifyCookie), ServerCertificateCustomValidationCallback = delegate { return true; } }));
                    else
                        spClient = new SpotifyClient();
                }
                return spClient;
            }
        }
        Track? track;
        string mimeType = "";
        string link = "";
        TimeSpan duration;
        string title = "";
        string[] artists = [];
        string[] artistsWithLinks = [];
        string albumThumbnailLink = "";
        string audioFilePath = "";
        string pcmFile = "";
        string album = "";
        string albumWithLink = "";
        FileStream? musicPCMDataStream;
        bool canGetStream;
        bool _disposed;

        public SpotifyMusic() { }
        public SpotifyMusic(string linkOrKeyword)
        {
            if (!GetRegexMatchSpotifyLink().IsMatch(linkOrKeyword))
            {
                List<TrackSearchResult> result = SPClient.Search.GetTracksAsync(linkOrKeyword, 0, 1).Result;
                if (result.Count == 0)
                    throw new MusicException("songs not found");
                linkOrKeyword = result[0].Url;
            }
            if (linkOrKeyword.Contains("spotify.link"))
                linkOrKeyword = new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, linkOrKeyword), HttpCompletionOption.ResponseHeadersRead).Result.RequestMessage.RequestUri.ToString();
            link = linkOrKeyword;
            track = SPClient.Tracks.GetAsync(linkOrKeyword).Result;
            title = track.Title;
            artists = track.Artists.Select(a => a.Name).ToArray();
            artistsWithLinks = track.Artists.Select(a => Formatter.MaskedUrl(a.Name, new Uri($"https://open.spotify.com/artist/{a.Id}"))).ToArray();
            album = track.Album.Name;
            albumWithLink = Formatter.MaskedUrl(track.Album.Name, new Uri($"https://open.spotify.com/album/{track.Album.Id}"));
            if (track.Album.Images.Count != 0)
                albumThumbnailLink = track.Album.Images[0].Url;
            trackID = GetRegexMatchSpotifyLink().Match(link).Groups[1].Value;
        }

        ~SpotifyMusic() => Dispose(false);

        public void Download()
        {
            audioFilePath = Path.GetTempFileName();
            try
            {
                DownloadTrackUsingZotify();
                //if (MusicUtils.IsFFMPEGInPATH())
                //    mimeType = "taglib/mp3";
                //else
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
                        HttpClient httpClient = new HttpClient();
                        byte[] data = httpClient.GetByteArrayAsync(GetLinkFromSpotifyDown()).Result;
                        File.WriteAllBytes(audioFilePath, data);
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
            //File.Delete(audioFilePath);
            //audioFilePath = null;
        }

        public MusicType MusicType => MusicType.Spotify;
        public string PathOrLink => link;
        public TimeSpan Duration => duration;
        public string Title => title;
        public string TitleWithLink => Formatter.MaskedUrl(title, new Uri(link));
        public string[] Artists => artists;
        public string[] ArtistsWithLinks => artistsWithLinks;
        public string AllArtists => string.Join(", ", artists);
        public string AllArtistsWithLinks => string.Join(", ", artistsWithLinks);
        public string Album => album;
        public string AlbumWithLink => albumWithLink;
        public string AlbumThumbnailLink => albumThumbnailLink;
        public SponsorBlockOptions? SponsorBlockOptions
        {
            get => null;
            set { }
        }

        public Stream? MusicPCMDataStream
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MusicPCMDataStream));
                return musicPCMDataStream;
            }
        }

        public LyricData? GetLyric() => new LyricData("Chỉ người dùng Premium mới xem được lời bài hát!");   //TODO: Get lyrics using BeautifulLyrics/LyricPlus backend

        public DiscordEmbedBuilder AddFooter(DiscordEmbedBuilder embed) => embed.WithFooter("Powered by Spotify", spotifyIconLink);

        public string[] GetFilesInUse() => [audioFilePath, pcmFile];

        public string GetIcon() => Config.gI().SpotifyIcon;

        public string GetSongDesc(bool hasTimeStamp = false)
        {
            while (!canGetStream)
                Thread.Sleep(500);
            string musicDesc = $"Bài hát: {TitleWithLink}" + Environment.NewLine;
            musicDesc += $"Nghệ sĩ: {AllArtistsWithLinks}" + Environment.NewLine;
            musicDesc += $"Album: {AlbumWithLink}" + Environment.NewLine;
            if (hasTimeStamp)
                musicDesc += new TimeSpan((long)(MusicPCMDataStream.Position / (float)MusicPCMDataStream.Length * Duration.Ticks)).toString() + " / " + Duration.toString();
            else
                musicDesc += "Thời lượng: " + Duration.toString();
            return musicDesc;
        }

        public MusicFileDownload GetDownloadFile() => new MusicFileDownload('.' + mimeType.Remove(0, 7), new FileStream(audioFilePath, FileMode.Open, FileAccess.Read));

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
                response = httpClient.GetAsync(url).Result;
            }
            catch (TaskCanceledException)
            {
                throw new MusicException(MusicType.Spotify, "music download timeout");
            }
            JObject responseData = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            string downloadUrl = responseData["link"].ToString();
            return downloadUrl;
        }

        public bool isLinkMatch(string link) => GetRegexMatchSpotifyLink().IsMatch(link);

        public string GetPCMFilePath() => pcmFile;

        public void DeletePCMFile()
        {
            try
            {
                File.Delete(pcmFile);
                pcmFile = "";
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
