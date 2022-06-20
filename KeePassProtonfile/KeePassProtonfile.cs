using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Windows.Forms;
using System.Collections.Generic;

using KeePass.Plugins;
using KeePass.Forms;
using KeePassLib;

namespace KeePassProtonfile
{
    
    public sealed class KeePassProtonfileExt : Plugin
    {
        private IPluginHost m_host = null;
        private Configuration configuration;
        private ProtonfileApi protonfileApi;

        public enum StatusPriority
        {
            eStatusBar,
            eMessageBox,
            eMessageBoxInfo,
            eMessageBoxFatal
        }

        public override bool Initialize(IPluginHost host)
        {
            m_host = host;
            m_host.MainWindow.FileSaving += this.OnFileSaving;

            ToolStripItemCollection tsMenu = m_host.MainWindow.ToolsMenu.DropDownItems;

            var m_tsSeparator = new ToolStripSeparator();
            tsMenu.Add(m_tsSeparator);

            var m_tsmiPopup = new ToolStripMenuItem();
            m_tsmiPopup.Text = "Protonfile";
            m_tsmiPopup.Image = Properties.Resources.Protonfile_16x16;
            tsMenu.Add(m_tsmiPopup);

            var m_tsmiOptions = new ToolStripMenuItem();
            m_tsmiOptions.Text = "Options";
            m_tsmiOptions.Click += OnMenuShowOptions;
            m_tsmiPopup.DropDownItems.Add(m_tsmiOptions);

            configuration = new Configuration(m_host);

            m_host.MainWindow.FileOpened += FileOpened;
            m_host.MainWindow.FileClosed += FileClosed;

            return true;
        }

        private void FileOpened(object sender, FileOpenedEventArgs e)
        {
            configuration.init();
            var authEntry = configuration.getEntry("auth");
            var email = authEntry.Strings.Get(PwDefs.UserNameField).ReadString();
            var password = authEntry.Strings.Get(PwDefs.PasswordField).ReadString();
            this.protonfileApi = new ProtonfileApi(email, password);
        }
        private async void FileClosed(object sender, FileClosedEventArgs e)
        {
            await this.protonfileApi.Dispose();
        }

        private void UpdateProtonfileStatus(string status, string desc = "")
        {
            m_host.MainWindow.RemoveCustomToolBarButton("protonfileStatus");
            m_host.MainWindow.AddCustomToolBarButton("protonfileStatus", status, desc);
        }

