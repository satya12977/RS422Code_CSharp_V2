using System;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Models
{
    /// <summary>
    /// Custom EventArgs carrying a single string message.
    /// Fixes: EventHandler(string) not valid in .NET 4.0
    /// </summary>
    public class StringEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public StringEventArgs(string message)
        {
            Message = message ?? string.Empty;
        }
    }
}