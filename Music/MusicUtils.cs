using DiscordBot.Music.Local;
using DiscordBot.Music.NhacCuaTui;
using DiscordBot.Music.YouTube;
using DiscordBot.Music.ZingMP3;
using Leaf.xNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
            else if (musicType == MusicType.Local)
                return new LocalMusic(linkOrKeywordOrPath);
            else if (musicType == MusicType.ZingMP3)
                return new ZingMP3Music(linkOrKeywordOrPath);
            else if (musicType == MusicType.NhacCuaTui)
                return new NhacCuaTuiMusic(linkOrKeywordOrPath);
            else if (musicType == MusicType.YouTube)
                return new YouTubeMusic(linkOrKeywordOrPath);
            throw new ArgumentOutOfRangeException();
        }

        internal static bool TryCreateMusicInstance(string link, out IMusic music)
        {
            music = null;
            if (link.StartsWith(ZingMP3Music.zingMP3Link))
            {
                music = new ZingMP3Music(link);
                return true;
            }
            else if (link.StartsWith(NhacCuaTuiMusic.nhacCuaTuiLink))
            {
                music = new NhacCuaTuiMusic(link);
                return true;
            }
            else if (YouTubeMusic.regexMatchYTLink.IsMatch(link))
            {
                music = new YouTubeMusic(link);
                return true;
            }
            return false;
        }

        internal static string GetPCMFile(string filePath, ref string tempFile)
        {
            tempFile = Path.GetTempFileName();
            Console.WriteLine("--------------FFMpeg Console output--------------");
            Process ffmpeg = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-hide_banner -i \"" + filePath + "\" -ac 2 -f s16le -ar 48000 -y -threads 1 \"" + tempFile + "\"",
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            ffmpeg.ErrorDataReceived += (_, e) => Console.WriteLine(e.Data);
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.WaitForExit();
            Console.WriteLine("--------------End of FFMpeg Console output--------------");

            return tempFile;
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
                httpRequestWithCookie.AddHeader("Sec-Ch-Ua", Config.SecChUaHeader);
                httpRequestWithCookie.AddHeader("Sec-Ch-Ua-Mobile", "?0");
                httpRequestWithCookie.AddHeader("Sec-Ch-Ua-Platform", "\"Windows\"");
                httpRequestWithCookie.AddHeader("Sec-Fetch-Dest", "empty");
                httpRequestWithCookie.AddHeader("Sec-Fetch-Mode", "cors");
                httpRequestWithCookie.AddHeader("Sec-Fetch-Site", "same-origin");
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
                http.AddHeader("Sec-Ch-Ua", Config.SecChUaHeader);
                http.AddHeader("Sec-Ch-Ua-Mobile", "?0");
                http.AddHeader("Sec-Ch-Ua-Platform", "\"Windows\"");
                http.AddHeader("Sec-Fetch-Dest", "empty");
                http.AddHeader("Sec-Fetch-Mode", "cors");
                http.AddHeader("Sec-Fetch-Site", "same-origin");
                string zingMP3Web = http.Get(ZingMP3Music.zingMP3Link, null).ToString();
                while(string.IsNullOrWhiteSpace(ZingMP3Music.zingMP3Version))
                    ZingMP3Music.zingMP3Version = mainMinJSRegex.Match(zingMP3Web).Groups[1].Value.Replace("v", "");
                lastTimeCheckZingMP3Version = DateTime.Now;
            }
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
