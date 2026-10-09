using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SmbUserSwitch.Core;

namespace SmbUserSwitch.UI
{
    /// <summary>
    /// Apple 风格账号列表。自绘而非用 ListView ——
    /// ListView 没法做圆角行、悬停渐变、选中滑入动画。
    /// 行高固定，用虚拟滚动只画可见行，账号多了也不卡。
    /// </summary>
    public class AccountList : Control
    {
        public class Row
        {
            public AccountProfile Account;
            public string Name;
            public string Login;
            public string Drive;
            public string Password;
            public string Note;
            public bool IsCurrent;
            public string Server;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly VScrollBar _scroll;
        int _hoverIndex = -1;
        int _selectedIndex = -1;
        int _headerHeight = 34;
        int _rowHeight = 52;

        Animator _hoverAnim;
        Animator _selAnim;
        double _hoverT;
        double _selT;
        // 记录"哪一行正在做补间"。只有这一行取 _hoverT/_selT，
        // 其余行直接渲染终态 —— 否则整张列表都会跟着一起淡入。
        int _hoverAnimIndex = -1;
        int _selAnimIndex = -1;

        public event EventHandler SelectionChanged;
        public event EventHandler ItemActivated;

        public AccountList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBg;
            Font = Theme.Body(9f);

            _scroll = new VScrollBar();
            _scroll.Dock = DockStyle.Right;
            _scroll.Width = 10;
            _scroll.Visible = false;
            _scroll.ValueChanged += delegate(object s, EventArgs e) { Invalidate(); };
            Controls.Add(_scroll);

            _hoverAnim = new Animator(delegate(double v) { _hoverT = v; Invalidate(); });
            _selAnim = new Animator(delegate(double v) { _selT = v; Invalidate(); });

            MouseMove += OnMouseMoveHandler;
            MouseLeave += delegate(object s, EventArgs e) { SetHover(-1); };
            MouseDown += OnMouseDownHandler;
            MouseDoubleClick += delegate(object s, MouseEventArgs e)
            {
                int i = IndexAt(e.Y);
                if (i >= 0 && ItemActivated != null) ItemActivated(this, EventArgs.Empty);
            };
            MouseWheel += delegate(object s, MouseEventArgs e) { ScrollBy(-e.Delta / 120 * 3); };
        }

        public int RowHeight
        {
            get { return _rowHeight; }
            set { _rowHeight = Math.Max(28, value); Invalidate(); }
        }

