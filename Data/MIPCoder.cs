using System.IO.Compression;

namespace RHMIPTool.Data
{
    public class MIPCoder
    {
        public enum MIPCompressionMode
        {
            Compress,
            Decompress
        }

        public static async Task CompressToMipAsync(string folderPath, MIPCompressionMode compressionMode, int saveFrequency = 100, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new ArgumentException("Folder path does not exist.", nameof(folderPath));
            }

            string outputFolderName = compressionMode == MIPCompressionMode.Compress ? "MIP" : "Original";
            string outputRootFolder = Path.Combine("Output", outputFolderName);

            if (!Directory.Exists(outputRootFolder))
            {
                Directory.CreateDirectory(outputRootFolder);
            }

            // Create or load file database
            Dictionary<string, Tuple<long, string>> fileDatabase = new();
            string fileDatabasePath = Path.Combine(outputRootFolder, "filelist.txt");
            if (File.Exists(fileDatabasePath))
            {
                string[] fileDatabaseLines = await File.ReadAllLinesAsync(fileDatabasePath, cancellationToken);
                foreach (string line in fileDatabaseLines)
                {
                    string[] parts = line.Split(' ');

                    if (parts.Length >= 3)
                    {
                        string filePath = string.Join(' ', parts.Take(parts.Length - 2));

                        if (long.TryParse(parts[^2], out long fileSize))
                        {
                            fileDatabase.Add(filePath, new Tuple<long, string>(fileSize, parts[^1]));
                        }
                        else
                        {
                            Console.WriteLine($"Error parsing file size for '{filePath}'");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Invalid format for line: '{line}'");
                    }
                }
            }

            int totalFiles = 0;
            int currentTotalFiles = 0;
            int convertedFiles = 0;
            int skippedFiles = 0;
            int totalDeletedFiles = 0;
            int errorFiles = 0;

            List<string> files = Directory.EnumerateFiles(folderPath, ".", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            totalFiles = files.Count;

            Console.WriteLine("Processing...");

            // Get list of new files to convert
            List<string> newFiles = [];
            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(folderPath, file);
                string outputPath = compressionMode == MIPCompressionMode.Compress
                ? Path.Combine(outputRootFolder, relativePath) + ".mip"
                : Path.Combine(outputRootFolder, relativePath[..^4]);

                if (!fileDatabase.TryGetValue(relativePath, out Tuple<long, string>? info) ||
                info == null ||
                info.Item1 != new FileInfo(file).Length ||
                info.Item2 != Crc32.GetFileHash(file) ||
                !File.Exists(outputPath))
                {
                    newFiles.Add(file);
                }

                else
                {
                    skippedFiles++;
                }

            }

            // Update total files to include only new files
            currentTotalFiles = newFiles.Count;

            int filesProcessed = 0; // Counter for files processed since last save

            // Create log file
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.log");
            StreamWriter logFile = new(logFilePath, true);

            foreach (string file in newFiles)
            {
                FileInfo fileInfo = new(file);
                if (fileInfo.Length == 0)
                {
                    // Skip files with a size of 0 bytes
                    logFile.WriteLine("\nEmpty File skipped: {0}", file);
                    skippedFiles++;
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(folderPath, file);
                string outputPath = compressionMode == MIPCompressionMode.Compress
                ? Path.Combine(outputRootFolder, relativePath) + ".mip"
                : Path.Combine(outputRootFolder, relativePath[..^4]);

                try
                {
                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    }

                    if (compressionMode == MIPCompressionMode.Compress)
                    {
                        // Compress file
                        await CompressFileZlibAsync(file, outputPath, cancellationToken);
                    }
                    else if (compressionMode == MIPCompressionMode.Decompress)
                    {
                        // Decompress file
                        await DecompressFileZlibAsync(file, outputPath, cancellationToken);
                    }

                    // Update file database
                    long size = new FileInfo(file).Length;
                    string hashNew = Crc32.GetFileHash(file);
                    fileDatabase[relativePath] = new Tuple<long, string>(size, hashNew);

                    // Update progress after processing the file
                    convertedFiles++;
                    filesProcessed++;
                    double progress = (double)convertedFiles / currentTotalFiles * 100;
                    int roundedProgress = (int)Math.Round(progress);
                    const int maxFileNameLength = 50;
                    string fileNameDisplay = relativePath.Length > maxFileNameLength ?
                        "..." + relativePath[(relativePath.Length - maxFileNameLength + 3)..] :
                        relativePath.PadRight(maxFileNameLength);
                    Console.Write("\rConverting: {0}. Processed Files: {1}/{2} ({3}% complete)", fileNameDisplay, convertedFiles, currentTotalFiles, roundedProgress);


                    // Save file database periodically
                    if (filesProcessed == saveFrequency)
                    {
                        List<string> fileDatabaseLinesNew = new();
                        foreach (KeyValuePair<string, Tuple<long, string>> entry in fileDatabase)
                        {
                            fileDatabaseLinesNew.Add($"{entry.Key} {entry.Value.Item1} {entry.Value.Item2}");
                        }

                        try
                        {
                            await File.WriteAllLinesAsync(fileDatabasePath, fileDatabaseLinesNew, cancellationToken);
                            filesProcessed = 0;
                        }
                        catch (Exception ex)
                        {
                            logFile.WriteLine("Error saving file database: {0}", ex.Message);
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {
                    logFile.WriteLine("File not found: {0}", relativePath);
                    logFile.WriteLine("Exception: {0}", ex.Message);
                    errorFiles++;
                }
                catch (IOException ex)
                {
                    logFile.WriteLine("\nI/O error converting file: {0}", relativePath);
                    logFile.WriteLine("Exception: {0}", ex.Message);
                    errorFiles++;
                }
                catch (Exception ex)
                {
                    logFile.WriteLine("\nError converting file: {0}", relativePath);
                    logFile.WriteLine("Exception: {0}", ex.Message);
                    errorFiles++;
                }
            }

            // Remove deleted files from file database and output folder
            List<string> deletedFiles = [];
            foreach (KeyValuePair<string, Tuple<long, string>> entry in fileDatabase)
            {
                string filePath = Path.Combine(folderPath, entry.Key);
                if (!File.Exists(filePath))
                {
                    deletedFiles.Add(entry.Key);

                    string mipPath = Path.Combine(outputRootFolder, entry.Key) + ".mip";
                    if (File.Exists(mipPath))
                    {
                        try
                        {
                            File.Delete(mipPath);
                            totalDeletedFiles++;
                        }
                        catch (Exception ex)
                        {
                            logFile.WriteLine("\nError deleting file: {0}", mipPath);
                            logFile.WriteLine("Exception: {0}", ex.Message);
                        }
                    }
                }
            }
            foreach (string deletedFile in deletedFiles)
            {
                fileDatabase.Remove(deletedFile);
            }

            // Save file database
            List<string> fileDatabaseLinesFinal = [];
            foreach (KeyValuePair<string, Tuple<long, string>> entry in fileDatabase)
            {
                fileDatabaseLinesFinal.Add($"{entry.Key} {entry.Value.Item1} {entry.Value.Item2}");
            }
            try
            {
                await File.WriteAllLinesAsync(fileDatabasePath, fileDatabaseLinesFinal, cancellationToken);
            }
            catch (Exception ex)
            {
                logFile.WriteLine("Error saving file database: {0}", ex.Message);
            }

            logFile.Close();

            Console.WriteLine("\nCompression complete. Total files: {0}, Compressed files: {1}, Skipped files: {2}, Deleted files: {3}, Error files: {4}", totalFiles, convertedFiles, skippedFiles, totalDeletedFiles, errorFiles);

            if (errorFiles > 0)
            {
                Console.WriteLine("{0} file(s) failed to be converted. Check the error log for more info.", errorFiles);
            }
        }

        private static async Task CompressFileZlibAsync(string filePath, string outputPath, CancellationToken cancellationToken)
        {
            byte[] buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);

            // Check if the file is empty
            if (buffer.Length == 0)
            {
                return;
            }

            buffer = CompressBytesZlib(buffer);
            MIP.BytesWithCodeMip(buffer);
            await File.WriteAllBytesAsync(outputPath, buffer, cancellationToken);
        }

        public static byte[] CompressBytesZlib(byte[] toBytes)
        {
            using MemoryStream outputStream = new();
            using (ZLibStream deflateStream = new(outputStream, CompressionMode.Compress))
            {
                deflateStream.Write(toBytes, 0, toBytes.Length);
            }

            return outputStream.ToArray();
        }

        private static async Task DecompressFileZlibAsync(string filePath, string outputPath, CancellationToken cancellationToken)
        {
            byte[] buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);
            MIP.BytesWithCodeMip(buffer);
            buffer = DecompressBytesZlib(buffer);
            await File.WriteAllBytesAsync(outputPath, buffer, cancellationToken);
        }

        private static byte[] DecompressBytesZlib(byte[] toBytes)
        {
            using MemoryStream inputStream = new(toBytes);
            using ZLibStream inflateStream = new(inputStream, CompressionMode.Decompress);
            using MemoryStream outputStream = new();
            inflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }

        public static async Task CreateFileListAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new ArgumentException("Folder path does not exist.", nameof(folderPath));
            }

            string fileDatabasePath = Path.Combine(folderPath, "filelist.txt");

            // Retrieve all files in the specified folder
            List<string> files = [.. Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

            Console.WriteLine("Creating filelist...");

            // Create file database
            List<string> fileDatabaseLines = [];

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileInfo fileInfo = new(file);
                string relativePath = Path.GetRelativePath(folderPath, file);
                long size = fileInfo.Length;
                string hash = Crc32.GetFileHash(file);

                // Add file information to database
                fileDatabaseLines.Add($"{relativePath} {size} {hash}");
            }

            // Save file database
            try
            {
                await File.WriteAllLinesAsync(fileDatabasePath, fileDatabaseLines, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving file database: {0}", ex.Message);
            }

            Console.WriteLine("\nFile list creation complete. Total files: {0}", files.Count);
        }

    }
}