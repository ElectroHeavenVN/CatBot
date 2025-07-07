using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CatBot.Instance;
using CatBot.Music;
using DSharpPlus;

namespace CatBot
{
    public static class Utils
    {
        static Random random = new Random();

        internal static Stream GetPCMStream(string filePath)
        {
            Process? ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "Files\\ffmpeg\\ffmpeg",
                Arguments = "-nostdin -hide_banner -loglevel panic -i \"" + filePath + "\" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            return ffmpeg?.StandardOutput.BaseStream ?? throw new NullReferenceException();
        }

        internal static void LogException(Exception ex, bool reportException = true)
        {
            string exceptionMessage = $"[Ex: {DateTime.Now}] {ex}";
            Console.WriteLine(exceptionMessage);
            try
            {
                if (reportException)
                    Config.gI().exceptionReportChannel?.SendMessageAsync("```\r\n" + exceptionMessage + "\r\n```").GetAwaiter().GetResult();
            }
            catch (Exception ex2) { Console.WriteLine(ex2); }
        }

        internal static List<string> GetAllFilesInUse()
        {
            List<string> result = [];
            foreach (BotServerInstance serverInstance in BotServerInstance.serverInstances)
            {
                foreach (IMusic music in serverInstance.musicPlayer.musicQueue)     
                {
                    if (music is not null)
                        result.AddRange(music.GetFilesInUse());
                }
                if (serverInstance.musicPlayer.currentlyPlayingSong is not null)
                    result.AddRange(serverInstance.musicPlayer.currentlyPlayingSong.GetFilesInUse());
            }
            return result.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        internal static string RandomString(int length)
        {
            string str = "1234567890qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";
            string result = "";
            for (int i = 0; i < length; i++)
                result += str[random.Next(0, str.Length)];
            return result;
        }

        internal static string GetMemorySize(ulong bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (bytes == 0)
                return "0" + suf[0];
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 3);
            return num + suf[place];
        }

        internal static bool IsBotOwner(ulong userId) => Config.gI().BotOwnerIDs.Contains(userId) || (DiscordBotMain.botClient.CurrentApplication?.Owners?.Any(u => u.Id == userId) ?? false);

        internal static string ComputeSHA256Hash(byte[] data)
        {
            SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
