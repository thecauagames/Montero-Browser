using CefSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Montero
{
    internal sealed class DownloadHandler : IDownloadHandler
    {
        private readonly SynchronizationContext uiContext;
        private readonly object syncLock = new object();
        private readonly HashSet<int> completionNotifiedIds = new HashSet<int>();
        private readonly HashSet<int> cancelledNotifiedIds = new HashSet<int>();

        public DownloadHandler()
        {
            uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod)
        {
            return true;
        }

        public bool OnBeforeDownload(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            DownloadItem downloadItem,
            IBeforeDownloadCallback callback)
        {
            if (downloadItem == null || callback == null || callback.IsDisposed)
            {
                return false;
            }

            uiContext.Post(_ =>
            {
                if (callback.IsDisposed)
                {
                    return;
                }

                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Save file";
                    saveDialog.FileName = string.IsNullOrWhiteSpace(downloadItem.SuggestedFileName)
                        ? "download"
                        : downloadItem.SuggestedFileName;
                    saveDialog.InitialDirectory = GetDownloadsPath();
                    saveDialog.Filter = "All files (*.*)|*.*";
                    saveDialog.RestoreDirectory = true;

                    DialogResult result = saveDialog.ShowDialog();
                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(saveDialog.FileName))
                    {
                        callback.Continue(saveDialog.FileName, showDialog: false);
                        return;
                    }
                }

                callback.Dispose();
            }, null);

            return true;
        }

        public void OnDownloadUpdated(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            DownloadItem downloadItem,
            IDownloadItemCallback callback)
        {
            if (downloadItem == null)
            {
                return;
            }

            if (downloadItem.IsComplete)
            {
                bool shouldNotify;
                lock (syncLock)
                {
                    shouldNotify = completionNotifiedIds.Add(downloadItem.Id);
                }

                if (!shouldNotify)
                {
                    return;
                }

                string path = downloadItem.FullPath;
                uiContext.Post(_ =>
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(path)
                            ? "Download completed."
                            : "Download completed:\n" + path,
                        "Download",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }, null);
                return;
            }

            if (downloadItem.IsCancelled)
            {
                bool shouldNotify;
                lock (syncLock)
                {
                    shouldNotify = cancelledNotifiedIds.Add(downloadItem.Id);
                }

                if (!shouldNotify)
                {
                    return;
                }

                uiContext.Post(_ =>
                {
                    MessageBox.Show(
                        "Download cancelled.",
                        "Download",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }, null);
            }
        }

        private static string GetDownloadsPath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");
            return Directory.Exists(downloads)
                ? downloads
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }
    }
}
