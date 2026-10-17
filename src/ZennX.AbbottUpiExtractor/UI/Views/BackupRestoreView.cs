using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class BackupRestoreView : UserControl
    {
        private Label _lblDbLocation = null!;
        private Label _lblDbSize = null!;
        private ListBox _lstBackups = null!;

        public BackupRestoreView()
        {
            InitializeComponent();
            RefreshBackupList();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Local Database Backup & Restore",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Perform safe offline backups, inspect integrity, and restore local SQLite data without administrator privileges.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // DB Location card
            var dbCard = Theme.CreateCardPanel();
            dbCard.Location = new Point(24, 85);
            dbCard.Size = new Size(1110, 160);

            var dbTitle = new Label
            {
                Text = "Active Database Information",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            _lblDbLocation = new Label
            {
                Text = $"Path: {AppPaths.GetDatabasePath()}",
                Location = new Point(16, 48),
                Size = new Size(950, 24),
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary
            };

            _lblDbSize = new Label
            {
                Text = "Size: Calculating...",
                Location = new Point(16, 75),
                Size = new Size(400, 24),
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary
            };

            var btnOpenDataFolder = Theme.CreateSecondaryButton("📂 Open App Directory", 180, 36);
            btnOpenDataFolder.Location = new Point(16, 105);
            btnOpenDataFolder.Click += (s, e) =>
            {
                Process.Start("explorer.exe", AppPaths.GetAppDataFolder());
            };

            var btnBackupNow = Theme.CreatePrimaryButton("💾 Create Database Backup Now", 240, 36);
            btnBackupNow.Location = new Point(250, 105);
            btnBackupNow.Click += (s, e) => CreateBackup();

            dbCard.Controls.Add(dbTitle);
            dbCard.Controls.Add(_lblDbLocation);
            dbCard.Controls.Add(_lblDbSize);
            dbCard.Controls.Add(btnOpenDataFolder);
            dbCard.Controls.Add(btnBackupNow);

            // Restore Card
            var restoreCard = Theme.CreateCardPanel();
            restoreCard.Location = new Point(24, 260);
            restoreCard.Size = new Size(1110, 420);

            var restoreTitle = new Label
            {
                Text = "Available Local Backups & Database Restoration",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            _lstBackups = new ListBox
            {
                Location = new Point(16, 50),
                Size = new Size(1075, 230),
                Font = Theme.FontBody
            };

            var btnRestoreSelected = Theme.CreateOrangeButton("⚠️ Restore Selected Backup", 220, 40);
            btnRestoreSelected.Location = new Point(16, 295);
            btnRestoreSelected.Click += (s, e) => RestoreSelected();

            var btnRestoreExternal = Theme.CreateSecondaryButton("Browse External Backup File...", 240, 40);
            btnRestoreExternal.Location = new Point(250, 295);
            btnRestoreExternal.Click += (s, e) => BrowseAndRestoreExternal();

            var warningNotice = new Label
            {
                Text = "⚠️ Safeguard Warning: Restoring a backup replaces the active database. Before any restore begins, an automatic safety backup of your current database is created in the Backups folder.",
                Location = new Point(16, 350),
                Size = new Size(1000, 40),
                Font = Theme.FontSmall,
                ForeColor = Theme.StatusWarning
            };

            restoreCard.Controls.Add(restoreTitle);
            restoreCard.Controls.Add(_lstBackups);
            restoreCard.Controls.Add(btnRestoreSelected);
            restoreCard.Controls.Add(btnRestoreExternal);
            restoreCard.Controls.Add(warningNotice);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(dbCard);
            Controls.Add(restoreCard);

            UpdateDbSize();
        }

        private void UpdateDbSize()
        {
            string dbPath = AppPaths.GetDatabasePath();
            if (File.Exists(dbPath))
            {
                var fi = new FileInfo(dbPath);
                _lblDbSize.Text = $"Database File Size: {(fi.Length / 1024.0):F2} KB | Storage: LocalAppData (No Admin Rights Needed)";
            }
            else
            {
                _lblDbSize.Text = "Database not yet initialized.";
            }
        }

        private void RefreshBackupList()
        {
            _lstBackups.Items.Clear();
            string backupFolder = AppPaths.GetBackupsFolder();
            if (Directory.Exists(backupFolder))
            {
                var files = Directory.GetFiles(backupFolder, "*.db");
                Array.Sort(files);
                Array.Reverse(files);

                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    _lstBackups.Items.Add($"{Path.GetFileName(f)}  ({fi.Length / 1024.0:F1} KB, Modified {fi.LastWriteTime:yyyy-MM-dd HH:mm})");
                }
            }

            if (_lstBackups.Items.Count > 0)
            {
                _lstBackups.SelectedIndex = 0;
            }
        }

        private void CreateBackup()
        {
            var result = BackupRestoreService.CreateBackup();
            if (result.Success)
            {
                MessageBox.Show($"Backup created successfully at:\n{result.BackupFilePath}", "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshBackupList();
                UpdateDbSize();
            }
            else
            {
                MessageBox.Show($"Failed to create backup: {result.ErrorMessage}", "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreSelected()
        {
            if (_lstBackups.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a backup file from the list first.", "None Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string itemText = _lstBackups.SelectedItem!.ToString()!;
            string filename = itemText.Split("  (")[0];
            string fullPath = Path.Combine(AppPaths.GetBackupsFolder(), filename);

            ConfirmAndRestore(fullPath);
        }

        private void BrowseAndRestoreExternal()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select ZennX Database Backup File",
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                ConfirmAndRestore(ofd.FileName);
            }
        }

        private void ConfirmAndRestore(string backupFilePath)
        {
            // Step 1: Validate file first
            var validation = BackupRestoreService.ValidateBackupFile(backupFilePath);
            if (!validation.Valid)
            {
                MessageBox.Show($"Invalid Backup File:\n{validation.Message}", "Integrity Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"{validation.Message}\n\nAre you sure you want to restore this backup?\n\n(A safety backup of your current active data will be saved automatically first).",
                "Confirm Database Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                var restoreResult = BackupRestoreService.RestoreBackup(backupFilePath);
                if (restoreResult.Success)
                {
                    MessageBox.Show("Database successfully restored! The application will refresh local records.", "Restore Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshBackupList();
                    UpdateDbSize();
                }
                else
                {
                    MessageBox.Show($"Restore failed: {restoreResult.ErrorMessage}", "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
