using System;

namespace CatBot.Music
{
    internal class NotAPlaylistException : Exception
    {
        public NotAPlaylistException() : base() { }
        public NotAPlaylistException(string errorMessage) : base(errorMessage) { }
    }
}
