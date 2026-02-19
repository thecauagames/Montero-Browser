using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Montero
{
    public partial class AboutWindow : Form
    {
        private const string ReleasesPageUrl = "https://github.com/thecauagames/Montero-Browser/releases";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/thecauagames/Montero-Browser/releases/latest";

        private bool updateCheckInProgress;

        public AboutWindow()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void About_Load(object sender, EventArgs e)
        {
            linkLabel1.Text = "Check for updates";
        }

        private void label2_Click(object sender, EventArgs e)
        {
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/cefsharp/CefSharp");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private async void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (updateCheckInProgress)
            {
                return;
            }

            updateCheckInProgress = true;
            linkLabel1.Enabled = false;
            linkLabel1.Text = "Checking for updates...";

            try
            {
                ReleaseInfo latestRelease = await Task.Run(() => GetLatestRelease());
                Version currentVersion = GetCurrentVersion();
                Version latestVersion = ExtractVersion(latestRelease?.TagName);

                if (latestRelease == null || latestVersion == null || currentVersion == null || latestVersion <= currentVersion)
                {
                    linkLabel1.Text = "No updates found";
                    return;
                }

                DialogResult result = MessageBox.Show(
                    "A new version (" + latestRelease.TagName + ") was found. Do you want to open the downloads page?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    Process.Start(string.IsNullOrWhiteSpace(latestRelease.HtmlUrl) ? ReleasesPageUrl : latestRelease.HtmlUrl);
                }

                linkLabel1.Text = "Check for updates";
            }
            catch
            {
                linkLabel1.Text = "No updates found";
            }
            finally
            {
                updateCheckInProgress = false;
                linkLabel1.Enabled = true;
            }
        }

        private static ReleaseInfo GetLatestRelease()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(LatestReleaseApiUrl);
            request.Method = "GET";
            request.Accept = "application/vnd.github+json";
            request.UserAgent = "MonteroBrowser-Updater";
            request.Timeout = 10000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                string tag = ExtractJsonString(json, "tag_name");
                string htmlUrl = ExtractJsonString(json, "html_url");

                if (string.IsNullOrWhiteSpace(tag))
                {
                    return null;
                }

                return new ReleaseInfo
                {
                    TagName = tag,
                    HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? ReleasesPageUrl : htmlUrl
                };
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Value.Replace("\\/", "/");
        }

        private Version GetCurrentVersion()
        {
            // Prefer the displayed version text in About first, then fallback to assembly version.
            Version displayed = ExtractVersion(label2.Text);
            if (displayed != null)
            {
                return displayed;
            }

            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion != null)
            {
                return assemblyVersion;
            }

            return null;
        }

        private static Version ExtractVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Match match = Regex.Match(text, @"\d+(\.\d+){1,3}");
            if (!match.Success)
            {
                return null;
            }

            Version version;
            return Version.TryParse(match.Value, out version) ? version : null;
        }

        private sealed class ReleaseInfo
        {
            public string TagName { get; set; }
            public string HtmlUrl { get; set; }
        }
    }
}
