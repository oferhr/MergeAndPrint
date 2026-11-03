using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using IronPdf;
using SimpleLogger;

namespace MergeAndPrint
{
    /// <summary>
    /// Spire and IronPdf are slow the first time they are used after the app starts (measured: Spire ~10 s on .NET 8,
    /// IronPdf ~4-6 s to start its Chrome engine). Doing that work once in the background at start-up means the
    /// first real file does not pay for it. Nothing is printed and the temporary files are deleted.
    /// </summary>
    internal static class Warmup
    {
        private static Task _running = Task.CompletedTask;

        /// <summary>Start warming up in the background. Call after the license keys are applied.</summary>
        public static void Start()
        {
            _running = Task.Run(Run);
        }

        /// <summary>Block until the warm-up has finished, so a run never competes with it for the libraries.</summary>
        public static void WaitIfRunning(Action<string> notify)
        {
            if (_running.IsCompleted)
                return;
            notify?.Invoke("ממתין לסיום טעינת רכיבי ה-PDF...");
            var watch = Stopwatch.StartNew();
            try
            {
                _running.Wait();
            }
            catch
            {
                // Run() logs its own failures; a failed warm-up must never stop printing.
            }
            SimpleLog.Info($"Warm-up: run waited {watch.ElapsedMilliseconds} ms for the warm-up to finish");
        }

        private static void Run()
        {
            var dir = Path.Combine(Path.GetTempPath(), "MergeAndPrint-warmup-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                Time("Spire", () => WarmSpire());
                Time("IronPdf", () => WarmIronPdf(dir));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* temp folder, harmless if left */ }
            }
        }

        private static void Time(string name, Action action)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                action();
                SimpleLog.Info($"Warm-up: {name} ready in {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                SimpleLog.Warning($"Warm-up: {name} failed after {watch.ElapsedMilliseconds} ms (printing is not affected): {ex.Message}");
            }
        }

        /// <summary>Build a small PDF with a system TrueType font, load it back and draw its page.</summary>
        private static void WarmSpire()
        {
            using (var stream = new MemoryStream())
            {
                using (var created = new Spire.Pdf.PdfDocument())
                {
                    var page = created.Pages.Add();
                    var font = new Spire.Pdf.Graphics.PdfTrueTypeFont(new Font("Arial", 12f), true);
                    page.Canvas.DrawString("warm-up שלום", font, Spire.Pdf.Graphics.PdfBrushes.Black, 20, 20);
                    created.SaveToStream(stream);
                }
                stream.Position = 0;
                using (var loaded = new Spire.Pdf.PdfDocument())
                {
                    loaded.LoadFromStream(stream);
                    using (var image = loaded.SaveAsImage(0))
                    {
                    }
                }
            }
        }

        /// <summary>Convert a tiny image to PDF and merge it, the same calls a real run makes.</summary>
        private static void WarmIronPdf(string dir)
        {
            var jpg = Path.Combine(dir, "warmup.jpg");
            using (var bitmap = new Bitmap(20, 20))
            {
                using (var g = Graphics.FromImage(bitmap))
                    g.Clear(Color.White);
                bitmap.Save(jpg, ImageFormat.Jpeg);
            }
            using (var first = ImageToPdfConverter.ImageToPdf(jpg, IronPdf.Imaging.ImageBehavior.FitToPage))
            using (var second = ImageToPdfConverter.ImageToPdf(jpg, IronPdf.Imaging.ImageBehavior.FitToPage))
            using (var merged = PdfDocument.Merge(first, second))
            {
                merged.SaveAs(Path.Combine(dir, "warmup.pdf"));
            }
        }
    }
}
