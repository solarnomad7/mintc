namespace MintCompiler
{
    static class Preprocessor
    {
        /// <summary>
        /// Preprocesses the given file, recursively handling included code files..
        /// </summary>
        /// <param name="filename">Path to code file</param>
        /// <returns>
        /// PreprocessorData object containing the merged code, files included by include directives in the
        /// topmost file, and all imported modules in the file tree (may contain duplicates).
        /// </returns>
        public static PreprocessorData Process(string filename)
        {
            string code = File.ReadAllText(filename);
            code = RemoveComments(code);
            PreprocessorData pData = HandleDirectives(code);

            for (int i = 0; i < pData.Includes.Count; i++)
            {
                PreprocessorData includeData = Preprocessor.Process(pData.Includes[i]);
                pData.Imports.AddRange(includeData.Imports);
                pData.ProcessedCode += "\n" + includeData.ProcessedCode;
            }

            return pData;
        }

        /// <summary>
        /// Removes all comments from raw code
        /// </summary>
        /// <param name="code">Raw code</param>
        /// <returns>Raw code with comments removed</returns>
        private static string RemoveComments(string code)
        {
            List<char> newCode = [];
            int nested = 0;

            foreach (char c in code)
            {
                if (c == '(') nested++;
                else if (c == ')' && nested > 0) nested--;
                else if (nested == 0) newCode.Add(c);
            }

            return String.Concat(newCode);
        }

        /// <summary>
        /// Handles include and import directives as well as removing extraneous whitespace.
        /// </summary>
        /// <param name="code">Raw code</param>
        /// <returns>
        /// PreprocessorData object containing the processed code, included files, and
        /// imported modules.
        /// </returns>
        private static PreprocessorData HandleDirectives(string code)
        {
            PreprocessorData pData = new();

            List<string> lines = [.. code.Split('\n')];

            string[] pLines = lines.Where(l => l.Trim().StartsWith('/')).ToArray();
            foreach (string line in pLines)
            {
                string[] tokens = line.Split(' ');

                switch (tokens[0])
                {
                    case "/include":
                        pData.Includes.Add(tokens[1].Trim());
                        break;
                    case "/import":
                        pData.Imports.Add(tokens[1].Trim());
                        break;
                }

                lines.Remove(line);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = lines[i].Trim();
            }

            pData.ProcessedCode = String.Join('\n', lines);
            return pData;
        }
    }

    public sealed class PreprocessorData
    {
        public string ProcessedCode { get; set; }
        public List<string> Includes { get; }
        public List<string> Imports { get; }

        public PreprocessorData()
        {
            ProcessedCode = "";
            Includes = [];
            Imports = [];
        }
    }
}