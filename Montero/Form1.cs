using CefSharp;
using CefSharp.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Montero
{
    public partial class Form1 : Form
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        private ToolStripMenuItem darkModeButton;
        private Dictionary<PictureBox, Image> originalNavIcons;
        private Dictionary<PictureBox, Image> darkNavIcons;
        private Image backDisabledLightIcon;
        private Image backDisabledDarkIcon;
        private Image forwardDisabledLightIcon;
        private Image forwardDisabledDarkIcon;
        private readonly Dictionary<string, ChromiumWebBrowser> browsersByTabId = new Dictionary<string, ChromiumWebBrowser>();
        private readonly Dictionary<string, string> tabTitleById = new Dictionary<string, string>();
        private readonly Dictionary<string, Image> faviconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly object faviconCacheLock = new object();
        private PhotonTabBar tabBar;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMargins);

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        public string InitialUrl { get; set; } = "https://www.google.com";

        private ChromiumWebBrowser ActiveBrowser
        {
            get
            {
                if (tabBar == null || string.IsNullOrWhiteSpace(tabBar.SelectedTabId))
                {
                    return null;
                }

                browsersByTabId.TryGetValue(tabBar.SelectedTabId, out ChromiumWebBrowser browser);
                return browser;
            }
        }

        public Form1()
        {
            Icon = Properties.Resources.unstable;
            InitializeComponent();
            newWindowButton.Click += newWindowButton_Click;
            SizeChanged += Form1_SizeChanged;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            EnsureNavigationIconsPrepared();
            InitializePhotonTabs();

            if (!Cef.IsInitialized)
            {
                var settings = new CefSettings
                {
                    CachePath = Path.GetFullPath("cache")
                };
                Cef.Initialize(settings);
            }

            AddDarkModeMenuButton();
            ApplyTheme();
            AddNewBrowserTab(InitialUrl, true);
        }

        private void InitializePhotonTabs()
        {
            tabBar = new PhotonTabBar
            {
                Height = 36
            };

            tabBar.AddTabRequested += TabBar_AddTabRequested;
            tabBar.TabCloseRequested += TabBar_TabCloseRequested;
            tabBar.TabSelected += TabBar_TabSelected;

            Controls.Add(tabBar);
            tabBar.BringToFront();

            MoveNavigationBarDown(tabBar.Height);
            ApplyWindowChrome();
        }

        private void MoveNavigationBarDown(int offset)
        {
            pictureBox1.Top += offset;
            pictureBox2.Top += offset;
            pictureBox3.Top += offset;
            pictureBox4.Top += offset;
            panel3.Top += offset;

            panel1.Top += offset;
            panel1.Height -= offset;
        }

        private void AddNewBrowserTab(string url, bool selectTab)
        {
            string tabId = Guid.NewGuid().ToString("N");
            tabBar.AddTab(tabId, "New Tab");
            tabTitleById[tabId] = "New Tab";

            var browser = new ChromiumWebBrowser(NormalizeUrl(url));
            browser.Dock = DockStyle.Fill;
            browser.AddressChanged += (sender, e) => Browser_AddressChanged(tabId, e);
            browser.TitleChanged += (sender, e) => Browser_TitleChanged(tabId, e);
            browser.LoadingStateChanged += (sender, e) => Browser_LoadingStateChanged(tabId, e);
            browser.DownloadHandler = new DownloadHandler();

            browsersByTabId[tabId] = browser;

            if (selectTab)
            {
                tabBar.SelectTab(tabId);
                ShowBrowser(tabId);
                UpdateWindowTitle(tabId);
            }
        }

        private void ShowBrowser(string tabId)
        {
            panel1.Controls.Clear();
            if (!string.IsNullOrWhiteSpace(tabId) && browsersByTabId.TryGetValue(tabId, out ChromiumWebBrowser browser))
            {
                panel1.Controls.Add(browser);
                urlBox.Text = browser.Address;
                UpdateNavigationButtons(browser.CanGoBack, browser.CanGoForward);
                return;
            }

            UpdateNavigationButtons(false, false);
        }

        private void CloseBrowserTab(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) || !browsersByTabId.TryGetValue(tabId, out ChromiumWebBrowser browser))
            {
                return;
            }

            // Keep one tab alive like mainstream browsers; never close the whole app from tab-close.
            if (browsersByTabId.Count == 1)
            {
                string resetUrl = "https://www.google.com";
                tabTitleById[tabId] = "New Tab";
                tabBar.UpdateTabTitle(tabId, "New Tab");
                tabBar.UpdateTabIcon(tabId, null);
                browser.Load(resetUrl);

                if (tabBar.SelectedTabId == tabId)
                {
                    urlBox.Text = resetUrl;
                    UpdateWindowTitle(tabId);
                }

                return;
            }

            bool wasActive = tabBar.SelectedTabId == tabId;
            string fallbackTabId = null;
            if (wasActive)
            {
                fallbackTabId = browsersByTabId.Keys.FirstOrDefault(id => id != tabId);
                if (!string.IsNullOrWhiteSpace(fallbackTabId))
                {
                    tabBar.SelectTab(fallbackTabId);
                    ShowBrowser(fallbackTabId);
                    UpdateWindowTitle(fallbackTabId);
                }
            }

            browsersByTabId.Remove(tabId);
            tabTitleById.Remove(tabId);
            tabBar.RemoveTab(tabId);
            try
            {
                browser.Dispose();
            }
            catch
            {
            }

            if (wasActive && !string.IsNullOrWhiteSpace(tabBar.SelectedTabId))
            {
                ShowBrowser(tabBar.SelectedTabId);
                UpdateWindowTitle(tabBar.SelectedTabId);
            }
        }

        private void Browser_AddressChanged(string tabId, AddressChangedEventArgs e)
        {
            SafeUiInvoke(() =>
            {
                if (tabBar.SelectedTabId == tabId)
                {
                    urlBox.Text = e.Address;
                }
            });

            TryUpdateTabFavicon(tabId, e.Address);
        }

        private void Browser_TitleChanged(string tabId, TitleChangedEventArgs e)
        {
            SafeUiInvoke(() =>
            {
                string title = string.IsNullOrWhiteSpace(e.Title) ? "New Tab" : e.Title;
                tabTitleById[tabId] = title;
                string shortTitle = title.Length > 26 ? title.Substring(0, 26) + "..." : title;
                tabBar.UpdateTabTitle(tabId, shortTitle);

                if (tabBar.SelectedTabId == tabId)
                {
                    Text = FormatWindowTitle(title);
                }
            });
        }

        private void Browser_LoadingStateChanged(string tabId, LoadingStateChangedEventArgs e)
        {
            SafeUiInvoke(() =>
            {
                if (tabBar.SelectedTabId == tabId)
                {
                    UpdateNavigationButtons(e.CanGoBack, e.CanGoForward);
                }
            });
        }

        private void TabBar_AddTabRequested(object sender, EventArgs e)
        {
            AddNewBrowserTab("https://www.google.com", true);
        }

        private void TabBar_TabCloseRequested(object sender, string tabId)
        {
            if (browsersByTabId.Count <= 1)
            {
                return;
            }

            CloseBrowserTab(tabId);
        }

        private void TabBar_TabSelected(object sender, string tabId)
        {
            ShowBrowser(tabId);
            UpdateWindowTitle(tabId);
        }

        private void AddDarkModeMenuButton()
        {
            darkModeButton = new ToolStripMenuItem
            {
                Name = "darkModeButton",
                Text = "Dark Mode",
                CheckOnClick = true,
                Checked = AppContainer.IsDarkModeEnabled
            };
            darkModeButton.CheckedChanged += darkModeButton_CheckedChanged;

            mainMenu.Items.Insert(mainMenu.Items.Count - 2, new ToolStripSeparator());
            mainMenu.Items.Insert(mainMenu.Items.Count - 2, darkModeButton);
        }

        public void ApplyTheme()
        {
            bool dark = AppContainer.IsDarkModeEnabled;
            Color background = dark ? Color.FromArgb(24, 26, 31) : Color.White;
            Color chromeSurface = dark ? Color.FromArgb(33, 36, 43) : Color.FromArgb(236, 236, 236);
            Color textColor = dark ? Color.FromArgb(224, 227, 233) : Color.Black;

            BackColor = background;
            panel1.BackColor = background;
            panel3.BackColor = chromeSurface;
            panel3.BackgroundImage = null;
            urlBox.BackColor = chromeSurface;
            urlBox.ForeColor = textColor;
            mainMenu.BackColor = dark ? Color.FromArgb(38, 41, 48) : Color.White;
            mainMenu.ForeColor = textColor;
            tabBar.DarkMode = dark;
            tabBar.Invalidate();
            ApplyNavigationIconTheme(dark);
            ApplyWindowChrome();

            if (darkModeButton != null)
            {
                darkModeButton.Checked = dark;
            }

            ClearForcedPageThemeStyles();
        }

        private void darkModeButton_CheckedChanged(object sender, EventArgs e)
        {
            AppContainer.SetDarkModeEnabled(darkModeButton.Checked);
            ApplyTheme();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoBack)
            {
                ActiveBrowser.Back();
                UpdateNavigationButtonsState();
            }
        }

        private void urlBox_TextChanged(object sender, EventArgs e)
        {
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoForward)
            {
                ActiveBrowser.Forward();
                UpdateNavigationButtonsState();
            }
        }

        private void urlBox_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                ActiveBrowser?.Load(NormalizeUrl(urlBox.Text));
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            mainMenu.Show(pictureBox3, 0, pictureBox3.Height + 7);
        }

        private static string NormalizeUrl(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "https://www.google.com";
            }

            string trimmed = raw.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri _))
            {
                return trimmed;
            }

            if (trimmed.Contains(" "))
            {
                return "https://www.google.com/search?q=" + Uri.EscapeDataString(trimmed);
            }

            return "https://" + trimmed;
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            using (var about = new AboutWindow())
            {
                about.ShowDialog();
            }
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void newTabButton_Click(object sender, EventArgs e)
        {
            AddNewBrowserTab("https://www.google.com", true);
        }

        private void newWindowButton_Click(object sender, EventArgs e)
        {
            var window = new Form1();
            window.Show();
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            ActiveBrowser?.Reload();
            UpdateNavigationButtonsState();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            ApplyWindowChrome();
        }

        private void EnsureNavigationIconsPrepared()
        {
            if (originalNavIcons != null)
            {
                return;
            }

            originalNavIcons = new Dictionary<PictureBox, Image>
            {
                [pictureBox1] = pictureBox1.BackgroundImage,
                [pictureBox2] = pictureBox2.BackgroundImage,
                [pictureBox3] = pictureBox3.BackgroundImage,
                [pictureBox4] = pictureBox4.BackgroundImage
            };

            darkNavIcons = new Dictionary<PictureBox, Image>();
            foreach (var entry in originalNavIcons)
            {
                darkNavIcons[entry.Key] = InvertImage(entry.Value);
            }

            backDisabledLightIcon = CreateDisabledNavImage(originalNavIcons[pictureBox1]);
            backDisabledDarkIcon = CreateDisabledNavImage(darkNavIcons[pictureBox1]);
            forwardDisabledLightIcon = CreateDisabledNavImage(originalNavIcons[pictureBox2]);
            forwardDisabledDarkIcon = CreateDisabledNavImage(darkNavIcons[pictureBox2]);
        }

        private void ApplyNavigationIconTheme(bool dark)
        {
            EnsureNavigationIconsPrepared();
            var iconSet = dark ? darkNavIcons : originalNavIcons;
            foreach (var entry in iconSet)
            {
                entry.Key.BackgroundImage = entry.Value;
            }

            UpdateNavigationButtonsState();
        }

        private static Image InvertImage(Image image)
        {
            if (image == null)
            {
                return null;
            }

            Bitmap source = new Bitmap(image);
            Bitmap output = new Bitmap(source.Width, source.Height);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    Color inverted = Color.FromArgb(pixel.A, 255 - pixel.R, 255 - pixel.G, 255 - pixel.B);
                    output.SetPixel(x, y, inverted);
                }
            }

            source.Dispose();
            return output;
        }

        private static Image CreateDisabledNavImage(Image image)
        {
            if (image == null)
            {
                return null;
            }

            Bitmap source = new Bitmap(image);
            Bitmap output = new Bitmap(source.Width, source.Height);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                ControlPaint.DrawImageDisabled(g, source, 0, 0, Color.Transparent);
            }

            source.Dispose();
            return output;
        }

        private void UpdateNavigationButtonsState()
        {
            ChromiumWebBrowser browser = ActiveBrowser;
            if (browser == null)
            {
                UpdateNavigationButtons(false, false);
                return;
            }

            UpdateNavigationButtons(browser.CanGoBack, browser.CanGoForward);
        }

        private void UpdateNavigationButtons(bool canGoBack, bool canGoForward)
        {
            bool dark = AppContainer.IsDarkModeEnabled;

            pictureBox1.Cursor = canGoBack ? Cursors.Default : Cursors.Default;
            pictureBox2.Cursor = canGoForward ? Cursors.Default : Cursors.Default;

            pictureBox1.BackgroundImage = canGoBack
                ? (dark ? darkNavIcons[pictureBox1] : originalNavIcons[pictureBox1])
                : (dark ? backDisabledDarkIcon : backDisabledLightIcon);

            pictureBox2.BackgroundImage = canGoForward
                ? (dark ? darkNavIcons[pictureBox2] : originalNavIcons[pictureBox2])
                : (dark ? forwardDisabledDarkIcon : forwardDisabledLightIcon);
        }

        private void ApplyWindowChrome()
        {
            if (!IsHandleCreated || tabBar == null)
            {
                return;
            }

            int dark = AppContainer.IsDarkModeEnabled ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));

            var margins = new Margins
            {
                Left = 0,
                Right = 0,
                Top = tabBar.Height + 2,
                Bottom = 0
            };

            DwmExtendFrameIntoClientArea(Handle, ref margins);
        }

        private void TryUpdateTabFavicon(string tabId, string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri pageUri) || string.IsNullOrWhiteSpace(pageUri.Host))
            {
                return;
            }

            Image cachedIcon;
            lock (faviconCacheLock)
            {
                faviconCache.TryGetValue(pageUri.Host, out cachedIcon);
            }

            if (cachedIcon != null)
            {
                TryInvokeTabIconUpdate(tabId, cachedIcon);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    Uri faviconUri = new Uri(pageUri.Scheme + "://" + pageUri.Host + "/favicon.ico");
                    using (var client = new WebClient())
                    using (var stream = new MemoryStream(client.DownloadData(faviconUri)))
                    using (var icon = new Icon(stream))
                    {
                        Image iconImage = icon.ToBitmap();
                        Image cached;
                        lock (faviconCacheLock)
                        {
                            if (!faviconCache.TryGetValue(pageUri.Host, out cached))
                            {
                                faviconCache[pageUri.Host] = iconImage;
                                cached = iconImage;
                            }
                        }

                        if (cached != iconImage)
                        {
                            iconImage.Dispose();
                        }

                        TryInvokeTabIconUpdate(tabId, cached);
                    }
                }
                catch
                {
                }
            });
        }

        private void TryInvokeTabIconUpdate(string tabId, Image icon)
        {
            if (IsDisposed || !IsHandleCreated || tabBar == null || tabBar.IsDisposed)
            {
                return;
            }

            try
            {
                BeginInvoke(new MethodInvoker(() => tabBar.UpdateTabIcon(tabId, icon)));
            }
            catch
            {
            }
        }

        private void ClearForcedPageThemeStyles()
        {
            const string removeScript = "(function(){var s=document.getElementById('montero-dark-mode-style'); if(s){s.remove();}})();";
            foreach (var browser in browsersByTabId.Values)
            {
                if (browser == null || browser.IsDisposed)
                {
                    continue;
                }

                try
                {
                    browser.GetMainFrame().ExecuteJavaScriptAsync(removeScript);
                }
                catch
                {
                }
            }
        }

        private void SafeUiInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    action();
                }));
            }
            catch
            {
            }
        }

        private static string FormatWindowTitle(string pageTitle)
        {
            string value = string.IsNullOrWhiteSpace(pageTitle) ? "New Tab" : pageTitle;
            return value + " - Montero";
        }

        private void UpdateWindowTitle(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId))
            {
                Text = "Montero";
                return;
            }

            if (!tabTitleById.TryGetValue(tabId, out string title))
            {
                title = "New Tab";
            }

            Text = FormatWindowTitle(title);
        }

    }
}
