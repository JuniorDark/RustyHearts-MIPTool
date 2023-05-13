/*
Rusty Hearts MIP Tool - Implementation in C#
Author: JuniorDark
GitHub Repository: https://github.com/JuniorDark/RustyHearts-MIPTool
This tool requires further development to improve functionality and ensure stability. 
Please check the GitHub repository for updates.
*/

namespace RHMIPTool
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string InputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Input");

            if (!Directory.Exists(InputPath))
            {
                Directory.CreateDirectory(InputPath);
            }

            if (Directory.GetFiles(InputPath, "*", SearchOption.AllDirectories).Length == 0)
            {
                Console.WriteLine("Rusty Hearts MIP Tool by JuniorDark\n");
                Console.WriteLine("The 'Input' folder is empty. There is nothing to do.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Rusty Hearts MIP Tool by JuniorDark\n");

            string? choice;
            while (true)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Compress to MIP");
                Console.WriteLine("2. Decompress MIP");
                Console.WriteLine("3. Exit");

                choice = Console.ReadLine();

                if (choice == "1")
                {
                    Console.WriteLine("\nSelect a compression format:");
                    Console.WriteLine("1. Deflate");
                    Console.WriteLine("2. Zlib (Original MIP compression format. May have issues with some files.)");
                    string? compressionTypeChoice = Console.ReadLine();

                    if (compressionTypeChoice == "1")
                    {
                        await MIPCoder.CompressToMipAsync(InputPath, CompressionType.Deflate);
                        
                    }
                    else if (compressionTypeChoice == "2")
                    {
                        await MIPCoder.CompressToMipAsync(InputPath, CompressionType.Zlib);
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice.");
                        continue;
                    }

                    break;
                }
                else if (choice == "2")
                {
                    Console.WriteLine("\nSelect the decompression format:");
                    Console.WriteLine("1. Deflate");
                    Console.WriteLine("2. Zlib (Original MIP compression format. May have issues with some files.)");
                    string? decompressionTypeChoice = Console.ReadLine();

                    if (decompressionTypeChoice == "1")
                    {
                        await MIPDecoder.DecompressMipAsync(InputPath, CompressionType.Deflate);

                    }
                    else if (decompressionTypeChoice == "2")
                    {
                        await MIPDecoder.DecompressMipAsync(InputPath, CompressionType.Zlib);
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice.");
                        continue;
                    }

                    break;
                }
                else if (choice == "3")
                {
                    return;
                }
                else
                {
                    Console.WriteLine("Invalid choice.");
                    continue;
                }
            }

            Console.WriteLine("Done!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        public enum CompressionType
        {
            Zlib,
            Deflate
        }

    }
}
