using System.Runtime.InteropServices;

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
