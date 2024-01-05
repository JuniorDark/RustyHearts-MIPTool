using static RHMIPTool.Data.MIPCoder;
using static RHMIPTool.Data.ZipHelper;
/*
Rusty Hearts MIP Tool - Implementation in C#
Author: JuniorDark
GitHub Repository: https://github.com/JuniorDark/RustyHearts-MIPTool
*/
namespace RHMIPTool
{
    internal class Program
    {
        private const string InputFolderPath = "Input";
        private const string EmptyInputMessage = "The 'Input' folder is empty. There is nothing to do.";
        private const string PressKeyToExitMessage = "Press any key to exit...";
        private const string InvalidChoiceMessage = "Invalid choice.";

        private static async Task Main()
        {
            Console.WriteLine("Rusty Hearts MIP Tool by JuniorDark\n");
            Console.WriteLine("Version: 2.0\n");

            try
            {
                string inputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, InputFolderPath);
                EnsureInputDirectoryExists(inputPath);

                if (IsInputDirectoryEmpty(inputPath))
                {
                    Console.WriteLine(EmptyInputMessage);
                    WaitForKeyPress();
                    return;
                }

                while (true)
                {
                    Console.WriteLine("Choose an option:");
                    Console.WriteLine("1. Compress to MIP - Compress files to MIP format");
                    Console.WriteLine("2. Decompress MIP - Decompress MIP files");
                    Console.WriteLine("3. Compress files - Compress files to ZIP format, can be used for creating client download files");
                    Console.WriteLine("4. Generate filelist - Generate a filelist of files in the 'Input' folder with name, size and hash");
                    Console.WriteLine("5. Exit");

                    string? choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            await CompressToMipAsync(inputPath, MIPCompressionMode.Compress);
                            break;
                        case "2":
                            await CompressToMipAsync(inputPath, MIPCompressionMode.Decompress);
                            break;
                        case "3":
                            await CompressToZipPartsAsync(inputPath);
                            break;
                        case "4":
                            await CreateFileListAsync(inputPath);
                            break;
                        case "5":
                            return;
                        default:
                            Console.WriteLine(InvalidChoiceMessage);
                            continue;
                    }

                    break;
                }

                Console.WriteLine("Done!\n\n" + PressKeyToExitMessage);
                WaitForKeyPress();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                WaitForKeyPress();
            }
        }

        private static void EnsureInputDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static bool IsInputDirectoryEmpty(string path)
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length == 0;
        }

        private static void WaitForKeyPress()
        {
            Console.WriteLine(PressKeyToExitMessage);
            Console.ReadKey();
        }
    }

}
