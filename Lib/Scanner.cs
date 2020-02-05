﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using ConvertApiDotNet;

namespace Lib
{
    internal static class Scanner
    {
        /// <summary>
        /// Creates ConvertApiFileParam from local files (unzips if needed) and removes
        /// </summary>
        /// <param name="files">Files</param>
        /// <returns>Convertapi params</returns>
        public static IEnumerable<ConvertApiFileParam> GetFileParams(IEnumerable<FileInfo> files)
        {
            var filteredFiles = files.Where(f => !string.Equals(f.Name, "config.txt", StringComparison.InvariantCultureIgnoreCase));
            var tmpFiles = MoveFiles(filteredFiles);
            var readyFiles = PrepareFiles(tmpFiles);
            return FilesToParams(readyFiles);
        }

        private static List<FileInfo> MoveFiles(IEnumerable<FileInfo> filteredFiles)
        {
            var tempDir = new DirectoryInfo(Path.GetTempPath());
            if (filteredFiles.Any())
            {
                tempDir = CreateTempDir();
            }

            var tmpFiles = filteredFiles.Select(f =>
            {
                var tmpPath = Path.Combine(tempDir.FullName, f.Name);

                var retryNo = 0;
                while (true)
                {
                    try
                    {
                        f.MoveTo(tmpPath);
                        break;
                    }
                    catch (IOException e)
                    {
                        if (retryNo++ > 100)
                        {
                            Console.Error.WriteLine($"Unable access: {f.FullName}\n{e.Message}");
                            break;
                        }

                        Thread.Sleep(500);
                    }
                }

                return new FileInfo(tmpPath);
            }).ToList();
            return tmpFiles;
        }

        private static List<FileInfo> PrepareFiles(List<FileInfo> tmpFiles)
        {
            var readyFiles = tmpFiles.SelectMany(f =>
            {
                var result = new List<FileInfo>();
                var ext = f.Extension.Replace(".", "");
                if (ext.Equals("zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    using (var zip = ZipFile.OpenRead(f.FullName))
                    {
                        var zipDir = CreateTempDir();
                        zip.ExtractToDirectory(zipDir.FullName);
                        result.AddRange(zipDir.GetFiles());
                    }
                    f.Directory.Delete(true);
                }
                else
                {
                    result.Add(f);
                }

                return result;
            }).ToList();
            return readyFiles;
        }

        private static List<ConvertApiFileParam> FilesToParams(List<FileInfo> readyFiles)
        {
            return readyFiles.Select(f =>
            {
                var fp = new ConvertApiFileParam(f);

                // Delete uploaded file from local file system
                fp.GetValueAsync().ContinueWith(fm =>
                {
                    try
                    {
                        var dir = f.Directory;
                        f.Delete();
                        if (!dir.GetFileSystemInfos().Any()) dir.Delete();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.Message);
                    }
                });

                return fp;
            }).ToList();
        }

        private static DirectoryInfo CreateTempDir()
        {
            var uniqueTempDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "convertapi-automator", Guid.NewGuid().ToString()));
            return Directory.CreateDirectory(uniqueTempDir);
        }
    }
}