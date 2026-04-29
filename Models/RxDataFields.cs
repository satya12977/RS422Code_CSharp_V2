using System;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Models
{
    /// <summary>
    /// Structured fields parsed from a 26-byte telemetry frame
    /// </summary>
    public class RxDataFields
    {
        public DateTime Timestamp { get; set; }
        public bool IsValid { get; set; }

        // Raw data
        public byte[] FullFrame26 { get; set; }
        public byte[] Extracted8 { get; set; }

        // Byte 2 — Relay Status
        public byte Rs422DriverMainBMU { get; set; }
        public byte Rs422DriverRedundantBMU { get; set; }
        public byte RelayStatusDriverA { get; set; }
        public byte RelayStatusDriverB { get; set; }

        // Bytes 3-4 — Frame Counter (Big-endian)
        public ushort FrameCounter { get; set; }

        // Byte 11 — Health Status
        public byte DataLock { get; set; }
        public byte CarrierLock { get; set; }
        public byte AdcIpBelowMax { get; set; }
        public byte AdcIpAboveMin { get; set; }
        public byte TcRxbbMainRedundant { get; set; }

        // Bytes 13-14 — Command Counts
        public byte NormalCommandCount { get; set; }
        public byte ContingencyCommandCount { get; set; }

        // Byte 24 — Command Flags
        public byte NewCommand { get; set; }
        public byte NormalCommand { get; set; }
        public byte ContingencyCommand { get; set; }
    }
}