using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using MarkdownDeep;
using MdkControllerUpdate.Extensions;
using MdkControllerUpdate.Messages;
using MdkControllerUpdate.Model;
using Octokit;
using Octokit.Internal;
using Release = MdkControllerUpdate.Model.Release;

namespace MdkControllerUpdate.ViewModel
{
    class MainViewModel : ViewModelBase
    {
        private const string RepositoryOwner = "milindur";
        private const string RepositoryName = "MdkController";
        private const string GitHubToken = "1ee8bba55fd719d69ba64751c7e88258cf5044cd";

        private readonly IDialogService _dialogService;

        public MainViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                _isBusy = value;
                RaisePropertyChanged(() => IsBusy);

                RefreshPortsCommand.RaiseCanExecuteChanged();
                RefreshReleasesCommand.RaiseCanExecuteChanged();
                UpdateCommand.RaiseCanExecuteChanged();
                UpdateFromFileCommand.RaiseCanExecuteChanged();
            }
        }

        public string Title => $"MDK Controller Update v{Version}";
        public Version Version => typeof(App).Assembly.GetName().Version;

        public ObservableCollection<PortName> Ports { get; } = new ObservableCollection<PortName>();
        public ObservableCollection<Release> Releases { get; } = new ObservableCollection<Release>();

        private PortName _selectedComPort;

        public PortName SelectedComPort
        {
            get { return _selectedComPort; }
            set
            {
                _selectedComPort = value;

                RaisePropertyChanged(() => SelectedComPort);

                UpdateCommand.RaiseCanExecuteChanged();
                UpdateFromFileCommand.RaiseCanExecuteChanged();
            }
        }

        private Release _selectedRelease;

        public Release SelectedRelease
        {
            get { return _selectedRelease; }
            set
            {
                _selectedRelease = value;

                RaisePropertyChanged(() => SelectedRelease);
                RaisePropertyChanged(() => SelectedReleaseDescriptionAsHtml);

                UpdateCommand.RaiseCanExecuteChanged();
                UpdateFromFileCommand.RaiseCanExecuteChanged();
            }
        }

        public string SelectedReleaseDescriptionAsHtml
        {
            get
            {
                if (_selectedRelease == null) return null;

                return new Markdown().Transform(_selectedRelease.Description);
            }
        }

        private RelayCommand _refreshPortsCommand;
        public RelayCommand RefreshPortsCommand
        {
            get
            {
                return _refreshPortsCommand ?? (_refreshPortsCommand = new RelayCommand(() =>
                {
                    IsBusy = true;

                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM WIN32_SerialPort"))
                    {
                        var portNames = SerialPort.GetPortNames();
                        var portObjects = searcher.Get().Cast<ManagementBaseObject>().ToList();

                        Ports.Clear();
                        portNames.Join(portObjects,
                                o => o,
                                i => i["DeviceID"].ToString(),
                                (o, i) => new PortName { ComPort = o, Name = i["Caption"].ToString(), PnpDeviceId = i["PNPDeviceID"].ToString() })
                            .Where(p => p.PnpDeviceId.Contains("USB\\VID_2341&PID_003D") || p.PnpDeviceId.Contains("USB\\VID_2A03&PID_003D"))
                            .ToList()
                            .ForEach(Ports.Add);
                    }

                    SelectedComPort = Ports.FirstOrDefault();

                    IsBusy = false;
                }, () => !IsBusy));
            }
        }

        private RelayCommand _refreshReleasesCommand;
        public RelayCommand RefreshReleasesCommand
        {
            get
            {
                return _refreshReleasesCommand ?? (_refreshReleasesCommand = new RelayCommand(async () =>
                {
                    IsBusy = true;

                    try
                    {
                        var gitHub = new GitHubClient(new ProductHeaderValue("MdkControllerUpdate"), new InMemoryCredentialStore(new Credentials(GitHubToken)));
                        var releases = await gitHub.Release.GetAll(RepositoryOwner, RepositoryName);

                        var results = new List<Release>();
                        foreach (var release in releases)
                        {
                            var assets = await gitHub.Release.GetAllAssets(RepositoryOwner, RepositoryName, release.Id);

                            var firmwareAsset = assets.SingleOrDefault(ra => ra.Name.ToLowerInvariant().EndsWith(".bin"));
                            //if (firmwareAsset == null) continue;

                            results.Add(new Release
                            {
                                Label = release.Name,
                                Description = release.Body,
                                CreatedOn = release.CreatedAt,
                                FirmwareUri = firmwareAsset?.BrowserDownloadUrl
                            });
                        }

                        Releases.Clear();
                        results.ForEach(Releases.Add);

                        SelectedRelease = Releases.FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        await _dialogService.ShowMessage($"Error while receiving available releases.\r\nPlease make sure that you are connected to the Internet.\r\n\r\n{ex.Message}", "Releases");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }, () => !IsBusy));
            }
        }

        private RelayCommand _updateCommand;
        public RelayCommand UpdateCommand
        {
            get
            {
                return _updateCommand ?? (_updateCommand = new RelayCommand(async () =>
                {
                    IsBusy = true;

                    var firmwareFilePath = Path.GetTempFileName();
                    await new WebClient().DownloadFileTaskAsync(SelectedRelease.FirmwareUri, firmwareFilePath);
                    await UpdateFirmware(firmwareFilePath);
                }, () => SelectedComPort != null && SelectedRelease?.FirmwareUri != null && !IsBusy));
            }
        }

        private RelayCommand _updateFromFileCommand;
        public RelayCommand UpdateFromFileCommand
        {
            get
            {
                return _updateFromFileCommand ?? (_updateFromFileCommand = new RelayCommand(async () =>
                {
                    IsBusy = true;

                    var fileSelected = new TaskCompletionSource<string>();
                    var fileSelectedTask = fileSelected.Task;

                    MessengerInstance.Send(new FileOpenDialogMessage(fileName =>
                    {
                        fileSelected.SetResult(fileName);
                    }, () =>
                    {
                        fileSelected.SetCanceled();
                    }));

                    try
                    {
                        var firmwareFilePath = await fileSelectedTask;
                        await UpdateFirmware(firmwareFilePath);
                    }
                    catch (AggregateException ae)
                    {
                        if (ae.GetBaseException() is TaskCanceledException) return;

                        await _dialogService.ShowMessage("Firmware could not be updated.", "Update");
                    }
                }, () => SelectedComPort != null && !IsBusy));
            }
        }

        private async Task UpdateFirmware(string firmwareFilePath)
        {
                    using (var sp = new SerialPort(SelectedComPort.ComPort, 1200, Parity.None, 8, StopBits.One))
                    {
                        sp.Open();
                        Thread.Sleep(100);
                        sp.Close();
                    }

                    var bossaFilePath = Path.Combine(Path.GetTempPath(), @"bossac.exe");
                    File.WriteAllBytes(bossaFilePath, Properties.Resources.BossaCmd);

                    var flashProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo(
                            bossaFilePath,
                            $"--port={SelectedComPort.ComPort} -U false -e -w -v -b \"{firmwareFilePath}\" -R")
                    };

                    flashProcess.Start();
                    await flashProcess.WaitForExitAsync();
                    var exitCode = flashProcess.ExitCode;

                    if (exitCode != 0)
                    {
                        await _dialogService.ShowMessage("Firmware could not be updated.", "Update");
                    }
                    else
                    {
                        await _dialogService.ShowMessage("Firmware update was successful.", "Update");
                    }

                    IsBusy = false;
        }
    }
}