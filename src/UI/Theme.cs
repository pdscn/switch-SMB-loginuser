using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SmbUserSwitch.UI
{
    /// <summary>
    /// Apple 风格设计令牌：颜色、字体、圆角、动画曲线。
    /// 所有视觉常量集中在这里，改风格只动这一个文件。
    /// </summary>
    public static class Theme
    {
        // ---- 系统色（取自 iOS/macOS 系统调色板）----
        public static readonly Color Accent = Color.FromArgb(0x00, 0x7A, 0xFF);  // systemBlue
        public static readonly Color AccentHover = Color.FromArgb(0x0A, 0x84, 0xFF);
        public static readonly Color AccentPress = Color.FromArgb(0x00, 0x62, 0xCC);
        public static readonly Color Danger = Color.FromArgb(0xFF, 0x3B, 0x30);  // systemRed
        public static readonly Color DangerHover = Color.FromArgb(0xFF, 0x50, 0x46);
        public static readonly Color Success = Color.FromArgb(0x34, 0xC7, 0x59); // systemGreen
        public static readonly Color Warning = Color.FromArgb(0xFF, 0x95, 0x00); // systemOrange

        // ---- 背景与容器 ----
        public static readonly Color WindowBg = Color.FromArgb(0xF2, 0xF2, 0xF7); // systemGroupedBackground
        public static readonly Color CardBg = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color CardBorder = Color.FromArgb(0xE5, 0xE5, 0xEA);
        public static readonly Color FillSecondary = Color.FromArgb(0xE9, 0xE9, 0xEF); // 次级按钮底色
        public static readonly Color FillSecondaryHover = Color.FromArgb(0xE0, 0xE0, 0xE6);
        public static readonly Color FillSecondaryPress = Color.FromArgb(0xD4, 0xD4, 0xDB);
        public static readonly Color RowHover = Color.FromArgb(0xF5, 0xF5, 0xF8);
        public static readonly Color RowSelected = Color.FromArgb(0xE8, 0xF1, 0xFF);

        // ---- 文字 ----
        public static readonly Color TextPrimary = Color.FromArgb(0x1C, 0x1C, 0x1E);
        public static readonly Color TextSecondary = Color.FromArgb(0x8E, 0x8E, 0x93);
        public static readonly Color TextOnAccent = Color.White;
        public static readonly Color Separator = Color.FromArgb(0xE8, 0xE8, 0xED);

        // ---- 几何 ----
        public const int RadiusCard = 12;
        public const int RadiusButton = 8;
        public const int RadiusField = 8;

        // ---- 字体 ----
        const string UiFamily = "Microsoft YaHei UI";
        const string FallbackFamily = "Segoe UI";

        public static Font Body(float size)
        {
            return new Font(UiFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font BodyBold(float size)
        {
            return new Font(UiFamily, size, FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Font Title(float size)
        {
            return new Font(UiFamily, size, FontStyle.Bold, GraphicsUnit.Point);
        }

        public static Font Small(float size)
        {
            return new Font(UiFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        // ---- 绘图辅助 ----

        /// <summary>圆角矩形路径。radius 为 0 时退化为直角矩形。</summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// 画柔和投影。GDI+ 没有真正的模糊，用若干层递减 alpha 的圆角矩形近似，
        /// 效果足够柔和且比 GaussianBlur 快得多。
        /// </summary>
        public static void DrawShadow(Graphics g, Rectangle bounds, int radius, int spread, int alpha)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                for (int i = spread; i >= 1; i--)
                {
                    Rectangle r = new Rectangle(
                        bounds.X - i, bounds.Y - i + 1,
                        bounds.Width + i * 2, bounds.Height + i * 2);
                    int a = alpha * (spread - i + 1) / (spread * (spread + 1) / 2 + spread);
                    if (a < 1) a = 1;
                    using (GraphicsPath p = RoundedRect(r, radius + i))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 0, 0, 0)))
                    {
                        g.FillPath(b, p);
                    }
                }
            }
            finally
            {
                g.SmoothingMode = old;
            }
        }

        /// <summary>把颜色按比例向目标色插值，t 取 0..1。</summary>
        public static Color Lerp(Color from, Color to, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            int a = (int)Math.Round(from.A + (to.A - from.A) * t);
            int r = (int)Math.Round(from.R + (to.R - from.R) * t);
            int g = (int)Math.Round(from.G + (to.G - from.G) * t);
            int b = (int)Math.Round(from.B + (to.B - from.B) * t);
            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>按控件当前 DPI 缩放一个逻辑像素值。</summary>
        public static int Scale(Control c, int value)
        {
            if (c == null) return value;
            try
            {
                int dpi = c.DeviceDpi;
                if (dpi <= 0) dpi = 96;
                return (int)Math.Round(value * dpi / 96.0);
            }
            catch (Exception)
            {
                return value;
            }
        }
    }

    /// <summary>
    /// 轻量补间动画。基于 WinForms Timer（约 60fps），
    /// 到达目标后自动停表，不空转占 CPU。
    /// </summary>
    public class Animator : IDisposable
    {
        readonly Timer _timer;
        readonly Action<double> _onFrame;
        Action _onDone;
        long _startTick;
        int _duration;
        double _from, _to;
        bool _running;

        public Animator(Action<double> onFrame)
        {
            _onFrame = onFrame;
            _timer = new Timer();
            _timer.Interval = 16;   // ~60fps
            _timer.Tick += OnTick;
        }

        /// <summary>从当前值补间到 to。duration 单位毫秒。</summary>
        public void To(double from, double to, int duration)
        {
            To(from, to, duration, null);
        }

        public void To(double from, double to, int duration, Action onDone)
        {
            _from = from;
            _to = to;
            _duration = duration < 1 ? 1 : duration;
            _onDone = onDone;
            _startTick = Environment.TickCount;
            if (!_running)
            {
                _running = true;
                _timer.Start();
            }
            OnTick(null, EventArgs.Empty);   // 立刻渲染第一帧，避免启动延迟
        }

        /// <summary>停止动画，停在当前值。</summary>
        public void Stop()
        {
            _running = false;
            _timer.Stop();
            _onDone = null;
        }

        /// <summary>动画进行中？</summary>
        public bool IsRunning { get { return _running; } }

        void OnTick(object sender, EventArgs e)
        {
            double elapsed = Environment.TickCount - _startTick;
            double t = elapsed / (double)_duration;
            if (t >= 1.0)
            {
                _running = false;
                _timer.Stop();
                double final = _to;
                Action done = _onDone;
                _onDone = null;
                _onFrame(final);
                if (done != null) done();
                return;
            }
            _onFrame(_from + (_to - _from) * EaseOut(t));
        }

        /// <summary>easeOutCubic —— 起步快、收尾缓，Apple 动效的典型手感。</summary>
        public static double EaseOut(double t)
        {
            double inv = 1.0 - t;
            return 1.0 - inv * inv * inv;
        }

        /// <summary>easeInOutCubic —— 两端缓、中间快，用于位移类动画。</summary>
        public static double EaseInOut(double t)
        {
            if (t < 0.5) return 4 * t * t * t;
            double f = 2 * t - 2;
            return 0.5 * f * f * f + 1;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer.Dispose();
            }
        }
    }
}
