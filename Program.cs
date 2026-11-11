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
            // Errors nobody catches: log them before the process dies, so a "crash without a message" leaves a trace.
            // (A native crash inside a library, or the process being ended from Task Manager / "Not Responding",
            // cannot be logged; a log that just stops without "Shutdown" points to one of those.)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogFatal("Unhandled exception (app is closing: " + e.IsTerminating + ")", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogFatal("Unobserved background task exception", e.Exception);
                e.SetObserved();
            };
            Application.ThreadException += (s, e) =>
            {
                LogFatal("Unhandled exception on the UI thread", e.Exception);
                MessageBox.Show("אירעה שגיאה לא צפויה. הפרטים נרשמו בקובץ הלוג." + Environment.NewLine + e.Exception.Message,
                    "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

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
            // Load Spire/IronPdf in the background now, so the first file does not wait ~10 s for them.
            Warmup.Start();
            Application.Run(new Form1());
            SimpleLogger.SimpleLog.Info("Shutdown");
        }

        private static void LogFatal(string what, Exception ex)
        {
            try
            {
                SimpleLogger.SimpleLog.Log(what + ": " + (ex?.ToString() ?? "(no exception details)"), SimpleLogger.SimpleLog.Severity.Error, useBackgroundTask: false);
            }
            catch
            {
                // Logging must never throw from a crash handler.
            }
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
