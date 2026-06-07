using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PowerCapsule.Services
{
    public class TrayService : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _menu;
        private IntPtr _iconHandle = IntPtr.Zero;

        private ToolStripMenuItem _miShow, _miHide, _miSettings, _miToggle,
            _miCancelShutdown, _miCancelWake, _miCancelStandby, _miLang, _miExit;

        public event Action ShowCapsuleRequested;
        public event Action HideCapsuleRequested;
        public event Action OpenSettingsRequested;
        public event Action TogglePreventSleepRequested;
        public event Action CancelShutdownRequested;
        public event Action CancelWakeRequested;
        public event Action CancelStandbyRequested;
        public event Action LanguageRequested;
        public event Action ExitRequested;

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public void Initialize()
        {
            _menu = new ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer { RoundedEdges = false },
                BackColor = ColorTranslator.FromHtml("#22222C"),
                ForeColor = ColorTranslator.FromHtml("#E2E2EC"),
                Font = new Font("Microsoft YaHei UI", 9f),
                ShowImageMargin = false
            };

            _miShow = AddItem(L("Tray.Show"), () => ShowCapsuleRequested?.Invoke());
            _miHide = AddItem(L("Tray.Hide"), () => HideCapsuleRequested?.Invoke());
            AddSeparator();
            _miSettings = AddItem(L("Tray.Settings"), () => OpenSettingsRequested?.Invoke());
            _miToggle = AddItem(L("Tray.ToggleSleep"), () => TogglePreventSleepRequested?.Invoke());
            _miCancelShutdown = AddItem(L("Tray.CancelShutdown"), () => CancelShutdownRequested?.Invoke());
            _miCancelWake = AddItem(L("Tray.CancelWake"), () => CancelWakeRequested?.Invoke());
            _miCancelStandby = AddItem(L("Tray.CancelStandby"), () => CancelStandbyRequested?.Invoke());
            AddSeparator();
            _miLang = AddItem(L("Tray.Language"), () => LanguageRequested?.Invoke());
            AddSeparator();
            _miExit = AddItem(L("Tray.Exit"), () => ExitRequested?.Invoke());

            _notifyIcon = new NotifyIcon
            {
                Text = "PowerCapsule",
                Icon = CreateCapsuleIcon(),
                ContextMenuStrip = _menu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => ShowCapsuleRequested?.Invoke();

            LocalizationManager.LanguageChanged += RefreshTexts;
        }

        private static string L(string key) => LocalizationManager.L(key);

        private void RefreshTexts()
        {
            _miShow.Text = L("Tray.Show");
            _miHide.Text = L("Tray.Hide");
            _miSettings.Text = L("Tray.Settings");
            _miToggle.Text = L("Tray.ToggleSleep");
            _miCancelShutdown.Text = L("Tray.CancelShutdown");
            _miCancelWake.Text = L("Tray.CancelWake");
            _miCancelStandby.Text = L("Tray.CancelStandby");
            _miLang.Text = L("Tray.Language");
            _miExit.Text = L("Tray.Exit");
        }

        private ToolStripMenuItem AddItem(string text, Action onClick)
        {
            var item = new ToolStripMenuItem(text)
            {
                ForeColor = ColorTranslator.FromHtml("#E2E2EC")
            };
            item.Click += (s, e) => onClick();
            _menu.Items.Add(item);
            return item;
        }

        private void AddSeparator()
        {
            _menu.Items.Add(new ToolStripSeparator());
        }

        // 托盘图标：优先使用内嵌的 app-logo.png，失败则回退到生成图标
        private Icon CreateCapsuleIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/app-logo.png");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info != null)
                {
                    using (var src = new Bitmap(info.Stream))
                    using (var resized = new Bitmap(src, new Size(32, 32)))
                    {
                        _iconHandle = resized.GetHicon();
                        return Icon.FromHandle(_iconHandle);
                    }
                }
            }
            catch { /* 资源不可用则回退 */ }

            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var bg = new SolidBrush(ColorTranslator.FromHtml("#2A2A38")))
                    g.FillEllipse(bg, 1, 1, 30, 30);
                using (var ring = new Pen(ColorTranslator.FromHtml("#D4A040"), 2.6f))
                    g.DrawEllipse(ring, 3, 3, 26, 26);
                using (var dot = new SolidBrush(ColorTranslator.FromHtml("#D4A040")))
                    g.FillEllipse(dot, 11, 11, 10, 10);
            }

            _iconHandle = bmp.GetHicon();
            var icon = Icon.FromHandle(_iconHandle);
            bmp.Dispose();
            return icon;
        }

        public void SetIconVisible(bool visible)
        {
            if (_notifyIcon != null)
                _notifyIcon.Visible = visible;
        }

        public void UpdateTooltip(string text)
        {
            if (_notifyIcon != null)
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        public void Dispose()
        {
            LocalizationManager.LanguageChanged -= RefreshTexts;
            _notifyIcon?.Dispose();
            _menu?.Dispose();
            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }
        }

        // 渲染器：选中项用铜金底 + 深色字，保证高对比可读
        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Selected
                    ? ColorTranslator.FromHtml("#1C1C24")
                    : ColorTranslator.FromHtml("#E2E2EC");
                base.OnRenderItemText(e);
            }
        }

        // 深色铜金菜单配色
        private class DarkColorTable : ProfessionalColorTable
        {
            private static readonly Color Bg = ColorTranslator.FromHtml("#22222C");
            private static readonly Color Accent = ColorTranslator.FromHtml("#D4A040");
            private static readonly Color Pressed = ColorTranslator.FromHtml("#C09030");
            private static readonly Color Sep = ColorTranslator.FromHtml("#35354A");

            public override Color ToolStripDropDownBackground => Bg;
            public override Color MenuBorder => Sep;
            public override Color MenuItemBorder => Accent;
            public override Color MenuItemSelected => Accent;
            public override Color MenuItemSelectedGradientBegin => Accent;
            public override Color MenuItemSelectedGradientEnd => Accent;
            public override Color MenuItemPressedGradientBegin => Pressed;
            public override Color MenuItemPressedGradientEnd => Pressed;
            public override Color ImageMarginGradientBegin => Bg;
            public override Color ImageMarginGradientMiddle => Bg;
            public override Color ImageMarginGradientEnd => Bg;
            public override Color SeparatorDark => Sep;
            public override Color SeparatorLight => Sep;
        }
    }
}
