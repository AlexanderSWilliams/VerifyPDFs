﻿using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerifyPDFs
{
    internal class Program
    {
        public static string[] ExecuteProcess(string cmd, string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = cmd;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();

                var Standard = process.StandardOutput.ReadToEnd();
                var Error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new[] { Standard, Error };
            }
        }

        public static void MoveFolder(string folder, string path)
        {
            var FolderName = System.IO.Path.GetFileName(folder);
            var Rest = FolderName.Substring(Math.Min(FolderName.Length - 1, FolderName.LastIndexOf(' ') + 1));
            int test;
            if (int.TryParse(Rest, out test))
                FolderName = FolderName.Substring(0, FolderName.LastIndexOf(' '));

            var FolderNumbers = System.IO.Directory.EnumerateDirectories(path)
                .Select(x => System.IO.Path.GetFileName(x).ToLower())
            .Where(x => x.Contains(FolderName.ToLower()) && x.Replace(FolderName.ToLower(), "").AsEnumerable().All(y => Char.IsDigit(y) || y == ' '))
            .Select(x =>
            {
                var SpaceIndex = x.LastIndexOf(' ');
                if (SpaceIndex == -1)
                    return 0;
                int result;
                int.TryParse(x.Substring(SpaceIndex), out result);
                return result;
            })
            .ToArray();

            var NextNumber = Enumerable.Range(1, FolderNumbers.Length + 1).Except(FolderNumbers).Min();

            var Suffix = FolderNumbers.Any() ? " " + NextNumber.ToString().PadLeft(3, '0') : "";

            Directory.Move(folder, path + "\\" + FolderName + Suffix);
            File.Delete(path + "\\" + FolderName + Suffix + "\\processing.txt");
        }

        public static string PDFFolderErrors(string ghostScriptPath, string folder)
        {
            File.WriteAllText(folder + "\\processing.txt", "");

            var PDFFiles = Directory.GetFiles(folder, "*.pdf");
            if (!PDFFiles.Any())
                return "There are no pdf files in " + folder;
            var FolderName = System.IO.Path.GetFileName(folder);
            var ParentPath = Directory.GetParent(Directory.GetParent(folder).ToString()).ToString();

            if (!Directory.Exists(ParentPath + "\\Merge"))
                Directory.CreateDirectory(ParentPath + "\\Merge");

            if (!Directory.Exists(ParentPath + "\\Large PDFs"))
                Directory.CreateDirectory(ParentPath + "\\Large PDFs");

            if (!Directory.Exists(ParentPath + "\\Errors"))
                Directory.CreateDirectory(ParentPath + "\\Errors");

            // Not empty
            if (!Directory.EnumerateFiles(folder, "*.pdf").Any())
                return "The folder does not contain any pdf files: " + folder;

            // No subfolders
            if (Directory.EnumerateDirectories(folder).Any())
                return "The folder contains a subdirectory: " + folder;

            // Just pdf files
            var Files = Directory.EnumerateFiles(folder).Where(x => System.IO.Path.GetFileName(x) != "processing.txt").ToArray();
            if (Files.Where(x => System.IO.Path.GetExtension(x).ToLower() != ".pdf").Any())
                return "The folder contains non-pdf file: " + folder;

            // Can be read by ITextSharp (valid pdf without password)
            foreach (var file in Files)
            {
                try
                {
                    using (var Reader = new PdfReader(file))
                    {
                        var NumberOfPages = Reader.NumberOfPages;

                        // Can be read by Ghostscript https://stackoverflow.com/a/3694338/7981551
                        if (ExecuteProcess(ghostScriptPath, "-dLastPage=\"1\" -dNOPAUSE -dNODISPLAY -o nul -sDEVICE=nullpage \"" + file + "\"")[1].Length > 0)
                            return "Ghostscript cannot read the file: " + file;

                        if (ExecuteProcess(ghostScriptPath, "-dFirstPage=\"" + NumberOfPages + "\" -dNOPAUSE -dNODISPLAY -o nul -sDEVICE=nullpage \"" + file + "\"")[1].Length > 0)
                            return "Ghostscript cannot read the file: " + file;
                    }
                }
                catch (Exception e)
                {
                    return "ITextSharp cannot read the file: " + file;
                }
            }

            // Not an active form or portfolio
            foreach (var file in Files)
            {
                using (var Reader = new PdfReader(file))
                {
                    if (Reader.AcroFields.Fields.Any())
                        return "The file has an AcroField Fields: " + file;

                    var Portfolio = Reader.Catalog.GetAsDict(PdfName.COLLECTION);
                    if (Portfolio != null)
                        return "The file has is a portfolio: " + file;
                }
            }

            return null;
        }

        private static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(@"verifypdfs ""path to folder containing the pdfs"" ""path to gswin64c.exe"".  E.g. verifypdfs ""c:\pdffolders\Process"" ""C:\Program Files\gs\gs9.21\bin\gswin64c.exe""");
                return;
            }

            var ProcessFolderPath = args[0].TrimEnd(new[] { '\\' });
            var GhostScriptPath = args[1];

            var Folder = Directory.GetDirectories(ProcessFolderPath)
                .Where(x => !Directory.GetFiles(x, "*.txt").Any(y => y.EndsWith("processing.txt")))
                .OrderBy(x => Directory.GetLastWriteTime(x)).FirstOrDefault();

            if (Folder == null)
            {
                Console.WriteLine("The process folder has no new subfolders: " + ProcessFolderPath);
                return;
            }

            var FolderName = System.IO.Path.GetFileName(Folder);
            var ParentPath = Directory.GetParent(Directory.GetParent(Folder).ToString()).ToString();
            var Error = PDFFolderErrors(GhostScriptPath, Folder);

            if (Error != null)
            {
                Console.WriteLine(Error);
                MoveFolder(Folder, ParentPath + "\\Errors");
            }
            else if (Directory.GetFiles(Folder).Select(x => new FileInfo(x).Length).Sum() > 1900000000)
                MoveFolder(Folder, ParentPath + "\\Large PDFs");
            else
                MoveFolder(Folder, ParentPath + "\\Merge");
        }
    }
}