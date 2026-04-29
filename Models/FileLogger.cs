using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Models
{
    /// <summary>
    /// Thread-safe sequential file logger.
    /// Guarantees write order via BlockingCollection FIFO queue.
    /// </summary>
    public class FileLogger : IDisposable
    {
        private readonly string _filePath;
        private readonly BlockingCollection<string> _queue;
        private readonly Thread _writerThread;
        private StreamWriter _writer;
        private volatile bool _isDisposed = false;
        private long _linesWritten = 0;

        public string FilePath { get { return _filePath; } }
        public long LinesWritten { get { return Interlocked.Read(ref _linesWritten); } }

        public FileLogger(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath");

            _filePath = filePath;

            // Create directory
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    "FileLogger: Cannot create directory for " + filePath, ex);
            }

            // Open file in append mode
            try
            {
                _writer = new StreamWriter(filePath, true, Encoding.UTF8)
                {
                    AutoFlush = false  // Manual flush for performance
                };
            }
            catch (Exception ex)
            {
                throw new IOException(
                    "FileLogger: Cannot open file " + filePath, ex);
            }

            // Write session header
            WriteHeader();

            // Create FIFO queue (unbounded for safety)
            _queue = new BlockingCollection<string>(new ConcurrentQueue<string>());

            // Start dedicated writer thread
            _writerThread = new Thread(WriterThreadLoop)
            {
                IsBackground = true,
                Name = "FileLogger_" + Path.GetFileName(filePath),
                Priority = ThreadPriority.BelowNormal
            };
            _writerThread.Start();
        }

        private void WriteHeader()
        {
            try
            {
                string sep = new string('=', 70);
                _writer.WriteLine("# " + sep);
                _writer.WriteLine("# ANVESHA TCRX Health Status Monitor V2");
                _writer.WriteLine("# Session Start: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                _writer.WriteLine("# Log File: " + _filePath);
                _writer.WriteLine("# Frame: 26 bytes | Header: 0x0D 0x0A | Footer: 0xAB@[25]");
                _writer.WriteLine("# Extract: [16..23] = 8 bytes");
                _writer.WriteLine("# " + sep);
                _writer.Flush();
            }
            catch
            {
                // Non-critical
            }
        }

        /// <summary>
        /// Enqueue log line (never blocks caller — safe from any thread)
        /// </summary>
        public void Log(string line)
        {
            if (_isDisposed || line == null) return;

            try
            {
                // ConcurrentQueue guarantees FIFO order
                _queue.TryAdd(line, 0);
            }
            catch
            {
                // Queue disposed — silently discard
            }
        }

        /// <summary>
        /// Dedicated writer thread — processes queue sequentially
        /// </summary>
        private void WriterThreadLoop()
        {
            try
            {
                // GetConsumingEnumerable blocks until items available
                // Maintains FIFO order from ConcurrentQueue
                foreach (string line in _queue.GetConsumingEnumerable())
                {
                    if (_writer == null) break;

                    try
                    {
                        _writer.WriteLine(line);
                        Interlocked.Increment(ref _linesWritten);

                        // Periodic flush (every 50 lines for performance)
                        if (_linesWritten % 50 == 0)
                            _writer.Flush();
                    }
                    catch
                    {
                        // Skip bad line, continue processing
                    }
                }
            }
            catch
            {
                // Thread exiting
            }
            finally
            {
                CloseWriter();
            }
        }

        /// <summary>
        /// Manual flush (safe to call anytime)
        /// </summary>
        public void Flush()
        {
            try
            {
                if (_writer != null && !_isDisposed)
                    _writer.Flush();
            }
            catch { }
        }

        private void CloseWriter()
        {
            try
            {
                if (_writer != null)
                {
                    _writer.WriteLine("# Session End: " +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    _writer.WriteLine("# Total Lines: " + _linesWritten);
                    _writer.Flush();
                    _writer.Close();
                    _writer.Dispose();
                    _writer = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Signal writer thread to finish
                if (_queue != null && !_queue.IsAddingCompleted)
                    _queue.CompleteAdding();

                // Wait for writer to drain queue (max 5 seconds)
                if (_writerThread != null && _writerThread.IsAlive)
                    _writerThread.Join(5000);

                if (_queue != null)
                    _queue.Dispose();
            }
            catch { }
        }
    }
}