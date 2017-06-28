# VerifyPDFs
This is a console application that perfoms some validations on pdfs such as verifying that is at least one pdf, that there are only pdfs, that the PDFs are not corrupt, that there are no interactive fields, or any portfolios.

### Requirements
- Visual Studio 2015+
- Ghostscript

### Usage
   ```sh
   verifypdfs "path to parent folder containing folder of pdfs" "path to gswin64c.exe".
   ```
   - E.g. verifypdfs ""c:\PDFfolders"" ""C:\Program Files\gs\gs9.21\bin\gswin64c.exe""

### To-do
- For most use cases it would be helpful to modify the code to use the path to the folder containing the pdfs rather than the parent folder.  The current folder conventions used here were to fulfill specific user requests.