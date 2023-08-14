using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    internal class NotAPlaylistException : Exception
    {
        public NotAPlaylistException() : base() { }
        public NotAPlaylistException(string errorMessage) : base(errorMessage) { }
    }
}
