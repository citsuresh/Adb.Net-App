using FluentAdb;
using FluentAdb.Enums;
using FluentAdb.Interfaces;
using JetBrains.Annotations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Reactive.Linq;
using System.Windows.Threading;
using System.Security.Cryptography;
using System.IO.Packaging;

namespace AdbdotNetApp
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAdb _adb;
        private IAdbTargeted _deviceAdb;
        private string _deviceName;
        private IDeviceInfo _selectedDeviceInfo;
        private Visibility _parametersLoadingAnimationVisibility = Visibility.Collapsed;
        private Visibility _packagesLoadingAnimationVisibility = Visibility.Collapsed;
        private Visibility _logsUpdatingAnimationVisibility = Visibility.Collapsed;
        private bool _isParametersTabSelected;
        private bool _isPackagesTabSelected;
        private bool _isLogsTabSelected;

        public ICommand RefreshDevicesCommand { get; set; }
        public ICommand ClearLogsCommand { get; set; }
        public ICommand BackupCommand { get; set; }
        public ObservableCollection<string> Logcat { get; set; }
        public ObservableCollection<DeviceDisplayInfo> AvailableDeviceInfos { get; set; }
        public IDeviceInfo SelectedDeviceInfo
        {
            get => _selectedDeviceInfo;
            set
            {
                _selectedDeviceInfo = value;
                this.OnPropertyChanged();
                this.PopulateDeviceData();
            }
        }

        public bool IsParametersTabSelected
        {
            get => _isParametersTabSelected;
            set
            {
                _isParametersTabSelected = value;
                this.OnPropertyChanged();
            }
        }
        
        public bool IsPackagesTabSelected
        {
            get => _isPackagesTabSelected;
            set
            {
                _isPackagesTabSelected = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsLogsTabSelected
        {
            get => _isLogsTabSelected;
            set
            {
                _isLogsTabSelected = value;
                this.OnPropertyChanged();
            }
        }

        public Visibility ParametersLoadingAnimationVisibility
        {
            get => _parametersLoadingAnimationVisibility;
            set
            {
                _parametersLoadingAnimationVisibility = value;
                OnPropertyChanged();
            }
        }

        public Visibility PackagesLoadingAnimationVisibility
        {
            get => _packagesLoadingAnimationVisibility;
            set
            {
                _packagesLoadingAnimationVisibility = value;
                OnPropertyChanged();
            }
        }

        public Visibility LogsUpdatingAnimationVisibility
        {
            get => _logsUpdatingAnimationVisibility;
            set
            {
                _logsUpdatingAnimationVisibility = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Parameter> Parameters { get; set; }

        public ObservableCollection<AppPackage> Packages { get; set; }

        public string DeviceName
        {
            get { return _deviceName; }
            set
            {
                if (value == _deviceName) return;
                _deviceName = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            DeviceName = "Not connected";
            AvailableDeviceInfos = new ObservableCollection<DeviceDisplayInfo>();
            IsParametersTabSelected = true;
            Parameters = new ObservableCollection<Parameter>();
            Packages = new ObservableCollection<AppPackage>();
            Logcat = new ObservableCollection<string>();
            RefreshDevicesCommand = new AsyncCommand(async () => await LoadDevices(), CancellationToken.None);
            ClearLogsCommand = new RelayCommand(() => ClearLogs());
            BackupCommand = new AsyncCommand(async () => await LoadPackageList(), CancellationToken.None);
            try
            {
                _adb = FluentAdb.Adb.New(".\\adb\\adb.exe");
            }
            catch (AdbException ex)
            {
                MessageBox.Show(ex.Message);
                Environment.Exit(1);
            }
        }

        private void ClearLogs()
        {
            this.Logcat.Clear();
        }

        private async Task LoadDevices(CancellationToken cancellationToken = default(CancellationToken))
        {
            AvailableDeviceInfos.Clear();
            DeviceName = "Not connected";
            AppendLog(message: await _adb.Version(cancellationToken));
            var availableDevices = await _adb.GetDevices(cancellationToken);
            availableDevices.ToList().ForEach(d => AvailableDeviceInfos.Add(new DeviceDisplayInfo { DeviceInfoObject = d, DisplayText = d.SerialNumber + " (" + d.State + ")" }));
            if (this.AvailableDeviceInfos.Any())
            {
                this.SelectedDeviceInfo = AvailableDeviceInfos.First().DeviceInfoObject;
            }
        }

        private void AppendLog(string message)
        {
            this.LogsUpdatingAnimationVisibility = Visibility.Visible;
            this.Logcat.Add(message);
            this.LogsUpdatingAnimationVisibility = Visibility.Collapsed;
        }

        private async Task PopulateDeviceData(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.LoadDeviceName(cancellationToken);
            await this.LoadSelectedDeviceProperties(cancellationToken);
            await this.LoadPackageList(cancellationToken);
        }

        private async Task PopulateActiveTab(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this._isParametersTabSelected)
            {
                await this.LoadSelectedDeviceProperties(cancellationToken);
            }
        }
        private async Task LoadDeviceName(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deviceInfo = this.SelectedDeviceInfo;
            if (deviceInfo != null)
            {
                _deviceAdb = _adb.Target(deviceInfo.SerialNumber);
                var model = await _deviceAdb.Shell.GetProperty("ro.product.model", cancellationToken);
                var manufacturer = await _deviceAdb.Shell.GetProperty("ro.product.manufacturer", cancellationToken);
                DeviceName = manufacturer + " " + model;
            }
        }

        private async Task LoadSelectedDeviceProperties(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deviceInfo = this.SelectedDeviceInfo;
            if (deviceInfo != null)
            {
                this.ParametersLoadingAnimationVisibility = Visibility.Visible;
                _deviceAdb = _adb.Target(deviceInfo.SerialNumber);
                var parameters = await _deviceAdb.Shell.GetAllProperties(cancellationToken);
                Parameters.Clear();
                foreach (var kv in parameters)
                {
                    Parameters.Add(new Parameter(kv.Key, kv.Value));
                }

                this.ParametersLoadingAnimationVisibility = Visibility.Collapsed;
            }
        }
        private async Task LoadPackageList(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deviceInfo = this.SelectedDeviceInfo;
            if (deviceInfo != null)
            {
                this.PackagesLoadingAnimationVisibility = Visibility.Visible;
                _deviceAdb = _adb.Target(deviceInfo.SerialNumber);
                var packageList = await _deviceAdb.Shell.PackageManager.GetPackages("",PackageListOptions.None,null, cancellationToken);
                Packages.Clear();
                foreach (var package in packageList)
                {
                    Packages.Add(new AppPackage { PackageName = package });
                }
                this.PackagesLoadingAnimationVisibility = Visibility.Collapsed;
            }
        }

        private async Task GetLogs(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deviceInfo = this.SelectedDeviceInfo;
            if (deviceInfo != null)
            {
                this.ParametersLoadingAnimationVisibility = Visibility.Visible;
                _deviceAdb = _adb.Target(deviceInfo.SerialNumber);

                _deviceAdb.Logcat().Buffer(TimeSpan.FromSeconds(3))
                    .ObserveOnDispatcher()
                    .Subscribe(log =>
                    {
                        foreach (var l in log)
                        {
                            this.AppendLog(l);
                        }
                    });
            }
        }

        private async Task BackupDevice(CancellationToken cancellationToken = default(CancellationToken))
        {
            var deviceInfo = this.SelectedDeviceInfo;
            if (deviceInfo != null)
            {
                _deviceAdb = _adb.Target(deviceInfo.SerialNumber);
                await _deviceAdb.Backup(BackupOptions.All, null, "testbackup.bak", cancellationToken);
            }
        }


        delegate void ParametrizedMethodInvoker5(string str);

        private void UpdateProgress(string str)
        {
            if (!Application.Current.Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Application.Current.Dispatcher.Invoke(new ParametrizedMethodInvoker5(UpdateProgress), str);
                return;
            }

            AppendLog(str);
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}