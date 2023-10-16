using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music
{
    internal class NotAPlaylistException : Exception
    {
        public NotAPlaylistException() : base() { }
        public NotAPlaylistException(string errorMessage) : base(errorMessage) { }
    }
}
