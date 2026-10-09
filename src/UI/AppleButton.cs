using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SmbUserSwitch.UI
{
    public enum AppleButtonKind
    {
        /// <summary>填充蓝色实心按钮，用于主操作。</summary>
        Primary,
        /// <summary>浅灰填充按钮，用于次级操作。</summary>
        Secondary,
        /// <summary>红色填充按钮，用于破坏性操作。</summary>
        Danger,
        /// <summary>无底色，仅文字，用于最轻量的操作。</summary>
        Plain
    }

    /// <summary>
    /// Apple 风格按钮。
    ///
    /// 动画：鼠标按下时底色/缩放补间到"按下态"，松开后弹回；
    /// 悬停时底色也做一次补间。缩放用变换矩阵实现，不改变控件尺寸，
    /// 因此不会触发父容器重新布局（避免抖动）。
    /// </summary>
    public class AppleButton : Control
    {
        AppleButtonKind _kind = AppleButtonKind.Secondary;
        bool _pressed;
        Animator _colorAnim;
        Animator _scaleAnim;
        double _colorT;    // 0 = 常态, 1 = 悬停态
        double _pressT;    // 0 = 常态, 1 = 按下态

        int _radius = Theme.RadiusButton;

        public AppleButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Body(9f);
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(90, 30);

            _colorAnim = new Animator(delegate(double v) { _colorT = v; Invalidate(); });
            _scaleAnim = new Animator(delegate(double v) { _pressT = v; Invalidate(); });
        }

        public AppleButtonKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        /// <summary>
        /// 用基类的 Enabled 作为唯一状态源。
        /// 早先这里用 `new bool Enabled` 遮蔽基类属性，结果是两套状态：
        /// WinForms 内部（以及任何持有 Control 引用的代码）访问的是基类那份，
        /// 两者一旦不同步，控件会看起来可用却点不动，或反之。
        /// </summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _pressed = false;
                _colorT = 0;
                _pressT = 0;
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Enabled) return;
            _colorAnim.To(_colorT, 1.0, 140);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!Enabled) return;
            _colorAnim.To(_colorT, 0.0, 160);
            if (_pressed) { _pressed = false; _scaleAnim.To(_pressT, 0.0, 120); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            _pressed = true;
            _scaleAnim.To(_pressT, 1.0, 90);
            Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            bool wasPressed = _pressed;
            _pressed = false;
            _scaleAnim.To(_pressT, 0.0, 170);
            // 只有在按钮内部松开才算点击（与系统按钮行为一致）
            if (wasPressed && ClientRectangle.Contains(e.Location))
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = true;
                _scaleAnim.To(_pressT, 1.0, 90);
                e.Handled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                bool wasPressed = _pressed;
                _pressed = false;
                _scaleAnim.To(_pressT, 0.0, 170);
                if (wasPressed) OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // ---- 配色 ----

        Color BaseFill()
        {
            switch (_kind)
            {
                case AppleButtonKind.Primary: return Theme.Accent;
                case AppleButtonKind.Danger: return Theme.Danger;
                case AppleButtonKind.Plain: return Color.Transparent;
                default: return Theme.FillSecondary;
            }
        }

        Color HoverFill()
        {
            switch (_kind)
            {
                case AppleButtonKind.Primary: return Theme.AccentHover;
                case AppleButtonKind.Danger: return Theme.DangerHover;
                case AppleButtonKind.Plain: return Color.FromArgb(20, 0, 0, 0);
                default: return Theme.FillSecondaryHover;
            }
        }

        Color PressFill()
        {
            switch (_kind)
            {
                case AppleButtonKind.Primary: return Theme.AccentPress;
                case AppleButtonKind.Danger: return Color.FromArgb(0xD7, 0x2B, 0x22);
                case AppleButtonKind.Plain: return Color.FromArgb(36, 0, 0, 0);
                default: return Theme.FillSecondaryPress;
            }
        }

        Color TextColor()
        {
            if (!Enabled) return Theme.TextSecondary;
            switch (_kind)
            {
                case AppleButtonKind.Primary:
                case AppleButtonKind.Danger: return Theme.TextOnAccent;
                case AppleButtonKind.Plain: return Theme.Accent;
                default: return Theme.TextPrimary;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 按下时整体轻微缩小，模拟"按进去"的物理反馈
            double scale = 1.0 - 0.035 * _pressT;
            RectangleF boundsF = new RectangleF(0, 0, Width, Height);
            if (scale < 0.999)
            {
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.ScaleTransform((float)scale, (float)scale);
                g.TranslateTransform(-Width / 2f, -Height / 2f);
            }

            Rectangle r = new Rectangle(0, 0, Width, Height);
            r.Inflate(-1, -1);

            Color fill = Theme.Lerp(BaseFill(), HoverFill(), _colorT);
            fill = Theme.Lerp(fill, PressFill(), _pressT * 0.85);

            if (fill.A > 0)
            {
                using (GraphicsPath p = Theme.RoundedRect(r, _radius))
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, p);
                }
            }

            // 键盘焦点环
            if (Focused && Enabled)
            {
                using (GraphicsPath p = Theme.RoundedRect(r, _radius))
                using (Pen pen = new Pen(Color.FromArgb(90, Theme.Accent), 2f))
                {
                    g.DrawPath(pen, p);
                }
            }

            string text = Text ?? "";
            if (text.Length > 0)
            {
                using (SolidBrush tb = new SolidBrush(TextColor()))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(text, Font, tb, new RectangleF(4, 0, Width - 8, Height), sf);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_colorAnim != null) _colorAnim.Dispose();
                if (_scaleAnim != null) _scaleAnim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
