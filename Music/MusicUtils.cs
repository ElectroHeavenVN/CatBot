using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CatBot.Music.Dummy;
using CatBot.Music.Local;
using CatBot.Music.SponsorBlock;
using Newtonsoft.Json.Linq;
using SpotifyExplode.Albums;

namespace CatBot.Music
{
    internal static class MusicUtils
    {
        internal static IMusic CreateMusicInstance(string keywordOrPath, MusicType musicType)
        {
            if (musicType == 0)
                throw new NullReferenceException();
            try
            {
                Type[] musicTypes = typeof(IMusic).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IMusic))).ToArray();
                foreach (Type type in musicTypes)
                {
                    ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    IMusic singletonInstance = (IMusic)constructors.First(c => c.GetParameters().Length == 0).Invoke(null);
                    if (singletonInstance.MusicType == musicType)
                        return (IMusic)constructors.First(c => c.GetParameters().Length == 1).Invoke([keywordOrPath]);
                }
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException && ex.InnerException is MusicException)
                    throw ex.InnerException;
                throw new MusicException("not found");
            }
            throw new ArgumentOutOfRangeException();
        }

        internal static bool TryCreateMusicInstance(string link, out IMusic music)
        {
            music = null;
            Type[] musicTypes = typeof(IMusic).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IMusic))).ToArray();
            foreach (Type musicType in musicTypes)
            {
                ConstructorInfo[] constructors = musicType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                IMusic singletonInstance = (IMusic)constructors.First(c => c.GetParameters().Length == 0).Invoke(null);
                if (singletonInstance.isLinkMatch(link))
                {
                    music = (IMusic)constructors.First(c => c.GetParameters().Length == 1).Invoke([link]);
                    return true;
                }
            }
            return false;
        }

        internal static bool TryCreateMusicPlaylistInstance(string link, MusicQueue musicQueue, out IPlaylist playlist)
        {
            playlist = null;
            Type[] musicPlaylistTypes = typeof(IPlaylist).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPlaylist))).ToArray();
            foreach (Type musicPlaylistType in musicPlaylistTypes)
            {
                ConstructorInfo[] constructors = musicPlaylistType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                IPlaylist singletonInstance = (IPlaylist)constructors.First(c => c.GetParameters().Length == 0).Invoke(null);
                if (singletonInstance.isLinkMatch(link))
                {
                    try
                    {
                        playlist = (IPlaylist)constructors.First(c => c.GetParameters().Length == 2).Invoke([link, musicQueue]);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException is NotAPlaylistException)
                            continue;
                        if (ex.InnerException is MusicException)
                            throw;
                    }
                    return true;
                }
            }
            return false;
        }

        internal static string GetPCMFile(string filePath, ref string tempFile)
        {
            tempFile = Path.GetTempFileName();
            Process ffmpeg = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg\\ffmpeg",
                    Arguments = "-nostdin -hide_banner -i \"" + filePath + "\" -ac 2 -f s16le -ar 48000 -y -threads:a 1 \"" + tempFile + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------FFMpeg Console output--------------");
            ffmpeg.Start();
            ffmpeg.WaitForExit();
            Console.WriteLine("--------------End of FFMpeg Console output--------------");

            return tempFile;
        }

        internal static void DownloadOGGFromSpotify(string link, ref string tempFile)
        {
            string randomString = "tmp" + Utils.RandomString(10);
            string tempFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), randomString);
            Directory.CreateDirectory(tempFolder);
            Process zotify = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "Zotify\\Zotify",
                    Arguments = $"--username {Config.gI().SpotifyUsername} --password {Config.gI().SpotifyPassword} --root-path .\\ --temp-download-dir .\\ --output ..tmp {link}",
                    WorkingDirectory = tempFolder,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------Zotify Console output--------------");
            zotify.Start();
            zotify.WaitForExit();
            Console.WriteLine("--------------End of Zotify Console output--------------");
            tempFile = tempFolder + ".tmp";
            File.Move(Path.Combine(tempFolder, "..tmp"), tempFile);
            Directory.Delete(tempFolder, true);
        }

        internal static void DownloadTrackUsingSpotdl(string link, ref string tempFile)
        {
            string randomString = "tmp" + Utils.RandomString(10);
            string tempFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), randomString);
            Directory.CreateDirectory(tempFolder);
            Process spotdl = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "spotdl\\spotdl",
                    Arguments = $"--ffmpeg ../ffmpeg/ffmpeg.exe --threads 1 --ffmpeg-args \"-threads:a 1\" --audio slider-kz --audio soundcloud --audio bandcamp --audio piped --audio youtube --audio youtube-music --output {tempFolder} {link}",
                    WorkingDirectory = "spotdl",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------Spotdl Console output--------------");
            spotdl.Start();
            spotdl.WaitForExit();
            Console.WriteLine("--------------End of Spotdl Console output--------------");
            tempFile = tempFolder + ".tmp";
            File.Move(new DirectoryInfo(tempFolder).GetFiles()[0].FullName, tempFile);
            Directory.Delete(tempFolder, true);
        }

        internal static void DownloadWEBMFromYouTube(string link, ref string tempFile, SponsorBlockSkipSegment[] sponsorBlockSkipSegments = null)
        {
            string randomString = Utils.RandomString(10);
            tempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.webm");
            Thread.Sleep(100);
            Process yt_dlp = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp\\yt-dlp",
                    Arguments = $"-f bestaudio --paths {Path.GetDirectoryName(tempFile)} -o {Path.GetFileName(tempFile)} --force-overwrites {link}",
                    WorkingDirectory = "yt-dlp",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------yt-dlp Console output--------------");
            yt_dlp.Start();
            yt_dlp.WaitForExit();
            Console.WriteLine("--------------End of yt-dlp Console output--------------");
            if (sponsorBlockSkipSegments != null && sponsorBlockSkipSegments.Length > 0)
            {
                string tempWEBMFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.temp.webm");
                string concatFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.temp.webm.concat");
                File.Move(tempFile, tempWEBMFile);
                WriteConcatFile(concatFile, tempWEBMFile, sponsorBlockSkipSegments);
                Process ffmpeg = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg\\ffmpeg",
                        Arguments = $"-y -hide_banner -f concat -safe 0 -i \"{concatFile}\" -map 0 -dn -ignore_unknown -c copy -movflags +faststart \"{tempFile}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                    },
                    EnableRaisingEvents = true,
                };
                Console.WriteLine("--------------FFMpeg Console output--------------");
                ffmpeg.Start();
                ffmpeg.WaitForExit();
                Console.WriteLine("--------------End of FFMpeg Console output--------------");
                File.Delete(tempWEBMFile);
                File.Delete(concatFile);
            }
        }

        static void WriteConcatFile(string concatFile, string tempWEBMFile, SponsorBlockSkipSegment[] sponsorBlockSkipSegments)
        {
            string concatFileContent = "ffconcat version 1.0" + Environment.NewLine;
            List<(double, double)> segmentsToKeep = new List<(double, double)>();
            (double, double) segmentToKeep = (0, -1);
            foreach (SponsorBlockSkipSegment segment in sponsorBlockSkipSegments)
            {
                if (segment.Segment.IsLengthZero())
                    continue;
                if (segment.Segment.Start > 0)
                    segmentToKeep.Item2 = segment.Segment.Start;
                if (segmentToKeep.Item1 != -1 && segmentToKeep.Item2 != -1)
                {
                    if (segmentToKeep.Item1 < segmentToKeep.Item2)
                        segmentsToKeep.Add(segmentToKeep);
                    segmentToKeep = (-1, -1);
                }
                if (segment.VideoDuration - segment.Segment.End > .5d)
                    segmentToKeep.Item1 = segment.Segment.End;
                if (segmentToKeep.Item1 != -1 && segmentToKeep.Item2 != -1)
                {
                    if (segmentToKeep.Item1 < segmentToKeep.Item2)
                        segmentsToKeep.Add(segmentToKeep);
                    segmentToKeep = (-1, -1);
                }
            }
            if (segmentToKeep.Item1 != -1 && segmentToKeep.Item2 == -1 && segmentToKeep.Item1 < sponsorBlockSkipSegments[0].VideoDuration)
            {
                segmentToKeep.Item2 = sponsorBlockSkipSegments[0].VideoDuration;
                segmentsToKeep.Add(segmentToKeep);
            }
            foreach (var segment in segmentsToKeep)
            {
                concatFileContent += "file '" + tempWEBMFile + "'" + Environment.NewLine;
                concatFileContent += "inpoint " + segment.Item1.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
                concatFileContent += "outpoint " + segment.Item2.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
            }
            File.WriteAllText(concatFile, concatFileContent);
        }

        internal static byte[] TrimStartNullBytes(byte[] data)
        {
            if (data.Length == 0)
                return data;
            List<byte> result = data.ToList();
            while (result[0] == 0)
                result.RemoveAt(0);
            return result.ToArray();
        }

        internal static string RemoveLyricTimestamps(string lyrics)
        {
            string result = "";
            foreach (string sentence in lyrics.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                string lyric = sentence;
                if (sentence.Contains("]"))
                    lyric = sentence.Remove(sentence.IndexOf('['), sentence.LastIndexOf(']') - sentence.IndexOf('[') + 1);
                result += lyric + Environment.NewLine;
            }
            result = result.Trim(Environment.NewLine.ToCharArray());
            return result;
        }

        internal static string HashSHA512(string text, string secretKey) => BitConverter.ToString(new HMACSHA512(Encoding.UTF8.GetBytes(secretKey)).ComputeHash(Encoding.UTF8.GetBytes(text)), 0).Replace("-", "").ToLower();

        internal static string HashSHA256(string text) => BitConverter.ToString(new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes(text)), 0).Replace("-", "").ToLower();

        internal static MemoryStream GetMusicWaveform(IMusic music, bool fullGray = false)
        {
            byte[] buffer = File.ReadAllBytes(music.GetPCMFilePath());
            long pos = music.MusicPCMDataStream.Position;
            Bitmap bitmap = new Bitmap(1500, 90);
            int samplesPerBar = buffer.Length / 2 / (bitmap.Width - 1);
            int[] samples = new int[bitmap.Width];
            int currentSampleIndex = 0;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                int index = i / 2 / samplesPerBar;
                if (i == pos - pos % 2)
                    currentSampleIndex = index;
                samples[index] = Math.Max(samples[index], Math.Abs((int)BitConverter.ToInt16(buffer, i)));
            }

            Graphics g = Graphics.FromImage(bitmap);
            Pen pen = new Pen(Color.White, 1);
            Pen pen2 = new Pen(Color.Gray, 1);
            g.Clear(Color.Transparent);
            int maxHeight = bitmap.Height / 2;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!fullGray && i <= currentSampleIndex)
                    g.DrawLine(pen, i, maxHeight + samples[i] * (maxHeight / 32767f), i, maxHeight - samples[i] * (maxHeight / 32767f));
                else
                    g.DrawLine(pen2, i, maxHeight + samples[i] * (maxHeight / 32767f), i, maxHeight - samples[i] * (maxHeight / 32767f));
            }
            MemoryStream result = new MemoryStream();
            bitmap.Save(result, ImageFormat.Png);
            result.Position = 0;
            return result;
        }

        internal static HttpClient CreateHttpClientWithCookies(string cookies)
        {
            CookieContainer cookieContainer = new CookieContainer();
            cookies = cookies.Replace("Cookie: ", "");
            string[] array = cookies.Split(';');
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Contains("="))
                    continue;
                string[] cookie = array[i].Trim().Split('=');
                try
                {
                    cookieContainer.Add(new Cookie(cookie[0], cookie[1]));
                }
                catch { }
            }
            HttpClient httpClient = new HttpClient(new HttpClientHandler() { UseCookies = true, CookieContainer = cookieContainer, AutomaticDecompression = DecompressionMethods.All });
            return httpClient;
        }

        internal static CookieContainer GetCookie(string domain, string cookies)
        {
            cookies = cookies.Replace("Cookie: ", "");
            string[] array = cookies.Split(';');
            CookieContainer result = new CookieContainer();
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Contains("="))
                    continue;
                string[] cookie = array[i].Trim().Split('=');
                try
                {
                    result.Add(new Cookie(cookie[0], cookie[1], "/", domain));
                }
                catch { }
            }
            return result;
        }

        internal static void SetCookie(this HttpWebRequest httpRequest, string cookies, string path, string domain, bool RemoveOldCookie = true)
        {
            cookies = cookies.Replace("Cookie: ", "");
            string[] array = cookies.Split(';');
            if (RemoveOldCookie)
                httpRequest.CookieContainer = new CookieContainer();
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Contains("="))
                    continue;
                string[] cookie = array[i].Split('=');
                try
                {
                    httpRequest.CookieContainer.Add(new Cookie(cookie[0].Trim(), cookie[1].Trim(), path, domain));
                }
                catch { }
            }
        }

        internal static bool IsFFMPEGInPATH()
        {
            return Environment.GetEnvironmentVariable("PATH").Split(';').Any(p => File.Exists(Path.Combine(p, "ffmpeg.exe")));
        }

        internal static bool TryGetLyricsFromLRCLIB(this IMusic music, out LyricData? lyricData)
        {
            lyricData = null;
            try
            {
                if (GetLyricsFromLRCLIB(music, out LyricData? result))
                {
                    lyricData = result;
                    return true;
                }
                if (FindLyricsFromLRCLIB(music, out result))
                {
                    lyricData = result;
                    return true;
                }
                if (FindLyricsFromLRCLIB(music.Title, out result))
                {
                    lyricData = result;
                    return true;
                }
            }
            catch { }
            return false;
        }

        static bool GetLyricsFromLRCLIB(IMusic music, out LyricData? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(music.Title) || music.Artists.Length == 0 || string.IsNullOrWhiteSpace(music.Album))
                return false;
            if (music is DummyMusic)
                return false;
            string address = $"{Config.gI().LyricAPI}get?track_name={Uri.EscapeDataString(music.Title)}&artist_name=";
            foreach (string artist in music.Artists)
                address += $"{Uri.EscapeDataString(artist)}+";
            address = $"{address[..^1]}&album_name={Uri.EscapeDataString(music.Album)}&duration={(int)music.Duration.TotalSeconds}";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", $"CatBot v{typeof(LocalMusic).Assembly.GetName().Version} ({typeof(LocalMusic).Assembly.GetCustomAttribute<AssemblyMetadataAttribute>().Value})");
            string jsonLyric = client.GetStringAsync(address).Result;
            JObject lyricData = JObject.Parse(jsonLyric);
            if (lyricData["name"].Value<string>() == "TrackNotFound")
                return false;
            if (lyricData["instrumental"].Value<bool>())
                result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), music.AlbumThumbnailLink) { PlainLyrics = "Bài hát này là bản nhạc không lời!" };
            else
                result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), music.AlbumThumbnailLink)
                {
                    PlainLyrics = lyricData["plainLyrics"].Value<string>(),
                    SyncedLyrics = lyricData["syncedLyrics"].Value<string>()
                };
            return true;
        }

        static bool FindLyricsFromLRCLIB(IMusic music, out LyricData? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(music.Title))
                return false;
            string address = $"{Config.gI().LyricAPI}search?track_name={Uri.EscapeDataString(music.Title)}";
            if (music.Artists.Length != 0)
            {
                address += "&artist_name=";
                foreach (string artist in music.Artists)
                    address += $"{Uri.EscapeDataString(artist)}+";
                address = $"{address[..^1]}";
            }
            if (!string.IsNullOrWhiteSpace(music.Album))
                address += $"&album_name={Uri.EscapeDataString(music.Album)}";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", $"CatBot v{typeof(LocalMusic).Assembly.GetName().Version} ({typeof(LocalMusic).Assembly.GetCustomAttribute<AssemblyMetadataAttribute>().Value})");
            string jsonLyric = client.GetStringAsync(address).Result;
            JArray lyricDatas = JArray.Parse(jsonLyric);
            foreach (JToken lyricData in lyricDatas)
            {
                if (music is not DummyMusic && Math.Abs(lyricData["duration"].Value<long>() - music.Duration.TotalSeconds) > 10)
                    continue;
                if (lyricData["name"].Value<string>() == "TrackNotFound")
                    return false;
                if (lyricData["instrumental"].Value<bool>())
                    result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), music.AlbumThumbnailLink) { PlainLyrics = "Bài hát này là bản nhạc không lời!" };
                else
                    result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), music.AlbumThumbnailLink)
                    {
                        PlainLyrics = lyricData["plainLyrics"].Value<string>(),
                        SyncedLyrics = lyricData["syncedLyrics"].Value<string>()
                    };
                return true;
            }
            return false;
        }
        
        static bool FindLyricsFromLRCLIB(string trackName, out LyricData? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(trackName))
                return false;
            string address = $"{Config.gI().LyricAPI}search?track_name={Uri.EscapeDataString(trackName)}";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", $"CatBot v{typeof(LocalMusic).Assembly.GetName().Version} ({typeof(LocalMusic).Assembly.GetCustomAttribute<AssemblyMetadataAttribute>().Value})");
            string jsonLyric = client.GetStringAsync(address).Result;
            JArray lyricDatas = JArray.Parse(jsonLyric);
            foreach (JToken lyricData in lyricDatas)
            {
                if (lyricData["name"].Value<string>() == "TrackNotFound")
                    return false;
                if (lyricData["instrumental"].Value<bool>())
                    result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), "") { PlainLyrics = "Bài hát này là bản nhạc không lời!" };
                else
                    result = new LyricData(lyricData["trackName"].Value<string>(), lyricData["artistName"].Value<string>(), lyricData["albumName"].Value<string>(), "")
                    {
                        PlainLyrics = lyricData["plainLyrics"].Value<string>(),
                        SyncedLyrics = lyricData["syncedLyrics"].Value<string>()
                    };
                return true;
            }
            return false;
        }
    }
}