        public void SetRows(List<Row> rows)
        {
            _rows.Clear();
            if (rows != null) _rows.AddRange(rows);

            // 列表内容变了，正在动画的行号可能已失效，重置以免动画落在错误的行上
            _hoverAnimIndex = -1;
            _selAnimIndex = -1;
            _hoverIndex = -1;
            _hoverAnim.Stop();
            _selAnim.Stop();

            if (_selectedIndex >= _rows.Count) _selectedIndex = _rows.Count - 1;
            UpdateScroll();
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public Row Selected
        {
            get { return (_selectedIndex >= 0 && _selectedIndex < _rows.Count) ? _rows[_selectedIndex] : null; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int v = (value >= -1 && value < _rows.Count) ? value : -1;
                if (_selectedIndex == v) return;
                _selectedIndex = v;
                _selAnimIndex = v;
                _selT = 0;
                _selAnim.To(0, 1, 180);
                UpdateScroll();
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        void UpdateScroll()
        {
            int contentH = _rows.Count * _rowHeight;
            int viewH = ViewHeight;

            // LargeChange 必须在 Maximum 之前设 —— 否则第 124 行读到的是上一次的旧值。
            // WinForms 的滚动条约定是 Maximum 含端点的上界，
            // 故 Maximum = 内容高 - 视口高 + LargeChange - 1。
            int large = Math.Max(1, viewH / _rowHeight);
            _scroll.LargeChange = large;
            _scroll.SmallChange = 1;

            if (contentH > viewH)
            {
                _scroll.Visible = true;
                _scroll.Minimum = 0;
                _scroll.Maximum = contentH - viewH + large - 1;
            }
            else
            {
                _scroll.Visible = false;
                _scroll.Value = 0;
            }
        }

        void ScrollBy(int rows)
        {
            if (!_scroll.Visible) return;
            int v = _scroll.Value + rows * _rowHeight;
            if (v < _scroll.Minimum) v = _scroll.Minimum;
            int max = _scroll.Maximum - _scroll.LargeChange + 1;
            if (v > max) v = max;
            _scroll.Value = v;
            Invalidate();
        }

        int ViewHeight
        {
            get { return Math.Max(0, Height - _headerHeight); }
        }

        int ScrollOffset
        {
            get { return _scroll.Visible ? _scroll.Value : 0; }
        }

        int IndexAt(int y)
        {
            if (y < _headerHeight) return -1;
            int idx = (y - _headerHeight + ScrollOffset) / _rowHeight;
            return (idx >= 0 && idx < _rows.Count) ? idx : -1;
        }

        void SetHover(int index)
        {
            if (_hoverIndex == index) return;
            _hoverIndex = index;
            _hoverAnimIndex = index;
            _hoverT = 0;
            _hoverAnim.To(0, 1, 130);
        }

        void OnMouseMoveHandler(object sender, MouseEventArgs e)
        {
            SetHover(IndexAt(e.Y));
        }

        void OnMouseDownHandler(object sender, MouseEventArgs e)
        {
            int i = IndexAt(e.Y);
            if (i >= 0) SelectedIndex = i;
        }

        // 列宽（相对可用宽度的比例）
        static readonly double[] ColW = new double[] { 0.20, 0.26, 0.11, 0.13, 0.30 };
        static readonly string[] ColT = new string[] { "账号", "登录名", "盘符", "密码", "备注" };

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int availW = Width - (_scroll.Visible ? _scroll.Width : 0);

            // ---- 表头 ----
            Rectangle headerRect = new Rectangle(0, 0, availW, _headerHeight);
            using (SolidBrush b = new SolidBrush(Theme.CardBg))
                g.FillRectangle(b, headerRect);

            int[] colX = new int[ColW.Length + 1];
            colX[0] = 14;
            int usable = availW - 14 - 14;
            for (int i = 0; i < ColW.Length; i++)
                colX[i + 1] = colX[i] + (int)(usable * ColW[i]);

            using (Font hf = Theme.Body(8.5f))
            using (SolidBrush hb = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                for (int i = 0; i < ColT.Length; i++)
                {
                    Rectangle r = new Rectangle(colX[i], 0, colX[i + 1] - colX[i], _headerHeight);
                    g.DrawString(ColT[i], hf, hb, r, sf);
                }
            }
            using (Pen p = new Pen(Theme.Separator, 1f))
                g.DrawLine(p, 0, _headerHeight - 1, availW, _headerHeight - 1);

            // ---- 行 ----
            int offset = ScrollOffset;
            int first = offset / _rowHeight;
            int last = Math.Min(_rows.Count - 1, (offset + ViewHeight) / _rowHeight + 1);

            if (_rows.Count == 0)
            {
                using (Font ef = Theme.Body(9.5f))
                using (SolidBrush eb = new SolidBrush(Theme.TextSecondary))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("还没有账号，点下方「新增」添加一个", ef, eb,
                        new RectangleF(0, _headerHeight, availW, ViewHeight), sf);
                }
                return;
            }

            for (int i = first; i <= last && i < _rows.Count; i++)
            {
                int y = _headerHeight + i * _rowHeight - offset;
                Rectangle rowRect = new Rectangle(4, y + 2, availW - 8, _rowHeight - 4);
                DrawRow(g, i, rowRect, colX, availW);
            }
        }

        void DrawRow(Graphics g, int index, Rectangle r, int[] colX, int availW)
        {
            Row row = _rows[index];

            bool selected = index == _selectedIndex;
            bool hovered = index == _hoverIndex;

            // 选中/悬停底色。选中优先级更高，二者互斥显示。
            // 进度值按行记录：只有正在动画的那一行取补间值，其余行直接给终态。
            if (selected)
            {
                double t = (index == _selAnimIndex) ? _selT : 1.0;
                int alpha = (int)(255 * t);
                Color c = Theme.RowSelected;
                using (GraphicsPath p = Theme.RoundedRect(r, 8))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, c)))
                    g.FillPath(b, p);
            }
            else if (hovered)
            {
                double t = (index == _hoverAnimIndex) ? _hoverT : 1.0;
                // 悬停时从中心轻微放大，模拟"浮起"
                double grow = 0.012 * t;
                Rectangle hr = r;
                if (grow > 0.0001)
                {
                    int dw = (int)(r.Width * grow);
                    int dh = (int)(r.Height * grow);
                    hr = new Rectangle(r.X - dw / 2, r.Y - dh / 2, r.Width + dw, r.Height + dh);
                }
                int alpha = (int)(255 * t);
                using (GraphicsPath p = Theme.RoundedRect(hr, 8))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, Theme.RowHover)))
                    g.FillPath(b, p);
            }

            int textTop = r.Y + 8;

            // 当前生效账号左侧加一条蓝色指示条
            if (row.IsCurrent)
            {
                Rectangle bar = new Rectangle(r.X + 2, r.Y + 12, 3, r.Height - 24);
                using (GraphicsPath p = Theme.RoundedRect(bar, 2))
                using (SolidBrush b = new SolidBrush(Theme.Success))
                    g.FillPath(b, p);
            }

            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.EllipsisCharacter;

                // 第一列：账号名（当前生效的加粗）
                using (Font f = row.IsCurrent ? Theme.BodyBold(9.5f) : Theme.Body(9.5f))
                using (SolidBrush b = new SolidBrush(Theme.TextPrimary))
                {
                    Rectangle cr = new Rectangle(colX[0], r.Y, colX[1] - colX[0], r.Height);
                    g.DrawString(row.Name, f, b, cr, sf);
                }

                using (Font f = Theme.Body(9f))
                using (SolidBrush b = new SolidBrush(Theme.TextSecondary))
                {
                    for (int c = 1; c < ColT.Length; c++)
                    {
                        string val = c == 1 ? row.Login
                                   : c == 2 ? row.Drive
                                   : c == 3 ? row.Password
                                   : row.Note;
                        Rectangle cr = new Rectangle(colX[c], r.Y, colX[c + 1] - colX[c], r.Height);
                        g.DrawString(val ?? "", f, b, cr, sf);
                    }
                }
            }

            // 分隔线（最后一行不画）
            if (index < _rows.Count - 1)
            {
                using (Pen p = new Pen(Theme.Separator, 1f))
                    g.DrawLine(p, r.X + 10, r.Bottom + 1, availW - 12, r.Bottom + 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hoverAnim != null) _hoverAnim.Dispose();
                if (_selAnim != null) _selAnim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
