using System;
using System.Windows.Forms;

namespace ClockApp
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメインエントリポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
