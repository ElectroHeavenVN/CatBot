using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DiscordBot.Music.SponsorBlock;
using System.Threading;
using DiscordBot.Music.ZingMP3;
using Leaf.xNet;

namespace DiscordBot.Music
{
    internal static class MusicUtils
    {
        static Regex regexMatchEmbedLink = new Regex("\\[(.*?)\\]\\((.*?)\\)", RegexOptions.Compiled);
        static readonly Regex mainMinJSRegex = new Regex("https://zjs.zmdcdn.me/zmp3-desktop/releases/(.*?)/static/js/main.min.js", RegexOptions.Compiled);
        static DateTime lastTimeCheckZingMP3Version = DateTime.Now.Subtract(new TimeSpan(12, 1, 0));
        private static HttpRequest httpRequestWithCookie;

        internal static IMusic CreateMusicInstance(string linkOrKeywordOrPath, MusicType musicType)
        {
            if (musicType == 0)
                throw new NullReferenceException();
            Type[] musicTypes = typeof(IMusic).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IMusic))).ToArray();
            foreach (Type type in musicTypes)
            {
                IMusic singletonInstance = (IMusic)Activator.CreateInstance(type, true);
                if (singletonInstance.MusicType == musicType)
                    try
                    {
                        return (IMusic)Activator.CreateInstance(type, linkOrKeywordOrPath);
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw ex.InnerException;
                    }
            }
            throw new ArgumentOutOfRangeException();
        }

        internal static bool TryCreateMusicInstance(string link, out IMusic music)
        {
            music = null;
            Type[] musicTypes = typeof(IMusic).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IMusic))).ToArray();
            foreach (Type musicType in musicTypes)
            {
                IMusic singletonInstance = (IMusic)Activator.CreateInstance(musicType, true);
                if (singletonInstance.isLinkMatch(link))
                {
                    try
                    {
                        music = (IMusic)Activator.CreateInstance(musicType, link);
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw ex.InnerException;
                    }
                    return true;
                }
            }
            return false;
        }

        internal static bool TryCreateMusicPlaylistInstance(string link, out IPlaylist playlist)
        {
            playlist = null;
            Type[] musicPlaylistTypes = typeof(IPlaylist).Assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPlaylist))).ToArray();
            foreach (Type musicPlaylistType in musicPlaylistTypes)
            {
                IPlaylist singletonInstance = (IPlaylist)Activator.CreateInstance(musicPlaylistType, true);
                if (singletonInstance.isLinkMatch(link))
                {
                    try
                    {
                        playlist = (IPlaylist)Activator.CreateInstance(musicPlaylistType, link);
                    }
                    catch (TargetInvocationException ex) 
                    {
                        if (ex.InnerException is NotAPlaylistException)
                            continue;
                        if (ex.InnerException is MusicException)
                        throw ex.InnerException;
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
            string tempFolder = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), Utils.RandomString(10));
            Directory.CreateDirectory(tempFolder);
            tempFile = Path.Combine(tempFolder, "..tmp");
            Process zotify = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "Zotify\\Zotify",
                    Arguments = $"--username {Config.SpotifyUsername} --password {Config.SpotifyPassword} --root-path .\\ --temp-download-dir .\\ --output {tempFile} {link}",
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
            

        }

        internal static void DownloadWEBMFromYouTube(string link, ref string tempFile, SponsorBlockSkipSegment[] sponsorBlockSkipSegments = null)
        {
            string randomString = Utils.RandomString(10);
            tempFile = Path.Combine(Environment.ExpandEnvironmentVariables("%temp%"), $"tmp{randomString}.webm");
            Thread.Sleep(100);
            Process yt_dlp_x86 = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp\\yt-dlp_x86",
                    Arguments = $"-f bestaudio --paths {Path.GetDirectoryName(tempFile)} -o {Path.GetFileName(tempFile)} --force-overwrites {link}",
                    WorkingDirectory = "yt-dlp",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            Console.WriteLine("--------------yt-dlp Console output--------------");
            yt_dlp_x86.Start();
            yt_dlp_x86.WaitForExit();
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
            foreach (SponsorBlockSkipSegment segment in sponsorBlockSkipSegments)
            {
                if (segment.Segment.IsLengthZero())
                    continue;
                concatFileContent += "file '" + tempWEBMFile + "'" + Environment.NewLine;
                if (segment.Segment.Start > 0)
                    concatFileContent += "outpoint " + segment.Segment.Start.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
                if (segment.VideoDuration - segment.Segment.End > 0)
                    concatFileContent += "inpoint " + segment.Segment.End.ToString("0.000000").Replace(',', '.') + Environment.NewLine;
            }
            File.WriteAllText(concatFile, concatFileContent);
        }

        internal static string GetLocalSongTitle(string musicFileName)
        {
            TagLib.File taglibMusicFile = TagLib.File.Create(Path.Combine(Config.MusicFolder, musicFileName + ".mp3"));
            string str = string.IsNullOrWhiteSpace(taglibMusicFile.Tag.Title) ? musicFileName : taglibMusicFile.Tag.Title;
            string artists = string.Join(", ", taglibMusicFile.Tag.Performers);
            if (!string.IsNullOrWhiteSpace(artists))
                str += " - " + artists;
            return str;
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

        internal static string RemoveEmbedLink(string withEmbedLink)
        {
            return regexMatchEmbedLink.Replace(withEmbedLink, m => m.Groups[1].Value);
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

        internal static HttpRequest InitHttpRequestWithCookie()
        {
            if (httpRequestWithCookie == null)
            {
                CheckZingMP3Version();
                httpRequestWithCookie = new HttpRequest { SslProtocols = SslProtocols.Tls12 };
                SetCookie(httpRequestWithCookie, string.Format(Config.ZingMP3Cookie, ZingMP3Music.zingMP3Version.Replace(".", "")));
                httpRequestWithCookie.UserAgent = Config.UserAgent;
                httpRequestWithCookie.AcceptEncoding = "gzip, deflate, br";
                httpRequestWithCookie.Referer = ZingMP3Music.zingMP3Link;
                httpRequestWithCookie.AddHeader(HttpHeader.Accept, "*/*");
                httpRequestWithCookie.AddHeader(HttpHeader.AcceptLanguage, "vi");
                httpRequestWithCookie.AddHeader("Host", "zingmp3.vn");
                httpRequestWithCookie.Get(ZingMP3Music.zingMP3Link, null);    
            }
            return httpRequestWithCookie;
        }

        internal static void CheckZingMP3Version()
        {
            if ((DateTime.Now - lastTimeCheckZingMP3Version).TotalHours > 12)
            {
                HttpRequest http = new HttpRequest { SslProtocols = SslProtocols.Tls12 };
                http.UserAgent = Config.UserAgent;
                http.AcceptEncoding = "gzip, deflate, br";
                http.Referer = ZingMP3Music.zingMP3Link;
                http.AddHeader(HttpHeader.Accept, "*/*");
                http.AddHeader(HttpHeader.AcceptLanguage, "vi");
                http.AddHeader("Host", "zingmp3.vn");
                string zingMP3Web = http.Get(ZingMP3Music.zingMP3Link, null).ToString();
                while(string.IsNullOrWhiteSpace(ZingMP3Music.zingMP3Version))
                    ZingMP3Music.zingMP3Version = mainMinJSRegex.Match(zingMP3Web).Groups[1].Value.Replace("v", "");
                lastTimeCheckZingMP3Version = DateTime.Now;
            }
        }

        internal static MemoryStream GetMusicWaveform(IMusic music, bool fullGray = false)
        {
            byte[] buffer = File.ReadAllBytes(music.GetPCMFilePath());
            long pos = music.MusicPCMDataStream.Position;
            Bitmap bitmap = new Bitmap(500, 30);
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

        internal static void SetCookie(HttpRequest httpRequest, string cookies, bool RemoveOldCookie = true)
        {
            cookies = cookies.Replace("Cookie: ", "");
            string[] array = cookies.Split(';');
            if (RemoveOldCookie)
                httpRequest.Cookies = new CookieStorage(false);
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Contains("="))
                    continue;
                string[] cookie = array[i].Split('=');
                string value = "";
                for (int j = 1; j < cookie.Length; j++)
                    value += cookie[j].Trim() + "=";
                value = value.Remove(value.Length - 1);
                try
                {
                    httpRequest.Cookies.Add(new Cookie(cookie[0].Trim(), value));
                }
                catch
                {
                }
            }
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
                catch
                {
                }
            }
        }
    }
}
