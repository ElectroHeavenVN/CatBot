using DiscordBot.SoundCloudExplodeExtension;
using DiscordBot.Voice;
using SoundCloudExplode;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{

    internal class Program
    {
        [DllImport("kernel32")]
        static extern bool AllocConsole();

        static void Main(string[] args)
        {
            AllocConsole();
            DiscordBotMain.Main();
        }
    }
}
