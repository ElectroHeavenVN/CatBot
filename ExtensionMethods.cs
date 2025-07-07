using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CatBot
{
    internal static class ExtensionMethods
    {
        internal static bool IsDisposed(this VoiceNextConnection? connection) => (bool?)typeof(VoiceNextConnection).GetProperty("IsDisposed", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(connection) ?? false;

        internal static void SetVolume(this VoiceNextConnection connection, double value) => connection.GetTransmitSink().VolumeModifier = value;

        internal static bool IsInAdminUser(this DiscordUser user) => Config.gI().AdminUserIDs.Contains(user.Id) || Utils.IsBotOwner(user.Id);

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
        
        internal static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
                return text;
            return string.Concat(text.Substring(0, pos), replace, text.Substring(pos + search.Length));
        }
        
        internal static List<DiscordEmbedBuilder> SplitLongEmbed(this DiscordEmbedBuilder embed)
        {
            List<DiscordEmbedBuilder> embeds = new List<DiscordEmbedBuilder>();
            string? description = embed.Description;
            do
            {
                DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
                embedBuilder.Description = description?.Length > 2000 ? description.Substring(0, 2000) : description;
                description = description?.Length > 2000 ? description.Substring(2000) : "";
                embeds.Add(embedBuilder);
            }
            while (description.Length > 0);
            if (embeds.Count < 2)
                return [embed];
            embeds[embeds.Count - 1].Footer = embed.Footer;
            embeds[0] = embed.WithDescription(embeds[0].Description ?? "").WithFooter();
            return embeds;
        }

        internal static string Min(this string str, int length) => str.Length <= length ? str : str.Substring(0, length);

        internal static bool IsBotExcluded(this DiscordUser member) => member.IsBot && Config.gI().ExcludeBotIDs.Contains(member.Id);

        internal static string? GetName(this Enum v) => Enum.GetName(v.GetType(), v);
    }
}
