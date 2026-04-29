using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.ViewModels
{
    /// <summary>
    /// OfflineCompareViewModel - Handles offline file comparison with loop tracking
    /// Compatible with .NET 4.0 (VS2010)
    /// </summary>
    public class OfflineCompareViewModel : INotifyPropertyChanged
    {
        // =============================================
        // FRAME STRUCTURE CONSTANTS
        // =============================================
        private const byte START_BYTE_1 = 0x0D;
        private const byte START_BYTE_2 = 0x0A;
        private const byte END_BYTE = 0xAB;
        private const int FRAME_SIZE = 26;
        private const int CMD_OFFSET = 16;
        private const int CMD_SIZE = 8;
        private const int SOURCE_BYTE_INDEX = 11;
        private const byte SOURCE_BIT_MASK = 0x10;

        // =============================================
        // PRIVATE FIELDS
        // =============================================
        private string _expectedFilePath = "";
        private string _singleFilePath = "";
        private string _mainFilePath = "";
        private string _redundantFilePath = "";
        private string _comparisonResult = "";
        private string _statusMessage = "Ready";
        private bool _isMultipleFileMode = false;
        private bool _isProcessing = false;

        // =============================================
        // PROPERTY CHANGED EVENT
        // =============================================
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        // =============================================
        // PROPERTIES
        // =============================================
        public string ExpectedFilePath
        {
            get { return _expectedFilePath; }
            set { SetProperty(ref _expectedFilePath, value, "ExpectedFilePath"); }
        }

        public string SingleFilePath
        {
            get { return _singleFilePath; }
            set { SetProperty(ref _singleFilePath, value, "SingleFilePath"); }
        }

        public string MainFilePath
        {
            get { return _mainFilePath; }
            set { SetProperty(ref _mainFilePath, value, "MainFilePath"); }
        }

        public string RedundantFilePath
        {
            get { return _redundantFilePath; }
            set { SetProperty(ref _redundantFilePath, value, "RedundantFilePath"); }
        }

        public string ComparisonResult
        {
            get { return _comparisonResult; }
            set { SetProperty(ref _comparisonResult, value, "ComparisonResult"); }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value, "StatusMessage"); }
        }

        public bool IsMultipleFileMode
        {
            get { return _isMultipleFileMode; }
            set { SetProperty(ref _isMultipleFileMode, value, "IsMultipleFileMode"); }
        }

        public bool IsProcessing
        {
            get { return _isProcessing; }
            set { SetProperty(ref _isProcessing, value, "IsProcessing"); }
        }

        // =============================================
        // HELPER CLASSES
        // =============================================
        private class ExpectedCommand
        {
            public byte[] CommandBytes { get; set; }
            public string HexString { get; set; }
            public bool FoundInMain { get; set; }
            public bool FoundInRedundant { get; set; }
            public HashSet<int> FoundInMainLoops { get; set; }
            public HashSet<int> FoundInRedundantLoops { get; set; }

            public ExpectedCommand(byte[] cmdBytes)
            {
                CommandBytes = new byte[cmdBytes.Length];
                Array.Copy(cmdBytes, CommandBytes, cmdBytes.Length);
                HexString = BitConverter.ToString(cmdBytes).Replace("-", " ");
                FoundInMain = false;
                FoundInRedundant = false;
                FoundInMainLoops = new HashSet<int>();
                FoundInRedundantLoops = new HashSet<int>();
            }
        }

        private class LogCommand
        {
            public DateTime Timestamp { get; set; }
            public string Port { get; set; }
            public int FrameNumber { get; set; }
            public byte[] Command8 { get; set; }
            public string Source { get; set; }
            public string HexString { get; set; }
        }

        private class ComparisonResult
        {
            public int PassCount { get; set; }
            public int FailCount { get; set; }
            public int TotalLoops { get; set; }
            public int TotalTookFromRed { get; set; }
            public double SuccessRate { get; set; }
        }

        // =============================================
        // PUBLIC METHODS
        // =============================================
        public void StartComparison()
        {
            if (IsProcessing)
            {
                StatusMessage = "Comparison already in progress...";
                return;
            }

            // Validate inputs
            if (string.IsNullOrEmpty(_expectedFilePath) || !File.Exists(_expectedFilePath))
            {
                StatusMessage = "ERROR: Expected commands file not selected or not found";
                return;
            }

            if (!_isMultipleFileMode)
            {
                if (string.IsNullOrEmpty(_singleFilePath) || !File.Exists(_singleFilePath))
                {
                    StatusMessage = "ERROR: Single log file not selected or not found";
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(_mainFilePath) || !File.Exists(_mainFilePath))
                {
                    StatusMessage = "ERROR: MAIN file not selected or not found";
                    return;
                }
                if (string.IsNullOrEmpty(_redundantFilePath) || !File.Exists(_redundantFilePath))
                {
                    StatusMessage = "ERROR: REDUNDANT file not selected or not found";
                    return;
                }
            }

            IsProcessing = true;
            StatusMessage = "Processing...";
            ComparisonResult = "";

            // Run on background thread (.NET 4.0 compatible)
            Thread workerThread = new Thread(PerformComparison);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        // =============================================
        // PRIVATE METHODS
        // =============================================
        private void PerformComparison()
        {
            try
            {
                DateTime startTime = DateTime.Now;

                // Load expected commands
                StatusMessage = "Loading expected commands...";
                List<ExpectedCommand> expectedCommands = LoadExpectedCommands(_expectedFilePath);

                if (expectedCommands.Count == 0)
                {
                    StatusMessage = "ERROR: No valid expected commands found";
                    return;
                }

                byte[] firstCmd = expectedCommands[0].CommandBytes;
                byte[] lastCmd = expectedCommands[expectedCommands.Count - 1].CommandBytes;

                List<List<LogCommand>> mainLoops;
                List<List<LogCommand>> redundantLoops;

                if (!_isMultipleFileMode)
                {
                    StatusMessage = "Extracting loops from single file...";
                    ExtractLoopsFromSingleFile(_singleFilePath, firstCmd, lastCmd,
                        out mainLoops, out redundantLoops);
                }
                else
                {
                    StatusMessage = "Extracting MAIN and REDUNDANT loops...";
                    mainLoops = ExtractLoopsFromFile(_mainFilePath, "MAIN", firstCmd, lastCmd);
                    redundantLoops = ExtractLoopsFromFile(_redundantFilePath, "REDUNDANT", firstCmd, lastCmd);
                }

                if (mainLoops.Count == 0 && redundantLoops.Count == 0)
                {
                    StatusMessage = "ERROR: No loops detected in files";
                    return;
                }

                // Perform comparison
                StatusMessage = "Comparing loops...";
                ComparisonResult result = CompareLoopsWithExpected(expectedCommands, mainLoops, redundantLoops);

                // Generate report
                TimeSpan elapsed = DateTime.Now - startTime;
                GenerateReport(result, expectedCommands, mainLoops, redundantLoops, elapsed.TotalSeconds);

                StatusMessage = string.Format("Complete: {0}/{1} loops PASS | {2:F1}s",
                    result.PassCount,
                    result.TotalLoops,
                    elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                StatusMessage = "ERROR: " + ex.Message;
                ComparisonResult = "Exception:\n" + ex.Message + "\n\n" + ex.StackTrace;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private List<ExpectedCommand> LoadExpectedCommands(string filePath)
        {
            List<ExpectedCommand> commands = new List<ExpectedCommand>();
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (trimmed.StartsWith("#")) continue;
                    if (trimmed.StartsWith("//")) continue;

                    byte[] bytes = ParseExpectedCommandLine(trimmed);
                    if (bytes != null && bytes.Length == 8)
                    {
                        bool duplicate = false;
                        foreach (ExpectedCommand cmd in commands)
                        {
                            if (CompareByteArrays(cmd.CommandBytes, bytes))
                            {
                                duplicate = true;
                                break;
                            }
                        }
                        if (!duplicate)
                            commands.Add(new ExpectedCommand(bytes));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load expected commands: " + ex.Message);
            }
            return commands;
        }

        private byte[] ParseExpectedCommandLine(string line)
        {
            try
            {
                string cleaned = line.Trim()
                    .Replace(" ", "")
                    .Replace(",", "")
                    .Replace("-", "")
                    .Replace("\t", "")
                    .Replace("0x", "")
                    .Replace("0X", "");

                if (cleaned.Length == 16)
                {
                    byte[] bytes = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        string hexByte = cleaned.Substring(i * 2, 2);
                        if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                            return null;
                    }
                    return bytes;
                }

                string[] parts = line.Split(new char[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 8)
                {
                    byte[] bytes = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        string hexByte = parts[i].Replace("0x", "").Replace("0X", "");
                        if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                            return null;
                    }
                    return bytes;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ExtractLoopsFromSingleFile(string filePath, byte[] firstCmd, byte[] lastCmd,
            out List<List<LogCommand>> mainLoops, out List<List<LogCommand>> redundantLoops)
        {
            mainLoops = new List<List<LogCommand>>();
            redundantLoops = new List<List<LogCommand>>();

            List<LogCommand> currentMainLoop = null;
            List<LogCommand> currentRedLoop = null;
            bool mainStarted = false;
            bool redStarted = false;
            bool firstEqualsLast = CompareByteArrays(firstCmd, lastCmd);

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (IsHeaderLine(line)) continue;

                    byte[] frame = ParseLineToFrame(line);
                    if (frame == null) continue;

                    byte[] command8 = new byte[CMD_SIZE];
                    Array.Copy(frame, CMD_OFFSET, command8, 0, CMD_SIZE);

                    if (IsAllZeros(command8)) continue;

                    bool isMain = (frame[SOURCE_BYTE_INDEX] & SOURCE_BIT_MASK) == 0;
                    int frameCnt = (frame[2] << 8) | frame[3];

                    bool isFirstCmd = CompareByteArrays(command8, firstCmd);
                    bool isLastCmd = CompareByteArrays(command8, lastCmd);

                    if (isMain)
                    {
                        if (firstEqualsLast && isFirstCmd)
                        {
                            if (currentMainLoop != null && currentMainLoop.Count > 0)
                                mainLoops.Add(currentMainLoop);
                            currentMainLoop = new List<LogCommand>();
                            mainStarted = true;
                        }
                        else if (!firstEqualsLast && isFirstCmd)
                        {
                            currentMainLoop = new List<LogCommand>();
                            mainStarted = true;
                        }

                        if (mainStarted && currentMainLoop != null && !ContainsCommand(currentMainLoop, command8))
                        {
                            currentMainLoop.Add(new LogCommand
                            {
                                Command8 = command8,
                                FrameNumber = frameCnt,
                                Source = "MAIN"
                            });
                        }

                        if (!firstEqualsLast && isLastCmd && !isFirstCmd && currentMainLoop != null && currentMainLoop.Count > 0)
                        {
                            mainLoops.Add(currentMainLoop);
                            currentMainLoop = null;
                            mainStarted = false;
                        }
                    }
                    else
                    {
                        if (firstEqualsLast && isFirstCmd)
                        {
                            if (currentRedLoop != null && currentRedLoop.Count > 0)
                                redundantLoops.Add(currentRedLoop);
                            currentRedLoop = new List<LogCommand>();
                            redStarted = true;
                        }
                        else if (!firstEqualsLast && isFirstCmd)
                        {
                            currentRedLoop = new List<LogCommand>();
                            redStarted = true;
                        }

                        if (redStarted && currentRedLoop != null && !ContainsCommand(currentRedLoop, command8))
                        {
                            currentRedLoop.Add(new LogCommand
                            {
                                Command8 = command8,
                                FrameNumber = frameCnt,
                                Source = "REDUNDANT"
                            });
                        }

                        if (!firstEqualsLast && isLastCmd && !isFirstCmd && currentRedLoop != null && currentRedLoop.Count > 0)
                        {
                            redundantLoops.Add(currentRedLoop);
                            currentRedLoop = null;
                            redStarted = false;
                        }
                    }
                }

                if (currentMainLoop != null && currentMainLoop.Count > 0)
                    mainLoops.Add(currentMainLoop);
                if (currentRedLoop != null && currentRedLoop.Count > 0)
                    redundantLoops.Add(currentRedLoop);
            }
            catch (Exception ex)
            {
                throw new Exception("ExtractLoopsFromSingleFile failed: " + ex.Message);
            }
        }

        private List<List<LogCommand>> ExtractLoopsFromFile(string filePath, string expectedSource,
            byte[] firstCmd, byte[] lastCmd)
        {
            List<List<LogCommand>> allLoops = new List<List<LogCommand>>();
            bool firstEqualsLast = CompareByteArrays(firstCmd, lastCmd);
            List<LogCommand> currentLoop = null;
            bool loopStarted = false;

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (IsHeaderLine(line)) continue;

                    byte[] frame = ParseLineToFrame(line);
                    if (frame == null) continue;

                    byte[] command8 = new byte[CMD_SIZE];
                    Array.Copy(frame, CMD_OFFSET, command8, 0, CMD_SIZE);
                    if (IsAllZeros(command8)) continue;

                    bool isMain = (frame[SOURCE_BYTE_INDEX] & SOURCE_BIT_MASK) == 0;
                    string detectedSource = isMain ? "MAIN" : "REDUNDANT";

                    if (detectedSource != expectedSource) continue;

                    int frameCnt = (frame[2] << 8) | frame[3];
                    bool isFirstCmd = CompareByteArrays(command8, firstCmd);
                    bool isLastCmd = CompareByteArrays(command8, lastCmd);

                    if (firstEqualsLast && isFirstCmd)
                    {
                        if (currentLoop != null && currentLoop.Count > 0)
                            allLoops.Add(currentLoop);
                        currentLoop = new List<LogCommand>();
                        loopStarted = true;
                    }
                    else if (!firstEqualsLast && isFirstCmd)
                    {
                        currentLoop = new List<LogCommand>();
                        loopStarted = true;
                    }

                    if (loopStarted && currentLoop != null && !ContainsCommand(currentLoop, command8))
                    {
                        currentLoop.Add(new LogCommand
                        {
                            Command8 = command8,
                            FrameNumber = frameCnt,
                            Source = detectedSource
                        });
                    }

                    if (!firstEqualsLast && isLastCmd && !isFirstCmd && currentLoop != null && currentLoop.Count > 0)
                    {
                        allLoops.Add(currentLoop);
                        currentLoop = null;
                        loopStarted = false;
                    }
                }

                if (currentLoop != null && currentLoop.Count > 0)
                    allLoops.Add(currentLoop);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("ExtractLoopsFromFile failed [{0}]: {1}", expectedSource, ex.Message));
            }

            return allLoops;
        }

        private ComparisonResult CompareLoopsWithExpected(List<ExpectedCommand> expectedCommands,
            List<List<LogCommand>> mainLoops, List<List<LogCommand>> redundantLoops)
        {
            ComparisonResult result = new ComparisonResult();
            int totalLoops = Math.Max(mainLoops.Count, redundantLoops.Count);
            result.TotalLoops = totalLoops;

            for (int loopIdx = 0; loopIdx < totalLoops; loopIdx++)
            {
                List<LogCommand> mainLoop = loopIdx < mainLoops.Count ? mainLoops[loopIdx] : null;
                List<LogCommand> redLoop = loopIdx < redundantLoops.Count ? redundantLoops[loopIdx] : null;

                int trulyMissing = 0;
                foreach (ExpectedCommand expected in expectedCommands)
                {
                    bool inMain = false;
                    if (mainLoop != null)
                    {
                        foreach (LogCommand cmd in mainLoop)
                        {
                            if (CompareByteArrays(expected.CommandBytes, cmd.Command8))
                            {
                                inMain = true;
                                break;
                            }
                        }
                    }

                    if (!inMain)
                    {
                        bool inRed = false;
                        if (redLoop != null)
                        {
                            foreach (LogCommand cmd in redLoop)
                            {
                                if (CompareByteArrays(expected.CommandBytes, cmd.Command8))
                                {
                                    inRed = true;
                                    break;
                                }
                            }
                        }

                        if (inRed)
                            result.TotalTookFromRed++;
                        else
                            trulyMissing++;
                    }
                }

                if (trulyMissing == 0)
                    result.PassCount++;
                else
                    result.FailCount++;
            }

            result.SuccessRate = result.TotalLoops > 0 ? (result.PassCount * 100.0 / result.TotalLoops) : 0.0;
            return result;
        }

        private void GenerateReport(ComparisonResult result, List<ExpectedCommand> expectedCommands,
            List<List<LogCommand>> mainLoops, List<List<LogCommand>> redundantLoops, double elapsedSeconds)
        {
            StringBuilder report = new StringBuilder();
            string sepLine = new string('=', 80);

            report.AppendLine(sepLine);
            report.AppendLine("OFFLINE LOOP-BY-LOOP VERIFICATION REPORT");
            report.AppendLine("Logic: MAIN first -> missing only checked in REDUNDANT");
            report.AppendLine(sepLine);
            report.AppendLine();

            report.AppendLine("FILES:");
            report.AppendLine(string.Format("  Expected    : {0}", Path.GetFileName(_expectedFilePath)));
            if (!_isMultipleFileMode)
            {
                report.AppendLine(string.Format("  Log File    : {0}", Path.GetFileName(_singleFilePath)));
            }
            else
            {
                report.AppendLine(string.Format("  Main        : {0}", Path.GetFileName(_mainFilePath)));
                report.AppendLine(string.Format("  Redundant   : {0}", Path.GetFileName(_redundantFilePath)));
            }
            report.AppendLine();

            report.AppendLine("LOOP DETECTION:");
            report.AppendLine(string.Format("  MAIN Loops   : {0}", mainLoops.Count));
            report.AppendLine(string.Format("  RED Loops    : {0}", redundantLoops.Count));
            report.AppendLine(string.Format("  Expected Cmds: {0}", expectedCommands.Count));
            report.AppendLine();
            report.AppendLine(sepLine);
            report.AppendLine();

            report.AppendLine("FINAL SUMMARY");
            report.AppendLine(string.Format("Expected Commands : {0}", expectedCommands.Count));
            report.AppendLine(string.Format("Total Loops       : {0}", result.TotalLoops));
            report.AppendLine(string.Format("PASS Loops        : {0}", result.PassCount));
            report.AppendLine(string.Format("FAIL Loops        : {0}", result.FailCount));
            report.AppendLine(string.Format("Success Rate      : {0:F2}%", result.SuccessRate));

            if (result.TotalTookFromRed > 0)
            {
                report.AppendLine();
                report.AppendLine(string.Format("Commands recovered from REDUNDANT : {0}", result.TotalTookFromRed));
            }

            report.AppendLine();
            report.AppendLine(sepLine);
            report.AppendLine(string.Format("Processing Time : {0:F2} seconds", elapsedSeconds));
            report.AppendLine(sepLine);

            ComparisonResult = report.ToString();
        }

        private bool CompareByteArrays(byte[] arr1, byte[] arr2)
        {
            if (arr1 == null || arr2 == null) return false;
            if (arr1.Length != arr2.Length) return false;
            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i]) return false;
            }
            return true;
        }

        private bool ContainsCommand(List<LogCommand> loop, byte[] command8)
        {
            foreach (LogCommand cmd in loop)
            {
                if (CompareByteArrays(cmd.Command8, command8))
                    return true;
            }
            return false;
        }

        private bool IsHeaderLine(string line)
        {
            return line.StartsWith("#") || line.StartsWith("//") || line.StartsWith("=") ||
                   line.StartsWith("-") || line.StartsWith("RS422") ||
                   line.StartsWith("Started:") || line.StartsWith("Stopped:");
        }

        private byte[] ParseLineToFrame(string line)
        {
            try
            {
                string hexPart = line;
                if (line.Contains("Frame:"))
                {
                    int idx = line.IndexOf("Frame:");
                    if (idx >= 0)
                    {
                        int hexStart = idx + 14;
                        if (hexStart < line.Length)
                            hexPart = line.Substring(hexStart);
                        else
                            return null;
                    }
                }

                string[] tokens = hexPart.Trim().Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length < 26) return null;

                byte[] frame = new byte[26];
                for (int i = 0; i < 26; i++)
                {
                    if (!byte.TryParse(tokens[i],
                        System.Globalization.NumberStyles.HexNumber,
                        null, out frame[i]))
                        return null;
                }

                if (frame[0] != START_BYTE_1 || frame[1] != START_BYTE_2 || frame[25] != END_BYTE)
                    return null;

                return frame;
            }
            catch
            {
                return null;
            }
        }

        private bool IsAllZeros(byte[] bytes)
        {
            if (bytes == null) return true;
            foreach (byte b in bytes)
            {
                if (b != 0x00) return false;
            }
            return true;
        }
    }
}
