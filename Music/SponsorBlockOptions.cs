using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    internal class SponsorBlockOptions
    {
        internal bool Enabled { get; set; } = true;

        SponsorBlockSectionType options = SponsorBlockSectionType.All;

        internal void AddOrRemoveOptions(SponsorBlockSectionType type)
        {
            if (options == 0 && type != 0)
                Enabled = true;
            options ^= type;
            if (options == 0)
                Enabled = false;
        }

        internal void SetOptions(SponsorBlockSectionType type) => options = type;

        internal string GetName()
        {
            string result = "";
            if (options == SponsorBlockSectionType.All)
                result += "Tất cả";
            else
            {
                if (options.HasFlag(SponsorBlockSectionType.Sponsor))
                    result += SponsorBlockSectionType.Sponsor.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.Intro))
                    result += SponsorBlockSectionType.Intro.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.Outro))
                    result += SponsorBlockSectionType.Outro.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.SelfPromo))
                    result += SponsorBlockSectionType.SelfPromo.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.Preview))
                    result += SponsorBlockSectionType.Preview.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.Filler))
                    result += SponsorBlockSectionType.Filler.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.Interaction))
                    result += SponsorBlockSectionType.Interaction.GetName() + ", ";
                if (options.HasFlag(SponsorBlockSectionType.MusicOfftopic))
                    result += SponsorBlockSectionType.MusicOfftopic.GetName() + ", ";
            }
            return result.Trim(", ".ToCharArray());
        }

        internal bool HasOption(SponsorBlockSectionType type) => options.HasFlag(type);

        internal string GetArgument()
        {
            if (!Enabled)
                return " ";
            return " --sponsorblock-remove=" + GetCategoryArgument(options);
        }

        static string GetCategoryArgument(SponsorBlockSectionType category)
        {
            string result = "";
            if (category == SponsorBlockSectionType.All)
                result += "all";
            else
            {
                if (category.HasFlag(SponsorBlockSectionType.Sponsor))
                    result += "sponsor,";
                if (category.HasFlag(SponsorBlockSectionType.Intro))
                    result += "intro,";
                if (category.HasFlag(SponsorBlockSectionType.Outro))
                    result += "outro,";
                if (category.HasFlag(SponsorBlockSectionType.SelfPromo))
                    result += "selfpromo,";
                if (category.HasFlag(SponsorBlockSectionType.Preview))
                    result += "preview,";
                if (category.HasFlag(SponsorBlockSectionType.Filler))
                    result += "filler,";
                if (category.HasFlag(SponsorBlockSectionType.Interaction))
                    result += "interaction,";
                if (category.HasFlag(SponsorBlockSectionType.MusicOfftopic))
                    result += "music_offtopic,";
            }
            return result.Trim(',');
        }

        internal string[] GetCategory()
        {
            List<string> result = new List<string>();
            if (options.HasFlag(SponsorBlockSectionType.Sponsor))
                result.Add("sponsor");
            if (options.HasFlag(SponsorBlockSectionType.Intro))
                result.Add("intro");
            if (options.HasFlag(SponsorBlockSectionType.Outro))
                result.Add("outro");
            if (options.HasFlag(SponsorBlockSectionType.SelfPromo))
                result.Add("selfpromo");
            if (options.HasFlag(SponsorBlockSectionType.Preview))
                result.Add("preview");
            if (options.HasFlag(SponsorBlockSectionType.Filler))
                result.Add("filler");
            if (options.HasFlag(SponsorBlockSectionType.Interaction))
                result.Add("interaction");
            if (options.HasFlag(SponsorBlockSectionType.MusicOfftopic))
                result.Add("music_offtopic");
            return result.ToArray();
        }
    }
    public enum SponsorBlockSectionType
    {
        Sponsor = 1,
        [ChoiceName("Intermission/Intro animation")]
        Intro = 2,
        [ChoiceName("Endcards/Credits")]
        Outro = 4,
        [ChoiceName("Unpaid/Self promotion")]
        SelfPromo = 8,
        [ChoiceName("Preview/Recap")]
        Preview = 16,
        [ChoiceName("Filler tangent/Jokes")]
        Filler = 32,
        [ChoiceName("Interaction reminder (Subscribe)")]
        Interaction = 64,
        [ChoiceName("Music: Non-music section")]
        MusicOfftopic = 128,
        [ChoiceName("Tất cả")]
        All = Sponsor | Intro | Outro | SelfPromo | Preview | Filler | Interaction | MusicOfftopic
    }
}
