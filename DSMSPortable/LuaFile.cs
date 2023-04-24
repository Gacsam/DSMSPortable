using System.Text.RegularExpressions;

namespace DSMSPortable
{
    public class LuaFile
    {
        readonly string pattern = @$"(?<![-\t ])function (.[^()]*)\((.*)\).*((\n[-\t ]+.*)*\nend)";
        public Regex parse;
        public List<LuaString> source;
        public int head = 0;
        public string sourceFile;
        public LuaFile(string sourceFile)
        {
            this.sourceFile = sourceFile;
            source = new();
            parse = new Regex(pattern);
            string sourceText = File.ReadAllText(sourceFile).ReplaceLineEndings("\n");
            while (sourceText.Contains("\n\n")) sourceText = sourceText.Replace("\n\n", "\n");
            Dictionary<string, LuaFunction> functions = new();
            Dictionary<string, int> duplicates = new();
            foreach (Match match in parse.Matches(sourceText))
            {
                try
                {
                    functions.Add(match.Groups[1].Value, new LuaFunction(match.Value, match.Groups[1].Value, match.Groups[2].Value));
                }
                catch(Exception)
                {
                    Console.Error.WriteLine($@"Warning: Duplicate function detected: {match.Groups[1].Value}({match.Groups[2].Value})");
                    // Lua takes the most recent definition, so keep the duplicate
                    functions[match.Groups[1].Value] = new LuaFunction(match.Value, match.Groups[1].Value, match.Groups[2].Value);
                    if (!duplicates.ContainsKey(match.Groups[1].Value)) 
                        duplicates.Add(match.Groups[1].Value, 1);
                    else duplicates[match.Groups[1].Value]++;
                }
            }
            sourceText = parse.Replace(sourceText, "function $1\n");
            foreach (string line in sourceText.Split("\n"))
            {
                if (line.StartsWith("function"))
                {
                    string funcName = line.Split(" ", 2)[1];
                    // Don't write duplicates
                    if(duplicates.ContainsKey(funcName))
                    {
                        duplicates[funcName]--;
                        if (duplicates[funcName] == 0) duplicates.Remove(funcName);
                    }
                    else source.Add(functions[funcName]);
                    if(head == 0) head = source.Count - 1;
                }
                else source.Add(new LuaString(line));
            }
        }
        public bool AddFunction(LuaFunction newFunction)
        {
            foreach(LuaString line in source)
            {
                if (line is LuaFunction function)
                {
                    if(function.functionName == newFunction.functionName)
                    {
                        function.args = newFunction.args;
                        function.source = newFunction.source;
                        return true;
                    }
                }
            }
            source.Insert(head, newFunction);
            return false;
        }
        public override string ToString()
        {
            string output = "";
            foreach (LuaString line in source)
            {
                output += line.source + "\n";
                if (line is LuaFunction) output += "\n";
            }
            return output;
        }
        public void Save()
        {
            File.WriteAllText(sourceFile, ToString());
        }
        public void SaveAs(string newPath)
        {
            File.WriteAllText(newPath, ToString());
        }
    }

    public class LuaString
    {
        public string source;
        public LuaString(string source)
        {
            this.source = source;
        }
    }

    public class LuaFunction : LuaString
    {
        public string functionName;
        public string[] args;
        public LuaFunction(string source, string functionName, string args) : base(source)
        {
            this.functionName = functionName;
            this.args = args.Split(",");
            for (int i=0; i<this.args.Length; i++)
            {
                this.args[i] = this.args[i].Trim();
            }
        }

        public bool Equals(LuaFunction other)
        {
            if (functionName != other.functionName) return false;
            if (!args.SequenceEqual(other.args)) return false;
            List<string> comparableSource = new();
            List<string> comparableSourceOther = new();
            foreach (string line in source.Split("\n"))
            {
                string comparableLine = Regex.Replace(line, $@"--.*", "").Trim();
                if (comparableLine != "") comparableSource.Add(comparableLine);
            }
            foreach (string line in other.source.Split("\n"))
            {
                string comparableLine = Regex.Replace(line, $@"--.*", "").Trim();
                if (comparableLine != "") comparableSourceOther.Add(comparableLine);
            }
            return comparableSource.SequenceEqual(comparableSourceOther);
        }
    }
}
