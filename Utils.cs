using CatBot.Instance;
using CatBot.Music;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using Leaf.xNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CatBot
{
    public static class Utils
    {
        static Random random = new Random();

        public static bool isDisposed(this VoiceNextConnection connection) => (bool)typeof(VoiceNextConnection).GetProperty("IsDisposed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(connection);

        public static void SetVolume(this VoiceNextConnection connection, double value) => connection.GetTransmitSink().VolumeModifier = value;

        public static bool isInAdminServer(this DiscordMember member) => Config.gI().adminServer.Members.Keys.Contains(member.Id);

        public static async Task<bool> TryRespondAsync(this SnowflakeObject obj, params object[] parameters)
        {
            bool result = true;
            if (obj is DiscordMessage message)
            {
                if (parameters.Length == 1)
                {
                    if (parameters[0] is string str)
                        await message.RespondAsync(str);
                    else if (parameters[0] is DiscordEmbed embed)
                        await message.RespondAsync(embed);
                    else if (parameters[0] is DiscordEmbedBuilder embedBuilder)
                        await message.RespondAsync(embedBuilder);
                    else if (parameters[0] is DiscordMessageBuilder builder)
                        await message.RespondAsync(builder);
                    else if (parameters[0] is Action<DiscordMessageBuilder> action)
                        await message.RespondAsync(action);
                }
                else if (parameters.Length == 2 && parameters[0] is string str && parameters[1] is DiscordEmbed embed)
                    await message.RespondAsync(str, embed);
            }
            else if (obj is DiscordInteraction interaction)
            {
                if (parameters[0] is InteractionResponseType || parameters[0] is int)
                    await interaction.CreateResponseAsync((InteractionResponseType)parameters[0], (DiscordInteractionResponseBuilder)(parameters.Length == 2 ? parameters[1] : null));
                else if (parameters[0] is DiscordInteractionResponseBuilder)
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, (DiscordInteractionResponseBuilder)parameters[0]);
                else if (parameters[0] is string)
                {
                    if (parameters.Length >= 2 && (parameters[1] is DiscordEmbed || parameters[1] is DiscordEmbedBuilder))
                    {
                        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent((string)parameters[0]).AddEmbed((DiscordEmbed)parameters[1]).AsEphemeral((bool)(parameters.Length > 2 ? parameters[2] : false)));
                    }
                    else
                    {
                        await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent((string)parameters[0]).AsEphemeral((bool)(parameters.Length == 2 ? parameters[1] : false)));
                    }
                }
                else if (parameters[0] is DiscordEmbed || parameters[0] is DiscordEmbedBuilder)
                {
                    await interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed((DiscordEmbed)parameters[0]).AsEphemeral((bool)(parameters.Length == 2 ? parameters[1] : false)));
                }
            }
            else
                result = false;
            if (!result)
                await Console.Out.WriteLineAsync("Method not found!");
            return result;
        }

        public static DiscordUser TryGetUser(this SnowflakeObject obj)
        {
            if (obj is DiscordMessage message)
                return message.Author;
            else if (obj is DiscordInteraction interaction)
                return interaction.User;
            else 
                return null;
        }
        
        public static DiscordChannel TryGetChannel(this SnowflakeObject obj)
        {
            if (obj is DiscordMessage message)
                return message.Channel;
            else if (obj is DiscordInteraction interaction)
                return interaction.Channel;
            else 
                return null;
        }

        internal static string toString(this TimeSpan timeSpan)
        {
            string result = $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            if (timeSpan.TotalHours > 1)
                result = $"{timeSpan.TotalHours:00}:" + result;
            return result;
        }
        
        internal static string toVietnameseString(this TimeSpan timeSpan)
        {
            string seconds = $"{timeSpan.Seconds:#0} giây";
            string minutes = $"{timeSpan.Minutes:#0} phút";
            string hours = $"{timeSpan.TotalHours:#0} giờ";
            if (timeSpan.Seconds == 0 && (timeSpan.Minutes > 0 || timeSpan.Hours > 0))
                seconds = "";
            if (timeSpan.Minutes == 0)
                minutes = "";
            if (timeSpan.Hours == 0)
                hours = "";
            return (hours + " " + minutes + " " + seconds).Trim();
        }

        internal static Stream GetPCMStream(string filePath)
        {
            Process ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg\\ffmpeg",
                Arguments = "-nostdin -hide_banner -loglevel panic -i \"" + filePath + "\" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            return ffmpeg.StandardOutput.BaseStream;
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
            List<string> result = new List<string>();
            foreach (BotServerInstance serverInstance in BotServerInstance.serverInstances)
            {
                foreach (IMusic music in serverInstance.musicPlayer.musicQueue)     
                {
                    if (music != null)
                        result.AddRange(music.GetFilesInUse());
                }
                if (serverInstance.musicPlayer.currentlyPlayingSong != null)
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

        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
                return text;
            return string.Concat(text.Substring(0, pos), replace, text.Substring(pos + search.Length));
        }

        public static bool IsBotOwner(ulong userId) => Config.gI().BotOwnersID.Contains(userId) || DiscordBotMain.botClient.CurrentApplication.Owners.Any(u => u.Id == userId);

        //internal static List<DiscordEmbedBuilder> SplitLongEmbed(this DiscordEmbedBuilder embed)
        //{
        //    List<DiscordEmbedBuilder> embeds = new List<DiscordEmbedBuilder>();
        //    string description = embed.Description;
        //    while (description.Length > 4096)
        //    {
        //        DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
        //        embedBuilder.Description = description.Substring(0, 4096);
        //        description = description.Substring(4096);
        //        embeds.Add(embedBuilder);
        //    }
        //    embeds[0] = embed.WithDescription(embeds[0].Description);
        //    return embeds;
        //}
    }
}
