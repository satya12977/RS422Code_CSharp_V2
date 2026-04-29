using System;
using System.Windows;
using System.Windows.Controls;
using ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.ViewModels;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2
{
    public partial class MainWindow : Window
    {
        public  MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(Dispatcher);
            DataContext = _vm;
        }

        private void Window_Closing(object sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_vm != null)
            {
                _vm.Port1.Disconnect();
                _vm.Port2.Disconnect();
                _vm.Dispose();
            }
        }

        // ── Common buttons ─────────────────────────────────────────────────
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.Port1.RefreshPorts();
            _vm.Port2.RefreshPorts();
            _vm.AppStatus = string.Format(
                "COM ports refreshed: {0} available",
                _vm.Port1.AvailablePorts.Count);
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_vm.Port1.SelectedPort) ||
                string.IsNullOrEmpty(_vm.Port2.SelectedPort))
            {
                MessageBox.Show(
                    "Select a COM port for BOTH PORT 1 and PORT 2.",
                    "Port Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_vm.Port1.SelectedPort == _vm.Port2.SelectedPort)
            {
                MessageBox.Show(
                    "PORT 1 and PORT 2 must use different COM ports.\n" +
                    "Both are currently set to: " + _vm.Port1.SelectedPort,
                    "Port Conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _vm.Port1.Disconnect();
            _vm.Port2.Disconnect();

            bool ok1 = _vm.Port1.Connect();
            bool ok2 = _vm.Port2.Connect();

            if (ok1 && ok2)
            {
                _vm.AppStatus = string.Format(
                    "BOTH PORTS CONNECTED  |  " +
                    "PORT1=[{0}]@{1}  PORT2=[{2}]@{3}",
                    _vm.Port1.SelectedPort, _vm.Port1.SelectedBaudRate,
                    _vm.Port2.SelectedPort, _vm.Port2.SelectedBaudRate);
            }
            else
            {
                if (ok1) _vm.Port1.Disconnect();
                if (ok2) _vm.Port2.Disconnect();

                string failMsg = "Connection FAILED:\n\n";
                if (!ok1) failMsg += "  PORT 1 [" + _vm.Port1.SelectedPort + "]\n";
                if (!ok2) failMsg += "  PORT 2 [" + _vm.Port2.SelectedPort + "]\n";
                failMsg +=
                    "\nCheck:\n" +
                    "  RS422 adapter connected\n" +
                    "  Correct COM port selected\n" +
                    "  Port not in use by another application";

                MessageBox.Show(failMsg, "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                _vm.AppStatus = "Connection failed.";
            }

            _vm.UpdateAcquisitionState();
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            long p1 = _vm.Port1.TotalFrames;
            long p2 = _vm.Port2.TotalFrames;

            _vm.Port1.Disconnect();
            _vm.Port2.Disconnect();
            _vm.UpdateAcquisitionState();

            _vm.AppStatus = string.Format(
                "BOTH DISCONNECTED  |  PORT1={0} frames  PORT2={1} frames",
                p1, p2);
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _vm.Port1.ClearLogs();
            _vm.Port2.ClearLogs();
            _vm.AppStatus = "All display logs cleared.";
        }

        // ── Tab 5 — Offline Compare ────────────────────────────────────────
        private void BtnBrowseExpected_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Expected Commands File"
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.OfflineCompare.ExpectedFilePath = dlg.FileName;
            }
        }

        private void BtnBrowseActual1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Actual File 1"
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.OfflineCompare.SingleFilePath = dlg.FileName;
            }
        }

        private void BtnBrowseActual2_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Actual File 2"
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.OfflineCompare.MainFilePath = dlg.FileName;
            }
        }

        private void BtnBrowseActual3_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Select Actual File 3"
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.OfflineCompare.RedundantFilePath = dlg.FileName;
            }
        }

        private void BtnStartCompare_Click(object sender, RoutedEventArgs e)
        {
            _vm.OfflineCompare.StartComparison();
        }

        private void BtnClearCompare_Click(object sender, RoutedEventArgs e)
        {
            _vm.OfflineCompare.ComparisonResult = string.Empty;
            _vm.OfflineCompare.StatusMessage = "Ready";
        }

        // ── Tab 1 — 26 Bytes Raw ───────────────────────────────────────────
        private void TbPort1Raw_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        private void TbPort2Raw_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        // ── Tab 2 — 8 Bytes Payload ────────────────────────────────────────
        private void TbPort1Payload_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        private void TbPort2Payload_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        // ── Tab 3 — RX Live Compare ────────────────────────────────────────
        private void TbPort1RxLive_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        private void TbPort2RxLive_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        // ── Tab 4 — RX Live Stats Compact ──────────────────────────────────
        private void TbPort1Compact_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        private void TbPort2Compact_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollToEnd(sender as TextBox);
        }

        private static void ScrollToEnd(TextBox tb)
        {
            if (tb != null) tb.ScrollToEnd();
        }
    }
}