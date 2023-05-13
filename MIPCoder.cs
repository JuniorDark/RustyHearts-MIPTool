using RHMIPTool;
using System.IO.Compression;
using static RHMIPTool.Program;

internal class MIPCoder
{
    public static async Task CompressToMipAsync(string folderPath, CompressionType compressionType, int saveFrequency = 100, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new ArgumentException("Folder path does not exist.", nameof(folderPath));
        }

        string outputRootFolder = Path.Combine("Output", "MIP");

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
                fileDatabase.Add(parts[0], new Tuple<long, string>(long.Parse(parts[1]), parts[2]));
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
        List<string> newFiles = new();
        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(folderPath, file);
            string outputPath = Path.Combine(outputRootFolder, relativePath) + ".mip";

            if (!fileDatabase.TryGetValue(relativePath, out Tuple<long, string> info) ||
                info.Item1 != new FileInfo(file).Length ||
                info.Item2 != MIP.GetFileHash(file) ||
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
        string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        StreamWriter logFile = new(logFilePath, true);

        foreach (string file in newFiles)
        {
            if (file.Length == 0)
            {
                // Skip files with a size of 0 bytes
                skippedFiles++;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(folderPath, file);
            string outputPath = Path.Combine(outputRootFolder, relativePath) + ".mip";

            try
            {
                // Convert file to mip
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                if (compressionType == CompressionType.Zlib)
                {
                    await CompressFileZlibAsync(file, outputPath, cancellationToken);
                }
                else if (compressionType == CompressionType.Deflate)
                {
                    await CompressFileDeflateAsync(file, outputPath, cancellationToken);
                }
                else
                {
                    throw new ArgumentException("Invalid compression type", nameof(compressionType));
                }

                // Update file database
                long size = new FileInfo(file).Length;
                string hashNew = MIP.GetFileHash(file);
                fileDatabase[relativePath] = new Tuple<long, string>(size, hashNew);

                // Update progress after processing the file
                convertedFiles++;
                filesProcessed++;
                double progress = (double)convertedFiles / currentTotalFiles * 100;
                int roundedProgress = (int)Math.Round(progress);
                const int maxFileNameLength = 50;
                string fileNameDisplay = relativePath.Length > maxFileNameLength ?
                    "..." + relativePath.Substring(relativePath.Length - maxFileNameLength + 3) :
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
        List<string> deletedFiles = new();
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
        List<string> fileDatabaseLinesFinal = new();
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

    private static async Task CompressFileDeflateAsync(string filePath, string outputPath, CancellationToken cancellationToken)
    {
        byte[] buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);
        buffer = CompressBytesDeflate(buffer);
        MIP.BytesWithCodeMip(buffer);
        await File.WriteAllBytesAsync(outputPath, buffer, cancellationToken);
    }

    public static byte[] CompressBytesDeflate(byte[] toBytes)
    {
        using MemoryStream inputStream = new(toBytes);
        using MemoryStream outputStream = new();
        using DeflateStream deflateStream = new(outputStream, CompressionLevel.Optimal);
        inputStream.CopyTo(deflateStream);
        deflateStream.Close();
        return outputStream.ToArray();
    }

    private static async Task CompressFileZlibAsync(string filePath, string outputPath, CancellationToken cancellationToken)
    {
        byte[] buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);
        buffer = CompressBytesZlib(buffer);
        MIP.BytesWithCodeMip(buffer);
        await File.WriteAllBytesAsync(outputPath, buffer, cancellationToken);
    }

    public static byte[] CompressBytesZlib(byte[] toBytes)
    {
        int num = (toBytes.Length << 4) - toBytes.Length;
        byte[] buffer = new byte[num];
        int err = ZLibDll.Compress(toBytes, toBytes.Length, buffer, ref num);
        if (err != 0) throw new Exception(string.Format("Zlib compress returned error code {0}.", err));
        toBytes = new byte[num];
        Buffer.BlockCopy(buffer, 0, toBytes, 0, num);
        return toBytes;
    }
}