using System.IO;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace MintCompiler
{
    static class Program
    {
        private static readonly string[] objFilePaths = [
            "lib"
        ];

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                DisplayHelp();
                return;
            }

            string mode = args[0].ToLower();
            string source = args[1];

            if (mode == "-p" && args.Length == 2)
            {
                string dest = args[2];
                File.WriteAllText(dest, Preprocessor.Process(source).ProcessedCode);
            }
            else if (mode == "-t" && args.Length == 2)
            {
                string code = Preprocessor.Process(source).ProcessedCode;
                Lexer lexer = new(code);
                lexer.Tokenize();

                Token t = lexer.NextToken();
                while (t.Type != TokenType.EOF)
                {
                    Console.WriteLine(t.Type.ToString() + ": " + t.Content);
                    t = lexer.NextToken();
                }
            }
            else if (mode == "-co" && args.Length == 3)
            {
                string dest = args[2];
                PreprocessorData preprocessorData = Preprocessor.Process(source);
                Lexer lexer = new(preprocessorData.ProcessedCode);

                HandleCompiler(dest, lexer);
            }
            else if (mode == "-cx" && args.Length == 3)
            {
                string dest = args[2];
                PreprocessorData preprocessorData = Preprocessor.Process(source);
                Lexer lexer = new(preprocessorData.ProcessedCode);

                string tempObjPath = dest + ".mo";
                if(HandleCompiler(tempObjPath, lexer))
                {
                    List<string> linkFiles = preprocessorData.Imports;
                    linkFiles.Add(tempObjPath);

                    HandleLinker(dest, linkFiles);
                    File.Delete(tempObjPath);
                }
            }
        }

        static bool HandleCompiler(string destFile, Lexer lexer)
        {
            Compiler compiler = new(lexer);
            File.WriteAllBytes(destFile, [.. compiler.Assemble()]);
            
            // TODO: handle exceptions
            return true;
        }

        static void HandleLinker(string destFile, List<string> linkFiles)
        {
            Linker linker = new();
            File.WriteAllBytes(destFile, [.. linker.Link(GetObjectFilePaths(linkFiles))]);
        }

        static List<string> GetObjectFilePaths(List<string> files)
        {
            List<string> paths = [];

            foreach (string file in files)
            {
                if (!File.Exists(file)) // File not found in working directory
                {
                    foreach (string searchPath in objFilePaths)
                    {
                        string workingDir = Directory.GetCurrentDirectory();
                        string path = Path.Combine(workingDir, searchPath, file);

                        if (!File.Exists(path))
                        {
                            Console.WriteLine("mintc: Could not find " + file);
                        }
                        else
                        {
                            paths.Add(path);
                        }
                    }
                }
                else
                {
                    paths.Add(file);
                }
            }

            return paths;
        }

        static void DisplayHelp()
        {
            Console.WriteLine("Usage: mintc [mode] <files...>");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("\t-co <source> <destination>");
            Console.WriteLine("\t\tCompile an object or library file.");
            Console.WriteLine("\t-cx <source> <destination>");
            Console.WriteLine("\t\tCompile and link an executable.");
            Console.WriteLine("\t-p <source> <destination>");
            Console.WriteLine("\t\tPreprocess a source file.");
            Console.WriteLine("\t-t <source>");
            Console.WriteLine("\t\tTokenize a source file (for debugging purposes).");
            Console.WriteLine();
        }
    }
}