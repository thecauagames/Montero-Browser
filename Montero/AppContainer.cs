using EasyTabs;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Montero
{
    public partial class AppContainer : TitleBarTabs
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        public static bool IsDarkModeEnabled { get; private set; } = true;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public AppContainer()
        {
            InitializeComponent();

            AeroPeekEnabled = false;
            ShowTooltips = true;
            TabRenderer = new Win11TabRenderer(this);
            Icon = Properties.Resources.unstable;
            Text = "Montero";
            BackColor = Color.FromArgb(30, 30, 30);
        }

        public override TitleBarTab CreateTab()
        {
            return new TitleBarTab(this)
            {
                Content = new Form1
                {
                    Text = "Nova guia"
                }
            };
        }

        public void AddAndSelectNewTab(string initialUrl = "https://www.google.com")
        {
            var tab = CreateTab();
            if (tab.Content is Form1 browserForm)
            {
                browserForm.InitialUrl = initialUrl;
            }

            Tabs.Add(tab);
            SelectedTabIndex = Tabs.Count - 1;
        }

        public static void SetDarkModeEnabled(bool enabled)
        {
            IsDarkModeEnabled = enabled;
        }

        public void ApplyThemeToOpenTabs()
        {
            foreach (var tab in Tabs)
            {
                if (tab.Content is Form1 browserForm)
                {
                    browserForm.ApplyTheme();
                }
            }

            ApplyWindowTheme();
            RedrawTabs();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWindowTheme();
        }

        private void ApplyWindowTheme()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            int useDark = IsDarkModeEnabled ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
            BackColor = IsDarkModeEnabled ? Color.FromArgb(30, 30, 30) : Color.WhiteSmoke;
        }

        private void AppContainer_Load(object sender, EventArgs e)
        {
        }
    }
}
