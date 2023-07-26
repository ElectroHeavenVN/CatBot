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
            //TestAsync().GetAwaiter().GetResult();
            DiscordBotMain.Main();
        }

        static async Task TestAsync()
        {
            var soundcloud = new SoundCloudClient();
            var track = await soundcloud.Tracks.GetAsync("https://soundcloud.com/taigamusic1028/maisondes-taiga-flip-1?si=bc2893fd7eb141e3b814e684e9de368c&utm_source=clipboard&utm_medium=text&utm_campaign=social_sharing");
            var downloadUrl = await soundcloud.Tracks.GetDownloadUrlAsync(track);
        }
    }
}
