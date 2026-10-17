# ZennX Abbott Cashback UPI Extractor
## Employee Quick-Start Guide & Operations Manual

---

### 1. Application Overview
The **ZennX Abbott Cashback UPI Extractor** is an internal, offline-first Windows desktop tool designed for operations teams to extract, validate, deduplicate, review, and export UPI IDs from retailer QR codes for Abbott cashback verification.

- **Branding**: ZennX Blue (`#0075CF`) & ZennX Orange (`#FF9829`).
- **Operating Environment**: Fully offline, zero internet or remote server connections needed.
- **Admin Access**: Runs without Windows administrator privileges (stores data safely in `%LOCALAPPDATA%\ZennX\AbbottCashbackExtractor\`).
- **No .NET Prerequisite**: Self-contained Windows x64 binary bundles its own runtime.

---

### 2. Launching the Application
Navigate to the application folder and double-click:
`ZennX.AbbottUpiExtractor.exe`

*(No installation, driver, or UAC prompt is required).*

---

### 3. Core Operational Workflows

#### Workflow A: Copy & Paste QR Code from Web Browser (Rapid Processing)
1. Open the retailer's ZennX profile in Google Chrome, Microsoft Edge, or Mozilla Firefox.
2. Locate the QR code image, **Right-Click** directly on the image, and select **"Copy Image"** *(Do not click "Copy image link")*.
3. Switch to the desktop application.
4. Click **"Paste QR (Ctrl+V)"** in the sidebar or simply press **Ctrl+V** anywhere in the application.
5. The image is rendered in the preview box, decoded locally, and the payee UPI ID is displayed.
6. Enter or confirm the **Retailer ZID** (e.g., `004910`). *Leading zeros are strictly preserved.*
7. Click **"Save Record"** or **"Save & Paste Next"** to immediately continue with the next retailer.

#### Workflow B: Bulk Processing Hundreds of Images
1. Click **"Bulk Processing"** in the sidebar.
2. Click **"Select Files..."** (supports multi-selection) or **"Select Folder..."** to load a folder of QR images (PNG, JPG, JPEG, WEBP).
3. If Retailer ZID is mandatory for this batch, check the **"Require Retailer ZID for this batch"** checkbox.
4. Click **"Start Processing"**.
5. The application decodes each image sequentially using an incremental memory-stream disposal pattern, keeping RAM usage below ~70 MB.
6. As rows appear in the table, you can type or edit the Retailer ZID directly in the table cell.
7. Any failed or unreadable QR codes are highlighted in light red; duplicate UPI IDs are highlighted in amber.

#### Workflow C: Results & Manual Review
1. Click **"Results & Review"** in the sidebar.
2. Use the filters:
   - Filter by Batch ID.
   - Filter by Status (*All, Success, Failed, Duplicates, Pending Review, Missing ZID*).
   - Search by UPI ID, Retailer ZID, or filename.
3. Select any record to see its details in the right pane.
4. Edit the Retailer ZID or correct the UPI ID, enter a review note, and click **"Save Corrections"**.
5. All modifications are permanently recorded in the **Audit Trail** below the detail panel.

#### Workflow D: Excel & CSV Export
1. Click **"Excel & CSV Export"** in the sidebar.
2. Select your scope:
   - *Ready for Export Only* (records that are successful, not missing required ZIDs, and reviewed).
   - *All Records*, *Specific Batch*, or *Failed Records* (for operations follow-up).
3. Choose format:
   - **Excel Workbook (.xlsx)** (ClosedXML with text-formatted ZID column and formula injection protection).
   - **CSV File (.csv)** (RFC 4180 standard).
4. Click **"Choose Location & Export"**, pick your destination folder, and save.

#### Workflow E: Cross-PC Report Consolidation
*Note: Each employee PC has its own isolated local SQLite database.*
1. Team members export their completed batches to Excel or CSV and place them in an approved internal shared folder or USB drive.
2. The team lead opens **"Cross-PC Reports"** in the sidebar.
3. Click **"Add Files..."** and select the export files from all colleagues.
4. Click **"Merge & Check Duplicates"**.
5. The application flags any UPI ID claimed across multiple PCs and displays detailed conflict notes.
6. Click **"Export Consolidated Report to Excel"** to save the master audit report.

#### Workflow F: Backup & Restore
1. Click **"Backup & Restore"** in the sidebar.
2. Click **"Create Database Backup Now"** to generate an atomic SQLite snapshot in the user's LocalAppData folder.
3. To restore from a previous backup, select a backup from the list and click **"Restore Selected Backup"**. The system validates database integrity and automatically creates a safety snapshot of the active database before restoring.

---

### 4. Technical Summary for IT & Operations
- **Executable**: `publish\win-x64\ZennX.AbbottUpiExtractor.exe`
- **Total Published Bundle Size**: ~157 MB (contains full self-contained .NET runtime + native libraries).
- **Target OS**: Windows 10 / Windows 11 (64-bit).
- **Database Engine**: SQLite 3 (embedded in `Microsoft.Data.Sqlite`).
- **Database Location**: `%LOCALAPPDATA%\ZennX\AbbottCashbackExtractor\zennx_cashback.db`.
