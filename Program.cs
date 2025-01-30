using System.IO;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace MintCompiler
{
    static class Program
    {
        private static readonly string[] objFilePaths = new string[] {
            "/lib"
        };

        static void Main(string[] args)
        {
            string mode = args[0].ToLower();
            string source = args[1];

            if (mode == "-p")
            {
                string dest = args[2];
                File.WriteAllText(dest, Preprocessor.Process(source).ProcessedCode);
            }
            else if (mode == "-t")
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
            else if (mode == "-co")
            {
                string dest = args[2];
                PreprocessorData preprocessorData = Preprocessor.Process(source);
                Lexer lexer = new(preprocessorData.ProcessedCode);

                Compiler compiler = new(lexer);
                File.WriteAllBytes(dest, [.. compiler.Assemble()]);
            }
            else if (mode == "-cx")
            {
                string dest = args[2];
                PreprocessorData preprocessorData = Preprocessor.Process(source);
                Lexer lexer = new(preprocessorData.ProcessedCode);

                Compiler compiler = new(lexer);
                string tempObjPath = dest + ".mo";
                File.WriteAllBytes(tempObjPath, [.. compiler.Assemble()]);

                Linker linker = new();
                List<string> linkFiles = preprocessorData.Imports;
                linkFiles.Add(tempObjPath);

                File.WriteAllBytes(dest, [.. linker.Link(GetObjectFilePaths(linkFiles))]);
                File.Delete(tempObjPath);
            }
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
            }

            return paths;
        }
    }
}