using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using StudioCore;
using StudioCore.Editor;
using StudioCore.ParamEditor;

namespace DSMSPortable
{
    class DSMSPortable
    {
        // Check this file locally for the full gamepath
        static readonly string GAMEPATH_FILE = "gamepath.txt";
        static readonly string DEFAULT_ER_GAMEPATH = "Steam\\steamapps\\common\\ELDEN RING\\Game";
        static string gamepath = null;
        static ArrayList csvFiles;
        static ArrayList c2mFiles;
        static Dictionary<string,bool> masseditFiles;
        static GameType gameType = GameType.EldenRing;
        static string outputFile = null;
        static string inputFile = null;
        static string workingDirectory = null;
        static void Main(string[] args)
        {
            ArrayList sortingRows = new();
            masseditFiles = new();
            csvFiles = new ArrayList();
            c2mFiles = new ArrayList();
            string exePath = null;
            // Set culture to invariant, so doubles don't try to parse with floating commas
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            // Save the current working directory
            workingDirectory = Directory.GetCurrentDirectory();
            try
            {
                ProcessArgs(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(2);
            }
            // Check the input file given
            if (gameType == GameType.EldenRing && !(Path.GetFileName(inputFile).ToLower().Equals("regulation.bin") || File.Exists(inputFile + "\\regulation.bin")))
            {
                Console.Error.WriteLine("ERROR: Invalid regulation.bin given");
                Environment.Exit(4);
            }
            // Navigate to wherever our dependencies are and make that the working directory while we initialize
            if (!File.Exists("Assets\\GameOffsets\\ER\\ParamOffsets.txt"))
            {
                exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (File.Exists($@"{exePath}\Assets\GameOffsets\ER\ParamOffsets.txt"))
                {
                    Directory.SetCurrentDirectory(exePath);
                }
                else
                {
                    Console.Error.WriteLine("ERROR: Could not find param definition assets in current directory");
                    Environment.Exit(2);
                }
            }
            FindGamepath();
            if (gamepath == null)
            {
                Console.Error.WriteLine("ERROR: Could not find game directory");
                Environment.Exit(3);
            }
            if (inputFile == null)
            {
                Console.Error.WriteLine("ERROR: No param file specified as input");
                Environment.Exit(4);
            }
            ProjectSettings settings = new()
            {
                PartialParams = false,
                UseLooseParams = false,
                GameType = gameType
            };
            if (gamepath != null) settings.GameRoot = gamepath;
            NewProjectOptions options = new()
            {
                settings = settings,
                loadDefaultNames = false,
                directory = new FileInfo(inputFile).Directory.FullName
            };
            ActionManager manager = new();
            AssetLocator locator = new();
            locator.SetFromProjectSettings(settings, new FileInfo(inputFile).Directory.FullName);
            ParamBank.PrimaryBank.SetAssetLocator(locator);
            ParamBank.VanillaBank.SetAssetLocator(locator);
            // This operation takes time in a separate thread, so just wait and poll it
            ParamBank.ReloadParams(settings, options);
            Console.Out.Write("Loading Params");
            while (ParamBank.PrimaryBank.IsLoadingParams)
            {
                Thread.Sleep(500);
                Console.Out.Write(".");
            }
            // Switch back to the original working directory
            if (exePath != null)
            {
                Directory.SetCurrentDirectory(workingDirectory);
                locator.SetFromProjectSettings(settings, new FileInfo(inputFile).Directory.FullName);
            }
            Console.Out.Write("\n");
            MassEditResult meresult;
            string opstring;
            Console.Out.WriteLine("Patching Params...");
            // Perform conversions
            foreach (string c2mfile in c2mFiles)
            {
                string mfile = "";
                bool addition = false;
                opstring = File.ReadAllText(c2mfile);
                string paramName = Path.GetFileNameWithoutExtension(c2mfile);
                // Save a copy of the current param for comparison
                FSParam.Param oldParam = null;
                try
                {
                    oldParam = new(ParamBank.PrimaryBank.GetParamFromName(paramName));
                }
                catch (NullReferenceException)
                {
                    Console.Error.WriteLine($@"ERROR: '{paramName}' does not correspond to any params in {inputFile}");
                    Environment.Exit(5);
                }
                foreach (FSParam.Param.Row r in ParamBank.PrimaryBank.GetParamFromName(paramName).Rows)
                    oldParam.AddRow(new(r, oldParam));
                // Apply the given CSV edit
                meresult = MassParamEditCSV.PerformMassEdit(ParamBank.PrimaryBank, opstring, manager, paramName, true, false, ',');
                // Get the new param
                FSParam.Param newParam = ParamBank.PrimaryBank.GetParamFromName(paramName);
                if (meresult.Type == MassEditResultType.SUCCESS)
                {
                    // Compare every row for changes
                    foreach (FSParam.Param.Row row in newParam.Rows)
                    {
                        if (row == null) continue;
                        FSParam.Param.Row oldRow;
                        // Try to get the old param's contents at this ID
                        oldRow = oldParam[row.ID];
                        // if this row was newly added, use the default row for comparison
                        if (oldRow == null)
                        {
                            oldRow = new(oldParam.Rows.FirstOrDefault());
                            for (int i = 0; i < oldRow.CellHandles.Count; i++)
                            {
                                try
                                {
                                    oldRow.CellHandles[i].SetValue(oldRow.CellHandles[i].Def.Default);
                                }
                                catch (Exception) { }
                            }
                            addition = true;
                        }
                        // Compare the whole row
                        if (!row.DataEquals(oldRow))
                        {
                            // Grab the new name if needed
                            if(!row.Name.Equals(oldRow.Name))
                            {
                                mfile += $@"param {paramName}: id {row.ID}: Name: = {row.Name};" + "\n";
                            }
                            // if something is different, check each cell for changes
                            for (int i=0; i<row.CellHandles.Count; i++)
                            {
                                // Convert each individual change to massedit format
                                if (row.CellHandles[i].Value.GetType() == typeof(byte[]))
                                {
                                    string value = ParamUtils.Dummy8Write((byte[])row.CellHandles[i].Value);
                                    string oldvalue = ParamUtils.Dummy8Write((byte[])oldRow.CellHandles[i].Value);
                                    if(!value.Equals(oldvalue))
                                        mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {value};" + "\n";
                                }
                                else if (!row.CellHandles[i].Value.Equals(oldRow.CellHandles[i].Value))
                                    mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {row.CellHandles[i].Value};"+"\n";
                            }
                        }
                    }
                    // Write the output in the same directory as the CSV file provided, but change the extension
                    string outputFile = $@"{new FileInfo(c2mfile).Directory.FullName}\{paramName}.MASSEDIT";
                    try
                    {
                        File.WriteAllText(outputFile, mfile);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($@"Converting {Path.GetFileNameWithoutExtension(c2mfile)} FAILED: {e.Message}");
                    }
                    Console.Out.WriteLine($@"Converting {Path.GetFileNameWithoutExtension(c2mfile)} {meresult.Type}: {outputFile}");
                    if (addition) Console.Out.WriteLine("Note: Row additions detected, use the -M+ switch when loading this script.");
                    // Undo the edits we made to the param file
                    manager.UndoAction();
                }
                else Console.Error.WriteLine($@"Converting {Path.GetFileNameWithoutExtension(c2mfile)} {meresult.Type}: {meresult.Information}");
            }
            // Process CSV edits first
            foreach (string csvfile in csvFiles)
            {
                opstring = File.ReadAllText(csvfile);
                meresult = MassParamEditCSV.PerformMassEdit(ParamBank.PrimaryBank, opstring, manager, Path.GetFileNameWithoutExtension(csvfile), true, false, ',');
                if (meresult.Type == MassEditResultType.SUCCESS)
                {
                    // Remember this Param to sort later
                    if(!sortingRows.Contains(Path.GetFileNameWithoutExtension(csvfile))) 
                        sortingRows.Add(Path.GetFileNameWithoutExtension(csvfile));
                    Console.Out.WriteLine($@"{Path.GetFileNameWithoutExtension(csvfile)} {meresult.Type}: {meresult.Information}");
                }
                else Console.Error.WriteLine($@"{Path.GetFileNameWithoutExtension(csvfile)} {meresult.Type}: {meresult.Information}");
                if (meresult.Information.Contains(" 0 rows added")) Console.Out.WriteLine("Warning: Use MASSEDIT scripts for modifying existing params to avoid conflicts\n");
            }
            // Then process massedit scripts
            foreach (string mefile in masseditFiles.Keys)
            {
                opstring = File.ReadAllText(mefile).ReplaceLineEndings("\n").Trim();
                // MassEdit throws errors if there are any empty lines
                while (!opstring.Equals(opstring.Replace("\n\n", "\n")))
                    opstring = opstring.Replace("\n\n", "\n");
                // If this was added with the M+ switch, look ahead to see if any ID's need to be added first
                masseditFiles.TryGetValue(mefile, out bool masseditAddition);
                if (masseditAddition)
                {
                    StringReader reader = new(opstring);
                    string line;
                    string param;
                    int id = 0;
                    while ((line=reader.ReadLine()) != null)
                    {
                        // Strip the param name and ID from the entry (who doesn't love regexes?)
                        param = Regex.Match(line, $@"(?i)(?<=\bparam \b)(.[^:]*)(?=:)").Value;
                        Match idMatch = Regex.Match(line.ToLower(), $@"(?<=: id )(.[^:]*)(?=:)");
                        if (!idMatch.Success) continue;
                        id = int.Parse(idMatch.Value);
                        if (!ParamBank.PrimaryBank.Params.TryGetValue(param, out FSParam.Param value))
                        {
                            Console.Error.WriteLine("Warning: Could not find param by name of " + param);
                            continue;
                        }
                        if (value[id] == null)
                        {
                            FSParam.Param.Row newrow = new(value.Rows.FirstOrDefault());
                            for (int i = 0; i < newrow.CellHandles.Count; i++)
                            {
                                try
                                {
                                    newrow.CellHandles[i].SetValue(newrow.CellHandles[i].Def.Default);
                                }
                                catch (Exception) { }
                            }
                            newrow.ID = id;
                            value.AddRow(newrow);
                            // We added a row, flag this Param to be sorted later
                            if (!sortingRows.Contains(param)) sortingRows.Add(param);
                        }
                    }
                }
                // Perform the massedit operation
                (meresult, ActionManager tmp) = MassParamEditRegex.PerformMassEdit(ParamBank.PrimaryBank, opstring, new ParamEditorSelectionState());
                if (meresult.Type == MassEditResultType.SUCCESS) Console.Out.WriteLine($@"{Path.GetFileNameWithoutExtension(mefile)} {meresult.Type}: {meresult.Information}");
                else Console.Error.WriteLine($@"{Path.GetFileNameWithoutExtension(mefile)} {meresult.Type}: {meresult.Information}");
            }
            Console.Out.WriteLine("Sorting Rows...");
            foreach (string s in sortingRows)
            {
                MassParamEditOther.SortRows(ParamBank.PrimaryBank, s).Execute();
            }
            Console.Out.WriteLine("Saving param file...");
            try
            {
                ParamBank.PrimaryBank.SaveParams(false, false);
            }
            catch (Exception e)
            {
                try
                {   // Try to stick the landing if SaveParams finds itself unable to overwrite the param file
                    if (gameType == GameType.EldenRing)
                    {
                        File.Move($@"{new FileInfo(inputFile).Directory.FullName}\regulation.bin.temp", $@"{new FileInfo(inputFile).Directory.FullName}\regulation.bin");
                    }
                    else File.Move($@"{inputFile}.temp", inputFile);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                }
            }
            if (outputFile != null)
            {
                try
                {   // if an output file is specified, wing it by just copying the param file, and renaming the backup
                    if (gameType == GameType.EldenRing)
                    {
                        File.Move($@"{new FileInfo(inputFile).Directory.FullName}\regulation.bin", outputFile);
                        File.Move($@"{new FileInfo(inputFile).Directory.FullName}\regulation.bin.prev", $@"{new FileInfo(inputFile).Directory.FullName}\regulation.bin");
                    }
                    else if (File.Exists($@"{inputFile}.prev"))
                    {
                        File.Move(inputFile, outputFile);
                        File.Move($@"{inputFile}.prev", inputFile);
                    }
                    else File.Copy(inputFile, outputFile);
                }
                catch (Exception ioe)
                {
                    Console.Error.WriteLine(ioe.Message);
                }
            }
            Console.Out.WriteLine("Success!");
        }
        private static void FindGamepath()
        {
            // Check working directory for gamepath.txt, if it wasn't specified on the command line
            if (gamepath == null && File.Exists($@"{workingDirectory}\{GAMEPATH_FILE}"))
                gamepath = File.ReadAllText($@"{workingDirectory}\{GAMEPATH_FILE}");
            // Check default path for gamepath.txt (which will be the exe directory here)
            else if (gamepath == null && File.Exists(GAMEPATH_FILE)) 
                gamepath = File.ReadAllText(GAMEPATH_FILE);
            // If the game is Elden Ring, we can make more thorough checks
            if (gameType == GameType.EldenRing)
            {
                // If the gamepath we have already works, return
                if (gamepath != null && File.Exists($@"{gamepath}\EldenRing.exe")) return;
                // Double check to make sure the top level gamepath wasn't specified
                if (gamepath != null && File.Exists($@"{gamepath}\Game\EldenRing.exe"))
                {
                    gamepath = $@"{gamepath}\Game";
                    File.WriteAllText(GAMEPATH_FILE, gamepath);
                    return;
                }
                // Check the input file just incase we were given the game folder's regulation.bin
                if (File.Exists($@"{new FileInfo(inputFile).Directory.FullName}\EldenRing.exe"))
                {
                    gamepath = new FileInfo(inputFile).Directory.FullName;
                    return;
                }
                // Check Program Files
                if (File.Exists($@"{Environment.GetEnvironmentVariable("ProgramFiles")}\{DEFAULT_ER_GAMEPATH}\EldenRing.exe"))
                {
                    gamepath = $@"{Environment.GetEnvironmentVariable("ProgramFiles")}\{DEFAULT_ER_GAMEPATH}";
                    return;
                }
                // Check Program Files(x86)
                if (File.Exists($@"{Environment.GetEnvironmentVariable("ProgramFiles(x86)")}\{DEFAULT_ER_GAMEPATH}\EldenRing.exe"))
                {
                    gamepath = $@"{Environment.GetEnvironmentVariable("ProgramFiles(x86)")}\{DEFAULT_ER_GAMEPATH}";
                    return;
                }
                // We have no idea what the gamepath is, return null and throw an error
                gamepath = null;
                return;
            }
            // No gamepath specified, set it to the input file's directory as a fallback plan
            gamepath ??= new FileInfo(inputFile).Directory.FullName;
        }
        private static void ProcessArgs(string[] args)
        {
            if (args.Length == 0)
                Help();
            ParamMode mode = ParamMode.NONE;
            foreach (string param in args)
            {
                if (IsSwitch(param))
                {
                    switch (param.ToUpper()[1])
                    {
                        case 'C':
                            if (param.Length > 3 && (param[1..].ToUpper() == "C2M" || param[1..].ToLower() == "convert")) mode = ParamMode.C2M;
                            else mode = ParamMode.CSV;
                            break;
                        case 'M':
                            if (param.Length > 2 && param[2] == '+') mode = ParamMode.MASSEDITPLUS;
                            else mode = ParamMode.MASSEDIT;
                            break;
                        case 'O':
                            mode = ParamMode.OUTPUT;
                            break;
                        case 'G':
                            mode = ParamMode.SETGAMETYPE;
                            break;
                        case 'P':
                            mode = ParamMode.SETGAMEPATH;
                            break;
                        case 'H':
                        case '?':
                            Help();
                            break;
                        default:
                            Console.Error.WriteLine("ERROR: Invalid switch: " + param);
                            Environment.Exit(5);
                            break;
                    }
                }
                else
                {
                    switch (mode)
                    {
                        case ParamMode.CSV:
                            if (File.Exists(param) && (param.ToLower().EndsWith("csv") || param.ToLower().EndsWith("txt")))
                                csvFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid CSV filename given: " + param);
                            break;
                        case ParamMode.C2M:
                            if (File.Exists(param) && (param.ToLower().EndsWith("csv") || param.ToLower().EndsWith("txt")))
                                c2mFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid CSV filename given: " + param);
                            break;
                        case ParamMode.MASSEDIT:
                            if (File.Exists(param) && (param.ToLower().EndsWith("txt") || param.ToLower().EndsWith("massedit")))
                                masseditFiles.Add(param, false);
                            else Console.Out.WriteLine("Warning: Invalid MASSEDIT filename given: " + param);
                            break;
                        case ParamMode.MASSEDITPLUS:
                            if (File.Exists(param) && (param.ToLower().EndsWith("txt") || param.ToLower().EndsWith("massedit")))
                                masseditFiles.Add(param, true);
                            else Console.Out.WriteLine("Warning: Invalid MASSEDIT filename given: " + param);
                            break;
                        case ParamMode.OUTPUT:
                            if (outputFile != null)
                                throw new Exception("Multiple output paths specified at once: " + outputFile + " and " + param);
                            outputFile = param;
                            mode = ParamMode.NONE;
                            break;
                        case ParamMode.SETGAMETYPE:
                            switch (param.ToLower())
                            {
                                case "eldenring":
                                case "er":
                                    gameType = GameType.EldenRing;
                                    break;
                                case "dsiii":
                                case "darksoulsiii":
                                case "ds3":
                                case "darksouls3":
                                    gameType = GameType.DarkSoulsIII;
                                    break;
                                case "des":
                                case "demonsouls":
                                case "demonssouls":
                                    gameType = GameType.DemonsSouls;
                                    break;
                                case "ds1":
                                case "darksouls":
                                case "darksouls1":
                                    gameType = GameType.DarkSoulsPTDE;
                                    break;
                                case "ds1r":
                                case "ds1remastered":
                                    gameType = GameType.DarkSoulsRemastered;
                                    break;
                                case "bloodborn":
                                case "bloodborne":
                                case "bb":
                                    gameType = GameType.Bloodborne;
                                    break;
                                case "sekiro":
                                    gameType = GameType.Sekiro;
                                    break;
                                case "dsii":
                                case "darksoulsii":
                                case "ds2":
                                case "ds2s":
                                case "darksouls2":
                                    gameType = GameType.DarkSoulsIISOTFS;
                                    break;
                                default:
                                    gameType = GameType.Undefined;
                                    break;
                            }
                            break;
                        case ParamMode.SETGAMEPATH:
                            gamepath = param;
                            break;
                        case ParamMode.NONE:
                            if (param.ToLower().Equals("help") || param.Equals("?"))
                            {
                                Help();
                                break;
                            }
                            if (inputFile != null)
                            {
                                Console.Error.WriteLine("Multiple input files specified at once: " + inputFile + " and " + param);
                                Environment.Exit(4);
                            }
                            inputFile = param;
                            break;
                    }
                }
            }
        }

        private static void Help()
        {
            Console.Out.WriteLine("DSMS Portable by mountlover.");
            Console.Out.WriteLine("Lightweight utility for patching FromSoft param files. Free to distribute with other mods, but not for sale.");
            Console.Out.WriteLine("DS Map Studio Core developed and maintained by the SoulsMods team: https://github.com/soulsmods/DSMapStudio\n");
            Console.Out.WriteLine("Usage: DSMSPortable [paramfile] [-M[+] masseditfile1 masseditfile2 ...] [-C csvfile1 csvfile2 ...] [-G gametype]");
            Console.Out.WriteLine("                                [-P gamepath] [-O outputpath]\n");
            Console.Out.WriteLine("  paramfile  Path to regulation.bin file (or respective param file for other FromSoft games) to modify");
            Console.Out.WriteLine("  -M[+] masseditfile1 masseditfile2 ...");
            Console.Out.WriteLine("             List of text files (.TXT or .MASSEDIT) containing a script of DS Map Studio MASSEDIT commands.");
            Console.Out.WriteLine("             It is highly recommended to use massedit scripts to modify existing params to avoid conflicting edits.");
            Console.Out.WriteLine("             Edit scripts of the same type are processed in the order in which they are specified.");
            Console.Out.WriteLine("             If -M+ is specified, any individual ID's found that do not exist in the param file will be created and");
            Console.Out.WriteLine("             populated with default values (usually whatever is in the first entry of the param).");
            Console.Out.WriteLine("  -C csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             List of CSV files (.TXT or .CSV) containing entire rows of params to add.");
            Console.Out.WriteLine("             Each file's name must perfectly match the param it is modifying (i.e. SpEffectParam.csv).");
            Console.Out.WriteLine("             CSV edits will be always be processed before massedit scripts.");
            Console.Out.WriteLine("  -C2M csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             Converts the specified CSV files into .MASSEDIT scripts.");
            Console.Out.WriteLine("             Resulting files are saved in the same directories as the CSV's provided.");
            Console.Out.WriteLine("  -G gametype");
            Console.Out.WriteLine("             Code indicating which game is being modified. The default is Elden Ring. Options are as follows:");
            Console.Out.WriteLine("             DS1R  Dark Souls Remastered    DS2  Dark Souls 2    DS3     Dark Souls 3");
            Console.Out.WriteLine("             ER    Elden Ring               BB   Bloodborne      SEKIRO  Sekiro");
            Console.Out.WriteLine("             DS1   Dark Souls PTDE          DES  Demon's Souls");
            Console.Out.WriteLine("  -P gamepath");
            Console.Out.WriteLine("             Path to the main install directory for the selected game, for loading vanilla params.");
            Console.Out.WriteLine("             The gamepath can also be implicitly specified in a gamepath.txt file in the working directory.");
            Console.Out.WriteLine("  -O outputpath");
            Console.Out.WriteLine("             Path where the resulting regulation.bin (or equivalent param file) will be saved.");
            Console.Out.WriteLine("             If this is not specified, the input file will be overwritten, and a backup will be made if possible.");
            Environment.Exit(0);
        }
        // Indicates what the last read switch was
        private enum ParamMode
        {
            CSV,
            C2M,
            MASSEDIT,
            MASSEDITPLUS,
            OUTPUT,
            SETGAMETYPE,
            SETGAMEPATH,
            NONE
        }
        // No reason to be anal about the exact switch character used, any of these is fine
        private static bool IsSwitch(string arg)
        {
            return (arg[0] == '\\' || arg[0] == '/' || arg[0] == '-');
        }
    }
}
