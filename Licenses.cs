using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using SimpleLogger;

namespace MergeAndPrint
{
    /// <summary>
    /// Loads the IronPdf and Spire license keys at startup. The keys are kept out of source control:
    /// each one comes from an environment variable or, failing that, from licenses.json next to the exe:
    /// <code>{ "IronPdf": "...", "Spire": "..." }</code>
    /// A missing key does not stop the app, but the library then runs in evaluation mode
    /// (IronPdf watermarks merged PDFs; Spire prints an "Evaluation Warning" line on every page),
    /// so the user is warned at startup.
    /// </summary>
    internal static class Licenses
    {
        private const string FileName = "licenses.json";
        private const string IronPdfEnv = "MERGEANDPRINT_IRONPDF_KEY";
        private const string SpireEnv = "MERGEANDPRINT_SPIRE_KEY";

        public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, FileName);

        private sealed class LicenseFile
        {
            public string IronPdf { get; set; }
            public string Spire { get; set; }
        }

        public static void Apply()
        {
            var file = ReadFile();
            var missing = "";

            var ironKey = Pick(IronPdfEnv, file?.IronPdf);
            if (ironKey != null)
                IronPdf.License.LicenseKey = ironKey;
            else
                missing += "IronPdf ";

            var spireKey = Pick(SpireEnv, file?.Spire);
            if (spireKey != null)
                Spire.License.LicenseProvider.SetLicenseKey(spireKey);
            else
                missing += "Spire ";

            if (missing.Length == 0)
            {
                SimpleLog.Info("Licenses: IronPdf and Spire keys loaded");
                return;
            }

            missing = missing.Trim();
            SimpleLog.Warning($"Licenses: no key for {missing}. Expected {FilePath} or environment variables " +
                $"{IronPdfEnv} / {SpireEnv}. The library runs in evaluation mode and adds a watermark.");
            MessageBox.Show("חסר מפתח רישיון עבור: " + missing + Environment.NewLine +
                "הקבצים יודפסו / ימוזגו עם סימן מים של גרסת ניסיון." + Environment.NewLine +
                "יש להעתיק את הקובץ licenses.json לתיקיית התוכנה:" + Environment.NewLine + AppContext.BaseDirectory,
                "רישיון חסר", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string Pick(string envName, string fromFile)
        {
            var fromEnv = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv.Trim();
            return string.IsNullOrWhiteSpace(fromFile) ? null : fromFile.Trim();
        }

        private static LicenseFile ReadFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;
                return JsonSerializer.Deserialize<LicenseFile>(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                SimpleLog.Warning("Licenses: could not read " + FilePath + ": " + ex.Message);
                return null;
            }
        }
    }
}
