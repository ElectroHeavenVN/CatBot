using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Music
{
    internal class MusicException : Exception
    {
        public MusicException() { }
        public MusicException(string message) : base(message) { }
    }
}
