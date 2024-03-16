using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music
{
    internal class MusicFileDownload
    {
        internal string Extension { get; set; }
        internal Stream Stream { get; set; }

        internal MusicFileDownload(string extension, Stream stream)
        {
            Extension = extension;
            Stream = stream;
        }
    }
}
