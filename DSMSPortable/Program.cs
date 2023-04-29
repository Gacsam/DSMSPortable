using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using SoulsFormats;
using StudioCore;
using StudioCore.Editor;
using StudioCore.ParamEditor;
using StudioCore.TextEditor;

namespace DSMSPortable
{
    class DSMSPortable
    {
        static readonly string VERSION = "1.7.3";
        // Check this file locally for the full gamepath
        static readonly string GAMEPATH_FILE = "gamepath.txt";
        static readonly string DEFAULT_ER_GAMEPATH = "Steam\\steamapps\\common\\ELDEN RING\\Game";
        static readonly string ER_PARAMFILE_NAME = "regulation.bin";
        static readonly string DS2_PARAMFILE_NAME = "enc_regulation.bnd.dcx";
        static readonly string DS3_PARAMFILE_NAME = "Data0.bdt";
        static readonly string OTHER_PARAMFILE_NAME = "gameparam.parambnd.dcx";
        static readonly string OTHER_PARAMFILE_PATH = "param\\gameparam";
        static string gamepath = null;
        static List<string> csvFiles;
        static List<string> c2mFiles;
        static List<string> masseditFiles;
        static List<string> masseditpFiles;
        static List<string> sortingRows;
        static List<string> exportParams = null;
        static List<string> removeParams;
        static ActionManager manager;
        static GameType gameType = GameType.EldenRing;
        static string paramFileName;
        static string paramFileRelPath = "";
        static string outputFile = null;
        static string inputFile = null;
        static string workingDirectory = null;
        static string compareParamFile = null;
        static string upgradeRefParamFile = null;
        // fmgmerge files
        static string msgbndFile = null;
        static List<string> fmgFiles;
        static List<string> fmgAdditions;
        // layoutmerge files
        static string sblytbndFile = null;
        static List<string> layoutFiles;
        // texturemerge files
        static string tpfFile = null;
        static List<string> ddsFiles;
        // animerge files
        static string anibndFile = null;
        static List<string> taeFiles;
        static bool animDiffmode = false;
        // hksmerge files
        static string hksFile = null;
        static List<string> luaFiles;
        // bndmerge files
        static string srcbndFile = null;
        static string destbndFile = null;
        static bool bndDiffmode = false;
        static bool gametypeContext = false;
        static bool folderMimic = false;
        static bool changesMade = false;
        static bool ignoreConflicts = false;
        static bool verbose = false;
        static bool sort = false;
        static void Main(string[] args)
        {
            masseditFiles = new();
            masseditpFiles = new();
            csvFiles = new();
            c2mFiles = new();
            sortingRows = new();
            removeParams = new();
            fmgFiles = new();
            fmgAdditions = new();
            layoutFiles = new();
            ddsFiles = new();
            taeFiles = new();
            luaFiles = new();
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
            // Perform FMGMerging if specified
            if (msgbndFile != null)
            {
                Console.Out.Write("Performing FMG merge for " + msgbndFile + "...");
                List<string> verboseOutput = FmgMerge(msgbndFile, fmgFiles, ignoreConflicts);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
            // Perform LayoutMerging if specified
            if (sblytbndFile != null)
            {
                Console.Out.Write("Performing Layout merge for " + sblytbndFile + "...");
                List<string> verboseOutput = LayoutMerge(sblytbndFile, layoutFiles, ignoreConflicts, sort);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
            // Perform TextureMerging if specified
            if (tpfFile != null)
            {
                Console.Out.Write("Performing Texture merge for " + tpfFile + "...");
                List<string> verboseOutput = TextureMerge(tpfFile, ddsFiles, ignoreConflicts);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
            // Perform Animation Merging/Diff if specified
            if (anibndFile != null)
            {
                if (animDiffmode) Console.Out.Write("Creating partial animations from diffs to " + anibndFile + "...");
                else Console.Out.Write("Performing Animation merge for " + anibndFile + "...");
                List<string> verboseOutput = AnimationMerge(anibndFile, taeFiles, ignoreConflicts, animDiffmode);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
            // Perform HKS Merging if specified
            if (hksFile != null)
            {
                Console.Out.Write("Performing HKS merge for " + hksFile + "...");
                List<string> verboseOutput = HKSMerge(hksFile, luaFiles);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
            // Perform generic Binder Merging if specified
            if (srcbndFile != null && destbndFile != null)
            {
                if (bndDiffmode) Console.Out.Write("Creating partial bnd from diffs to " + srcbndFile + "...");
                else Console.Out.Write("Performing BND merge for " + destbndFile + "...");
                List<string> verboseOutput = BndMerge(destbndFile, srcbndFile, ignoreConflicts, sort, bndDiffmode);
                if (verboseOutput == null)
                    Console.Out.WriteLine("No changes detected.");
                else
                {
                    Console.Out.WriteLine("Success!");
                    if (verbose) foreach (string output in verboseOutput) Console.Out.WriteLine(output);
                }
                return;
            }
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
            // Execute Row Removals
            if (removeParams.Count > 0)
            {
                Console.Out.WriteLine("Executing Row removals...");
                RemoveParams();
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
            // Perform diff and convert changes to massedit, if a file to compare against was specified
            if (compareParamFile != null)
            {
                Console.Out.WriteLine("Converting changes to MASSEDIT...");
                ConvertDiffsToMassedit();
            }
            // Unmimic directories if needed
            UnmimicDirectory(inputFile);
            // Perform CSV export if one was specified
            if (exportParams != null)
            {
                Console.Out.WriteLine("Exporting Params to CSV...");
                ExportParams();
            }
        }
        public static bool ConvertToMassedit(FSParam.Param oldParam, FSParam.Param newParam, string paramName, out string mfile)
        {
            mfile = "";
            bool addition = false;
            bool versionMismatch = false;
            List<int> ids = new();
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
                    {   // make sure to escape #'s to keep strings sanitized
                        mfile += $@"param {paramName}: id {row.ID}: Name: = {row.Name.Replace("\r", "").Replace("#", $@"\#")};" + "\n";
                    }
                    // if something is different, check each cell for changes
                    for (int i = 0; i < row.CellHandles.Count; i++)
                    {
                        // Convert each individual change to massedit format
                        if (row.CellHandles[i].Value.GetType() == typeof(byte[]))
                        {
                            string value = ParamUtils.Dummy8Write((byte[])row.CellHandles[i].Value);
                            try
                            {
                                string oldvalue = ParamUtils.Dummy8Write((byte[])oldRow.CellHandles[i].Value);
                                if (!value.Equals(oldvalue))
                                    mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {value};" + "\n";
                            }
                            catch (Exception)
                            {   // There's a version mismatch, write the new value anyway
                                mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {value};" + "\n";
                                versionMismatch = true;
                            }
                        }
                        else if (!row.CellHandles[i].Value.Equals(oldRow.CellHandles[i].Value))
                            mfile += $@"param {paramName}: id {row.ID}: {row.CellHandles[i].Def.InternalName}: = {row.CellHandles[i].Value};" + "\n";
                    }
                }
            }
            if (versionMismatch) Console.Error.WriteLine("Warning: Version mismatch between given param files. Resulting file may contain extraneous entries.");
            return addition;
        }
        public static FSParam.Param.Row AddNewRow(int id, FSParam.Param param)
        {
            if (param[id] != null) return null;
            FSParam.Param.Row newRow = new(param.Rows.FirstOrDefault());
            for (int i = 0; i < newRow.CellHandles.Count; i++)
            {   // Def.Default is just always 0. s32's where the minimum is explicitly -1 reference ID's so -1 makes a better default
                if ((newRow.CellHandles[i].Def.DisplayType == SoulsFormats.PARAMDEF.DefType.s32 ||
                    newRow.CellHandles[i].Def.DisplayType == SoulsFormats.PARAMDEF.DefType.s16)
                    && (int)newRow.CellHandles[i].Def.Minimum == -1)
                    newRow.CellHandles[i].SetValue(Convert.ChangeType(newRow.CellHandles[i].Def.Minimum, newRow.CellHandles[i].Value.GetType()));
                else if (newRow.CellHandles[i].Def.Default != null)
                    newRow.CellHandles[i].SetValue(Convert.ChangeType(newRow.CellHandles[i].Def.Default, newRow.CellHandles[i].Value.GetType()));
            }
            newRow.ID = id;
            param.AddRow(newRow);
            return newRow;
        }
        public static List<string> AnimationMerge(string anibndFile, List<string> taeFiles, bool ignoreConflicts)
        {
            return AnimationMerge(anibndFile, taeFiles, ignoreConflicts, false);
        }
        public static List<string> AnimationDiff(string anibndFile, List<string> taeFiles)
        {
            return AnimationMerge(anibndFile, taeFiles, false, true);
        }
        private static List<string> AnimationMerge(BinderFile destFile, BinderFile srcFile, bool ignoreConflicts, string diffFile, bool bndmode)
        {
            List<string> verboseOutput = new();
            if (destFile == null || srcFile == null) return verboseOutput;
            bool diffmode = (diffFile != null);
            TAE destAnims = null;
            TAE srcAnims = null;
            string taeName = Path.GetFileName(destFile.Name);
            try
            {
                destAnims = TAE.Read(destFile.Bytes);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read TAE File {taeName} in {destFile.Name}: {e.Message}");
                Environment.Exit(6);
            }
            try
            {
                srcAnims = TAE.Read(srcFile.Bytes);
            }
            catch (Exception e)
            {
                if (diffFile != null) Console.Error.WriteLine($@"ERROR: Could not read TAE File {diffFile}: {e.Message}");
                else Console.Error.WriteLine($@"ERROR: Could not read TAE File {taeName} in {srcFile.Name}: {e.Message}");
                Environment.Exit(6);
            }
            foreach (TAE.Animation anim in srcAnims.Animations)
            {   // Compare each Animation we're trying to merge by ID
                TAE.Animation conflictingAnim = destAnims.Animations.Find(x => x.ID == anim.ID);
                if (diffmode && conflictingAnim != null && anim.Equals(conflictingAnim))
                {   // Diff mode means stripping out every animation that already exists in the given reference
                    destAnims.Animations.Remove(anim);
                    verboseOutput.Add($@"Removed Animation ID {anim.ID} from {taeName}");
                    continue;
                }
                if (diffmode) continue;
                if (conflictingAnim == null)
                {   // If not found, add the Animation and sort
                    destAnims.Animations.Add(anim);
                    destAnims.Animations.Sort();
                    verboseOutput.Add($@"Added Animation ID {anim.ID} to {taeName}");
                }
                // If the -I switch was set, or these are perfectly identical Animations, nothing needs to be done
                else if (ignoreConflicts || anim.Equals(conflictingAnim)) continue;
                else
                {   // Update the conflicting Animation to match the one we're trying to merge
                    destAnims.Animations.Remove(conflictingAnim);
                    destAnims.Animations.Add(anim);
                    destAnims.Animations.Sort();
                    verboseOutput.Add($@"Updated Animation ID {anim.ID} in {taeName}");
                }
            }
            // If we're in diffmode, save a partial version of the TAE given
            if (diffmode && !bndmode)
            {
                string path;
                if (outputFile != null && !Directory.Exists(outputFile)) path = new FileInfo(outputFile).Directory.FullName;
                else if (outputFile != null) path = outputFile;
                else path = new FileInfo(diffFile).Directory.FullName;
                if (destAnims.Animations.Count == 0)
                {   // this partial TAE is now empty, delete it altogether.
                    verboseOutput.Add($@"Removed all animations from {taeName}");
                    if (File.Exists($@"{path}\{taeName}.partial"))
                        File.Delete($@"{path}\{taeName}.partial");
                }
                else File.WriteAllBytes($@"{path}\{taeName}.partial", srcAnims.Write());
            }
            // Write the changes we made to the binderfile
            else if ((!diffmode || bndmode) && verboseOutput.Count > 0) destFile.Bytes = destAnims.Write();
            return verboseOutput;
        }
        private static List<string> AnimationMerge(string anibndFile, List<string> taeFiles, bool ignoreConflicts, bool diffmode)
        {
            if (anibndFile == null) return null;
            if (taeFiles.Count == 0) return null;
            List<string> verboseOutput = new();
            IBinder animBinder = null;
            try // Attempt to read the anibnd file
            {
                if (gameType == GameType.DemonsSouls || gameType == GameType.DarkSoulsPTDE || gameType == GameType.DarkSoulsRemastered)
                    animBinder = BND3.Read(anibndFile);
                else animBinder = BND4.Read(anibndFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read anibnd File {anibndFile}: {e.Message}");
                Environment.Exit(14);
            }
            foreach (string taeFile in taeFiles)
            {
                if (!File.Exists(taeFile))
                {
                    Console.Error.WriteLine("Warning: Could not open tae file: " + taeFile);
                    continue;
                }
                string binderFilename = null;
                string taeName = Path.GetFileName(taeFile).Replace(".partial", "");
                bool fileMatch = false;
                int ID = 0;
                Binder.FileFlags flags = Binder.FileFlags.Flag1;
                // Check to make sure there isn't an existing tae file with the same name (non case sensitive)
                foreach (BinderFile oldFile in animBinder.Files)
                {
                    if (!TAE.Is(oldFile.Bytes)) continue;
                    flags = oldFile.Flags;
                    ID = oldFile.ID + 1;
                    binderFilename ??= $@"{new FileInfo(oldFile.Name).Directory.FullName}\{taeName}";
                    // Check to make sure there isn't an existing TAE file with the same name (non case sensitive)
                    if (Path.GetFileName(oldFile.Name).ToLower() == taeName.ToLower())
                    {   // We got a matching TAE file, now compare each Animation within
                        fileMatch = true;
                        binderFilename = $@"{new FileInfo(oldFile.Name).Directory.FullName}\{taeName}";
                        ID = oldFile.ID;
                        flags = oldFile.Flags;
                        if (diffmode) verboseOutput.AddRange(AnimationMerge(oldFile,
                            new BinderFile(flags, ID, binderFilename, File.ReadAllBytes(taeFile)), ignoreConflicts, taeFile, false));
                        else verboseOutput.AddRange(AnimationMerge(oldFile,
                            new BinderFile(flags, ID, binderFilename, File.ReadAllBytes(taeFile)), ignoreConflicts, null, false));
                        break;
                    }
                }
                if (!fileMatch && !diffmode)
                {
                    animBinder.Files.Add(new BinderFile(flags, ID, binderFilename, File.ReadAllBytes(taeFile)));
                    verboseOutput.Add($@"Added {binderFilename} to {anibndFile}");
                }
            }
            if (verboseOutput.Count == 0) return null;
            if (diffmode) return verboseOutput;
            // If changes were detected, save the anibnd file
            string savePath;
            if (outputFile != null && !Directory.Exists(outputFile)) savePath = new FileInfo(outputFile).Directory.FullName;
            else if (outputFile != null) savePath = outputFile;
            else savePath = new FileInfo(anibndFile).Directory.FullName;
            try
            {
                if (animBinder is BND3 bnd3)
                    Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(anibndFile), bnd3);
                else if (animBinder is BND4 bnd4)
                    Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(anibndFile), bnd4);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Environment.Exit(8);
            }
            return verboseOutput;
        }
        private static List<string> LayoutMerge(BinderFile destFile, BinderFile srcFile, bool ignoreConflicts, bool sort, bool diffmode)
        {
            List<string> verboseOutput = new();
            if (destFile == null || srcFile == null) return verboseOutput;
            TextureAtlas destLayout = new(destFile);
            TextureAtlas srcLayout = new(srcFile);
            foreach (TextureAtlas.SubTexture subTexture in srcLayout.SubTextures)
            {   // Compare each SubTexture we're trying to merge by name
                TextureAtlas.SubTexture conflictingSubTexture = destLayout.Find(subTexture);
                if (diffmode)
                {
                    if (conflictingSubTexture != null)
                    {
                        destLayout.SubTextures.Remove(conflictingSubTexture);
                        verboseOutput.Add($@"Removed SubTexture {subTexture.Name} from {Path.GetFileName(destLayout.FileName)}");
                    }
                }
                else if (conflictingSubTexture == null)
                {   // If not found, add the subtexture
                    destLayout.SubTextures.Add(subTexture);
                    // Sort if the -S switch was specified
                    if (sort) destLayout.SubTextures.Sort();
                    verboseOutput.Add($@"Added SubTexture {subTexture.Name} to {Path.GetFileName(destLayout.FileName)}");
                }
                // If the -I switch was set, or these are perfectly identical SubTextures, nothing needs to be done
                else if (ignoreConflicts || subTexture.Equals(conflictingSubTexture)) continue;
                else
                {   // Update the conflicting SubTexture to match the one we're trying to merge
                    conflictingSubTexture.XCoord = subTexture.XCoord;
                    conflictingSubTexture.YCoord = subTexture.YCoord;
                    conflictingSubTexture.Width = subTexture.Width;
                    conflictingSubTexture.Height = subTexture.Height;
                    conflictingSubTexture.Half = subTexture.Half;
                    verboseOutput.Add($@"Updated SubTexture {subTexture.Name} in {Path.GetFileName(destLayout.FileName)}");
                }
            }
            // Write the changes we made to the binderfile
            if (verboseOutput.Count > 0) destFile.Bytes = destLayout.Write();
            return verboseOutput;
        }
        public static List<string> LayoutMerge(string sblytbndFile, List<string> layoutFiles, bool ignoreConflicts, bool sort)
        {
            if (sblytbndFile == null) return null;
            if (layoutFiles.Count == 0) return null;
            List<string> verboseOutput = new();
            IBinder sblytBinder = null;
            try // Attempt to read the sblytbnd file
            {
                if (gameType == GameType.DemonsSouls || gameType == GameType.DarkSoulsPTDE || gameType == GameType.DarkSoulsRemastered)
                    sblytBinder = BND3.Read(sblytbndFile);
                else sblytBinder = BND4.Read(sblytbndFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read sblytbnd File {sblytbndFile}: {e.Message}");
                Environment.Exit(14);
            }
            foreach (string layoutFile in layoutFiles)
            {
                if (!File.Exists(layoutFile))
                {
                    Console.Error.WriteLine("Warning: Could not open layout file: " + layoutFile);
                    continue;
                }
                string name = Path.GetFileName(layoutFile);
                bool fileMatch = false;
                // Snag a reference from the existing layouts
                int index = sblytBinder.Files.Count;
                Binder.FileFlags flags = sblytBinder.Files[index - 1].Flags;
                name = $@"{new FileInfo(sblytBinder.Files[index - 1].Name).Directory.FullName}\{name}";
                // Check to make sure there isn't an existing layout with the same name (non case sensitive)
                foreach (BinderFile oldFile in sblytBinder.Files)
                {
                    if (oldFile.Name.ToLower() == name.ToLower())
                    {   // We got a matching layout file, now compare each SubTexture within
                        fileMatch = true;
                        index = oldFile.ID;
                        flags = oldFile.Flags;
                        BinderFile newFile = new(flags, index, name, File.ReadAllBytes(layoutFile));
                        verboseOutput.AddRange(LayoutMerge(oldFile, newFile, ignoreConflicts, sort, false));
                        break;
                    }
                }
                if (!fileMatch)
                {   // If the entire file was not found, add it
                    sblytBinder.Files.Add(new BinderFile(flags, index, name, File.ReadAllBytes(layoutFile)));
                    verboseOutput.Add($@"Added {name} to {Path.GetFileName(sblytbndFile)}");
                }
            }
            if (verboseOutput.Count == 0) return null;
            // If changes were detected, save the sblytbndFile file
            string savePath;
            if (outputFile != null && !Directory.Exists(outputFile)) savePath = new FileInfo(outputFile).Directory.FullName;
            else if (outputFile != null) savePath = outputFile;
            else savePath = new FileInfo(sblytbndFile).Directory.FullName;
            try
            {
                if (sblytBinder is BND3 bnd3)
                    Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(sblytbndFile), bnd3);
                else if (sblytBinder is BND4 bnd4)
                    Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(sblytbndFile), bnd4);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Environment.Exit(8);
            }
            return verboseOutput;
        }
        private static List<string> PlaintextMerge(BinderFile destFile, BinderFile srcFile, bool sort, bool diffmode)
        {
            List<string> verboseOutput = new();
            if (srcFile == null || srcFile.Bytes.Length == 0) return verboseOutput;
            // Remove empty lines from both files
            string utf8;
            List<string> destList;
            try
            {
                utf8 = System.Text.Encoding.ASCII.GetString(destFile.Bytes, 0, destFile.Bytes.Length).Replace("\r", "");
                while (utf8.Contains("\n\n")) utf8 = utf8.Replace("\n\n", "\n");
                destList = new(utf8.Split('\n'));
            }
            catch (Exception)
            {
                destList = new();
            }
            utf8 = System.Text.Encoding.ASCII.GetString(srcFile.Bytes, 0, srcFile.Bytes.Length).Replace("\r", "");
            while (utf8.Contains("\n\n")) utf8 = utf8.Replace("\n\n", "\n");
            List<string> srcList = new(utf8.Split('\n'));
            // Check if destList is a subset of srcList as a way to preserve ordering
            if (!sort && !diffmode && destList.All(i => srcList.Contains(i)))
            {   // srcList already contains destList, just overwrite instead of merging
                destFile.Bytes = srcFile.Bytes;
                verboseOutput.Add($@"Overwrote file {destFile.Name}");
                return verboseOutput;
            }
            foreach (string newLine in srcList)
            {
                if (newLine == "") continue;
                if (!diffmode && !destList.Contains(newLine))
                {
                    destList.Add(newLine);
                    verboseOutput.Add($@"Added new entry {newLine} to {destFile.Name}");
                }
                else if (diffmode && destList.Contains(newLine))
                {
                    destList.Remove(newLine);
                    verboseOutput.Add($@"Removed entry {newLine} from {destFile.Name}");
                }
            }
            // Write the changes we made to the binderfile
            if (verboseOutput.Count > 0)
            {
                if (sort) destList.Sort();
                string outputString = "";
                foreach (string res in destList)
                {
                    if (res == "") continue;
                    outputString += res + "\n";
                }
                BinaryWriterEx bw = new BinaryWriterEx(false);
                bw.WriteASCII(outputString);
                destFile.Bytes = bw.FinishBytes();
            }
            return verboseOutput;
        }
        private static List<string> BndMerge(IBinder destbnd, IBinder srcbnd, bool ignoreConflicts, bool sort)
        {
            List<string> verboseOutput = new();
            if (destbnd == null || srcbnd == null) return verboseOutput;
            // Dictionary for fast searching of files by their full path name
            Dictionary<string, BinderFile> destbndict = new();
            // Dictionary for making sure we don't accidentally add duplicate ID's
            Dictionary<int, BinderFile> destbndindex = new();
            // Convert to dictionary for performance
            foreach (BinderFile destFile in destbnd.Files)
            {   // If the ID for the file we're adding is taken, search for an available ID
                while (destbndindex.ContainsKey(destFile.ID))
                    destFile.ID++;
                destbndindex.Add(destFile.ID, destFile);
                destbndict.Add(destFile.Name.ToLower(), destFile);
            }
            foreach (BinderFile srcFile in srcbnd.Files)
            {
                BinderFile destFile;
                try
                {   // Check to see if the file exists. If not, add it
                    destFile = destbndict[srcFile.Name.ToLower()];
                }
                catch (KeyNotFoundException)
                {   // If the ID for the file we're adding is taken, search for an available ID
                    while (destbndindex.ContainsKey(srcFile.ID))
                        srcFile.ID++;
                    destbnd.Files.Add(srcFile);
                    destbndindex.Add(srcFile.ID, srcFile);
                    verboseOutput.Add($@"Added {srcFile.Name} to {Path.GetFileName(destbndFile)}");
                    continue;
                }
                if (destFile == null)
                {
                    while (destbndindex.ContainsKey(srcFile.ID))
                        srcFile.ID++;
                    destbnd.Files.Add(srcFile);
                    destbndindex.Add(srcFile.ID, srcFile);
                    verboseOutput.Add($@"Added {srcFile.Name} to {Path.GetFileName(destbndFile)}");
                    continue;
                }
                // No point in merging empty files
                if (srcFile.Bytes.Length == 0) continue;
                // We got a matching binder file name
                if (!destFile.Bytes.SequenceEqual(srcFile.Bytes))
                {
                    if (destFile.Name.EndsWith(".layout"))
                    {
                        verboseOutput.AddRange(LayoutMerge(destFile, srcFile, ignoreConflicts, sort, false));
                    }
                    else if (destFile.Name.EndsWith(".ffxreslist"))
                    {
                        verboseOutput.AddRange(PlaintextMerge(destFile, srcFile, sort, false));
                    }
                    else if (destFile.Name.EndsWith(".tpf"))
                    {
                        verboseOutput.AddRange(TextureMerge(destFile, srcFile, ignoreConflicts, false));
                    }
                    else if (destFile.Name.EndsWith(".tae"))
                    {
                        verboseOutput.AddRange(AnimationMerge(destFile, srcFile, ignoreConflicts, null, true));
                    }
                    else if (destFile.Name.EndsWith(".fmg"))
                    {
                        verboseOutput.AddRange(FmgMerge(destFile, srcFile, ignoreConflicts, false));
                    }
                    else if (destFile.Name.EndsWith("bnd"))
                    {   // boy can this get hairy
                        if (destbnd is BND3) verboseOutput.AddRange(BndMerge(BND3.Read(destFile.Bytes), BND3.Read(srcFile.Bytes), ignoreConflicts, sort));
                        else if (destbnd is BND4) verboseOutput.AddRange(BndMerge(BND4.Read(destFile.Bytes), BND4.Read(srcFile.Bytes), ignoreConflicts, sort));
                    }
                    else if (!ignoreConflicts)
                    {
                        destFile.Bytes = srcFile.Bytes;
                        verboseOutput.Add($@"Overwrote {destFile.Name} in {Path.GetFileName(destbndFile)}");
                    }
                }
            }
            return verboseOutput;
        }
        private static List<string> BndDiff(IBinder destbnd, IBinder srcbnd)
        {
            List<string> verboseOutput = new();
            if (destbnd == null || srcbnd == null) return verboseOutput;
            Dictionary<string, BinderFile> destbndict = new();
            // Convert to dictionary for performance
            foreach (BinderFile file in new List<BinderFile>(destbnd.Files))
            {   // Remove 0 length files regardless
                if (file.Bytes.Length == 0)
                {
                    destbnd.Files.Remove(file);
                    verboseOutput.Add($@"Removed 0 byte file {file.Name} from {Path.GetFileName(destbndFile)}");
                }
                else destbndict.Add(file.Name.ToLower(), file);
            }
            foreach (BinderFile srcFile in srcbnd.Files)
            {
                BinderFile destFile;
                try
                {
                    destFile = destbndict[srcFile.Name.ToLower()];
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }
                if (destFile == null) continue;
                // We got a matching binder file
                if (!destFile.Bytes.SequenceEqual(srcFile.Bytes))
                {
                    if (destFile.Name.EndsWith(".layout"))
                    {
                        verboseOutput.AddRange(LayoutMerge(destFile, srcFile, false, true, true));
                    }
                    /*else if (destFile.Name.EndsWith(".ffxreslist"))
                    {
                        verboseOutput.AddRange(PlaintextMerge(destFile, srcFile, true));
                    }*/
                    else if (destFile.Name.EndsWith(".tpf"))
                    {
                        verboseOutput.AddRange(TextureMerge(destFile, srcFile, false, true));
                    }
                    else if (destFile.Name.EndsWith(".tae"))
                    {
                        verboseOutput.AddRange(AnimationMerge(destFile, srcFile, false, destFile.Name, true));
                    }
                    else if (destFile.Name.EndsWith(".fmg"))
                    {
                        verboseOutput.AddRange(FmgMerge(destFile, srcFile, false, true));
                    }
                    else if (destFile.Name.EndsWith("bnd"))
                    {   // boy can this get hairy
                        if (destbnd is BND3) verboseOutput.AddRange(BndDiff(BND3.Read(destFile.Bytes), BND3.Read(srcFile.Bytes)));
                        else if (destbnd is BND4) verboseOutput.AddRange(BndDiff(BND4.Read(destFile.Bytes), BND4.Read(srcFile.Bytes)));
                    }
                }
                else if (!destFile.Name.EndsWith(".ffxreslist"))
                {   // These merge perfectly, so don't bother stripping them out
                    destbnd.Files.Remove(destFile);
                    verboseOutput.Add($@"Removed {destFile.Name} from {Path.GetFileName(destbndFile)}");
                }
            }
            return verboseOutput;
        }
        public static List<string> BndMerge(string destbndFile, string srcbndFile, bool ignoreConflicts, bool sort, bool diffmode)
        {
            if (destbndFile == null || srcbndFile == null) return null;
            List<string> verboseOutput = new();
            IBinder destbnd = null;
            IBinder srcbnd = null;
            try // Attempt to read the binder files
            {
                if (gameType == GameType.DemonsSouls || gameType == GameType.DarkSoulsPTDE || gameType == GameType.DarkSoulsRemastered)
                {
                    destbnd = BND3.Read(destbndFile);
                    srcbnd = BND3.Read(srcbndFile);
                }
                else
                {
                    destbnd = BND4.Read(destbndFile);
                    srcbnd = BND4.Read(srcbndFile);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read one or more binder files {destbnd}, {srcbnd}: {e.Message}");
                Environment.Exit(16);
            }
            if (!diffmode) verboseOutput.AddRange(BndMerge(destbnd, srcbnd, ignoreConflicts, sort));
            else verboseOutput.AddRange(BndDiff(destbnd, srcbnd));
            if (verboseOutput.Count == 0) return null;
            // If changes were detected, save the binder
            string savePath;
            if (outputFile != null && !Directory.Exists(outputFile)) savePath = new FileInfo(outputFile).Directory.FullName;
            else if (outputFile != null) savePath = outputFile;
            else savePath = new FileInfo(destbndFile).Directory.FullName;
            string filename = Path.GetFileName(destbndFile).Replace(".partial", "");
            if (diffmode) filename += ".partial";
            try
            {
                if (destbnd is BND3 bnd3)
                    Utils.WriteWithBackup(gamepath, savePath, filename, bnd3);
                else if (destbnd is BND4 bnd4)
                    Utils.WriteWithBackup(gamepath, savePath, filename, bnd4);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Environment.Exit(8);
            }
            return verboseOutput;
        }
        public static List<string> TextureMerge(BinderFile destFile, BinderFile srcFile, bool ignoreConflicts, bool diffmode)
        {
            List<string> verboseOutput = new();
            if (destFile == null || srcFile == null) return verboseOutput;
            TPF destTPF = null;
            TPF srcTPF = null;
            DCX.Type compression = DCX.Type.None;
            try // Attempt to read the TPF files
            {
                if (destFile.Name.ToLower().EndsWith(".dcx"))
                    destTPF = TPF.Read(DCX.Decompress(destFile.Bytes, out compression));
                else destTPF = TPF.Read(destFile.Bytes);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read TPF File {destFile}: {e.Message}");
                Environment.Exit(15);
            }
            try // Attempt to read the TPF files
            {
                if (srcFile.Name.ToLower().EndsWith(".dcx"))
                    srcTPF = TPF.Read(DCX.Decompress(srcFile.Bytes, out compression));
                else srcTPF = TPF.Read(srcFile.Bytes);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read TPF File {srcFile}: {e.Message}");
                Environment.Exit(15);
            }
            foreach (TPF.Texture srcTexture in srcTPF.Textures)
            {
                bool match = false;
                foreach (TPF.Texture destTexture in new List<TPF.Texture>(destTPF.Textures))
                {
                    if (srcTexture.Name == destTexture.Name)
                    {
                        match = true;
                        if (!diffmode && !srcTexture.Bytes.SequenceEqual(destTexture.Bytes))
                        {
                            if (!ignoreConflicts)
                            {
                                destTexture.Bytes = srcTexture.Bytes;
                                verboseOutput.Add($@"Updated Texture {destTexture.Name} in {Path.GetFileName(destFile.Name)}");
                            }
                        }
                        else if (diffmode && srcTexture.Bytes.SequenceEqual(destTexture.Bytes))
                        {
                            destTPF.Textures.Remove(destTexture);
                            verboseOutput.Add($@"Removed Texture {destTexture.Name} from {Path.GetFileName(destFile.Name)}");
                        }
                        break;
                    }
                }
                if (!match && !diffmode)
                {
                    destTPF.Textures.Add(srcTexture);
                    verboseOutput.Add($@"Added Texture {srcTexture.Name} to {Path.GetFileName(destFile.Name)}");
                }
            }
            if (verboseOutput.Count > 0) destFile.Bytes = destTPF.Write(compression);
            return verboseOutput;
        }
        public static List<string> TextureMerge(string tpfFile, List<string> ddsFiles, bool ignoreConflicts)
        {
            if (tpfFile == null) return null;
            if (ddsFiles.Count == 0) return null;
            List<string> verboseOutput = new();
            TPF tpf = null;
            DCX.Type compression = DCX.Type.None;
            try // Attempt to read the TPF file
            {
                if (tpfFile.ToLower().EndsWith(".dcx"))
                    tpf = TPF.Read(DCX.Decompress(tpfFile, out compression));
                else tpf = TPF.Read(tpfFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read TPF File {tpfFile}: {e.Message}");
                Environment.Exit(15);
            }
            foreach (string ddsFile in ddsFiles)
            {
                if (!File.Exists(ddsFile))
                {
                    Console.Error.WriteLine("Warning: Could not open DDS file: " + ddsFile);
                    continue;
                }
                string name = Path.GetFileNameWithoutExtension(ddsFile);
                // Snag a reference from the existing textures
                byte format = tpf.Textures[tpf.Textures.Count - 1].Format;
                byte flags = tpf.Textures[tpf.Textures.Count - 1].Flags1;
                byte[] bytes = File.ReadAllBytes(ddsFile);
                bool conflict = false;
                // Check to make sure there isn't an existing texture with the same name (non case sensitive)
                for (int i = 0; i < tpf.Textures.Count; i++)
                {
                    if (tpf.Textures[i].Name.ToLower() == name.ToLower())
                    {
                        conflict = true;
                        format = tpf.Textures[i].Format;
                        flags = tpf.Textures[i].Flags1;
                        if (!ignoreConflicts && !tpf.Textures[i].Bytes.SequenceEqual(bytes))
                        {
                            tpf.Textures[i] = new TPF.Texture(name, format, flags, bytes);
                            verboseOutput.Add($@"Updated Texture {name} in {Path.GetFileName(tpfFile)}");
                        }
                        break;
                    }
                }
                if (!conflict)
                {
                    tpf.Textures.Add(new TPF.Texture(name, format, flags, bytes));
                    verboseOutput.Add($@"Added Texture {name} to {Path.GetFileName(tpfFile)}");
                }
            }
            if (verboseOutput.Count == 0) return null;
            // If changes were detected, save the TPF file
            string savePath = tpfFile;
            if (outputFile != null)
            {
                savePath = outputFile;
                if (Directory.Exists(outputFile))
                {
                    if (compression == DCX.Type.None)
                        savePath = $@"{savePath}\{Path.GetFileName(tpfFile).Split(".")[0]}.tpf";
                    else
                        savePath = $@"{savePath}\{Path.GetFileName(tpfFile).Split(".")[0]}.tpf.dcx";
                }
            }
            try
            {
                if (File.Exists(savePath)) File.Move(savePath, savePath + ".prev", true);
                tpf.Write(savePath, compression);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e.Message);
                Environment.Exit(8);
            }
            return verboseOutput;
        }
        public static List<string> HKSMerge(string hksFile, List<string> luaFiles)
        {
            if (hksFile == null) return null;
            if (luaFiles.Count == 0) return null;
            List<string> verboseOutput = new();
            LuaFile destFile = null;
            List<LuaFile> srcFiles = new();
            try
            {
                destFile = new LuaFile(hksFile);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($@"ERROR: Could not read lua file {hksFile}");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
                Environment.Exit(17);
            }
            foreach (string file in luaFiles)
            {
                try
                {
                    srcFiles.Add(new LuaFile(file));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($@"ERROR: Could not read lua file {file}");
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                    Environment.Exit(17);
                }
            }
            foreach (LuaFile srcFile in srcFiles)
            {
                foreach (LuaString line in srcFile.source)
                {
                    if (line is LuaFunction function)
                    {
                        bool overwrite = destFile.AddFunction(function);
                        if (overwrite) verboseOutput.Add($@"Updated function {function.functionName} in {Path.GetFileName(hksFile)}");
                        else verboseOutput.Add($@"Added new function {function.functionName} to {Path.GetFileName(hksFile)}");
                    }
                }
            }
            if (verboseOutput.Count == 0) return null;
            // If changes were detected, save the HKS file
            if (outputFile != null)
            {
                string savePath = outputFile;
                if (Directory.Exists(outputFile)) savePath += $@"\{Path.GetFileName(hksFile)}";
                try
                {
                    destFile.SaveAs(savePath);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("ERROR: " + e.Message);
                    Environment.Exit(8);
                }
            }
            else destFile.Save();
            return verboseOutput;
        }
        private static List<string> FmgMerge(BinderFile destFile, BinderFile srcFile, bool ignoreConflicts, bool diffmode)
        {
            List<string> verboseOutput = new();
            if (destFile == null || srcFile == null) return verboseOutput;
            FMG destFmg = null;
            FMG srcFmg = null;
            try
            {
                destFmg = FMG.Read(destFile.Bytes);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: Could not read " + destFile.Name + ": " + e.Message);
                Environment.Exit(12);
            }
            try
            {
                srcFmg = FMG.Read(srcFile.Bytes);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: Could not read " + srcFile.Name + ": " + e.Message);
                Environment.Exit(12);
            }
            Dictionary<int, FMG.Entry> destDict = new();
            foreach (FMG.Entry destEntry in destFmg.Entries)
                destDict.Add(destEntry.ID, destEntry);
            foreach (FMG.Entry srcEntry in srcFmg.Entries)
            {
                FMG.Entry destEntry = destDict[srcEntry.ID];
                if (!diffmode && destEntry == null)
                {
                    destFmg.Entries.Add(srcEntry);
                    verboseOutput.Add($@"Added Entry ID {srcEntry.ID} to {destFile.Name}");
                    continue;
                }
                else if (destEntry == null) continue;
                if (diffmode)
                {
                    if (destEntry.Text == srcEntry.Text)
                    {
                        destFmg.Entries.Remove(srcEntry);
                        verboseOutput.Add($@"Removed Entry ID {srcEntry.ID} from {destFile.Name}");
                    }
                }
                else if ((!ignoreConflicts || destEntry.Text == "" || destEntry.Text == "%null%") && destEntry.Text != srcEntry.Text)
                {
                    destEntry.Text = srcEntry.Text;
                    verboseOutput.Add($@"Updated Entry ID {destEntry.ID} in {destFile.Name}");
                }
            }
            if (verboseOutput.Count > 0) destFile.Bytes = destFmg.Write(destFmg.Compression);
            return verboseOutput;
        }
        public static List<string> FmgMerge(string msgbndFile, List<string> fmgFiles, bool ignoreConflicts)
        {
            if (msgbndFile == null || fmgFiles == null) return null;
            List<string> verboseOutput = new();
            // Read msgbndfile
            List<FMGBank.FMGInfo> fmgBank = new();
            IBinder fmgBinder;
            if (gameType == GameType.DemonsSouls || gameType == GameType.DarkSoulsPTDE || gameType == GameType.DarkSoulsRemastered)
                fmgBinder = BND3.Read(msgbndFile);
            else fmgBinder = BND4.Read(msgbndFile);
            foreach (var file in fmgBinder.Files)
                fmgBank.Add(FMGBank.GenerateFMGInfo(file));
            // Read fmg files provided
            foreach (string fmgPath in fmgFiles)
            {
                FMG fmg = null;
                FMGBank.FMGInfo sourceFmg = null;
                // Find out which FMG we're dealing with
                foreach (FMGBank.FMGInfo src in fmgBank)
                {
                    if (src.Name.ToLower() == Path.GetFileNameWithoutExtension(fmgPath).Split(".")[0].ToLower() ||
                        src.FileName.Split(".")[0].ToLower() == Path.GetFileNameWithoutExtension(fmgPath).Split(".")[0].ToLower())
                    {
                        sourceFmg = src;
                        break;
                    }
                }
                if (sourceFmg == null)
                {
                    Console.Error.WriteLine("ERROR: Could not find FMG by name of " + Path.GetFileNameWithoutExtension(fmgPath).Split(".")[0] +
                        " in " + Path.GetFileName(msgbndFile));
                    Environment.Exit(11);
                }
                if (fmgPath.ToLower().EndsWith(".json"))
                {   // exported with DSMS
                    var file = File.ReadAllText(fmgPath);
                    var json = JsonConvert.DeserializeObject<FMGBank.JsonFMG>(@file);
                    fmg = json.Fmg;
                }
                else if (fmgPath.ToLower().EndsWith(".xml"))
                {   // exported with Yabber
                    fmg = new FMG();
                    XmlDocument xml = new();
                    try
                    {
                        xml.Load(fmgPath);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("ERROR: Could not parse " + Path.GetFileName(fmgPath) + " as an xml file: " + e.Message);
                        Environment.Exit(12);
                    }
                    if (!Enum.TryParse(xml.SelectSingleNode("fmg/compression")?.InnerText ?? "None", out DCX.Type compression))
                    {
                        Console.Error.WriteLine("ERROR: Could not parse " + Path.GetFileName(fmgPath) + " as an xml file");
                        Environment.Exit(12);
                    }
                    fmg.Compression = compression;
                    fmg.Version = (FMG.FMGVersion)Enum.Parse(typeof(FMG.FMGVersion), xml.SelectSingleNode("fmg/version").InnerText);
                    fmg.BigEndian = bool.Parse(xml.SelectSingleNode("fmg/bigendian").InnerText);
                    foreach (XmlNode textNode in xml.SelectNodes("fmg/entries/text"))
                    {
                        int id = int.Parse(textNode.Attributes["id"].InnerText);
                        string text = textNode.InnerText.Replace("\r", "");
                        if (text == "%null%")
                            text = null;
                        fmg.Entries.Add(new FMG.Entry(id, text));
                    }
                }
                else if (fmgPath.ToLower().EndsWith(".fmg"))
                {   // Raw data
                    try
                    {
                        fmg = FMG.Read(File.ReadAllBytes(fmgPath));
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("ERROR: Could not read " + Path.GetFileName(fmgPath) + " as an fmg file: " + e.Message);
                        Environment.Exit(12);
                    }
                }
                // Perform merge
                Dictionary<int, FMG.Entry> srcDict = new();
                // this is faster than using FMGInfo.GetEntry
                foreach (FMG.Entry entry in sourceFmg.Fmg.Entries)
                    srcDict.Add(entry.ID, entry);
                foreach (FMG.Entry entry in fmg.Entries)
                {
                    FMG.Entry existingEntry;
                    try
                    {
                        existingEntry = srcDict[entry.ID];
                        if (existingEntry == null)
                        {
                            sourceFmg.AddEntry(entry);
                            verboseOutput.Add($@"Added Entry ID {entry.ID} to {Path.GetFileName(fmgPath)}");
                        }
                        else if ((!ignoreConflicts || existingEntry.Text == "" || existingEntry.Text == "%null%") && existingEntry.Text != entry.Text)
                        {
                            existingEntry.Text = entry.Text;
                            verboseOutput.Add($@"Updated Entry ID {entry.ID} in {Path.GetFileName(fmgPath)}");
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        sourceFmg.AddEntry(entry);
                        verboseOutput.Add($@"Added Entry ID {entry.ID} to {Path.GetFileName(fmgPath)}");
                    }
                }
            }
            // Perform manual entries
            foreach (string fmgstring in fmgAdditions)
            {
                string name = fmgstring.Split(":", 3)[0].Trim();
                int id = 0;
                try
                {
                    id = int.Parse(fmgstring.Split(":", 3)[1].Trim());
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("ERROR: \"" + fmgstring + "\" is not a valid FMG entry in the format [Name]:[ID]:[Text]");
                    Environment.Exit(13);
                }
                string text = fmgstring.Split(":", 3)[2].Trim().Replace("\\n", "\n");
                // Find out which FMG we're dealing with
                FMGBank.FMGInfo sourceFmg = null;
                foreach (FMGBank.FMGInfo src in fmgBank)
                {
                    if (src.Name.ToLower() == name.ToLower() || src.FileName.Split(".")[0].ToLower() == name.ToLower())
                    {
                        sourceFmg = src;
                        break;
                    }
                }
                if (sourceFmg == null)
                {
                    Console.Error.WriteLine("ERROR: Could not find FMG by name of " + name + " in " + Path.GetFileName(msgbndFile));
                    Environment.Exit(11);
                }
                // Perform entry
                FMG.Entry newEntry = new(id, text);
                FMG.Entry existingEntry = sourceFmg.GetEntry(id);
                if (existingEntry == null)
                {
                    sourceFmg.AddEntry(newEntry);
                    verboseOutput.Add($@"Added Entry ID {newEntry.ID} to {sourceFmg.FileName}");
                }
                else if (existingEntry.Text != text)
                {
                    existingEntry.Text = text.Replace("$0", existingEntry.Text);
                    verboseOutput.Add($@"Updated Entry ID {newEntry.ID} in {sourceFmg.FileName}");
                }
            }
            // Save msgbnd file if necessary
            if (verboseOutput.Count == 0) return null;
            foreach (var file in fmgBinder.Files)
            {
                var info = fmgBank.Find(e => e.FmgID == (FMGBank.FmgIDType)file.ID);
                if (info != null)
                {
                    file.Bytes = info.Fmg.Write();
                }
            }
            string savePath;
            if (outputFile != null && !Directory.Exists(outputFile)) savePath = new FileInfo(outputFile).Directory.FullName;
            else if (outputFile != null) savePath = outputFile;
            else savePath = new FileInfo(msgbndFile).Directory.FullName;
            if (fmgBinder is BND3 bnd3)
                Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(msgbndFile), bnd3);
            else if (fmgBinder is BND4 bnd4)
                Utils.WriteWithBackup(gamepath, savePath, Path.GetFileName(msgbndFile), bnd4);
            return verboseOutput;
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
            // Copy oo2core to our initialization directory
            if (!File.Exists($@"{Directory.GetCurrentDirectory()}\oo2core_6_win64.dll") && File.Exists($@"{gamepath}\oo2core_6_win64.dll"))
                File.Copy($@"{gamepath}\oo2core_6_win64.dll", $@"{Directory.GetCurrentDirectory()}\oo2core_6_win64.dll");
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
            // Build diff cache in case we need to use the "modified" query
            ParamBank.PrimaryBank.RefreshParamDiffCaches();
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
                            foreach (int confId in conflicts[confParam])
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
        private static void RemoveParams()
        {
            string param = null;
            string query;
            // Read each argument, check for a query, and remove resulting rows
            foreach (string removalParam in removeParams)
            {
                query = "";
                if (!removalParam.Contains(':'))
                    param = removalParam.Trim();
                else
                {
                    param = removalParam.Split(':', 2)[0].Trim();
                    query = removalParam.Split(':', 2)[1].Trim();
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
                FSParam.Param selectedParam = ParamBank.PrimaryBank.GetParamFromName(param);
                if (selectedParam == null)
                {
                    Console.Error.WriteLine("Warning: No such param: " + param);
                    continue;
                }
                Console.Out.Write($@"Removing rows from {param}... ");
                List<FSParam.Param.Row> rows = RowSearchEngine.rse.Search((ParamBank.PrimaryBank, selectedParam), query, true, true);
                if (rows.Count == 0)
                {
                    Console.Out.WriteLine("No rows found.");
                    continue;
                }
                foreach (FSParam.Param.Row row in rows)
                {
                    selectedParam.RemoveRow(row);
                    changesMade = true;
                }
                Console.Out.WriteLine("Done!");
            }
        }
        private static void ExportParams()
        {
            string param = null;
            string query;
            // Empty list is a special case. Mass export all.
            if (exportParams.Count == 0)
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
                List<FSParam.Param.Row> rows = null;
                try
                {
                    rows = RowSearchEngine.rse.Search((ParamBank.PrimaryBank, ParamBank.PrimaryBank.GetParamFromName(param)), query, true, true);
                }
                catch (NullReferenceException)
                {
                    Console.Error.WriteLine("Warning: Could not find param by name of " + param);
                    continue;
                }
                string output = MassParamEditCSV.GenerateCSV(rows, ParamBank.PrimaryBank.GetParamFromName(param), ',');
                // Write the output in the same directory as the param file provided, unless a valid output path was specified
                string csvOutFile = $@"{new FileInfo(inputFile).Directory.FullName}\{param}.csv";
                if (outputFile != null && File.Exists(outputFile))
                    csvOutFile = $@"{new FileInfo(outputFile).Directory.FullName}\{param}.csv";
                else if (outputFile != null && Directory.Exists(outputFile))
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
                if (File.Exists(paramfile) && File.Exists(inputDir + "\\" + paramFileName))
                {
                    File.Move(inputDir + "\\" + paramFileName, inputDir + "\\" + paramFileName + ".tmp", true);
                    File.Move(paramfile, inputDir + "\\" + paramFileName);
                }
                else if (File.Exists(paramfile))
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
                    if (File.Exists($@"{paramFileDir}\{paramFileRelPath}{paramFileName}.tmp"))
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
                if (outputFile != null && (outputFile.ToLower().EndsWith(".massedit") || outputFile.ToLower().EndsWith(".txt")))
                    meOutFile = new FileInfo(outputFile).FullName;
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
                int len = 0;
                // Check for param name
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {   // We want to allow for filenames like bullet_new.csv so go with the longest paramname that matches
                    if (c2mNameNoExt.ToLower().StartsWith(p.ToLower()) && p.Length > len)
                    {
                        paramName = p;
                        len = p.Length;
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
                string paramNameNoExt = Path.GetFileNameWithoutExtension(csvfile);
                string paramName = paramNameNoExt;
                int len = 0;
                // Check for param name
                foreach (string p in ParamBank.PrimaryBank.Params.Keys)
                {   // We want to allow for filenames like bullet_new.csv so go with the longest paramname that matches
                    if (paramNameNoExt.ToLower().StartsWith(p.ToLower()) && p.Length > len)
                    {
                        paramName = p;
                        len = p.Length;
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
                if (meresult.Information.Contains(" 0 rows added"))
                    Console.Out.WriteLine("\tWarning: Use MASSEDIT scripts for modifying existing params to avoid conflicts");
            }
        }
        private static void ProcessMasseditWithAddition()
        {
            string opstring;
            MassEditResult meresult;
            foreach (string mepfile in masseditpFiles)
            {
                opstring = File.ReadAllText(mepfile).ReplaceLineEndings("\n").Trim();
                // Allow for comments with # and escapes with \#
                opstring = Regex.Replace(opstring, $@"(?<!\\)#(.[^\n]*)\n", "\n");
                opstring = Regex.Replace(opstring, $@"\\#", "#");
                // MassEdit throws errors if there are any empty lines
                while (!opstring.Equals(opstring.Replace("\n\n", "\n")))
                    opstring = opstring.Replace("\n\n", "\n");
                // Omit first line as well if its an empty line
                if (opstring.StartsWith("\n")) opstring = opstring.Split('\n', 2)[1];
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
                // Allow for comments with # and escapes with \#
                opstring = Regex.Replace(opstring, $@"(?<!\\)#(.[^\n]*)\n", "\n");
                opstring = Regex.Replace(opstring, $@"\\#", "#");
                // MassEdit throws errors if there are any empty lines
                while (!opstring.Equals(opstring.Replace("\n\n", "\n")))
                    opstring = opstring.Replace("\n\n", "\n");
                // Omit first line as well if its an empty line
                if (opstring.StartsWith("\n")) opstring = opstring.Split('\n', 2)[1];
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
            if (paramFileName.Equals(OTHER_PARAMFILE_NAME))
                Console.Out.WriteLine("ParamfileRelativePath:\t" + paramFileRelPath + paramFileName);
        }
        private static void GetParamName()
        {
            switch (gameType)
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
                Help(true);
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
                        case 'R':
                            mode = ParamMode.REMOVE;
                            break;
                        case 'D':
                            mode = ParamMode.DIFF;
                            break;
                        case 'U':
                            mode = ParamMode.UPGRADE;
                            break;
                        case 'S':
                            sort = true;
                            break;
                        case 'V':
                            verbose = true;
                            break;
                        case 'I':
                            ignoreConflicts = true;
                            break;
                        case 'H':
                        case '?':
                            Help(false);
                            break;
                        default:
                            if (param.ToLower() == "--fmgmerge")
                                mode = ParamMode.FMGMERGE;
                            else if (param.ToLower() == "--fmgentry")
                                mode = ParamMode.FMGENTRY;
                            else if (param.ToLower() == "--layoutmerge")
                                mode = ParamMode.LAYOUTMERGE;
                            else if (param.ToLower() == "--texturemerge")
                                mode = ParamMode.DDSMERGE;
                            else if (param.ToLower() == "--bndmerge")
                                mode = ParamMode.BNDMERGE;
                            else if (param.ToLower() == "--bnddiff")
                            {
                                bndDiffmode = true;
                                mode = ParamMode.BNDDIFF;
                            }
                            else if (param.ToLower() == "--animerge")
                                mode = ParamMode.ANIMERGE;
                            else if (param.ToLower() == "--animdiff")
                            {
                                animDiffmode = true;
                                mode = ParamMode.ANIMDIFF;
                            }
                            else if (param.ToLower() == "--hksmerge")
                                mode = ParamMode.HKSMERGE;
                            else
                            {
                                Console.Error.WriteLine("ERROR: Invalid switch: " + param);
                                Environment.Exit(5);
                            }
                            break;
                    }
                }
                else
                {
                    switch (mode)
                    {
                        case ParamMode.CSV:
                            if (Directory.Exists(param))
                            {
                                foreach (string file in Directory.EnumerateFiles(param))
                                {
                                    if (File.Exists(file) && file.ToLower().EndsWith(".csv"))
                                        csvFiles.Add(file);
                                    else Console.Error.WriteLine("Warning: Invalid CSV filename given: " + file);
                                }
                            }
                            else if (File.Exists(param) && (param.ToLower().EndsWith(".csv") || param.ToLower().EndsWith(".txt")))
                                csvFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid CSV filename given: " + param);
                            break;
                        case ParamMode.C2M:
                            if (Directory.Exists(param))
                            {
                                foreach (string file in Directory.EnumerateFiles(param))
                                {
                                    if (File.Exists(file) && file.ToLower().EndsWith(".csv"))
                                        c2mFiles.Add(file);
                                    else Console.Error.WriteLine("Warning: Invalid CSV filename given: " + file);
                                }
                            }
                            else if (File.Exists(param) && (param.ToLower().EndsWith(".csv") || param.ToLower().EndsWith(".txt")))
                                c2mFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid CSV filename given: " + param);
                            break;
                        case ParamMode.MASSEDIT:
                            if (Directory.Exists(param))
                            {
                                foreach (string file in Directory.EnumerateFiles(param))
                                {
                                    if (File.Exists(file) && file.ToLower().EndsWith(".massedit"))
                                        masseditFiles.Add(file);
                                    else Console.Error.WriteLine("Warning: Invalid MASSEDIT filename given: " + file);
                                }
                            }
                            else if (File.Exists(param) && (param.ToLower().EndsWith(".txt") || param.ToLower().EndsWith(".massedit")))
                                masseditFiles.Add(param);
                            else Console.Out.WriteLine("Warning: Invalid MASSEDIT filename given: " + param);
                            break;
                        case ParamMode.MASSEDITPLUS:
                            if (Directory.Exists(param))
                            {
                                foreach (string file in Directory.EnumerateFiles(param))
                                {
                                    if (File.Exists(file) && file.ToLower().EndsWith(".massedit"))
                                        masseditpFiles.Add(file);
                                    else Console.Error.WriteLine("Warning: Invalid MASSEDIT filename given: " + file);
                                }
                            }
                            else if (File.Exists(param) && (param.ToLower().EndsWith(".txt") || param.ToLower().EndsWith(".massedit")))
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
                            if (Path.EndsInDirectorySeparator(outputFile) && !Directory.Exists(outputFile))
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
                        case ParamMode.REMOVE:
                            removeParams.Add(param);
                            break;
                        case ParamMode.DIFF:
                            if (compareParamFile != null)
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
                        case ParamMode.FMGMERGE:
                            if (msgbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith(".msgbnd") || param.ToLower().EndsWith(".msgbnd.dcx")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid msgbnd file specified");
                                    Environment.Exit(10);
                                }
                                msgbndFile = param;
                            }
                            else
                            {
                                if (Directory.Exists(param))
                                {
                                    foreach (string file in Directory.EnumerateFiles(param))
                                    {
                                        if (File.Exists(file) && (file.ToLower().EndsWith(".fmg") || file.ToLower().EndsWith(".xml") || file.ToLower().EndsWith(".json")))
                                            fmgFiles.Add(file);
                                        else Console.Error.WriteLine("Warning: Invalid fmg file specified: " + file);
                                    }
                                }
                                else if (File.Exists(param) && (param.ToLower().EndsWith(".fmg") || param.ToLower().EndsWith(".xml") || param.ToLower().EndsWith(".json")))
                                    fmgFiles.Add(param);
                                else Console.Error.WriteLine("Warning: Invalid fmg file specified: " + param);
                            }
                            break;
                        case ParamMode.FMGENTRY:
                            if (msgbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith(".msgbnd") || param.ToLower().EndsWith(".msgbnd.dcx")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid msgbnd file specified");
                                    Environment.Exit(10);
                                }
                                msgbndFile = param;
                            }
                            else fmgAdditions.Add(param);
                            break;
                        case ParamMode.LAYOUTMERGE:
                            if (sblytbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith(".sblytbnd") || param.ToLower().EndsWith(".sblytbnd.dcx")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid sblytbnd file specified");
                                    Environment.Exit(14);
                                }
                                sblytbndFile = param;
                            }
                            else
                            {
                                if (Directory.Exists(param))
                                {
                                    foreach (string file in Directory.EnumerateFiles(param))
                                    {
                                        if (File.Exists(file) && (file.ToLower().EndsWith(".layout")))
                                            layoutFiles.Add(file);
                                        else Console.Error.WriteLine("Warning: Invalid layout file specified: " + file);
                                    }
                                }
                                else if (File.Exists(param) && (param.ToLower().EndsWith(".layout")))
                                    layoutFiles.Add(param);
                                else Console.Error.WriteLine("Warning: Invalid layout file specified: " + param);
                            }
                            break;
                        case ParamMode.DDSMERGE:
                            if (tpfFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith(".tpf") || param.ToLower().EndsWith(".tpf.dcx")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid tpf file specified");
                                    Environment.Exit(15);
                                }
                                tpfFile = param;
                            }
                            else
                            {
                                if (Directory.Exists(param))
                                {
                                    foreach (string file in Directory.EnumerateFiles(param))
                                    {
                                        if (File.Exists(file) && (file.ToLower().EndsWith(".dds")))
                                            ddsFiles.Add(file);
                                        else Console.Error.WriteLine("Warning: Invalid DDS file specified: " + file);
                                    }
                                }
                                else if (File.Exists(param) && (param.ToLower().EndsWith(".dds")))
                                    ddsFiles.Add(param);
                                else Console.Error.WriteLine("Warning: Invalid DDS file specified: " + param);
                            }
                            break;
                        case ParamMode.ANIMDIFF:
                        case ParamMode.ANIMERGE:
                            if (anibndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith(".anibnd") || param.ToLower().EndsWith(".anibnd.dcx")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid anibnd file specified");
                                    Environment.Exit(14);
                                }
                                anibndFile = param;
                            }
                            else
                            {
                                if (Directory.Exists(param))
                                {
                                    foreach (string file in Directory.EnumerateFiles(param))
                                    {
                                        if (File.Exists(file) && (file.ToLower().EndsWith(".tae") || file.ToLower().EndsWith(".tae.partial")))
                                            taeFiles.Add(file);
                                        else Console.Error.WriteLine("Warning: Invalid tae file specified: " + file);
                                    }
                                }
                                else if (File.Exists(param) && (param.ToLower().EndsWith(".tae") || param.ToLower().EndsWith(".tae.partial")))
                                    taeFiles.Add(param);
                                else Console.Error.WriteLine("Warning: Invalid tae file specified: " + param);
                            }
                            break;
                        case ParamMode.BNDMERGE:
                            if (destbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith("bnd.dcx") || param.ToLower().EndsWith("bnd")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid bnd file specified");
                                    Environment.Exit(15);
                                }
                                destbndFile = param;
                            }
                            else if (srcbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith("bnd.dcx") || param.ToLower().EndsWith("bnd") ||
                                    param.ToLower().EndsWith("bnd.dcx.partial") || param.ToLower().EndsWith("bnd.partial")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid bnd file or partial bnd file specified");
                                    Environment.Exit(15);
                                }
                                srcbndFile = param;
                            }
                            else Console.Error.WriteLine("Warning: Extra file specified: " + param);
                            break;
                        case ParamMode.BNDDIFF:
                            if (destbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith("bnd.dcx") || param.ToLower().EndsWith("bnd")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid bnd file specified");
                                    Environment.Exit(15);
                                }
                                destbndFile = param;
                            }
                            else if (srcbndFile == null)
                            {
                                if (!File.Exists(param) || !(param.ToLower().EndsWith("bnd.dcx") || param.ToLower().EndsWith("bnd") ||
                                    param.ToLower().EndsWith("bnd.dcx.partial") || param.ToLower().EndsWith("bnd.partial")))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid bnd file or partial bnd file specified");
                                    Environment.Exit(15);
                                }
                                srcbndFile = param;
                            }
                            else Console.Error.WriteLine("Warning: Extra file specified: " + param);
                            break;
                        case ParamMode.HKSMERGE:
                            if (hksFile == null)
                            {
                                if (!File.Exists(param) || !param.ToLower().EndsWith(".hks"))
                                {
                                    Console.Error.WriteLine("ERROR: Invalid HKS file specified");
                                    Environment.Exit(14);
                                }
                                hksFile = param;
                            }
                            else
                            {
                                if (Directory.Exists(param))
                                {
                                    foreach (string file in Directory.EnumerateFiles(param))
                                    {
                                        if (File.Exists(file) && (file.ToLower().EndsWith(".lua") || file.ToLower().EndsWith(".hks")))
                                            luaFiles.Add(file);
                                        else Console.Error.WriteLine("Warning: Invalid lua file specified: " + file);
                                    }
                                }
                                else if (File.Exists(param) && (param.ToLower().EndsWith(".lua") || param.ToLower().EndsWith(".hks")))
                                    luaFiles.Add(param);
                                else Console.Error.WriteLine("Warning: Invalid lua file specified: " + param);
                            }
                            break;
                        case ParamMode.NONE:
                            if (param.ToLower().Equals("help") || param.Equals("?"))
                            {
                                Help(false);
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

        private static void Help(bool pause)
        {
            Console.Out.WriteLine($@"DSMS Portable v{VERSION} by mountlover.");
            Console.Out.WriteLine("Lightweight utility for patching FromSoft param files. Free to distribute with other mods, but not for sale.");
            Console.Out.WriteLine("DS Map Studio Core developed and maintained by the SoulsMods team: https://github.com/soulsmods/DSMapStudio\n");
            Console.Out.WriteLine("Usage: DSMSPortable [paramfile] [-G gametype] [-P gamepath] [-U oldvanillaparams] [-C2M csvfile1 csvfile2 ...]");
            Console.Out.WriteLine("                                [-R paramname1:query1 paramname2:query2 ...] [-C csvfile1 csvfile2 ...]");
            Console.Out.WriteLine("                                [-M[+] masseditfile1 masseditfile2 ...] [-X paramname1[:query] paramname2 ...]");
            Console.Out.WriteLine("                                [-D diffparamfile] [-O outputpath]\n");
            Console.Out.WriteLine("       DSMSPortable --fmgentry [msgbndfile] [name1:id1:text1] [name2:id2:text2] ...");
            Console.Out.WriteLine("       DSMSPortable --fmgmerge [msgbndfile] [fmgfile1 fmgfile2 ...] [-I] [-V]");
            Console.Out.WriteLine("       DSMSPortable --layoutmerge [sblytbndfile] [layoutfile1 layoutfile2 ...] [-I] [-V]");
            Console.Out.WriteLine("       DSMSPortable --texturemerge [tpffile] [ddsfile1 ddsfile2 ...] [-I] [-V]");
            Console.Out.WriteLine("       DSMSPortable --animerge [anibnd] [taefile1 taefile2 ...] [-I] [-V]");
            Console.Out.WriteLine("       DSMSPortable --animdiff [anibnd] [taefile1 taefile2 ...] [-V]");
            Console.Out.WriteLine("       DSMSPortable --bndmerge [destbndfile] [srcbndfile] [-I] [-V] [-S]");
            Console.Out.WriteLine("       DSMSPortable --bnddiff [destbndfile] [srcbndfile] [-V]");
            Console.Out.WriteLine("       DSMSPortable --hksmerge [hksfile] [luafile1 luafile2 ...] [-V]");
            Console.Out.WriteLine();
            Console.Out.WriteLine("  paramfile  Path to regulation.bin file (or respective param file for other FromSoft games) to modify");
            Console.Out.WriteLine("  -G gametype");
            Console.Out.WriteLine("             Code indicating which game is being modified. The default is Elden Ring. Options are as follows:");
            Console.Out.WriteLine("             DS1R  Dark Souls Remastered    DS2  Dark Souls 2    DS3     Dark Souls 3");
            Console.Out.WriteLine("             ER    Elden Ring               BB   Bloodborne      SEKIRO  Sekiro");
            Console.Out.WriteLine("             DS1   Dark Souls PTDE          DES  Demon's Souls");
            Console.Out.WriteLine("  -P gamepath");
            Console.Out.WriteLine("             Path to the main install directory for the selected game, for loading vanilla params.");
            Console.Out.WriteLine("             The gamepath can also be implicitly specified in a gamepath.txt file in the working directory.");
            Console.Out.WriteLine("             Using this switch without specifying a paramfile will return paramfile metadata for that game");
            Console.Out.WriteLine("  -U oldvanillaparams");
            Console.Out.WriteLine("             Upgrades the paramfile to the latest version found in the gamepath, using the specified vanilla");
            Console.Out.WriteLine("             paramfile as a reference. If trying to upgrade a 1.0 param file to 1.1, oldvanillaparams should");
            Console.Out.WriteLine("             be a copy of the original 1.0 param file, and the game install in gamepath should be on 1.1.");
            Console.Out.WriteLine("             Upgrading occurs before processing any edits.");
            Console.Out.WriteLine("  -C2M csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             Converts the specified CSV files into .MASSEDIT scripts.");
            Console.Out.WriteLine("             Resulting files are saved in the same directories as the CSV's provided.");
            Console.Out.WriteLine("             If a valid output path is specified, they will be saved there instead.");
            Console.Out.WriteLine("  -R paramname1:query1 paramname2:query2 ...");
            Console.Out.WriteLine("             Removes the rows specified by the given query from the specified param. Same format as -X.");
            Console.Out.WriteLine("  -C csvfile1 csvfile2 ...");
            Console.Out.WriteLine("             List of CSV files (.TXT or .CSV) containing entire rows of params to add.");
            Console.Out.WriteLine("             Each file's name must perfectly match the param it is modifying (i.e. SpEffectParam.csv).");
            Console.Out.WriteLine("             CSV edits will be always be processed before massedit scripts.");
            Console.Out.WriteLine("  -M[+] masseditfile1 masseditfile2 ...");
            Console.Out.WriteLine("             List of text files (.TXT or .MASSEDIT) containing a script of DS Map Studio MASSEDIT commands.");
            Console.Out.WriteLine("             It is highly recommended to use massedit scripts to modify existing params to avoid conflicts.");
            Console.Out.WriteLine("             Edit scripts of the same type are processed in the order in which they are specified.");
            Console.Out.WriteLine("             If -M+ is specified, any individual ID's found that do not exist in the param file will be");
            Console.Out.WriteLine("             created and populated with default values (usually whatever is in the first entry of the param).");
            Console.Out.WriteLine("  -X paramname1[:query] paramname2 ...");
            Console.Out.WriteLine("             Exports the specified params to CSV, where paramname is the name of the param to be exported,");
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
            Console.Out.WriteLine("             If this is not specified, the input file will be overwritten, and a backup will be made.");
            Console.Out.WriteLine("  --fmgentry [msgbndfile] [name1:id1:text1] [name2:id2:text2] ...");
            Console.Out.WriteLine("             Separate operation mode for adding individual FMG entries to a msgbnd file. -G -P -O still apply");
            Console.Out.WriteLine("             Each argument should be one string with the fmg name, id, and text separated by a colon:");
            Console.Out.WriteLine("             i.e. \"AccessoryName: 6200: Amulet of Defenestration\"");
            Console.Out.WriteLine("             DSMSPortable will interpret new lines from the escape sequence \\n, and will reference the");
            Console.Out.WriteLine("             original text from the keyword $0, i.e  \"AccessoryInfo: 69: $0\\nNow explodes on equip\"");
            Console.Out.WriteLine("  --fmgmerge [msgbndfile] [fmgfile1 fmgfile2 ...] [-I] [-V]");
            Console.Out.WriteLine("             Separate operation mode for merging FMG edits into a msgbnd file. -G, -P, and -O still apply.");
            Console.Out.WriteLine("             First argument is a msgbnd file, with the extension .msgbnd.dcx, latter arguments are files");
            Console.Out.WriteLine("             containing FMG entries, in either json or xml format as exported by DSMS or Yabber, respectively,");
            Console.Out.WriteLine("             or even as raw FMG data files.");
            Console.Out.WriteLine("             If -I is specified, conflicting entries will be ignored and only new entries will be merged.");
            Console.Out.WriteLine("             If -V is specified, verbose output on all edits will be given.");
            Console.Out.WriteLine("  --animerge [anibnd] [taefile1 taefile2 ...] [-I] [-V]");
            Console.Out.WriteLine("             Separate operation mode for merging TAE or partial TAE files into an anibnd file.");
            Console.Out.WriteLine("             Same format as --fmgmerge. -G, -P, and -O still apply.");
            Console.Out.WriteLine("  --animdiff [anibnd] [taefile1 taefile2 ...] [-V]");
            Console.Out.WriteLine("             Separate operation mode for stripping given TAE files into partial TAE's for merging containing");
            Console.Out.WriteLine("             only animations that do not match the given anibnd file. -G, -P, and -O still apply");
            Console.Out.WriteLine("  --layoutmerge [sblytbndfile] [layoutfile1 layoutfile2 ...] [-I] [-V] [-S]");
            Console.Out.WriteLine("             Separate operation mode for merging layout files into a sblytbnd file. -G, -P, and -O still apply");
            Console.Out.WriteLine("             Specifying the -S switch will cause the merged layout file to be sorted by the name field.");
            Console.Out.WriteLine("  --texturemerge [tpffile] [ddsfile1 ddsfile2 ...] [-I] [-V]");
            Console.Out.WriteLine("             Separate operation mode for merging DDS textures into a TPF file. -P and -O still apply.");
            Console.Out.WriteLine("             Same format as other merge operations, but can only merge whole DDS files.");
            Console.Out.WriteLine("  --bndmerge [destbndfile] [srcbndfile] [-I] [-V] [-S]");
            Console.Out.WriteLine("             Generic operation mode for merging two binder files. -G, -P, and -O still apply");
            Console.Out.WriteLine("             Any files in srcbndfile not found in destbndfile will be added to destbndfile.");
            Console.Out.WriteLine("  --bnddiff [destbndfile] [srcbndfile] [-V]");
            Console.Out.WriteLine("             Strips all matching files with srcbndfile out of destbndfile and creates a partial BND file for");
            Console.Out.WriteLine("             more precise BND merging with --bndmerge. -G, -P, and -O still apply");
            Console.Out.WriteLine("  --hksmerge [hksfile] [luafile1 luafile2 ...] [-V]");
            Console.Out.WriteLine("             Merges given lua functions into a decompiled hks file. Will overwrite any overloaded functions.");
            if (pause) Console.ReadKey(true);
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
            REMOVE,
            DIFF,
            UPGRADE,
            NONE,
            FMGMERGE,
            FMGENTRY,
            LAYOUTMERGE,
            DDSMERGE,
            ANIMERGE,
            ANIMDIFF,
            BNDMERGE,
            BNDDIFF,
            HKSMERGE
        }
        // No reason to be anal about the exact switch character used, any of these is fine
        private static bool IsSwitch(string arg)
        {
            return (arg[0] == '\\' || arg[0] == '/' || arg[0] == '-');
        }
    }
}
