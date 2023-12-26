using Ionic.Zip;
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

            string zipFilePath = Path.Combine(outputRootFolder, "gameclient.zip");
            List<string> files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).ToList();

            long totalBytes = files.Sum(file => new FileInfo(file).Length);
            Dictionary<string, long> fileProgress = files.ToDictionary(file => file, file => 0L);
            long totalBytesProcessed = 0;

            using (ZipFile zip = new())
            {
                zip.MaxOutputSegmentSize = 256 * 1024 * 1024; // 256 MB
                zip.UseZip64WhenSaving = Zip64Option.AsNecessary;

                zip.AddDirectory(folderPath, "");
                zip.SaveProgress += (sender, e) =>
                {
                    if (e.EventType == ZipProgressEventType.Saving_EntryBytesRead)
                    {
                        fileProgress[e.CurrentEntry.FileName] = e.BytesTransferred;
                        totalBytesProcessed = fileProgress.Values.Sum();
                        double progressPercentage = (double)totalBytesProcessed / totalBytes * 100;
                        progressPercentage = progressPercentage > 100 ? 100 : progressPercentage;
                        Console.Write($"\rProgress: {progressPercentage:F2}% complete");
                    }
                };

                Console.WriteLine("Compressing files...");
                await Task.Run(() => zip.Save(zipFilePath), cancellationToken);
            }

            Console.WriteLine("\nCompression complete.");
            await CreateFileListAsync(outputRootFolder, cancellationToken);
        }

    }
}