        public void SetStatus(StatusPriority priority, string msg)
        {
            switch (priority)
            {
                case StatusPriority.eMessageBoxFatal:
                    MessageBox.Show(msg, "KeePassProtonfile", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
                case StatusPriority.eMessageBoxInfo:
                    MessageBox.Show(msg, "KeePassProtonfile", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case StatusPriority.eMessageBox:
                    MessageBox.Show(msg, "KeePassProtonfile", MessageBoxButtons.OK);
                    break;
                case StatusPriority.eStatusBar:
                    break;
            }
        }

        public override void Terminate()
        {
            this.m_host.MainWindow.FileSaving -= this.OnFileSaving;
            this.m_host.MainWindow.RemoveCustomToolBarButton("protonfileStatus");
        }

        private void OnMenuShowOptions(object sender, EventArgs e)
        {
            // Set the status bar to the KeePass default
            SetStatus(StatusPriority.eStatusBar, "");
            Settings settings = new Settings(m_host);

            settings.ShowDialog();

            if (!settings.isSaved()) return;

            var authEntry = configuration.getEntry("auth");
            var email = authEntry.Strings.Get(PwDefs.UserNameField).ReadString();
            var password = authEntry.Strings.Get(PwDefs.PasswordField).ReadString();
            this.protonfileApi.updateCredentials(email, password);
            m_host.MainWindow.UIFileSave(true);
        }        
        private string ExtractNumberFromString(string input)
        {
            var stack = new Stack<char>();

            for (var i = input.Length - 1; i >= 0; i--)
            {
                if (!char.IsNumber(input[i]))
                {
                    break;
                }

                stack.Push(input[i]);
            }

            var result = new string(stack.ToArray());
            return result;
        }

        private async void ProcessPostFile(string filePath)
        {
            try
            {
                // we locate the folder uid to which we'll upload the file
                var deserialized = await this.protonfileApi.getDb();

                var path = configuration.getEntry("destinationFolder").Strings.Get(PwDefs.UserNameField).ReadString();
                var split = path.Split('/');
                var parentFolder = "null";
                foreach (var folderName in split)
                {
                    var foundItem = deserialized.folders.Find(item => item.parent_folder == (parentFolder == "null" ? null : parentFolder) && item.title == folderName);
                    if (foundItem != null)
                    {
                        parentFolder = foundItem.folder_uid;
                    } else if (folderName != "")
                    {
                        var postResDeserialized = await this.protonfileApi.createFolder(folderName, parentFolder);   
                        parentFolder = postResDeserialized.folder_uid;
                    }
                }

                // https://stackoverflow.com/questions/40686901/how-can-i-specify-form-name-in-webclient-uploadfile
                var newSource = configuration.getEntry("filename").Strings.Get(PwDefs.UserNameField).ReadString();

                var relatedFiles = deserialized.files.FindAll(e => e.folder_uid == parentFolder 
                        && e.filename.StartsWith(newSource) && ExtractNumberFromString(Path.GetFileNameWithoutExtension(e.filename)) != "");
                relatedFiles.Sort(delegate (ApiFile x, ApiFile y)
                {
                    return x.filename.CompareTo(y.filename);
                });

                var multipleBackups = bool.Parse(configuration.getEntry("multipleBackups").Strings.Get(PwDefs.UserNameField).ReadString());

                if (relatedFiles.Count > 0 && multipleBackups)
                {
                    var last = relatedFiles[relatedFiles.Count - 1];
                    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(last.filename);
                    long extractedNumber = 0;
                    var max = Int64.Parse(configuration.getEntry("multipleBackupsNum").Strings.Get(PwDefs.UserNameField).ReadString());
                    try
                    {
                        extractedNumber = Int64.Parse(ExtractNumberFromString(filenameWithoutExtension));
                    } catch (Exception err) { }
                    if (extractedNumber < max)
                    {
                        newSource = newSource + (extractedNumber + 1) + ".kdbx";
                    }
                    else if (relatedFiles.Count < max && extractedNumber >= max)
                    {
                        int i = 1;
                        foreach (var file in relatedFiles)
                        {
                            var withoutExt = Path.GetFileNameWithoutExtension(file.filename);
                            var num = Int64.Parse(ExtractNumberFromString(withoutExt));
                            await this.protonfileApi.renameFile(file.file_uid, withoutExt.Replace(num.ToString(), i.ToString()) + ".kdbx");
                            i = i + 1;
                        }
                        newSource = newSource + i.ToString() + ".kdbx";
                    }
                    else if (extractedNumber >= max)
                    {
                        await this.protonfileApi.deleteFile(relatedFiles[0].file_uid);
                        relatedFiles.RemoveAt(0);
                        foreach (var file in relatedFiles)
                        {
                            var withoutExt = Path.GetFileNameWithoutExtension(file.filename);
                            var num = Int64.Parse(ExtractNumberFromString(withoutExt));
                            await this.protonfileApi.renameFile(file.file_uid, withoutExt.Replace(num.ToString(), (num - 1).ToString()) + ".kdbx");
                        }
                        newSource = newSource + max + ".kdbx";
                    }
                } else if (relatedFiles.Count <= 0 && multipleBackups)
                {
                    newSource = newSource + "1" + ".kdbx";
                }

                if (!multipleBackups)
                {
                    // simulate an overwrite if same files exist
                    var duplicateFiles = deserialized.files.FindAll(item => item.filename == newSource && item.folder_uid == (parentFolder == null ? "null" : parentFolder));
                    foreach (var dup in duplicateFiles)
                    {
                        await this.protonfileApi.deleteFile(dup.file_uid);
                    }
                }

                newSource = Path.Combine(Path.GetDirectoryName(filePath), newSource);
                File.Copy(filePath, newSource);

                this.protonfileApi.uploadFile(newSource, parentFolder);
                File.Delete(newSource);

                UpdateProtonfileStatus("PF | Synchronized");
            }
            catch (Exception err)
            {
                UpdateProtonfileStatus("PF | Synchronization Failed", err.Message + " " + err.StackTrace);
            }
        }

        private void OnFileSaving(object sender, FileSavingEventArgs e)
        {
            if(!e.Database.IsOpen)
            {
                return;
            }

            UpdateProtonfileStatus("PF | Synchronizing");

            string SourceFile;
            if (e.Database.IOConnectionInfo.IsLocalFile())
            {
                SourceFile = e.Database.IOConnectionInfo.Path;
            }
            else
            {
                // remote file
                SourceFile = Path.GetTempFileName();

                var wc = new WebClient();

                wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

                if ((e.Database.IOConnectionInfo.UserName.Length > 0) || (e.Database.IOConnectionInfo.Password.Length > 0))
                {
                    wc.Credentials = new NetworkCredential(e.Database.IOConnectionInfo.UserName, e.Database.IOConnectionInfo.Password);
                }

                wc.DownloadFile(e.Database.IOConnectionInfo.Path, SourceFile);
                wc.Dispose();
            }

            ProcessPostFile(SourceFile);
        }
    }
}
