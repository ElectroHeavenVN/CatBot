﻿using System;

namespace CatBot.Music
{
    [Serializable]
    internal class LyricException : Exception
    {
        public LyricException() { }
        public LyricException(string message) : base(message) { }
        public LyricException(string message, Exception innerException) : base(message, innerException) { }
    }
}