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
        static readonly string ER_PARAMFILE_NAME = "regulation.bin";
        static readonly string DS2_PARAMFILE_NAME = "enc_regulation.bnd.dcx";
        static readonly string DS3_PARAMFILE_NAME = "Data0.bdt";
        static readonly string OTHER_PARAMFILE_NAME = "gameparam.parambnd.dcx";
        static readonly string OTHER_PARAMFILE_PATH = "param\\gameparam";
        static string gamepath = null;
        static ArrayList csvFiles;
        static ArrayList c2mFiles;
        static ArrayList masseditFiles;
        static ArrayList masseditpFiles;
        static ArrayList sortingRows;
        static ArrayList exportParams = null;
        static ActionManager manager;
        static GameType gameType = GameType.EldenRing;
        static string paramFileName;
        static string paramFileRelPath = "";
        static string outputFile = null;
        static string inputFile = null;
        static string workingDirectory = null;
        static string compareParamFile = null;
        static string upgradeRefParamFile = null;
        static bool gametypeContext = false;
        static bool folderMimic = false;
        static bool changesMade = false;
        static void Main(string[] args)
        {
            masseditFiles = new();
            masseditpFiles = new();
            csvFiles = new();
            c2mFiles = new();
            sortingRows = new();
            manager = new();
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
            GetParamName();
            // Check the input file given
            if (inputFile == null)
            {
                if (gametypeContext)
                {
                    PrintGameContext();
                    return;
                }
                else
                {
                    Console.Error.WriteLine("ERROR: No param file specified as input");
                    Environment.Exit(2);
                }
            }
            try
            {
                MimicDirectory(inputFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                Environment.Exit(10);
            }
            FindGamepath();
            if (gamepath == null)
            {
                Console.Error.WriteLine("ERROR: Could not find game directory");
                Environment.Exit(3);
            }
            LoadParams();
            // Perform Param Upgrade
            if (upgradeRefParamFile != null)
            {
                Console.Out.WriteLine("Upgrading Params...");
                UpgradeParamFile();
            }
            // Perform conversions
            if (c2mFiles.Count > 0)
            {
                Console.Out.WriteLine("Converting CSV files to MASSEDIT...");
                ProcessCSVToMassedit();
            }
            // Process CSV edits first
            if (csvFiles.Count > 0)
            {
                Console.Out.WriteLine("Performing CSV edits...");
                ProcessCSV();
            }
            // Then process massedit+ scripts
            if (masseditpFiles.Count > 0)
            {
                Console.Out.WriteLine("Processing MASSEDIT scripts with row additions...");
                ProcessMasseditWithAddition();
            }
            // Then sort all our row additions
            if (sortingRows.Count > 0)
            {
                Console.Out.WriteLine("Sorting Added Rows...");
                foreach (string s in sortingRows)
                {
                    MassParamEditOther.SortRows(ParamBank.PrimaryBank, s).Execute();
                }
            }
            // Then process normal massedit scripts
            if (masseditFiles.Count > 0)
            {
                Console.Out.WriteLine("Processing MASSEDIT scripts...");
                ProcessMassedit();
            }
            // Save changes if we made any
            if (changesMade)
            {
                Console.Out.WriteLine("Saving param file...");
                SaveParamFile();
            }
            // Perform CSV export if one was specified
            if (exportParams != null)
            {
                Console.Out.WriteLine("Exporting Params to CSV...");
                ExportParams();
            }
            // Perform diff and convert changes to massedit, if a file to compare against was specified
            if (compareParamFile != null)
            {
                Console.Out.WriteLine("Converting changes to MASSEDIT...");
                ConvertDiffsToMassedit();
            }
        }
        public static bool ConvertToMassedit(FSParam.Param oldParam, FSParam.Param newParam, string paramName, out string mfile)
        {
            mfile = "";
            bool addition = false;
            ArrayList ids = new();
            // Compare every row for changes
            foreach (FSParam.Param.Row row in newParam.Rows)
            {
                if (row == null) continue;
                // ignore duplicate rows
                if (ids.Contains(row.ID)) continue;
                else ids.Add(row.ID);
                FSParam.Param.Row oldRow;
                // Try to get the old param's contents at this ID
                oldRow = oldParam[row.ID];
                // if this row was newly added, use the default row for comparison
                if (oldRow == null)
                {
                    oldRow = AddNewRow(row.ID, oldParam);
                    addition = true;
                }
                // Compare the whole row
                if (!row.DataEquals(oldRow))
                {
                    // Grab the new name if needed
                    if (row.Name.Replace("\r", "") != oldRow.Name.Replace("\r", ""))
                    {
                        mfile += $@"param {paramName}: id {row.ID}: Name: = {row.Name.Replace("\r", "")};" + "\n";
                    }
                    // if something is different, check each cell for changes
                    for (int i = 0; i < row.CellHandles.Count; i++)
                    {
                        // Convert each individual change to massedit format
                        if (row.CellHandles[i].Value.GetType() == typeof(byte[]))
                        {
                            string value = ParamUtils.Dummy8Write((byte[])row.CellHandles[i].Value);
                            string oldvalue = ParamUtils.Dummy8Write((byte[])oldRow.CellHandles[i].Value);
                            if (!value.Equals(oldvalue))
                                mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {value};" + "\n";
                        }
                        else if (!row.CellHandles[i].Value.Equals(oldRow.CellHandles[i].Value))
                            mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {row.CellHandles[i].Value};" + "\n";
                    }
                }
            }
            return addition;
        }
        public static FSParam.Param.Row AddNewRow(int id, FSParam.Param param)
        {
            if (param[id] != null) return null;
            FSParam.Param.Row newRow = new(param.Rows.FirstOrDefault());
            for (int i = 0; i < newRow.CellHandles.Count; i++)
            {   // Def.Default is just always 0. s32's where the minimum is explicitly -1 reference ID's so -1 makes a better default
                if ((newRow.CellHandles[i].Def.DisplayType == SoulsFormats.PARAMDEF.DefType.s32 || newRow.CellHandles[i].Def.DisplayType == SoulsFormats.PARAMDEF.DefType.s16)
                    && (int)newRow.CellHandles[i].Def.Minimum == -1)
                    newRow.CellHandles[i].SetValue(Convert.ChangeType(newRow.CellHandles[i].Def.Minimum, newRow.CellHandles[i].Value.GetType()));
                else if (newRow.CellHandles[i].Def.Default != null)
                    newRow.CellHandles[i].SetValue(Convert.ChangeType(newRow.CellHandles[i].Def.Default, newRow.CellHandles[i].Value.GetType()));
            }
            newRow.ID = id;
            param.AddRow(newRow);
            return newRow;
        }
        private static void LoadParams()
        {
            string exePath = null;
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
            AssetLocator locator = new();
            locator.SetFromProjectSettings(settings, new FileInfo(inputFile).Directory.FullName);
            ParamBank.PrimaryBank.SetAssetLocator(locator);
            ParamBank.VanillaBank.SetAssetLocator(locator);
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
                    Environment.Exit(4);
                }
            }
            // This operation takes time in a separate thread, so just wait and poll it
            ParamBank.ReloadParams(settings, options);
            Console.Out.Write("Loading Params");
            int timeout = 0;
            while (ParamBank.PrimaryBank.IsLoadingParams || ParamBank.VanillaBank.IsLoadingParams)
            {
                timeout++;
                if (timeout > 20)
                {
                    Console.Out.WriteLine("Failed due to timeout, ensure param file is valid and that the gamepath is specified.");
                    Environment.Exit(7);
                }
                Thread.Sleep(500);
                Console.Out.Write(".");
            }
            // Switch back to the original working directory
            if (exePath != null) Directory.SetCurrentDirectory(workingDirectory);
            Console.Out.WriteLine("Done!");
        }
        private static void UpgradeParamFile()
        {
            try
            {
                Dictionary<string, HashSet<int>> conflicts = new();
                ParamBank.ParamUpgradeResult result = ParamBank.PrimaryBank.UpgradeRegulation(ParamBank.VanillaBank, upgradeRefParamFile, conflicts);
                switch (result)
                {
                    case ParamBank.ParamUpgradeResult.Success:
                        Console.Out.WriteLine("Params Upgraded!");
                        changesMade = true;
                        break;
                    case ParamBank.ParamUpgradeResult.RowConflictsFound:
                        Console.Out.WriteLine("Warning: Conflicts found on the following rows:");
                        foreach (string confParam in conflicts.Keys)
                        {
                            foreach(int confId in conflicts[confParam])
                            {
                                Console.Out.WriteLine("\t" + $@"param {confParam}: id {confId}");
                            }
                        }
                        Console.Out.WriteLine("These rows were not able to be updated.");
                        changesMade = true;
                        break;
                    case ParamBank.ParamUpgradeResult.OldRegulationVersionMismatch:
                        Console.Error.WriteLine("ERROR: Invalid Reference Param File specified");
                        break;
                    case ParamBank.ParamUpgradeResult.OldRegulationNotFound:
                        Console.Error.WriteLine("ERROR: Could not find " + upgradeRefParamFile);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
            }
        }
        private static void ExportParams()
        {
            string param = null;
            string query;
            // Empty list is a special case. Mass export all.
            if(exportParams.Count == 0)
            {
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {
                    exportParams.Add(p);
                }
            }
            // Read each argument, check for a query, and generate CSV
            foreach (string exportParam in exportParams)
            {
                query = "";
                if (!exportParam.Contains(':')) 
                    param = exportParam.Trim();
                else
                {
                    param = exportParam.Split(':', 2)[0].Trim();
                    query = exportParam.Split(':', 2)[1].Trim();
                }
                // Check for param name
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {
                    if (param.ToLower() == p.ToLower())
                    {
                        param = p;
                        break;
                    }
                }
                Console.Out.Write($@"Exporting {param}... ");
                List<FSParam.Param.Row> rows = RowSearchEngine.rse.Search(ParamBank.PrimaryBank.GetParamFromName(param), query, true, true);
                string output = MassParamEditCSV.GenerateCSV(rows, ParamBank.PrimaryBank.GetParamFromName(param), ',');
                // Write the output in the same directory as the param file provided, unless a valid output path was specified
                string csvOutFile = $@"{new FileInfo(inputFile).Directory.FullName}\{param}.csv";
                if (outputFile != null && File.Exists(outputFile))
                    csvOutFile = $@"{new FileInfo(outputFile).Directory.FullName}\{param}.csv";
                else if(outputFile != null && Directory.Exists(outputFile))
                    csvOutFile = $@"{outputFile}\{param}.csv";
                try
                {
                    File.WriteAllText(csvOutFile, output);
                    Console.Out.WriteLine($@"SUCCESS:");
                    Console.Out.WriteLine("\t" + csvOutFile);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($@"FAILED: {e.Message}");
                }
            }
        }
        private static void MimicDirectory(string paramfile)
        {
            // Mimic the param folder structure if needed
            string inputDir = new FileInfo(paramfile).Directory.FullName;
            if (gameType != GameType.EldenRing && gameType != GameType.DarkSoulsIISOTFS && gameType != GameType.DarkSoulsIII)
            {
                if (File.Exists(paramfile))
                {
                    Directory.CreateDirectory($@"{inputDir}\{paramFileRelPath}");
                    if (File.Exists($@"{inputDir}\{paramFileRelPath}{paramFileName}"))
                        File.Move($@"{inputDir}\{paramFileRelPath}{paramFileName}", $@"{inputDir}\{paramFileRelPath}{paramFileName}.tmp", true);
                    File.Move(paramfile, $@"{inputDir}\{paramFileRelPath}{paramFileName}");
                    folderMimic = true;
                }
                else if (!File.Exists($@"{inputDir}\{paramFileRelPath}{paramFileName}"))
                {
                    Console.Error.WriteLine($@"ERROR: Cannot find {paramFileName}");
                    Environment.Exit(2);
                }
            } // Check to make sure the expected param file exists
            else if (!Path.GetFileName(paramfile).ToLower().Equals(paramFileName) && !File.Exists(paramfile + "\\" + paramFileName))
            {
                if(File.Exists(paramfile) && File.Exists(inputDir + "\\" + paramFileName))
                {
                    File.Move(inputDir + "\\" + paramFileName, inputDir + "\\" + paramFileName + ".tmp", true);
                    File.Move(paramfile, inputDir + "\\" + paramFileName);
                }
                else if(File.Exists(paramfile))
                {
                    File.Move(paramfile, inputDir + "\\" + paramFileName);
                }
                else
                {
                    Console.Error.WriteLine($@"ERROR: Cannot find {paramFileName}");
                    Environment.Exit(2);
                }
            }
        }
        private static void UnmimicDirectory(string paramfile)
        {
            string paramFileDir = new FileInfo(paramfile).Directory.FullName;
            if (new FileInfo(paramfile).FullName != $@"{paramFileDir}\{paramFileRelPath}{paramFileName}") try
            {   // if we mimicked the folder structure, revert it back to normal
                File.Move($@"{paramFileDir}\{paramFileRelPath}{paramFileName}", paramfile);
                if(File.Exists($@"{paramFileDir}\{paramFileRelPath}{paramFileName}.tmp"))
                    File.Move($@"{paramFileDir}\{paramFileRelPath}{paramFileName}.tmp", $@"{paramFileDir}\{paramFileRelPath}{paramFileName}");
                if (folderMimic && Directory.GetFiles($@"{paramFileDir}\{paramFileRelPath}").Length == 0)
                    Directory.Delete($@"{paramFileDir}\{paramFileRelPath}");
                if (folderMimic && Directory.GetFiles($@"{paramFileDir}\param").Length == 0)
                    Directory.Delete($@"{paramFileDir}\param");
            }
            catch (Exception e) 
            {
                Console.Error.WriteLine("Warning: Could not perform directory cleanup: " + e.Message);
            }
        }
        private static void ConvertDiffsToMassedit()
        {
            string tmp = gamepath;
            string comparePath = new FileInfo(compareParamFile).Directory.FullName + "\\temp";
            // Change the gamepath so we load the compare file as our vanilla param
            gamepath = comparePath;
            string compareParamFileCopy = comparePath + "\\" + paramFileRelPath + paramFileName;
            Directory.CreateDirectory(new FileInfo(compareParamFileCopy).Directory.FullName);
            File.Copy(compareParamFile, compareParamFileCopy, true);
            LoadParams();
            gamepath = tmp;
            Directory.Delete(comparePath, true);
            string mfile = "";
            bool addition = false;
            foreach (string paramName in ParamBank.PrimaryBank.Params.Keys)
            {
                Console.Out.Write($@"Comparing {paramName}... ");
                FSParam.Param oldParam = ParamBank.PrimaryBank.Params[paramName];
                FSParam.Param newParam = ParamBank.VanillaBank.Params[paramName];
                addition = ConvertToMassedit(oldParam, newParam, paramName, out string medits) || addition;
                mfile += medits;
                if (medits == "")
                    Console.Out.WriteLine("No changes found");
                else
                    Console.Out.WriteLine("Success!");
            }
            if (mfile != "")
            {
                // Write the output in the same directory as the param file provided, unless a valid output path was specified
                string meOutFile = $@"{new FileInfo(compareParamFile).Directory.FullName}\{Path.GetFileNameWithoutExtension(compareParamFile)}_diff.MASSEDIT";
                if (outputFile != null && File.Exists(outputFile))
                    meOutFile = $@"{new FileInfo(outputFile).Directory.FullName}\{Path.GetFileNameWithoutExtension(compareParamFile)}_diff.MASSEDIT";
                else if (outputFile != null && Directory.Exists(outputFile))
                    meOutFile = $@"{outputFile}\{Path.GetFileNameWithoutExtension(compareParamFile)}_diff.MASSEDIT";
                try
                {
                    File.WriteAllText(meOutFile, mfile);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($@"Exporting {meOutFile} FAILED: {e.Message}");
                }
                Console.Out.WriteLine($@"Exported {meOutFile}");
                if (addition) Console.Out.WriteLine("\tNote: Row additions detected, use the -M+ switch when loading this script.");
            }
            else Console.Out.WriteLine("No changes Detected");
        }
        private static void ProcessCSVToMassedit()
        {
            string opstring;
            MassEditResult meresult;
            foreach (string c2mfile in c2mFiles)
            {
                string mfile = "";
                bool addition = false;
                opstring = File.ReadAllText(c2mfile);
                string c2mNameNoExt = Path.GetFileNameWithoutExtension(c2mfile);
                string paramName = c2mNameNoExt;
                // Check for param name
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {
                    if (paramName.ToLower().StartsWith(p.ToLower()))
                    {
                        paramName = p;
                        break;
                    }
                }
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
                    addition = ConvertToMassedit(oldParam, newParam, paramName, out mfile) || addition;
                    // Write the output in the same directory as the CSV file provided, unless a valid output path was specified
                    string meOutFile = $@"{new FileInfo(c2mfile).Directory.FullName}\{c2mNameNoExt}.MASSEDIT";
                    if (outputFile != null && File.Exists(outputFile))
                        meOutFile = $@"{new FileInfo(outputFile).Directory.FullName}\{c2mNameNoExt}.MASSEDIT";
                    else if (outputFile != null && Directory.Exists(outputFile))
                        meOutFile = $@"{outputFile}\{c2mNameNoExt}.MASSEDIT";
                    try
                    {
                        File.WriteAllText(meOutFile, mfile);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($@"Converting {c2mNameNoExt} FAILED: {e.Message}");
                    }
                    Console.Out.WriteLine($@"Converting {c2mNameNoExt} {meresult.Type}:" + "\n\t" + meOutFile);
                    if (addition) Console.Out.WriteLine("\tNote: Row additions detected, use the -M+ switch when loading this script.");
                    // Undo the edits we made to the param file
                    manager.UndoAction();
                }
                else Console.Error.WriteLine($@"Converting {c2mNameNoExt} {meresult.Type}: {meresult.Information}");
            }
        }
        private static void ProcessCSV()
        {
            string opstring;
            MassEditResult meresult;
            foreach (string csvfile in csvFiles)
            {
                opstring = File.ReadAllText(csvfile);
                string paramName = Path.GetFileNameWithoutExtension(csvfile);
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {
                    if (paramName.ToLower() == p.ToLower())
                    {
                        paramName = p;
                        break;
                    }
                }
                meresult = MassParamEditCSV.PerformMassEdit(ParamBank.PrimaryBank, opstring, manager, paramName, true, false, ',');
                if (meresult.Type == MassEditResultType.SUCCESS)
                {
                    changesMade = true;
                    // Remember this Param to sort later
                    if (!sortingRows.Contains(paramName))
                        sortingRows.Add(paramName);
                    Console.Out.WriteLine($@"{paramName} {meresult.Type}: {meresult.Information}");
                }
                else Console.Error.WriteLine($@"{paramName} {meresult.Type}: {meresult.Information}");
                if (meresult.Information.Contains(" 0 rows added")) Console.Out.WriteLine("\tWarning: Use MASSEDIT scripts for modifying existing params to avoid conflicts");
            }
        }
        private static void ProcessMasseditWithAddition()
        {
            string opstring;
            MassEditResult meresult;
            foreach (string mepfile in masseditpFiles)
            {
                opstring = File.ReadAllText(mepfile).ReplaceLineEndings("\n").Trim();
                // MassEdit throws errors if there are any empty lines
                while (!opstring.Equals(opstring.Replace("\n\n", "\n")))
                    opstring = opstring.Replace("\n\n", "\n");
                // Row addition logic
                StringReader reader = new(opstring);
                string line;
                string param;
                int id = 0;
                while ((line = reader.ReadLine()) != null)
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
                        FSParam.Param.Row newRow = AddNewRow(id, value);
                        // We added a row, flag this Param to be sorted later
                        if (!sortingRows.Contains(param)) sortingRows.Add(param);
                    }
                }
                // Perform the massedit operation
                (meresult, ActionManager tmp) = MassParamEditRegex.PerformMassEdit(ParamBank.PrimaryBank, opstring, new ParamEditorSelectionState());
                if (meresult.Type == MassEditResultType.SUCCESS)
                {
                    changesMade = true;
                    Console.Out.WriteLine($@"{Path.GetFileNameWithoutExtension(mepfile)} {meresult.Type}: {meresult.Information}");
                }
                else Console.Error.WriteLine($@"{Path.GetFileNameWithoutExtension(mepfile)} {meresult.Type}: {meresult.Information}");
            }
        }
        private static void ProcessMassedit()
        {
            string opstring;
            MassEditResult meresult;
            foreach (string mefile in masseditFiles)
            {
                opstring = File.ReadAllText(mefile).ReplaceLineEndings("\n").Trim();
                // MassEdit throws errors if there are any empty lines
                while (!opstring.Equals(opstring.Replace("\n\n", "\n")))
                    opstring = opstring.Replace("\n\n", "\n");
                // Perform the massedit operation
                (meresult, ActionManager tmp) = MassParamEditRegex.PerformMassEdit(ParamBank.PrimaryBank, opstring, new ParamEditorSelectionState());
                if (meresult.Type == MassEditResultType.SUCCESS)
                {
                    changesMade = true;
                    Console.Out.WriteLine($@"{Path.GetFileNameWithoutExtension(mefile)} {meresult.Type}: {meresult.Information}");
                }
                else Console.Error.WriteLine($@"{Path.GetFileNameWithoutExtension(mefile)} {meresult.Type}: {meresult.Information}");
            }
        }
        private static void SaveParamFile()
        {
            string paramFileDir = new FileInfo(inputFile).Directory.FullName + "\\" + paramFileRelPath + paramFileName;
            try // Save the param file
            {
                ParamBank.PrimaryBank.SaveParams(false, false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Environment.Exit(9);
            }
            if (outputFile != null)
            {   // if an output file is specified, wing it by just copying the param file, and renaming the backup
                try
                {   // Peform rename operations in this order so the input file remains untouched if we fail to write the output file
                    File.Move(paramFileDir, paramFileDir + ".temp", true);
                    File.Move(paramFileDir + ".prev", paramFileDir);
                    if (Directory.Exists(outputFile))
                        File.Move(paramFileDir + ".temp", outputFile + "\\" + paramFileName, true);
                    else File.Move(paramFileDir + ".temp", outputFile, true);
                }
                catch (Exception ioe)
                {
                    Console.Error.WriteLine("ERROR: " + ioe.Message);
                    UnmimicDirectory(inputFile);
                    Environment.Exit(8);
                }
            }
            UnmimicDirectory(inputFile);
        }
        private static void PrintGameContext()
        {
            string gameDisplayName = "";
            switch (gameType)
            {
                case GameType.EldenRing:
                    gameDisplayName = "Elden Ring";
                    break;
                case GameType.DarkSoulsIISOTFS:
                    gameDisplayName = "Dark Souls 2";
                    break;
                case GameType.DarkSoulsIII:
                    gameDisplayName = "Dark Souls 3";
                    break;
                case GameType.Sekiro:
                    gameDisplayName = "Sekiro";
                    break;
                case GameType.Bloodborne:
                    gameDisplayName = "Bloodborne";
                    break;
                case GameType.DemonsSouls:
                    gameDisplayName = "Demons Souls";
                    break;
                case GameType.DarkSoulsRemastered:
                    gameDisplayName = "Dark Souls Remastered";
                    break;
                case GameType.DarkSoulsPTDE:
                    gameDisplayName = "Dark Souls Prepare to Die Edition";
                    break;
            }
            Console.Out.WriteLine(gameDisplayName);
            Console.Out.WriteLine("ParamfileName:\t" + paramFileName);
            if(paramFileName.Equals(OTHER_PARAMFILE_NAME)) 
                Console.Out.WriteLine("ParamfileRelativePath:\t" + paramFileRelPath + paramFileName);
        }
        private static void GetParamName()
        {
            switch(gameType)
            {
                case GameType.EldenRing:
                    paramFileName = ER_PARAMFILE_NAME;
                    break;
                case GameType.DarkSoulsIISOTFS:
                    paramFileName = DS2_PARAMFILE_NAME;
                    break;
                case GameType.DarkSoulsIII:
                    paramFileName = DS3_PARAMFILE_NAME;
                    break;
                default:
                    paramFileName = OTHER_PARAMFILE_NAME;
                    paramFileRelPath = OTHER_PARAMFILE_PATH + "\\";
                    break;
            }
        }
        private static void FindGamepath()
        {
            // Check working directory for gamepath.txt, if it wasn't specified on the command line
            if (gamepath == null && File.Exists($@"{workingDirectory}\{GAMEPATH_FILE}"))
                gamepath = File.ReadAllText($@"{workingDirectory}\{GAMEPATH_FILE}").Replace("\"", "").Trim();
            // Check default path for gamepath.txt (which will be the exe directory here)
            else if (gamepath == null && File.Exists(GAMEPATH_FILE)) 
                gamepath = File.ReadAllText(GAMEPATH_FILE).Replace("\"", "").Trim();
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
                            gametypeContext = true;
                            break;
                        case 'P':
                            mode = ParamMode.SETGAMEPATH;
                            break;
                        case 'X':
                            mode = ParamMode.EXPORT;
                            exportParams ??= new();
                            break;
                        case 'D':
                            mode = ParamMode.DIFF;
                            break;
                        case 'U':
                            mode = ParamMode.UPGRADE;
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
                                masseditFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid MASSEDIT filename given: " + param);
                            break;
                        case ParamMode.MASSEDITPLUS:
                            if (File.Exists(param) && (param.ToLower().EndsWith("txt") || param.ToLower().EndsWith("massedit")))
                                masseditpFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid MASSEDIT filename given: " + param);
                            break;
                        case ParamMode.OUTPUT:
                            if (outputFile != null)
                            {
                                Console.Error.WriteLine("Multiple output paths specified at once: " + outputFile + " and " + param);
                                Environment.Exit(4);
                            }
                            outputFile = param.Replace("\"", "");
                            if(Path.EndsInDirectorySeparator(outputFile) && !Directory.Exists(outputFile))
                            {
                                Console.Error.WriteLine($@"ERROR: Output path {outputFile} does not exist");
                                Environment.Exit(4);
                            }
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
                            mode = ParamMode.NONE;
                            break;
                        case ParamMode.SETGAMEPATH:
                            gamepath = param;
                            mode = ParamMode.NONE;
                            break;
                        case ParamMode.EXPORT:
                            exportParams.Add(param);
                            break;
                        case ParamMode.DIFF:
                            if(compareParamFile != null)
                            {
                                Console.Error.WriteLine("Multiple param files specified at once: " + compareParamFile + " and " + param);
                                Environment.Exit(4);
                            }
                            compareParamFile = param;
                            mode = ParamMode.NONE;
                            break;
                        case ParamMode.UPGRADE:
                            if (upgradeRefParamFile != null)
                            {
                                Console.Error.WriteLine("Multiple param files specified at once: " + upgradeRefParamFile + " and " + param);
                                Environment.Exit(4);
                            }
                            upgradeRefParamFile = param;
                            mode = ParamMode.NONE;
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
            Console.Out.WriteLine("Usage: DSMSPortable [paramfile] [-G gametype] [-P gamepath] [-U oldvanillaparams] [-C2M csvfile1 csvfile2 ...]");
            Console.Out.WriteLine("                                [-C csvfile1 csvfile2 ...] [-M[+] masseditfile1 masseditfile2 ...]");
            Console.Out.WriteLine("                                [-X paramname1[:query] paramname2 ...] [-D diffparamfile] [-O outputpath]\n");
            Console.Out.WriteLine("  paramfile  Path to regulation.bin file (or respective param file for other FromSoft games) to modify");
            Console.Out.WriteLine("  -G gametype");
            Console.Out.WriteLine("             Code indicating which game is being modified. The default is Elden Ring. Options are as follows:");
            Console.Out.WriteLine("             DS1R  Dark Souls Remastered    DS2  Dark Souls 2    DS3     Dark Souls 3");
            Console.Out.WriteLine("             ER    Elden Ring               BB   Bloodborne      SEKIRO  Sekiro");
            Console.Out.WriteLine("             DS1   Dark Souls PTDE          DES  Demon's Souls");
            Console.Out.WriteLine("  -P gamepath");
            Console.Out.WriteLine("             Path to the main install directory for the selected game, for loading vanilla params.");
            Console.Out.WriteLine("             The gamepath can also be implicitly specified in a gamepath.txt file in the working directory.");
            Console.Out.WriteLine("             Using this switch without specifying a paramfile will return the default paramfile name for that game");
            Console.Out.WriteLine("  -U oldvanillaparams");
            Console.Out.WriteLine("             Upgrades the paramfile to the latest version found in the gamepath, using the specified vanilla param");
            Console.Out.WriteLine("             file as a reference. If trying to upgrade a 1.0 param file to 1.1, oldvanillaparams should be");
            Console.Out.WriteLine("             a copy of the original 1.0 param file, and the game install in gamepath should be on 1.1.");
            Console.Out.WriteLine("             Upgrading occurs before processing any edits.");
            Console.Out.WriteLine("  -C2M csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             Converts the specified CSV files into .MASSEDIT scripts.");
            Console.Out.WriteLine("             Resulting files are saved in the same directories as the CSV's provided.");
            Console.Out.WriteLine("             If a valid output path is specified, they will be saved there instead.");
            Console.Out.WriteLine("  -C csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             List of CSV files (.TXT or .CSV) containing entire rows of params to add.");
            Console.Out.WriteLine("             Each file's name must perfectly match the param it is modifying (i.e. SpEffectParam.csv).");
            Console.Out.WriteLine("             CSV edits will be always be processed before massedit scripts.");
            Console.Out.WriteLine("  -M[+] masseditfile1 masseditfile2 ...");
            Console.Out.WriteLine("             List of text files (.TXT or .MASSEDIT) containing a script of DS Map Studio MASSEDIT commands.");
            Console.Out.WriteLine("             It is highly recommended to use massedit scripts to modify existing params to avoid conflicting edits.");
            Console.Out.WriteLine("             Edit scripts of the same type are processed in the order in which they are specified.");
            Console.Out.WriteLine("             If -M+ is specified, any individual ID's found that do not exist in the param file will be created and");
            Console.Out.WriteLine("             populated with default values (usually whatever is in the first entry of the param).");
            Console.Out.WriteLine("  -X paramname1[:query] paramname2 ...");
            Console.Out.WriteLine("             Exports the specified params to CSV, where paramname is the exact name of the param to be exported,");
            Console.Out.WriteLine("             and the query narrows down the export criteria, e.g. SpEffectParam: name Crucible && modified");
            Console.Out.WriteLine("             Specifying -X by itself will result in a full mass export.");
            Console.Out.WriteLine("             Resulting files are saved in the same directories as the paramfile.");
            Console.Out.WriteLine("             If a valid output path is specified, they will be saved there instead.");
            Console.Out.WriteLine("  -D diffparamfile");
            Console.Out.WriteLine("             Compares the specified param file against the input paramfile, and exports any differences as a");
            Console.Out.WriteLine("             massedit script. Diff is one way from paramfile -> diffparamfile.");
            Console.Out.WriteLine("             Resulting file is saved in the same directory as the diffparamfile.");
            Console.Out.WriteLine("             If a valid output path is specified, it will be saved there instead.");
            Console.Out.WriteLine("  -O outputpath");
            Console.Out.WriteLine("             Path where the resulting param file will be saved.");
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
            EXPORT,
            DIFF,
            UPGRADE,
            NONE
        }
        // No reason to be anal about the exact switch character used, any of these is fine
        private static bool IsSwitch(string arg)
        {
            return (arg[0] == '\\' || arg[0] == '/' || arg[0] == '-');
        }
    }
}
