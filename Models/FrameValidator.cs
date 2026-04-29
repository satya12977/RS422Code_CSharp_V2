using System;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Models
{
    /// <summary>
    /// Validates 26-byte frames and extracts payload bytes.
    /// Frame layout:
    ///   [0]      = 0x0D  (Header byte 1)
    ///   [1]      = 0x0A  (Header byte 2)
    ///   [2..25]  = payload / data
    ///   [25]     = 0xAB  (Footer)
    /// Extract: bytes [16..23]  (8 bytes)
    /// </summary>
    public static class FrameValidator
    {
        // ── Frame constants ──────────────────────────────────────────────────
        private const int FRAME_SIZE = 26;
        private const int EXTRACT_START_INDEX = 16;
        private const int EXTRACT_LENGTH = 8;
        private const byte FRAME_HEADER_1 = 0x0D;
        private const byte FRAME_HEADER_2 = 0x0A;
        private const int FOOTER_POSITION = 25;
        private const byte FRAME_FOOTER = 0xAB;

        // ── Public properties (read-only) ─────────────────────────────────
        public static int FrameSize { get { return FRAME_SIZE; } }
        public static int ExtractStartIndex { get { return EXTRACT_START_INDEX; } }
        public static int ExtractLength { get { return EXTRACT_LENGTH; } }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Validates a 26-byte frame buffer.</summary>
        /// <param name="buffer">Exactly 26 bytes.</param>
        /// <param name="reason">Human-readable failure reason.</param>
        /// <returns>True when frame is valid.</returns>
        public static bool ValidateFrame(byte[] buffer, out string reason)
        {
            reason = string.Empty;

            if (buffer == null)
            {
                reason = "Buffer is null";
                return false;
            }

            if (buffer.Length != FRAME_SIZE)
            {
                reason = string.Format(
                    "Invalid frame size: expected {0}, got {1}",
                    FRAME_SIZE, buffer.Length);
                return false;
            }

            if (buffer[0] != FRAME_HEADER_1)
            {
                reason = string.Format(
                    "Bad Header-1: expected 0x{0:X2}, got 0x{1:X2}",
                    FRAME_HEADER_1, buffer[0]);
                return false;
            }

            if (buffer[1] != FRAME_HEADER_2)
            {
                reason = string.Format(
                    "Bad Header-2: expected 0x{0:X2}, got 0x{1:X2}",
                    FRAME_HEADER_2, buffer[1]);
                return false;
            }

            if (buffer[FOOTER_POSITION] != FRAME_FOOTER)
            {
                reason = string.Format(
                    "Bad Footer @ [{0}]: expected 0x{1:X2}, got 0x{2:X2}",
                    FOOTER_POSITION, FRAME_FOOTER, buffer[FOOTER_POSITION]);
                return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Extracts 8 bytes starting at index 16.</summary>
        public static byte[] ExtractPayload(byte[] validFrame)
        {
            if (validFrame == null || validFrame.Length < EXTRACT_START_INDEX + EXTRACT_LENGTH)
                throw new ArgumentException("Frame too short to extract payload.");

            byte[] payload = new byte[EXTRACT_LENGTH];
            Array.Copy(validFrame, EXTRACT_START_INDEX, payload, 0, EXTRACT_LENGTH);
            return payload;
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Converts a byte array to a space-separated hex string.</summary>
        public static string BytesToHexString(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(data.Length * 3);
            foreach (byte b in data)
            {
                sb.AppendFormat("{0:X2} ", b);
            }
            return sb.ToString().TrimEnd();
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Builds the formatted log line for a raw frame.</summary>
        public static string BuildFrameLogLine(byte[] frame, int portNumber, long frameCount)
        {
            return string.Format(
                "[{0}] PORT{1} FRAME#{2:D6}  RAW: {3}",
                DateTime.Now.ToString("HH:mm:ss.fff"),
                portNumber,
                frameCount,
                BytesToHexString(frame));
        }

        /// <summary>Builds the formatted log line for extracted payload.</summary>
        public static string BuildPayloadLogLine(byte[] payload, int portNumber, long frameCount)
        {
            return string.Format(
                "[{0}] PORT{1} FRAME#{2:D6}  PAYLOAD[{3}..{4}]: {5}",
                DateTime.Now.ToString("HH:mm:ss.fff"),
                portNumber,
                frameCount,
                EXTRACT_START_INDEX,
                EXTRACT_START_INDEX + EXTRACT_LENGTH - 1,
                BytesToHexString(payload));
        }
    }
}