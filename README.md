# ZennX-UPI-Extractor
## Production-Ready Windows Desktop Application — C# / .NET 8 LTS

A lightweight, offline-first native Windows desktop application built with C# and Windows Forms to extract, validate, deduplicate, review, and export UPI IDs from QR code images submitted by retailers for Abbott cashback processing.

### Key Capabilities
- **Direct Clipboard Pasting (Ctrl+V)**: Copy any QR code from the retailer profile in a web browser and paste it instantly.
- **Bulk Processing**: Incrementally batch process hundreds of QR codes without holding full images in memory.
- **Retailer ZID Entry & Integrity**: Strict text storage preserving leading zeros across local SQLite database, UI, and Excel export.
- **Duplicate Detection**: Flags duplicates within the active batch and against historical PC records.
- **Spreadsheet Formula Protection**: Escapes formula payloads (`=`, `+`, `-`, `@`) when exporting to Excel/CSV.
- **Cross-PC Report Consolidation**: Merges multi-workstation exports and identifies cross-PC duplicate claims.
- **100% Offline & No Admin Privileges**: Fully functional without internet access and operates entirely in user space (`%LOCALAPPDATA%`).
- **Self-Contained Deployment**: Built and published for Windows x64 with bundled .NET runtime.
