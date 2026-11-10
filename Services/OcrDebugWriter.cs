using System.IO;
using System.Text;
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Dtos;

namespace DrugInspectionArchive.Services;

// يكتب ملف نصي بنتيجة OCR الخام وموقع الجدول لتشخيص أخطاء القراءة
public static class OcrDebugWriter
{
    public static string? Write(
        OcrDocumentResult document,
        InspectionBookDraft? draft,
        string fileName)
    {
        try
        {
            AppPaths.EnsureDirectories();

            string safe = string.Concat(
                Path.GetFileNameWithoutExtension(fileName)
                    .Select(c => char.IsLetterOrDigit(c) ? c : '_'));

            string path = Path.Combine(
                AppPaths.LogsDirectory,
                $"ocr_debug_{safe}.txt");

            var sb = new StringBuilder();

            foreach (var page in document.Pages)
            {
                sb.AppendLine(
                    $"=== PAGE {page.PageNumber} " +
                    $"size {page.ImageWidth}x{page.ImageHeight} " +
                    $"words {page.Words.Count} " +
                    $"productWords {page.ProductWords.Count}");

                var region = TableExtractor.Locate(page);

                int top = 0;
                int bottom = page.ImageHeight;

                if (region is null)
                {
                    sb.AppendLine("TABLE: not found");
                }
                else
                {
                    sb.AppendLine(
                        $"TABLE: left={region.TableLeft} " +
                        $"batchLeft={region.BatchLeft} " +
                        $"mfgLeft={region.MfgLeft} " +
                        $"manuLeft={region.ManufacturerLeft} " +
                        $"top={region.Top} bottom={region.Bottom}");

                    sb.AppendLine("BATCHES: " + string.Join(" , ",
                        region.Batches.Select(b =>
                            $"{b.Text}@{b.CenterX},{b.CenterY}")));

                    top = Math.Max(0, region.Top - 150);
                    bottom = region.Bottom + 150;
                }

                sb.AppendLine("--- PAGE WORDS IN TABLE BAND (y x1 x2 conf text)");

                foreach (var w in page.Words
                    .Where(w => w.CenterY >= top && w.CenterY <= bottom)
                    .OrderBy(w => w.CenterY)
                    .ThenBy(w => w.X1))
                {
                    sb.AppendLine(
                        $"{w.CenterY}\t{w.X1}\t{w.X2}\t{w.Confidence:0}\t{w.Text}");
                }

                sb.AppendLine("--- PRODUCT COLUMN WORDS (y x1 x2 conf text)");

                foreach (var w in page.ProductWords
                    .OrderBy(w => w.CenterY)
                    .ThenBy(w => w.X1))
                {
                    sb.AppendLine(
                        $"{w.CenterY}\t{w.X1}\t{w.X2}\t{w.Confidence:0}\t{w.Text}");
                }
            }

            if (draft is not null)
            {
                sb.AppendLine("=== EXTRACTED");
                sb.AppendLine($"COMPANY: {draft.CompanyName.Value}");

                foreach (var m in draft.Materials)
                {
                    sb.AppendLine(
                        $"{m.MaterialName}\t{m.BatchNumber}\tp{m.PageNumber}");
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            return path;
        }
        catch
        {
            return null;
        }
    }
}