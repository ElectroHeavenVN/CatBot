using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CatBot
{

    internal class Program
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("msvcrt.dll")]
        [return: MarshalAs(UnmanagedType.I4)]
        internal static extern int system(string cmd);

        static void Main(string[] args)
        {
            AllocConsole();
            Console.OutputEncoding = Encoding.Unicode;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DiscordBotMain.Main();
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject);
            system("pause");
            Environment.FailFast(e.ExceptionObject.ToString());
        }
    }
}
