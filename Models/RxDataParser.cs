using System;
using System.Text;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Models
{
    /// <summary>
    /// Parser for 26-byte telemetry frames.
    /// Produces structured RxDataFields and formatted display strings.
    /// </summary>
    public static class RxDataParser
    {
        private const int FRAME_SIZE = 26;
        private const int EXTRACT_START = 16;
        private const int EXTRACT_LENGTH = 8;

        // ── Parse ──────────────────────────────────────────────────────────
        public static RxDataFields ParseFrame(byte[] frame26)
        {
            RxDataFields fields = new RxDataFields();
            fields.FullFrame26 = frame26;
            fields.Timestamp = DateTime.Now;

            if (frame26 == null || frame26.Length < FRAME_SIZE)
            {
                fields.IsValid = false;
                return fields;
            }

            fields.IsValid = true;
            fields.Extracted8 = new byte[EXTRACT_LENGTH];
            Array.Copy(frame26, EXTRACT_START, fields.Extracted8, 0, EXTRACT_LENGTH);

            // Byte 2 - Relay Status (Bits 0-3)
            byte byte2 = frame26[2];
            fields.Rs422DriverMainBMU = (byte)((byte2 >> 3) & 0x01);
            fields.Rs422DriverRedundantBMU = (byte)((byte2 >> 2) & 0x01);
            fields.RelayStatusDriverA = (byte)((byte2 >> 1) & 0x01);
            fields.RelayStatusDriverB = (byte)(byte2 & 0x01);

            // Bytes 3-4 - Frame Counter (Big-endian: MSB first)
            fields.FrameCounter = (ushort)((frame26[3] << 8) | frame26[4]);

            // Byte 11 - Health Status (Bits 0-4)
            byte byte11 = frame26[11];
            fields.DataLock = (byte)(byte11 & 0x01);
            fields.CarrierLock = (byte)((byte11 >> 1) & 0x01);
            fields.AdcIpBelowMax = (byte)((byte11 >> 2) & 0x01);
            fields.AdcIpAboveMin = (byte)((byte11 >> 3) & 0x01);
            fields.TcRxbbMainRedundant = (byte)((byte11 >> 4) & 0x01);

            // Bytes 13-14 - Command Counts
            fields.NormalCommandCount = frame26[13];
            fields.ContingencyCommandCount = frame26[14];

            // Byte 24 - Command Flags
            byte byte24 = frame26[24];
            fields.NewCommand = (byte)(byte24 & 0x01);
            fields.NormalCommand = (byte)((byte24 >> 6) & 0x01);
            fields.ContingencyCommand = (byte)((byte24 >> 7) & 0x01);

            return fields;
        }

        // ── Hex string ─────────────────────────────────────────────────────
        public static string ToHexString(byte[] data, string separator)
        {
            if (data == null || data.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(separator);
            }
            return sb.ToString();
        }

        // ── Detailed display ───────────────────────────────────────────────
        public static string GenerateDetailedDisplay(RxDataFields fields,
                                                     int frameNumber)
        {
            const int LINE_W = 100;
            // Use ASCII dash — no Unicode box chars (avoids font issues)
            string sep = new string('-', LINE_W);

            string hex26 = ToHexString(fields.FullFrame26, "  ");
            string hex8 = "        " + ToHexString(fields.Extracted8, "  ");

            ushort header = (ushort)((fields.FullFrame26[0] << 8)
                                   | fields.FullFrame26[1]);
            byte footer = fields.FullFrame26[25];

            byte healthByte = (byte)(
                ((fields.TcRxbbMainRedundant & 1) << 4) |
                ((fields.AdcIpAboveMin & 1) << 3) |
                ((fields.AdcIpBelowMax & 1) << 2) |
                ((fields.CarrierLock & 1) << 1) |
                ((fields.DataLock & 1) << 0));

            string healthBin = Convert.ToString(healthByte, 2).PadLeft(8, '0');

            byte relayNibble = (byte)(
                ((fields.Rs422DriverMainBMU & 1) << 0) |
                ((fields.Rs422DriverRedundantBMU & 1) << 1) |
                ((fields.RelayStatusDriverA & 1) << 2) |
                ((fields.RelayStatusDriverB & 1) << 3));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(sep);
            sb.AppendLine(string.Format(
                "  RX LIVE  |  Frame# {0:D6}  |  {1:yyyy-MM-dd  HH:mm:ss.fff}",
                frameNumber, fields.Timestamp));
            sb.AppendLine(sep);
            sb.AppendLine("RECEIVED DATA [Hex]:");
            sb.AppendLine(hex26);
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-30}{1}",
                "EXTRACTED TC [8 Bytes]:", hex8.Trim()));
            sb.AppendLine(sep);
            sb.AppendLine();
            sb.AppendLine("RECEIVED HEALTH STATUS [Dec]:");
            sb.AppendLine();
            sb.AppendLine(FmtLine("HEADER",
                header.ToString(),
                string.Format("0x{0:X4} : 0D0Ah", header)));
            sb.AppendLine(FmtLine("FOOTER",
                footer.ToString(),
                string.Format("0x{0:X2} : ABh", footer)));
            sb.AppendLine();
            sb.AppendLine(FmtLine("RS422_RELAY_STATUS",
                relayNibble.ToString(),
                string.Format(
                    "Rs422DriverMainBMU={0}  Rs422DriverRedundantBMU={1}" +
                    "  RelayStatusDriverA={2}  RelayStatusDriverB={3}",
                    fields.Rs422DriverMainBMU,
                    fields.Rs422DriverRedundantBMU,
                    fields.RelayStatusDriverA,
                    fields.RelayStatusDriverB)));
            sb.AppendLine();
            sb.AppendLine(FmtLine("FRAME_COUNTER",
                fields.FrameCounter.ToString()));
            sb.AppendLine();
            sb.AppendLine(FmtLine("CONTINGENCY_/_NORMAL_COMMAND",
                string.Format("{0} {1}",
                    fields.ContingencyCommand, fields.NormalCommand),
                fields.ContingencyCommand == 1
                    ? "CONTINGENCY COMMAND" : "NORMAL COMMAND"));
            sb.AppendLine(FmtLine("RETRANSMITTED_/_NEW_COMMAND",
                string.Format("{0} {1}",
                    fields.NewCommand, fields.NormalCommand),
                fields.NewCommand == 1
                    ? "NEW_COMMAND" : "RETRANSMITTED_COMMAND"));
            sb.AppendLine();
            sb.AppendLine(FmtLine("NORMAL_COMMAND_CNT",
                fields.NormalCommandCount.ToString()));
            sb.AppendLine(FmtLine("CONTINGENCY_COMMAND_CNT",
                fields.ContingencyCommandCount.ToString()));
            sb.AppendLine(FmtLine("RECEIVED_TC_COMMAND", "",
                ToHexString(fields.Extracted8, "") + "h"));
            sb.AppendLine();
            sb.AppendLine(FmtLine("HEALTH",
                healthByte.ToString(), healthBin + "b"));
            sb.AppendLine();
            sb.AppendLine(FmtLineIndent("FROM_TCRXBB_MAIN_/_REDUNDANT",
                fields.TcRxbbMainRedundant.ToString(),
                fields.TcRxbbMainRedundant == 0
                    ? "MAIN TCRX(BB)" : "REDUNDANT TCRX(BB)"));
            sb.AppendLine();
            sb.AppendLine(FmtLineIndent("ADC_IP_ABOVE_MIN",
                fields.AdcIpAboveMin.ToString(),
                fields.AdcIpAboveMin == 1
                    ? "I/P SIGNAL PRESENT" : "I/P SIGNAL ABSENT"));
            sb.AppendLine(FmtLineIndent("ADC_IP_ABOVE_MAX",
                fields.AdcIpBelowMax.ToString()));
            sb.AppendLine();
            sb.AppendLine(FmtLineIndent("CARRIER_LOCKED",
                fields.CarrierLock.ToString(),
                fields.CarrierLock == 1 ? "LOCKED" : "UNLOCKED"));
            sb.AppendLine(FmtLineIndent("DATA_LOCKED",
                fields.DataLock.ToString(),
                fields.DataLock == 1 ? "LOCKED" : "UNLOCKED"));
            sb.AppendLine();
            sb.AppendLine(sep);

            return sb.ToString();
        }

        // ── Compact one-line summary ────────────────────────────────────────
        public static string FormatCompactSummary(RxDataFields fields,
                                                   int frameNumber)
        {
            return string.Format(
                "[{0:HH:mm:ss.fff}] F#{1:D6} FC:{2} | DL:{3} CL:{4} | " +
                "MainBMU:{5} RedBMU:{6} | NCnt:{7} CCnt:{8} | Cmd:{9}",
                fields.Timestamp,
                frameNumber,
                fields.FrameCounter,
                fields.DataLock == 1 ? "OK" : "NO",
                fields.CarrierLock == 1 ? "OK" : "NO",
                fields.Rs422DriverMainBMU,
                fields.Rs422DriverRedundantBMU,
                fields.NormalCommandCount,
                fields.ContingencyCommandCount,
                ToHexString(fields.Extracted8, ""));
        }

        // ── CSV row ────────────────────────────────────────────────────────
        public static string GenerateCSVRow(RxDataFields fields, int frameNumber)
        {
            return string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff},{1},{2},{3},{4},{5},{6},{7},{8}," +
                "{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}",
                fields.Timestamp,
                frameNumber,
                ToHexString(fields.FullFrame26, " "),
                ToHexString(fields.Extracted8, " "),
                fields.FrameCounter,
                fields.Rs422DriverMainBMU,
                fields.Rs422DriverRedundantBMU,
                fields.RelayStatusDriverA,
                fields.RelayStatusDriverB,
                fields.DataLock,
                fields.CarrierLock,
                fields.AdcIpBelowMax,
                fields.AdcIpAboveMin,
                fields.TcRxbbMainRedundant == 0 ? "Main" : "Redundant",
                fields.NormalCommandCount,
                fields.ContingencyCommandCount,
                fields.NewCommand,
                fields.NormalCommand,
                fields.ContingencyCommand);
        }

        // ── Format helpers ─────────────────────────────────────────────────
        private static string FmtLine(string label, string value,
                                      string annotation = "")
        {
            const int LBL = 35, VAL = 10;
            string lp = (label + "=").PadRight(LBL);
            string vp = value.PadLeft(VAL);
            return string.IsNullOrEmpty(annotation)
                ? string.Format("{0} {1}", lp, vp)
                : string.Format("{0} {1} : {2}", lp, vp, annotation);
        }

        private static string FmtLineIndent(string label, string value,
                                             string annotation = "")
        {
            const int LBL = 35, VAL = 10;
            string lp = ("    " + label + "=").PadRight(LBL + 4);
            string vp = value.PadLeft(VAL);
            return string.IsNullOrEmpty(annotation)
                ? string.Format("{0} {1}", lp, vp)
                : string.Format("{0} {1} : {2}", lp, vp, annotation);
        }
    }
}