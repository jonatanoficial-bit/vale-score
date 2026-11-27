using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Vale Score")]
[assembly: AssemblyProduct("Vale Score")]
[assembly: AssemblyDescription("Editor desktop de partituras")]
[assembly: AssemblyVersion("0.26.0.0")]
[assembly: AssemblyFileVersion("0.26.0.0")]

namespace ValeScore
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Editor());
        }
    }
}

