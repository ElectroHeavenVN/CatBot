//using DSharpPlus.CommandsNext;
//using DSharpPlus.CommandsNext.Converters;
//using DSharpPlus.CommandsNext.Entities;
//using DSharpPlus.Entities;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;

//namespace CatBot
//{
//    internal class HelpFormatter : DefaultHelpFormatter
//    {
//        public HelpFormatter(CommandContext ctx) : base(ctx)
//        {
//            EmbedBuilder.WithTitle("Giúp đỡ").WithColor(DiscordColor.Azure);
//        }
//        public override BaseHelpFormatter WithCommand(Command command)
//        {
//            base.WithCommand(command);
//            EmbedBuilder.WithDescription(EmbedBuilder.Description.Replace("No description provided.", "Không có mô tả nào được cung cấp.").Replace("This group can be executed as a standalone command.", "Nhóm lệnh này có thể được thực thi như một lệnh độc lập."));
//            foreach (DiscordEmbedField field in EmbedBuilder.Fields)
//                field.Name = field.Name.Replace("Aliases", "Lệnh thay thế").Replace("Arguments", "Tùy chọn"); 
//            return this;
//        }
//        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
//        {
//            base.WithSubcommands(subcommands);
//            foreach (DiscordEmbedField field in EmbedBuilder.Fields)
//                field.Name = field.Name.Replace("Subcommands", "Các lệnh con").Replace("Commands", "Lệnh").Replace("Uncategorized commands", "Lệnh chưa được phân loại");
//            return this;
//        }
//        public override CommandHelpMessage Build()
//        {
//            if (typeof(DefaultHelpFormatter).GetProperty("Command", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) == null)
//                EmbedBuilder.WithDescription("Tất cả các nhóm lệnh và lệnh cấp cao nhất, chỉ định một lệnh để xem thêm thông tin.");
//            return new CommandHelpMessage(null, EmbedBuilder.Build());
//        }
//    }
//}
