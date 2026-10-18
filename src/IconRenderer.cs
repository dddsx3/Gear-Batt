using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GearBatt
{
    internal static class IconRenderer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly string[][] Glyphs = new string[][]
        {
            new string[] { "111", "101", "101", "101", "111" }, // 0
            new string[] { "010", "110", "010", "010", "111" }, // 1
            new string[] { "111", "001", "111", "100", "111" }, // 2
            new string[] { "111", "001", "111", "001", "111" }, // 3
            new string[] { "101", "101", "111", "001", "001" }, // 4
            new string[] { "111", "100", "111", "001", "111" }, // 5
            new string[] { "111", "100", "111", "101", "111" }, // 6
            new string[] { "111", "001", "001", "010", "010" }, // 7
            new string[] { "111", "101", "111", "101", "111" }, // 8
            new string[] { "111", "101", "111", "001", "111" }  // 9
        };

        private static readonly string[] UnknownGlyph = new string[]
        {
            "111", "001", "011", "000", "010"
        };

        public static Icon CreateIcon(int size, int percent, bool charging, bool unknown)
        {
            using (Bitmap bitmap = RenderBitmap(size, percent, charging, unknown))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        public static Bitmap RenderBitmap(int size, int percent, bool charging, bool unknown)
        {
            if (size < 16)
                size = 16;
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.None;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

                int bodyWidth = (int)Math.Round(11.0 * size / 16.0);
                int bodyHeight = (int)Math.Round(8.0 * size / 16.0);
                int bodyY = (int)Math.Round(4.0 * size / 16.0);
                int nubWidth = Math.Max(2, (int)Math.Round(2.0 * size / 16.0));
                int nubHeight = Math.Max(2, (int)Math.Round(4.0 * size / 16.0));
                int outline = size >= 32 ? 2 : 1;
                int totalWidth = bodyWidth + nubWidth;
                int bodyX = Math.Max(1, (size - totalWidth) / 2);
                int nubX = bodyX + bodyWidth;
                int nubY = bodyY + (bodyHeight - nubHeight) / 2;

                Color outlineColor = unknown
                    ? Color.FromArgb(255, 142, 142, 147)
                    : Color.FromArgb(255, 244, 244, 244);
                Color bodyColor = Color.FromArgb(255, 28, 28, 30);
                Color terminalColor = unknown
                    ? Color.FromArgb(255, 110, 110, 115)
                    : Color.FromArgb(255, 230, 230, 232);

                using (SolidBrush bodyBrush = new SolidBrush(bodyColor))
                    graphics.FillRectangle(bodyBrush, bodyX, bodyY, bodyWidth, bodyHeight);

                int interiorX = bodyX + outline;
                int interiorY = bodyY + outline;
                int interiorWidth = Math.Max(0, bodyWidth - outline * 2);
                int interiorHeight = Math.Max(0, bodyHeight - outline * 2);

                if (!unknown && interiorWidth > 0 && interiorHeight > 0)
                {
                    int clamped = Math.Max(0, Math.Min(100, percent));
                    int fillWidth = (int)Math.Round(interiorWidth * clamped / 100.0);
                    if (clamped > 0 && fillWidth < 1)
                        fillWidth = 1;
                    if (clamped < 100 && fillWidth >= interiorWidth)
                        fillWidth = interiorWidth - 1;
                    if (fillWidth > 0)
                    {
                        Color fill = FillColor(clamped, charging);
                        using (SolidBrush fillBrush = new SolidBrush(fill))
                            graphics.FillRectangle(
                                fillBrush, interiorX, interiorY, fillWidth, interiorHeight);

                        if (interiorHeight >= 3)
                        {
                            Color highlight = Color.FromArgb(70, 255, 255, 255);
                            using (SolidBrush highlightBrush = new SolidBrush(highlight))
                                graphics.FillRectangle(
                                    highlightBrush, interiorX, interiorY, fillWidth, 1);
                        }
                    }
                }

                using (Pen outlinePen = new Pen(outlineColor, outline))
                {
                    outlinePen.Alignment = PenAlignment.Inset;
                    graphics.DrawRectangle(
                        outlinePen, bodyX, bodyY, bodyWidth - 1, bodyHeight - 1);
                }

                using (SolidBrush terminalBrush = new SolidBrush(terminalColor))
                    graphics.FillRectangle(terminalBrush, nubX, nubY, nubWidth, nubHeight);

                if (unknown)
                {
                    DrawCenteredGlyph(
                        graphics, UnknownGlyph, interiorX, interiorY,
                        interiorWidth, interiorHeight, 1,
                        Color.FromArgb(255, 220, 220, 222));
                }
                else if (charging)
                {
                    DrawBolt(graphics, bodyX, bodyY, bodyWidth, bodyHeight, size);
                }
                else
                {
                    int clamped = Math.Max(0, Math.Min(100, percent));
                    if (clamped < 100)
                        DrawPercent(
                            graphics, clamped, interiorX, interiorY,
                            interiorWidth, interiorHeight, size);
                }
            }
            return bitmap;
        }

        private static Color FillColor(int percent, bool charging)
        {
            if (charging)
                return Color.FromArgb(255, 48, 209, 88);
            if (percent <= 15)
                return Color.FromArgb(255, 255, 69, 58);
            if (percent <= 30)
                return Color.FromArgb(255, 255, 159, 10);
            if (percent <= 55)
                return Color.FromArgb(255, 255, 214, 10);
            return Color.FromArgb(255, 48, 209, 88);
        }

        private static void DrawPercent(
            Graphics graphics, int percent, int interiorX, int interiorY,
            int interiorWidth, int interiorHeight, int size)
        {
            string text = percent.ToString();
            int glyphScale = size >= 24 ? 2 : 1;
            int textWidth = text.Length * 3 * glyphScale +
                (text.Length - 1) * glyphScale;
            int shadow = 1;

            if (textWidth + shadow > interiorWidth)
            {
                glyphScale = 1;
                textWidth = text.Length * 3 * glyphScale +
                    (text.Length - 1) * glyphScale;
            }
            if (textWidth + shadow > interiorWidth ||
                5 * glyphScale > interiorHeight)
                return;

            int x = interiorX + (interiorWidth - textWidth) / 2;
            int y = interiorY + (interiorHeight - 5 * glyphScale) / 2;
            Color shadowColor = Color.FromArgb(220, 10, 10, 12);
            Color textColor = Color.FromArgb(255, 255, 255, 255);

            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit > 9)
                    continue;
                int digitX = x + i * (3 * glyphScale + glyphScale);
                DrawGlyph(graphics, Glyphs[digit], digitX + shadow,
                    y, glyphScale, shadowColor);
                DrawGlyph(graphics, Glyphs[digit], digitX,
                    y, glyphScale, textColor);
            }
        }

        private static void DrawCenteredGlyph(
            Graphics graphics, string[] glyph, int interiorX, int interiorY,
            int interiorWidth, int interiorHeight, int scale, Color color)
        {
            int width = 3 * scale;
            int height = 5 * scale;
            int x = interiorX + (interiorWidth - width) / 2;
            int y = interiorY + (interiorHeight - height) / 2;
            DrawGlyph(graphics, glyph, x + 1, y + 1, scale,
                Color.FromArgb(220, 10, 10, 12));
            DrawGlyph(graphics, glyph, x, y, scale, color);
        }

        private static void DrawGlyph(
            Graphics graphics, string[] glyph, int x, int y,
            int scale, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                for (int row = 0; row < glyph.Length; row++)
                {
                    string line = glyph[row];
                    for (int col = 0; col < line.Length; col++)
                    {
                        if (line[col] == '1')
                            graphics.FillRectangle(
                                brush, x + col * scale, y + row * scale,
                                scale, scale);
                    }
                }
            }
        }

        private static void DrawBolt(
            Graphics graphics, int bodyX, int bodyY,
            int bodyWidth, int bodyHeight, int size)
        {
            if (size <= 16)
            {
                DrawPixelBolt(graphics, bodyX, bodyY, bodyWidth);
                return;
            }

            float scale = size / 16.0f;
            float centerX = bodyX + bodyWidth / 2.0f;
            float centerY = bodyY + bodyHeight / 2.0f;

            PointF[] points = new PointF[]
            {
                new PointF(centerX + 0.9f * scale, centerY - 4.2f * scale),
                new PointF(centerX - 3.0f * scale, centerY + 1.2f * scale),
                new PointF(centerX - 0.7f * scale, centerY + 1.2f * scale),
                new PointF(centerX - 1.9f * scale, centerY + 4.4f * scale),
                new PointF(centerX + 3.0f * scale, centerY - 1.0f * scale),
                new PointF(centerX + 0.6f * scale, centerY - 1.0f * scale)
            };

            GraphicsState state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(points);
                PointF[] shadow = new PointF[points.Length];
                for (int i = 0; i < points.Length; i++)
                    shadow[i] = new PointF(points[i].X + 1.0f, points[i].Y + 1.0f);
                using (GraphicsPath shadowPath = new GraphicsPath())
                {
                    shadowPath.AddPolygon(shadow);
                    using (SolidBrush shadowBrush = new SolidBrush(
                               Color.FromArgb(210, 10, 10, 12)))
                        graphics.FillPath(shadowBrush, shadowPath);
                }
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 255, 214, 10)))
                    graphics.FillPath(brush, path);
                float penWidth = Math.Max(0.9f, size / 20.0f);
                using (Pen pen = new Pen(Color.FromArgb(255, 20, 20, 22), penWidth))
                {
                    pen.LineJoin = LineJoin.Miter;
                    graphics.DrawPath(pen, path);
                }
            }
            graphics.Restore(state);
        }

        private static void DrawPixelBolt(
            Graphics graphics, int bodyX, int bodyY, int bodyWidth)
        {
            string[] bolt = new string[]
            {
                "...##",
                "..##.",
                ".##..",
                "####.",
                "..##.",
                ".##..",
                "##..."
            };
            int x = bodyX + bodyWidth / 2 - 2;
            int y = bodyY;
            using (SolidBrush shadowBrush = new SolidBrush(
                       Color.FromArgb(220, 10, 10, 12)))
            using (SolidBrush boltBrush = new SolidBrush(
                       Color.FromArgb(255, 255, 214, 10)))
            {
                for (int row = 0; row < bolt.Length; row++)
                {
                    for (int col = 0; col < bolt[row].Length; col++)
                    {
                        if (bolt[row][col] != '#')
                            continue;
                        graphics.FillRectangle(shadowBrush, x + col + 1, y + row + 1, 1, 1);
                    }
                }
                for (int row = 0; row < bolt.Length; row++)
                {
                    for (int col = 0; col < bolt[row].Length; col++)
                    {
                        if (bolt[row][col] != '#')
                            continue;
                        graphics.FillRectangle(boltBrush, x + col, y + row, 1, 1);
                    }
                }
            }
        }
    }
}
