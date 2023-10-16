using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music
{
    internal class MusicException : Exception
    {
        public MusicException() { }
        public MusicException(string message) : base(message) { }
    }
}
