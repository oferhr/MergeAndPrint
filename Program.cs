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
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                _ = AppConfig.Current;
            }
            catch (Exception ex)
            {
                MessageBox.Show("טעינת ההגדרות נכשלה. יש לתקן את הקובץ ולהפעיל מחדש את התוכנה:" + Environment.NewLine
                    + AppConfig.FilePath + Environment.NewLine + ex.Message, "שגיאה בטעינת הגדרות",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IronPdf.License.LicenseKey = "IRONPDF.ERANMOR.IRO230226.9271.10135.622032-2DD0FD6081-KKPMPWATUQCCO-V2KQEWDNF34E-CLNVNYIYJKCB-AEDVOLCTLZHF-UGOPMILN7BQ7-J6UI42-LZC54LOUHIOTUA-LITE.5YR-UOUSZX.RENEW.SUPPORT.25.FEB.2028";
            // IronPdf.License.LicenseKey = "IRONPDF.ERANMOR.IRO210119.6673.61137.911012-E4A858C1E3-BVFV36ZMBODS75L-ZO7KIF2CSE6J-HSFYXRNZBPLL-YDMZFGUBQ6CZ-BRTEE3GQAWOA-NPJWTO-LDJM7U3RQN2DUA-PRO.1DEV.1YR-BJNCXP.RENEW.SUPPORT.19.JAN.2022";
            Application.Run(new Form1());
        }
    }
}
