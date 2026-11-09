using System.Drawing.Printing;
using DIMPIK.Models;

namespace DIMPIK.Services;

public static class DocumentPrintService
{
    public static void Print(IWin32Window owner, CaptureDocument document)
    {
        using var image = document.Compose();
        using var printDocument = new PrintDocument { DocumentName = document.Name };
        printDocument.DefaultPageSettings.Landscape = image.Width > image.Height;
        printDocument.PrintPage += (_, e) =>
        {
            if (e.Graphics == null) return;
            var bounds = e.MarginBounds;
            float scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            var destination = new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image, destination);
            e.HasMorePages = false;
        };
        using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true };
        if (dialog.ShowDialog(owner) == DialogResult.OK) printDocument.Print();
    }
}
