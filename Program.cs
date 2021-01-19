using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MergeAndPrint
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            IronPdf.License.LicenseKey = "IRONPDF.ERANMOR.IRO210119.6673.61137.911012-E4A858C1E3-BVFV36ZMBODS75L-ZO7KIF2CSE6J-HSFYXRNZBPLL-YDMZFGUBQ6CZ-BRTEE3GQAWOA-NPJWTO-LDJM7U3RQN2DUA-PRO.1DEV.1YR-BJNCXP.RENEW.SUPPORT.19.JAN.2022";
            Application.Run(new Form1());
        }
    }
}
