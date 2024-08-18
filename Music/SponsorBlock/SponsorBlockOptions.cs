namespace CatBot.Music.SponsorBlock
{
    internal class SponsorBlockOptions
    {
        internal bool Enabled { get; set; } = true;

        SponsorBlockCategory options = SponsorBlockCategory.All;

        internal void AddOrRemoveOptions(SponsorBlockCategory type)
        {
            if (options == 0 && type != 0)
                Enabled = true;
            options ^= type;
            if (options == 0)
                Enabled = false;
        }

        internal void SetOptions(SponsorBlockCategory type) => options = type;

        internal string GetName()
        {
            string result = "";
            if (options == SponsorBlockCategory.All)
                result += "Tất cả";
            else
            {
                if (options.HasFlag(SponsorBlockCategory.Sponsor))
                    result += SponsorBlockCategory.Sponsor.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.Intro))
                    result += SponsorBlockCategory.Intro.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.Outro))
                    result += SponsorBlockCategory.Outro.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.SelfPromo))
                    result += SponsorBlockCategory.SelfPromo.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.Preview))
                    result += SponsorBlockCategory.Preview.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.Filler))
                    result += SponsorBlockCategory.Filler.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.Interaction))
                    result += SponsorBlockCategory.Interaction.GetName() + ", ";
                if (options.HasFlag(SponsorBlockCategory.MusicOffTopic))
                    result += SponsorBlockCategory.MusicOffTopic.GetName() + ", ";
            }
            return result.Trim(", ".ToCharArray());
        }

        internal bool HasOption(SponsorBlockCategory type) => options.HasFlag(type);

        internal string[] GetCategory()
        {
            List<string> result = new List<string>();
            if (options.HasFlag(SponsorBlockCategory.Sponsor))
                result.Add("sponsor");
            if (options.HasFlag(SponsorBlockCategory.Intro))
                result.Add("intro");
            if (options.HasFlag(SponsorBlockCategory.Outro))
                result.Add("outro");
            if (options.HasFlag(SponsorBlockCategory.SelfPromo))
                result.Add("selfpromo");
            if (options.HasFlag(SponsorBlockCategory.Preview))
                result.Add("preview");
            if (options.HasFlag(SponsorBlockCategory.Filler))
                result.Add("filler");
            if (options.HasFlag(SponsorBlockCategory.Interaction))
                result.Add("interaction");
            if (options.HasFlag(SponsorBlockCategory.MusicOffTopic))
                result.Add("music_offtopic");
            return result.ToArray();
        }
    }
}
