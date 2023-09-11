using DiscordBot.Music.SponsorBlock;
using DiscordBot.SoundCloudExplodeExtension;
using DiscordBot.Voice;
using Newtonsoft.Json;
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
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("msvcrt.dll")]
        static extern int system(string cmd);

        static void Main(string[] args)
        {
            AllocConsole();
            //system("pause");
            //return;
            DiscordBotMain.Main();
        }
    }
}
