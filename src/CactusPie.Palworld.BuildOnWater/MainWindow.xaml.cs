using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memory;
using NHotkey;
using NHotkey.Wpf;

namespace CactusPie.Palworld.BuildOnWater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Mem GameMemory = new();

        private bool _buildingOnWaterEnabled;

        private string? _address;

        private readonly object _lock = new();

        private Task? _gameProcessSearchingTask;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _gameProcessSearchingTask = WaitForGameProcess();
        }

        private static async Task<long?> GetAddressForDisabledWaterBuilding()
        {
            IEnumerable<long>? addresses = await GameMemory
                .AoBScan("74 0E 0F B6 4E 30", true, true)
                .ConfigureAwait(false);

            long? address = addresses?.FirstOrDefault();
            return address;
        }

        private static async Task<long?> GetAddressForEnabledWaterBuilding()
        {
            IEnumerable<long>? addresses = await GameMemory
                .AoBScan("EB 0E 0F B6 4E 30", true, true)
                .ConfigureAwait(false);

            long? address = addresses?.FirstOrDefault();
            return address;
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            if (GameMemory.mProc.Process.HasExited)
            {
                _buildingOnWaterEnabled = false;
                HotkeyManager.Current.Remove("ToggleBuildOnWater");
                MainLabel.Content = "Waiting for the game to start...";
                MainLabel.Foreground = new SolidColorBrush(Colors.Black);

                lock (_lock)
                {
                    _gameProcessSearchingTask = WaitForGameProcess();
                }

                return;
            }

            ToggleBuildingOnWater();
            if (_buildingOnWaterEnabled)
            {
                MainLabel.Content = "Building on water: ENABLED (F9 to toggle)";
                MainLabel.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }
            else
            {
                MainLabel.Content = "Building on water: DISABLED (F9 to toggle)";
                MainLabel.Foreground = new SolidColorBrush(Colors.DarkRed);
            }
        }

        private void ToggleBuildingOnWater()
        {
            _buildingOnWaterEnabled = !_buildingOnWaterEnabled;

            if (_buildingOnWaterEnabled)
            {
                GameMemory.WriteBytes(_address, new byte[]{ 0xEB, 0x0E });
            }
            else
            {
                GameMemory.WriteBytes(_address, new byte[]{ 0x74, 0x0E });
            }
        }

        private async Task WaitForGameProcess()
        {
            if (_gameProcessSearchingTask != null)
            {
                return;
            }

            while (true)
            {
                bool opened = GameMemory.OpenProcess("Palworld-Win64-Shipping.exe");

                if (!opened)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    continue;
                }

                Dispatcher.Invoke(
                    () =>
                    {
                        MainLabel.Content = "Searching for the correct memory address...";
                        MainLabel.Foreground = new SolidColorBrush(Colors.Black);
                    });

                long? address = await GetAddressForDisabledWaterBuilding().ConfigureAwait(false);

                if (address is null or 0)
                {
                    address = await GetAddressForEnabledWaterBuilding().ConfigureAwait(false);
                }

                if (address is null or 0)
                {
                    Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Could not find the correct memory address.\n" +
                                "This could happen if you're not running the latest Steam version or a new game update broke the mod.\n" +
                                "The mod might not work with GamePass/Windows store version",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            this.Close();
                        }
                    );

                    return;
                }

                _address = address.Value.ToString("X");

                Dispatcher.Invoke(
                    () =>
                    {
                        _buildingOnWaterEnabled = false;
                        HotkeyManager.Current.AddOrReplace("ToggleBuildOnWater", Key.F9, ModifierKeys.None, OnHotkeyPressed);
                        MainLabel.Content = "Building on water: DISABLED (F9 to toggle)";
                        MainLabel.Foreground = new SolidColorBrush(Colors.DarkRed);
                    });

                break;
            }

            _gameProcessSearchingTask = null;
        }
    }
}