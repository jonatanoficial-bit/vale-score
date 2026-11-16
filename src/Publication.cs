using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ValeScore
{
    public sealed class Publication
    {
        public static readonly string[] PaperNames = { "A4", "Carta", "A3" };
        private static readonly Size[] PaperSizes = {
            new Size(827, 1169), new Size(850, 1100), new Size(1169, 1654)
        };
        private readonly Score content;
        private readonly ScoreLayout layout;
        private readonly int paper, margin;
        private readonly bool landscape;
        public int PageCount { get { return layout.PageCount; } }
        public int LayoutWidth { get { return layout.Width; } }
        public int LayoutHeight { get { return layout.Height; } }
        public string Title { get; private set; }

        public Publication(Score score, int? part, int paper, bool landscape, int margin)
        {
            this.paper = paper;
            this.landscape = landscape;
            this.margin = margin;
            content = part.HasValue ? score.ExtractPart(part.Value) : Score.Restore(score.Snapshot());
            var page = Paper(paper, landscape);
            int width = page.Width - 2 * margin, height = page.Height - 2 * margin;
            if (margin < 0 || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("margin");
            int rows = content.Parts.Sum(p => p.Staves.Count);
            int minimumHeight = ScoreLayout.FirstStaffTop + (rows - 1) * ScoreLayout.StaffGap + 135;
            float scale = Math.Max(1, Math.Max(625f / width, minimumHeight / (float)height));
            layout = new ScoreLayout(content, (int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
            Title = content.Title + (part.HasValue ? " - " + content.Parts[0].Name : "");
        }

        public static Size Paper(int index, bool landscape)
        {
            var size = PaperSizes[index];
            return landscape ? new Size(size.Height, size.Width) : size;
        }

        public RectangleF Fit(RectangleF available)
        {
            if (available.Width <= 0 || available.Height <= 0)
                throw new InvalidOperationException("As margens não deixam área suficiente para a partitura.");
            float scale = Math.Min(available.Width / layout.Width, available.Height / layout.Height);
            float width = layout.Width * scale, height = layout.Height * scale;
            return new RectangleF(available.X + (available.Width - width) / 2,
                available.Y + (available.Height - height) / 2, width, height);
        }

        public static int Margin(float millimeters)
        {
            if (float.IsNaN(millimeters) || millimeters < 5 || millimeters > 35)
                throw new ArgumentOutOfRangeException("millimeters");
            return (int)Math.Round(millimeters * 100 / 25.4f);
        }

        public void Draw(Graphics graphics, int page, RectangleF available)
        {
            if (page < 0 || page >= PageCount) throw new ArgumentOutOfRangeException("page");
            var bounds = Fit(available);
            var state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / layout.Width, bounds.Height / layout.Height);
                ScorePage.Draw(graphics, content, layout, page, null, -1, null);
            }
            finally { graphics.Restore(state); }
        }

        public void ExportPngPages(Stream stream)
        {
            Size size = Paper(paper, landscape);
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            using (var bitmap = new Bitmap(size.Width * 2, size.Height * 2))
            {
                bitmap.SetResolution(200, 200);
                for (int page = 0; page < PageCount; page++)
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.White);
                        Draw(g, page, new RectangleF(margin * 2, margin * 2, bitmap.Width - margin * 4, bitmap.Height - margin * 4));
                    }
                    // GDI+ needs a seekable stream to encode the image.
                    using (var image = new MemoryStream())
                    {
                        bitmap.Save(image, ImageFormat.Png);
                        image.Position = 0;
                        using (var entry = archive.CreateEntry("pagina-" + (page + 1).ToString("00") + ".png").Open()) image.CopyTo(entry);
                    }
                }
            }
        }

        public void Configure(PrintDocument document)
        {
            var size = Paper(paper, false);
            var desired = new[] { PaperKind.A4, PaperKind.Letter, PaperKind.A3 }[paper];
            var settings = document.DefaultPageSettings;
            settings.PaperSize = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.Kind == desired)
                ?? new PaperSize(PaperNames[paper], size.Width, size.Height);
            settings.Landscape = landscape;
            settings.Margins = new Margins(margin, margin, margin, margin);
        }
    }
}
