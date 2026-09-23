using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergeAndPrint
{
    /// <summary>
    /// הגדרות לשונית הקונפיגורציה, נשמרות בקובץ JSON מקומי ליד קובץ ההרצה.
    /// </summary>
    public class AppConfig
    {
        private const string FileName = "config.json";

        public string SourcePath { get; set; } = "";
        public string MainPath { get; set; } = "";
        public string WorkingPath { get; set; } = "";
        public string PrintPath { get; set; } = "";
        public string ArchivePath { get; set; } = "";
        public string TimerPeriod { get; set; } = "";
        public string SrcMinPeriod { get; set; } = "";
        public string Printer1 { get; set; } = "";
        public string Printer2 { get; set; } = "";
        public string Printer3 { get; set; } = "";
        public string Printer11 { get; set; } = "";
        public string Printer22 { get; set; } = "";
        public string Printer33 { get; set; } = "";
        public string Printer9 { get; set; } = "";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        private static AppConfig _current;

        /// <summary>
        /// המופע היחיד של ההגדרות. נטען בפעם הראשונה שניגשים אליו.
        /// </summary>
        public static AppConfig Current
        {
            get
            {
                if (_current == null)
                    _current = Load();
                return _current;
            }
        }

        /// <summary>
        /// נתיב קובץ ההגדרות - תמיד בתיקיית התוכנה, ליד קובץ ההרצה.
        /// </summary>
        public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, FileName);

        /// <summary>
        /// טעינה מחדש של ההגדרות מהקובץ, לאחר שינוי ידני של config.json.
        /// </summary>
        public static void Reload()
        {
            _current = Load();
        }

        private static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (cfg != null)
                        return cfg;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
            }

            // אין קובץ JSON - העברה חד פעמית מההגדרות הישנות של user.config
            var migrated = MigrateFromUserSettings();
            migrated.Save();
            return migrated;
        }

        private static AppConfig MigrateFromUserSettings()
        {
            var cfg = new AppConfig();
            try
            {
                var old = Properties.Settings.Default;
                cfg.SourcePath = old.SourcePath ?? "";
                cfg.MainPath = old.MainPath ?? "";
                cfg.WorkingPath = old.WorkingPath ?? "";
                cfg.PrintPath = old.PrintPath ?? "";
                cfg.ArchivePath = old.ArchivePath ?? "";
                cfg.TimerPeriod = old.TimerPeriod ?? "";
                cfg.SrcMinPeriod = old.SrcMinPeriod ?? "";
                cfg.Printer1 = old.Printer1 ?? "";
                cfg.Printer2 = old.Printer2 ?? "";
                cfg.Printer3 = old.Printer3 ?? "";
                cfg.Printer11 = old.Printer11 ?? "";
                cfg.Printer22 = old.Printer22 ?? "";
                cfg.Printer33 = old.Printer33 ?? "";
                cfg.Printer9 = old.Printer9 ?? "";
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
            }
            return cfg;
        }

        /// <summary>
        /// שמירת ההגדרות לקובץ ה-JSON.
        /// </summary>
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
            }
        }
    }
}
