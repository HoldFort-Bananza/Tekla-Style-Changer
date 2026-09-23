using System;
using System.Windows.Forms;

namespace StyleChanger
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Tryb konsolowy do diagnostyki bez GUI - patrz DiagRunner.cs.
            // Na tym etapie projektu jedyny cel: sprawdzić na żywej Tekli,
            // jak naprawdę nazywają się i gdzie leżą pliki stylów widoku,
            // zamiast zgadywać rozszerzenie/lokalizację.
            if (args.Length > 0 && args[0] == "--diag-active")
            {
                DiagRunner.RunOnActiveDrawing();
                return;
            }
            if (args.Length > 0 && args[0] == "--test-other-drawing")
            {
                DiagRunner.TestOnOtherDrawing();
                return;
            }
            if (args.Length > 1 && args[0] == "--dump-style")
            {
                DiagRunner.DumpStyleFile(args[1]);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
