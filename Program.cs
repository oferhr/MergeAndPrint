using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            // Log next to the exe, not to whatever the shortcut's "Start in" folder happens to be.
            // If that folder isn't writable (e.g. installed under Program Files), use %LOCALAPPDATA%\MergeAndPrint.
            var logDir = IsWritable(AppContext.BaseDirectory)
                ? AppContext.BaseDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MergeAndPrint");
            SimpleLogger.SimpleLog.SetLogDir(logDir, true);
            SimpleLogger.SimpleLog.Info(string.Format(
                "Startup: MergeAndPrint {0}, user {1}, machine {2}, exe dir {3}, log dir {4}, config {5}",
                FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? "").FileVersion,
                Environment.UserName, Environment.MachineName, AppContext.BaseDirectory, logDir, AppConfig.FilePath));
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

            Licenses.Apply();
            Application.Run(new Form1());
            SimpleLogger.SimpleLog.Info("Shutdown");
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
