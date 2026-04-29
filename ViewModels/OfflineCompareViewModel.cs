using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.ViewModels
{
    /// <summary>
    /// Offline file comparison with Main/Redundant fallback support
    /// Compatible with Visual Studio 2010 / .NET Framework 4.0
    /// </summary>
    public class OfflineCompareViewModel : INotifyPropertyChanged
    {
        // ══════════════════════════════════════════════════════════════════
        //  FRAME PROTOCOL CONSTANTS
        // ══════════════════════════════════════════════════════════════════
        private const int FRAME_SIZE = 26;
        private const int EXTRACT_START_INDEX = 16;
        private const int EXTRACT_LENGTH = 8;
        private const byte FRAME_HEADER_1 = 0x0D;
        private const byte FRAME_HEADER_2 = 0x0A;
        private const int FOOTER_POSITION = 25;
        private const byte FRAME_FOOTER = 0xAB;

        // ══════════════════════════════════════════════════════════════════
        //  BACKGROUND WORKER
        // ══════════════════════════════════════════════════════════════════
        private BackgroundWorker _worker;

        // ══════════════════════════════════════════════════════════════════
        //  BACKING FIELDS
        // ══════════════════════════════════════════════════════════════════
        private string _expectedFilePath;
        private string _singleFilePath;
        private string _mainFilePath;
        private string _redundantFilePath;

        private string _comparisonResult;
        private string _statusMessage;
        private bool _isComparing;
        private bool _useSingleFileMode;
        private int _progressPercent;
        private string _progressText;

        // File info properties
        private string _expectedFileInfo;
        private string _singleFileInfo;
        private string _mainFileInfo;
        private string _redundantFileInfo;

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════
        public OfflineCompareViewModel()
        {
            // Initialize fields
            _expectedFilePath = string.Empty;
            _singleFilePath = string.Empty;
            _mainFilePath = string.Empty;
            _redundantFilePath = string.Empty;
            _comparisonResult = string.Empty;
            _statusMessage = "Ready to compare";
            _isComparing = false;
            _useSingleFileMode = true;
            _progressPercent = 0;
            _progressText = string.Empty;
            _expectedFileInfo = string.Empty;
            _singleFileInfo = string.Empty;
            _mainFileInfo = string.Empty;
            _redundantFileInfo = string.Empty;

            // Initialize background worker
            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            _worker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════════
        public string ExpectedFilePath
        {
            get { return _expectedFilePath; }
            set
            {
                _expectedFilePath = value;
                OnPropertyChanged("ExpectedFilePath");
                UpdateFileInfo();
            }
        }

        public string SingleFilePath
        {
            get { return _singleFilePath; }
            set
            {
                _singleFilePath = value;
                OnPropertyChanged("SingleFilePath");
                UpdateFileInfo();
            }
        }

        public string MainFilePath
        {
            get { return _mainFilePath; }
            set
            {
                _mainFilePath = value;
                OnPropertyChanged("MainFilePath");
                UpdateFileInfo();
            }
        }

        public string RedundantFilePath
        {
            get { return _redundantFilePath; }
            set
            {
                _redundantFilePath = value;
                OnPropertyChanged("RedundantFilePath");
                UpdateFileInfo();
            }
        }

        public string ComparisonResult
        {
            get { return _comparisonResult; }
            set
            {
                _comparisonResult = value;
                OnPropertyChanged("ComparisonResult");
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        public bool IsComparing
        {
            get { return _isComparing; }
            set
            {
                _isComparing = value;
                OnPropertyChanged("IsComparing");
                OnPropertyChanged("IsNotComparing");
            }
        }

        public bool IsNotComparing
        {
            get { return !_isComparing; }
        }

        public bool UseSingleFileMode
        {
            get { return _useSingleFileMode; }
            set
            {
                _useSingleFileMode = value;
                OnPropertyChanged("UseSingleFileMode");
                OnPropertyChanged("UseMultipleFileMode");
                UpdateFileInfo();
            }
        }

        public bool UseMultipleFileMode
        {
            get { return !_useSingleFileMode; }
            set
            {
                _useSingleFileMode = !value;
                OnPropertyChanged("UseSingleFileMode");
                OnPropertyChanged("UseMultipleFileMode");
                UpdateFileInfo();
            }
        }

        public int ProgressPercent
        {
            get { return _progressPercent; }
            set
            {
                _progressPercent = value;
                OnPropertyChanged("ProgressPercent");
            }
        }

        public string ProgressText
        {
            get { return _progressText; }
            set
            {
                _progressText = value;
                OnPropertyChanged("ProgressText");
            }
        }

        // File info properties
        public string ExpectedFileInfo
        {
            get { return _expectedFileInfo; }
            set
            {
                _expectedFileInfo = value;
                OnPropertyChanged("ExpectedFileInfo");
            }
        }

        public string SingleFileInfo
        {
            get { return _singleFileInfo; }
            set
            {
                _singleFileInfo = value;
                OnPropertyChanged("SingleFileInfo");
            }
        }

        public string MainFileInfo
        {
            get { return _mainFileInfo; }
            set
            {
                _mainFileInfo = value;
                OnPropertyChanged("MainFileInfo");
            }
        }

        public string RedundantFileInfo
        {
            get { return _redundantFileInfo; }
            set
            {
                _redundantFileInfo = value;
                OnPropertyChanged("RedundantFileInfo");
            }
        }

        // Ready status
        public bool IsReadyToCompare
        {
            get
            {
                if (string.IsNullOrEmpty(ExpectedFilePath) || !File.Exists(ExpectedFilePath))
                    return false;

                if (UseSingleFileMode)
                {
                    return !string.IsNullOrEmpty(SingleFilePath) && File.Exists(SingleFilePath);
                }
                else
                {
                    return !string.IsNullOrEmpty(MainFilePath) && File.Exists(MainFilePath) &&
                           !string.IsNullOrEmpty(RedundantFilePath) && File.Exists(RedundantFilePath);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC METHODS
        // ══════════════════════════════════════════════════════════════════
        public void StartComparison()
        {
            if (IsComparing || _worker.IsBusy || !IsReadyToCompare)
                return;

            IsComparing = true;
            ComparisonResult = string.Empty;
            ProgressPercent = 0;
            ProgressText = "Starting comparison...";
            StatusMessage = "Comparison in progress...";

            _worker.RunWorkerAsync();
        }

        public void ClearResults()
        {
            ComparisonResult = string.Empty;
            StatusMessage = "Ready to compare";
            ProgressPercent = 0;
            ProgressText = string.Empty;
        }

        // ══════════════════════════════════════════════════════════════════
        //  BACKGROUND WORKER EVENTS
        // ══════════════════════════════════════════════════════════════════
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                e.Result = RunComparison();
            }
            catch (Exception ex)
            {
                ComparisonException compEx = new ComparisonException();
                compEx.Message = ex.Message;
                compEx.FullError = ex.ToString();
                e.Result = compEx;
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressPercent = e.ProgressPercentage;
            if (e.UserState != null)
            {
                ProgressText = e.UserState.ToString();
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsComparing = false;

            if (e.Result is ComparisonException)
            {
                ComparisonException ex = (ComparisonException)e.Result;
                StatusMessage = "ERROR: " + ex.Message;
                ComparisonResult = "═══════════════════════════════════════════════════════════════\n" +
                                   "  COMPARISON FAILED\n" +
                                   "═══════════════════════════════════════════════════════════════\n\n" +
                                   "ERROR: " + ex.Message + "\n\n" +
                                   "DETAILS:\n" + ex.FullError;
                ProgressPercent = 0;
                ProgressText = "Error occurred!";
            }
            else if (e.Result is string)
            {
                ComparisonResult = (string)e.Result;
                ProgressPercent = 100;
                ProgressText = "Comparison complete!";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  MAIN COMPARISON LOGIC
        // ══════════════════════════════════════════════════════════════════
        private string RunComparison()
        {
            DateTime startTime = DateTime.Now;

            ReportProgress(5, "Loading expected commands...");

            // Load expected commands
            List<ExpectedCommand> expectedCommands = LoadExpectedCommands(ExpectedFilePath);

            if (expectedCommands.Count == 0)
            {
                throw new Exception("No valid expected commands found in file!");
            }

            // Get first and last commands as loop markers
            byte[] firstCmd = expectedCommands[0].CommandBytes;
            byte[] lastCmd = expectedCommands[expectedCommands.Count - 1].CommandBytes;
            string firstHex = BitConverter.ToString(firstCmd).Replace("-", " ");
            string lastHex = BitConverter.ToString(lastCmd).Replace("-", " ");

            // Extract loops
            List<List<LogCommand>> mainLoops;
            List<List<LogCommand>> redundantLoops;

            if (UseSingleFileMode)
            {
                ReportProgress(30, "Extracting loops from single file...");
                ExtractLoopsFromSingleFile(SingleFilePath, firstCmd, lastCmd, out mainLoops, out redundantLoops);
            }
            else
            {
                ReportProgress(30, "Extracting MAIN loops...");
                mainLoops = ExtractLoopsFromFile(MainFilePath, "MAIN", firstCmd, lastCmd);

                ReportProgress(50, "Extracting REDUNDANT loops...");
                redundantLoops = ExtractLoopsFromFile(RedundantFilePath, "REDUNDANT", firstCmd, lastCmd);
            }

            if (mainLoops.Count == 0 && redundantLoops.Count == 0)
            {
                string errorMsg = string.Format("No loops detected!\n\n" +
                    "Loop markers used:\n" +
                    "  START: {0}\n" +
                    "  END: {1}\n\n" +
                    "Possible causes:\n" +
                    "  • Commands not found in log file\n" +
                    "  • Log file format does not match\n" +
                    "  • Frame filtering excluding data",
                    firstHex, lastHex);
                throw new Exception(errorMsg);
            }

            ReportProgress(70, string.Format("Found: MAIN={0} loops, RED={1} loops. Comparing...",
                mainLoops.Count, redundantLoops.Count));

            // Compare loops
            ComparisonResults results = CompareLoopsWithExpected(expectedCommands, mainLoops, redundantLoops);

            ReportProgress(90, "Generating report...");

            // Generate detailed report
            StringBuilder report = GenerateDetailedReport(expectedCommands, mainLoops, redundantLoops, results, startTime);

            TimeSpan elapsed = DateTime.Now - startTime;
            StatusMessage = string.Format("Complete: {0}/{1} loops PASS | {2:F1}s",
                results.PassCount, results.TotalLoops, elapsed.TotalSeconds);

            return report.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD EXPECTED COMMANDS
        // ══════════════════════════════════════════════════════════════════
        private List<ExpectedCommand> LoadExpectedCommands(string filePath)
        {
            List<ExpectedCommand> commands = new List<ExpectedCommand>();
            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("=") ||
                    trimmedLine.StartsWith("#") ||
                    trimmedLine.StartsWith("//"))
                    continue;

                byte[] payload = ParseExpectedLine(trimmedLine);
                if (payload != null && payload.Length == 8 && !IsAllZeros(payload))
                {
                    ExpectedCommand cmd = new ExpectedCommand();
                    cmd.CommandBytes = payload;
                    cmd.HexString = BitConverter.ToString(payload).Replace("-", " ");
                    commands.Add(cmd);
                }
            }

            return commands;
        }

        // ══════════════════════════════════════════════════════════════════
        //  EXTRACT LOOPS FROM SINGLE FILE (MAIN + REDUNDANT)
        // ══════════════════════════════════════════════════════════════════
        private void ExtractLoopsFromSingleFile(string filePath, byte[] startMarker, byte[] endMarker,
                                                out List<List<LogCommand>> mainLoops,
                                                out List<List<LogCommand>> redundantLoops)
        {
            mainLoops = new List<List<LogCommand>>();
            redundantLoops = new List<List<LogCommand>>();

            List<LogCommand> allCommands = LoadLogCommands(filePath, string.Empty);

            // Split into main and redundant based on PORT identifier
            List<LogCommand> mainCommands = new List<LogCommand>();
            List<LogCommand> redCommands = new List<LogCommand>();

            foreach (LogCommand cmd in allCommands)
            {
                if (cmd.Source.Contains("MAIN") || cmd.Source.Contains("PORT1"))
                {
                    mainCommands.Add(cmd);
                }
                else if (cmd.Source.Contains("RED") || cmd.Source.Contains("PORT2"))
                {
                    redCommands.Add(cmd);
                }
            }

            mainLoops = ExtractLoopsFromCommands(mainCommands, startMarker, endMarker);
            redundantLoops = ExtractLoopsFromCommands(redCommands, startMarker, endMarker);
        }

        // ══════════════════════════════════════════════════════════════════
        //  EXTRACT LOOPS FROM FILE
        // ══════════════════════════════════════════════════════════════════
        private List<List<LogCommand>> ExtractLoopsFromFile(string filePath, string source,
                                                           byte[] startMarker, byte[] endMarker)
        {
            List<LogCommand> commands = LoadLogCommands(filePath, source);
            return ExtractLoopsFromCommands(commands, startMarker, endMarker);
        }

        // ══════════════════════════════════════════════════════════════════
        //  EXTRACT LOOPS FROM COMMANDS
        // ══════════════════════════════════════════════════════════════════
        private List<List<LogCommand>> ExtractLoopsFromCommands(List<LogCommand> commands,
                                                               byte[] startMarker, byte[] endMarker)
        {
            List<List<LogCommand>> loops = new List<List<LogCommand>>();
            List<LogCommand> currentLoop = new List<LogCommand>();
            bool inLoop = false;

            foreach (LogCommand cmd in commands)
            {
                // Check for start marker
                if (CompareByteArrays(cmd.Command8, startMarker))
                {
                    // If already in loop, save current loop
                    if (inLoop && currentLoop.Count > 0)
                    {
                        loops.Add(new List<LogCommand>(currentLoop));
                    }

                    // Start new loop
                    currentLoop.Clear();
                    currentLoop.Add(cmd);
                    inLoop = true;
                }
                else if (inLoop)
                {
                    currentLoop.Add(cmd);

                    // Check for end marker
                    if (CompareByteArrays(cmd.Command8, endMarker))
                    {
                        loops.Add(new List<LogCommand>(currentLoop));
                        currentLoop.Clear();
                        inLoop = false;
                    }
                }
            }

            // Add last loop if still in progress
            if (inLoop && currentLoop.Count > 0)
            {
                loops.Add(currentLoop);
            }

            return loops;
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD LOG COMMANDS FROM FILE
        // ══════════════════════════════════════════════════════════════════
        private List<LogCommand> LoadLogCommands(string filePath, string source)
        {
            List<LogCommand> commands = new List<LogCommand>();
            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                byte[] frameBytes = ParseHexLine(line);
                if (frameBytes == null || frameBytes.Length != FRAME_SIZE)
                    continue;

                // Validate frame
                if (frameBytes[0] != FRAME_HEADER_1 ||
                    frameBytes[1] != FRAME_HEADER_2 ||
                    frameBytes[FOOTER_POSITION] != FRAME_FOOTER)
                    continue;

                // Extract 8 bytes from positions [16-23]
                byte[] payload = new byte[EXTRACT_LENGTH];
                Array.Copy(frameBytes, EXTRACT_START_INDEX, payload, 0, EXTRACT_LENGTH);

                // Skip all-zero payloads
                if (IsAllZeros(payload))
                    continue;

                LogCommand cmd = new LogCommand();
                cmd.Command8 = payload;
                cmd.Source = string.IsNullOrEmpty(source) ?
                    (line.Contains("PORT1") ? "MAIN" :
                     line.Contains("PORT2") ? "REDUNDANT" : "UNKNOWN") : source;
                cmd.OriginalLine = line;
                commands.Add(cmd);
            }

            return commands;
        }

        // ══════════════════════════════════════════════════════════════════
        //  COMPARE LOOPS WITH EXPECTED
        // ══════════════════════════════════════════════════════════════════
        private ComparisonResults CompareLoopsWithExpected(List<ExpectedCommand> expectedCommands,
                                                          List<List<LogCommand>> mainLoops,
                                                          List<List<LogCommand>> redundantLoops)
        {
            ComparisonResults results = new ComparisonResults();
            int totalLoops = Math.Max(mainLoops.Count, redundantLoops.Count);

            for (int loopIdx = 0; loopIdx < totalLoops; loopIdx++)
            {
                List<LogCommand> mainLoop = loopIdx < mainLoops.Count ? mainLoops[loopIdx] : null;
                List<LogCommand> redLoop = loopIdx < redundantLoops.Count ? redundantLoops[loopIdx] : null;

                LoopCompareResult loopResult = new LoopCompareResult();
                loopResult.LoopIndex = loopIdx;
                loopResult.MainCommands = mainLoop != null ? mainLoop.Count : 0;
                loopResult.RedundantCommands = redLoop != null ? redLoop.Count : 0;

                foreach (ExpectedCommand expected in expectedCommands)
                {
                    bool foundInMain = false;
                    bool foundInRed = false;

                    if (mainLoop != null)
                    {
                        foreach (LogCommand cmd in mainLoop)
                        {
                            if (CompareByteArrays(expected.CommandBytes, cmd.Command8))
                            {
                                foundInMain = true;
                                break;
                            }
                        }
                    }

                    if (!foundInMain && redLoop != null)
                    {
                        foreach (LogCommand cmd in redLoop)
                        {
                            if (CompareByteArrays(expected.CommandBytes, cmd.Command8))
                            {
                                foundInRed = true;
                                break;
                            }
                        }
                    }

                    if (foundInMain || foundInRed)
                    {
                        loopResult.MatchedCommands++;
                        if (!foundInMain && foundInRed)
                            loopResult.TookFromRedundant++;
                    }
                    else
                    {
                        loopResult.MissingCommands.Add(expected.HexString);
                    }
                }

                loopResult.IsPassed = loopResult.MatchedCommands == expectedCommands.Count;
                results.LoopResults.Add(loopResult);

                if (loopResult.IsPassed)
                    results.PassCount++;
                else
                    results.FailCount++;
            }

            results.TotalLoops = totalLoops;

            // Calculate total took from redundant
            int totalFromRed = 0;
            foreach (LoopCompareResult loopResult in results.LoopResults)
            {
                totalFromRed += loopResult.TookFromRedundant;
            }
            results.TotalTookFromRedundant = totalFromRed;

            return results;
        }

        // ══════════════════════════════════════════════════════════════════
        //  GENERATE DETAILED REPORT
        // ══════════════════════════════════════════════════════════════════
        private StringBuilder GenerateDetailedReport(List<ExpectedCommand> expectedCommands,
                                                    List<List<LogCommand>> mainLoops,
                                                    List<List<LogCommand>> redundantLoops,
                                                    ComparisonResults results,
                                                    DateTime startTime)
        {
            StringBuilder report = new StringBuilder();
            TimeSpan elapsed = DateTime.Now - startTime;

            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendLine("  OFFLINE COMPARE — MAIN/REDUNDANT FALLBACK MODE");
            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendLine();

            // File info
            report.AppendLine("FILES:");
            report.AppendFormat("  Expected:    {0}\n", Path.GetFileName(ExpectedFilePath));
            if (UseSingleFileMode)
            {
                report.AppendFormat("  Log File:    {0}\n", Path.GetFileName(SingleFilePath));
            }
            else
            {
                report.AppendFormat("  Main File:   {0}\n", Path.GetFileName(MainFilePath));
                report.AppendFormat("  Red. File:   {0}\n", Path.GetFileName(RedundantFilePath));
            }
            report.AppendLine();

            // Statistics
            report.AppendLine("STATISTICS:");
            report.AppendFormat("  Expected Commands:    {0}\n", expectedCommands.Count);
            report.AppendFormat("  Main Loops Found:     {0}\n", mainLoops.Count);
            report.AppendFormat("  Redundant Loops:      {0}\n", redundantLoops.Count);
            report.AppendFormat("  Total Loops:          {0}\n", results.TotalLoops);
            report.AppendLine();

            // Loop markers
            string firstHex = BitConverter.ToString(expectedCommands[0].CommandBytes).Replace("-", " ");
            string lastHex = BitConverter.ToString(expectedCommands[expectedCommands.Count - 1].CommandBytes).Replace("-", " ");
            report.AppendLine("LOOP MARKERS:");
            report.AppendFormat("  Start: {0}\n", firstHex);
            report.AppendFormat("  End:   {0}\n", lastHex);
            report.AppendLine();

            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendLine("LOOP-BY-LOOP RESULTS:");
            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendLine();

            // Loop results
            for (int i = 0; i < results.LoopResults.Count; i++)
            {
                LoopCompareResult loopResult = results.LoopResults[i];
                string status = loopResult.IsPassed ? "✓ PASS" : "✗ FAIL";

                report.AppendFormat("LOOP {0:D3}: {1} [{2}/{3}] Main={4} Red={5}",
                    i + 1, status,
                    loopResult.MatchedCommands, expectedCommands.Count,
                    loopResult.MainCommands, loopResult.RedundantCommands);

                if (loopResult.TookFromRedundant > 0)
                {
                    report.AppendFormat(" (Red.Used={0})", loopResult.TookFromRedundant);
                }

                report.AppendLine();

                if (loopResult.MissingCommands.Count > 0)
                {
                    report.AppendLine("      Missing Commands:");
                    foreach (string missing in loopResult.MissingCommands)
                    {
                        report.AppendFormat("        • {0}\n", missing);
                    }
                }
            }

            report.AppendLine();
            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendLine("FINAL SUMMARY:");
            report.AppendLine("═══════════════════════════════════════════════════════════════");
            report.AppendFormat("  PASS Loops:           {0}\n", results.PassCount);
            report.AppendFormat("  FAIL Loops:           {0}\n", results.FailCount);
            report.AppendFormat("  TOTAL Loops:          {0}\n", results.TotalLoops);
            report.AppendFormat("  Used Redundant:       {0} commands\n", results.TotalTookFromRedundant);
            report.AppendFormat("  Processing Time:      {0:F1} seconds\n", elapsed.TotalSeconds);
            report.AppendLine();

            if (results.PassCount == results.TotalLoops && results.TotalLoops > 0)
            {
                report.AppendLine("  STATUS:               ✓✓✓ ALL LOOPS PASSED ✓✓✓");
            }
            else
            {
                report.AppendFormat("  STATUS:               ✗✗✗ {0} LOOP(S) FAILED ✗✗✗\n", results.FailCount);
            }

            report.AppendLine("═══════════════════════════════════════════════════════════════");

            return report;
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPER METHODS
        // ══════════════════════════════════════════════════════════════════
        private void ReportProgress(int percent, string text)
        {
            if (_worker != null)
            {
                _worker.ReportProgress(percent, text);
            }
        }

        private byte[] ParseExpectedLine(string line)
        {
            try
            {
                line = line.Trim();
                string hexOnly = System.Text.RegularExpressions.Regex.Replace(line, @"[^0-9A-Fa-f]", "");

                if (hexOnly.Length != 16)
                    return null;

                byte[] bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    bytes[i] = Convert.ToByte(hexOnly.Substring(i * 2, 2), 16);
                }
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        private byte[] ParseHexLine(string line)
        {
            try
            {
                line = line.Trim();
                if (line.Contains(":"))
                {
                    int lastColonIndex = line.LastIndexOf(':');
                    line = line.Substring(lastColonIndex + 1).Trim();
                }

                string[] parts = line.Split(new char[] { ' ', '\t', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
                List<byte> bytes = new List<byte>();

                foreach (string part in parts)
                {
                    string cleanPart = part.Trim();
                    if (string.IsNullOrEmpty(cleanPart))
                        continue;

                    if (cleanPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        bytes.Add(Convert.ToByte(cleanPart.Substring(2), 16));
                    }
                    else if (cleanPart.Length == 2)
                    {
                        bytes.Add(Convert.ToByte(cleanPart, 16));
                    }
                }

                return bytes.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private bool IsAllZeros(byte[] data)
        {
            foreach (byte b in data)
            {
                if (b != 0)
                    return false;
            }
            return true;
        }

        private bool CompareByteArrays(byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }
            return true;
        }

        private void UpdateFileInfo()
        {
            // Update expected file info
            if (File.Exists(ExpectedFilePath))
            {
                try
                {
                    FileInfo fi = new FileInfo(ExpectedFilePath);
                    int cmdCount = LoadExpectedCommands(ExpectedFilePath).Count;
                    ExpectedFileInfo = string.Format("{0} ({1:N0} bytes, {2} commands)", fi.Name, fi.Length, cmdCount);
                }
                catch
                {
                    ExpectedFileInfo = Path.GetFileName(ExpectedFilePath);
                }
            }
            else
            {
                ExpectedFileInfo = "No file selected";
            }

            // Update other file info based on mode
            if (UseSingleFileMode)
            {
                if (File.Exists(SingleFilePath))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(SingleFilePath);
                        string[] lines = File.ReadAllLines(SingleFilePath);
                        SingleFileInfo = string.Format("{0} ({1:N0} bytes, {2} lines)", fi.Name, fi.Length, lines.Length);
                    }
                    catch
                    {
                        SingleFileInfo = Path.GetFileName(SingleFilePath);
                    }
                }
                else
                {
                    SingleFileInfo = "No file selected";
                }
            }
            else
            {
                // Main file info
                if (File.Exists(MainFilePath))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(MainFilePath);
                        string[] lines = File.ReadAllLines(MainFilePath);
                        MainFileInfo = string.Format("{0} ({1:N0} bytes, {2} lines)", fi.Name, fi.Length, lines.Length);
                    }
                    catch
                    {
                        MainFileInfo = Path.GetFileName(MainFilePath);
                    }
                }
                else
                {
                    MainFileInfo = "No file selected";
                }

                // Redundant file info
                if (File.Exists(RedundantFilePath))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(RedundantFilePath);
                        string[] lines = File.ReadAllLines(RedundantFilePath);
                        RedundantFileInfo = string.Format("{0} ({1:N0} bytes, {2} lines)", fi.Name, fi.Length, lines.Length);
                    }
                    catch
                    {
                        RedundantFileInfo = Path.GetFileName(RedundantFilePath);
                    }
                }
                else
                {
                    RedundantFileInfo = "No file selected";
                }
            }

            OnPropertyChanged("IsReadyToCompare");
        }

        // ══════════════════════════════════════════════════════════════════
        //  INotifyPropertyChanged
        // ══════════════════════════════════════════════════════════════════
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPER CLASSES
        // ══════════════════════════════════════════════════════════════════
        public class ExpectedCommand
        {
            public byte[] CommandBytes { get; set; }
            public string HexString { get; set; }
        }

        public class LogCommand
        {
            public byte[] Command8 { get; set; }
            public string Source { get; set; }
            public string OriginalLine { get; set; }
        }

        public class LoopCompareResult
        {
            public int LoopIndex { get; set; }
            public int MainCommands { get; set; }
            public int RedundantCommands { get; set; }
            public int MatchedCommands { get; set; }
            public int TookFromRedundant { get; set; }
            public bool IsPassed { get; set; }
            public List<string> MissingCommands { get; set; }

            public LoopCompareResult()
            {
                MissingCommands = new List<string>();
            }
        }

        public class ComparisonResults
        {
            public List<LoopCompareResult> LoopResults { get; set; }
            public int PassCount { get; set; }
            public int FailCount { get; set; }
            public int TotalLoops { get; set; }
            public int TotalTookFromRedundant { get; set; }

            public ComparisonResults()
            {
                LoopResults = new List<LoopCompareResult>();
            }
        }

        public class ComparisonException
        {
            public string Message { get; set; }
            public string FullError { get; set; }
        }
    }
}