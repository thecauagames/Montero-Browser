using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.SchemeHandler;
using CefSharp.WinForms;
using EasyTabs;

namespace Montero
{

    public partial class Form1 : Form
    {
        protected TitleBarTabs ParentTabs
        {
            get
            {
                return (ParentForm as TitleBarTabs);
            }
        }

        
        public ChromiumWebBrowser chrome;
        public Form1()
        {
            Icon = Properties.Resources.montero_unstable;
            InitializeComponent();
        }

        private void Chrome_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            this.Invoke(new MethodInvoker(() =>
            {
                urlBox.Text = e.Address;
            }));

            this.Invoke(new MethodInvoker(() =>
            {
                Uri url = new Uri("https://" + new Uri(chrome.Address).Host + "/favicon.ico");
                try
                {
                    Icon img = new Icon(new System.IO.MemoryStream(new
                    System.Net.WebClient().DownloadData(url)));
                    this.Icon = img;
                }
                catch (Exception)
                {
                    this.Icon = Properties.Resources.montero_unstable;
                }
            }));
        }

        private void Chrome_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            this.Invoke(new MethodInvoker(() =>
            {
                Text = e.Title;
            }));
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            var settings = new CefSettings();

   //         settings.RegisterScheme(new CefCustomScheme
   //         {
   //             SchemeName = "montero",
   //             DomainName = "cefsharp",
   //             SchemeHandlerFactory = new FolderSchemeHandlerFactory(
   //                 rootFolder: @".\Montero\Resources\",
   //                 hostName: "cefsharp",
   //                 defaultPage: "startpage.html" // will default to index.html
   //             )
   //         });

            settings.CachePath = "cache";
            urlBox.Text = "https://google.com";
            chrome = new ChromiumWebBrowser(urlBox.Text);
            this.Controls.Add(chrome);
            this.panel1.Controls.Add(chrome);
            chrome.Dock = DockStyle.Fill;
            chrome.AddressChanged += Chrome_AddressChanged;
            chrome.TitleChanged += Chrome_TitleChanged;
            DownloadHandler downloadHandler = new DownloadHandler();
            chrome.DownloadHandler = downloadHandler;
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (chrome.CanGoBack)
                chrome.Back();
        }

        private void urlBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            if (chrome.CanGoForward)
                chrome.Forward();
        }

        private void urlBox_KeyPress_1(object sender, KeyPressEventArgs e)
        {
             if (e.KeyChar == (char)13)
                chrome.Load(urlBox.Text);
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            mainMenu.Show(pictureBox3, 0, pictureBox3.Height + 7);
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            AboutWindow about = new AboutWindow();
            about.ShowDialog();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void newTabButton_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
                chrome.Reload();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}

