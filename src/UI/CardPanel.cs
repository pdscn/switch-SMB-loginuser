using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmbUserSwitch.UI
{
    /// <summary>EM_SETCUEBANNER 封装，用于输入框的原生占位提示。</summary>
    internal static class NativeCue
    {
        const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>showOnFocus=false 时，获得焦点后隐藏提示。</summary>
        public static void SetCue(TextBox box, string text, bool showOnFocus)
        {
            if (box == null || !box.IsHandleCreated) return;
            try
            {
                SendMessage(box.Handle, EM_SETCUEBANNER,
                    showOnFocus ? new IntPtr(1) : IntPtr.Zero, text ?? "");
            }
            catch (Exception) { }
        }
    }

    /// <summary>白色圆角卡片容器，带柔和投影。</summary>
    public class CardPanel : Panel
    {
        public int CornerRadius = Theme.RadiusCard;
        public bool ShowShadow = true;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WindowBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width, Height);
            r.Inflate(-1, -1);

            if (ShowShadow)
                Theme.DrawShadow(g, new Rectangle(1, 1, Width - 3, Height - 3), CornerRadius, 3, 10);

            using (GraphicsPath p = Theme.RoundedRect(r, CornerRadius))
            using (SolidBrush b = new SolidBrush(Theme.CardBg))
            {
                g.FillPath(b, p);
            }
            using (GraphicsPath p = Theme.RoundedRect(r, CornerRadius))
            using (Pen pen = new Pen(Theme.CardBorder, 1f))
            {
                g.DrawPath(pen, p);
            }
        }
    }

    /// <summary>圆角输入框，带聚焦时的蓝色描边动画。</summary>
    public class AppleTextBox : Control
    {
        readonly TextBox _inner;
        bool _focused;
        bool _hover;
        Animator _focusAnim;
        double _focusT;
        string _placeholder = "";

        public AppleTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBg;

            _inner = new TextBox();
            _inner.BorderStyle = BorderStyle.None;
            _inner.Font = Theme.Body(9.5f);
            _inner.ForeColor = Theme.TextPrimary;
            _inner.BackColor = Theme.CardBg;
            _inner.GotFocus += delegate(object s, EventArgs e) { _focused = true; _focusAnim.To(_focusT, 1.0, 160); Invalidate(); };
            _inner.LostFocus += delegate(object s, EventArgs e) { _focused = false; _focusAnim.To(_focusT, 0.0, 200); Invalidate(); };
            _inner.TextChanged += delegate(object s, EventArgs e) { OnTextChanged(EventArgs.Empty); };
            _inner.KeyDown += delegate(object s, KeyEventArgs e) { OnKeyDown(e); };
            Controls.Add(_inner);

            _focusAnim = new Animator(delegate(double v) { _focusT = v; Invalidate(); });
        }

        public TextBox Inner { get { return _inner; } }

        public override string Text
        {
            get { return _inner.Text; }
            set { _inner.Text = value; }
        }

        /// <summary>
        /// 占位提示。用系统原生的 cue banner（EM_SETCUEBANNER）实现 ——
        /// 早先的版本在外层控件上自绘，但子 TextBox 会盖住它，导致提示根本看不见。
        /// wParam=0 表示获得焦点后隐藏提示。
        /// </summary>
        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                if (_inner.IsHandleCreated)
                {
                    NativeCue.SetCue(_inner, _placeholder, false);
                }
                else
                {
                    _inner.HandleCreated += delegate(object s, EventArgs e)
                    {
                        NativeCue.SetCue(_inner, _placeholder, false);
                    };
                }
            }
        }

        public bool UseSystemPasswordChar
        {
            get { return _inner.UseSystemPasswordChar; }
            set { _inner.UseSystemPasswordChar = value; }
        }

        public new void Focus() { _inner.Focus(); }
        public new bool Focused { get { return _inner.Focused; } }

        public void SelectAll() { _inner.SelectAll(); }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _inner.Focus();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutInner();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInner();
        }

        void LayoutInner()
        {
            if (_inner == null) return;
            int padX = Theme.Scale(this, 10);
            int h = _inner.PreferredHeight;
            _inner.SetBounds(padX, (Height - h) / 2, Math.Max(10, Width - padX * 2), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width, Height);
            r.Inflate(-1, -1);

            Color fill = _hover && !_focused
                ? Theme.Lerp(Theme.FillSecondary, Theme.CardBg, 0.55)
                : Theme.FillSecondary;

            using (GraphicsPath p = Theme.RoundedRect(r, Theme.RadiusField))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
            }

            // 聚焦描边：从灰到蓝的补间
            Color borderColor = Theme.Lerp(Theme.CardBorder, Theme.Accent, _focusT);
            float borderW = 1f + (float)(_focusT * 0.8);
            using (GraphicsPath p = Theme.RoundedRect(r, Theme.RadiusField))
            using (Pen pen = new Pen(borderColor, borderW))
            {
                g.DrawPath(pen, p);
            }

            // 占位文字交给系统 cue banner 处理（见 Placeholder 属性），这里不再自绘
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _focusAnim != null) _focusAnim.Dispose();
            base.Dispose(disposing);
        }
    }
}
