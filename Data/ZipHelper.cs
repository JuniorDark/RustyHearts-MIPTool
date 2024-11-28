using System.IO.Compression;
using static RHMIPTool.Data.MIPCoder;

namespace RHMIPTool.Data
{
    public class ZipHelper
    {
        public static async Task CompressToZipPartsAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new ArgumentException("Folder path does not exist.", nameof(folderPath));
            }

            string outputRootFolder = Path.Combine("Output", "Zip");

            if (!Directory.Exists(outputRootFolder))
            {
                Directory.CreateDirectory(outputRootFolder);
            }

            List<string> files = [.. Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)];

            long totalBytes = files.Sum(file => new FileInfo(file).Length);
            long totalBytesProcessed = 0;

            long maxPartSize = 256 * 1024 * 1024; // 256 MB
            long currentPartSize = 0;
            int partNumber = 1;
            string zipFilePath = Path.Combine(outputRootFolder, $"gameclient_part{partNumber}.zip");

            FileStream zipFileStream = new(zipFilePath, FileMode.Create, FileAccess.Write);
            ZipArchive zip = new(zipFileStream, ZipArchiveMode.Create);

            try
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (currentPartSize >= maxPartSize)
                    {
                        zip.Dispose();
                        zipFileStream.Dispose();

                        partNumber++;
                        zipFilePath = Path.Combine(outputRootFolder, $"gameclient_part{partNumber}.zip");

                        // Open a new zip file and start writing
                        zipFileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write);
                        zip = new ZipArchive(zipFileStream, ZipArchiveMode.Create);

                        currentPartSize = 0;
                    }

                    // Create entry for each file in the archive
                    string entryName = Path.GetRelativePath(folderPath, file);
                    ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);

                    // Copy file data into the entry
                    using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
                    using Stream entryStream = entry.Open();
                    long fileLength = fs.Length;
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await entryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytesProcessed += bytesRead;
                        currentPartSize += bytesRead;

                        // Report progress
                        double progressPercentage = (double)totalBytesProcessed / totalBytes * 100;
                        progressPercentage = progressPercentage > 100 ? 100 : progressPercentage;
                        Console.Write($"\rProgress: {progressPercentage:F2}% complete");
                    }
                }
            }
            finally
            {
                zip?.Dispose();
                zipFileStream?.Dispose();
            }

            Console.WriteLine("\nCompression complete.");
            await CreateFileListAsync(outputRootFolder, cancellationToken);
        }
    }
}
