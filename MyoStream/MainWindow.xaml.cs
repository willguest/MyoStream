using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Threading;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

using System.Threading;
using System.IO;
using System.Collections.Specialized;

namespace MyoStream
{
    public partial class MainWindow
    {
        private string deviceFilterString = "Myo";      // Search string for devices
        public int _tick = 100;                         // millisecond interval between updates
        private String directory = "C:/Users/16102434/Desktop/Current Work/Myo/RDWT/testData";          // directory to pick up files for batch processing

        #region Variables

        private Plotter _plot = new Plotter();
        public List<string> fileList = new List<string>();
        public BatchProcessor _bp;

        // Watchers
        private BluetoothLEAdvertisementWatcher BleWatcher;
        private DeviceWatcher deviceWatcher;

        // BLE objects
        private BluetoothLEDevice currentDevice;
        private GattClientCharacteristicConfigurationDescriptorValue charDesVal_notify = GattClientCharacteristicConfigurationDescriptorValue.Notify;

        // List for connection handling
        private Dictionary<string, Guid> myoGuids;
        public List<MyoArmband> connectedMyos = new List<MyoArmband>(2);
        public ObservableCollection<string> bondedMyos { get; private set; } = new ObservableCollection<string>();
        private ObservableCollection<String> deviceList = new ObservableCollection<String>();
        private ObservableCollection<String> addressBook = new ObservableCollection<string>();

        // Status flags
        private bool isConnecting = false;
        private int readyMyos = 0;

        // Timer, just for ticks
        private DispatcherTimer dispatcherTimer = new DispatcherTimer();
        private double captureDuration = 0;



        // Data objects for batch processing


        #endregion Variables


        private void StopDataStream(object sender, EventArgs e)
        { StopDataStream(true); }

        private void StartDataStream(object sender, EventArgs e)
        { StartDataStream(); }

        private void LoadFile_Click(object sender, EventArgs e)
        { LoadDataFile(); }

        private void CleanFile_Click(object sender, EventArgs e)
        { CleanEMGData(); }


        private void dispatcherTimer_Tick(object sender, object e)
        { Update_Timer(); }

        


        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            deviceList.Clear();

            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, _tick);

            Setup_Data_Channels();

            Setup_Watchers();
            Start_Watchers();

            bondedMyos.CollectionChanged += bondedMyos_Changed;

            GetFileList();
        }


        private void GetFileList()
        {
            string[] files;
            
            files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(x => Path.GetFileName(x)).ToArray();
            cmbFileList.SetBinding(System.Windows.Controls.ComboBox.ItemsSourceProperty, new System.Windows.Data.Binding() { Source = files });
        }
        
        private void LoadDataFile()
        {
            string filename = cmbFileList.Text;
            _bp = new BatchProcessor();
            int noRecords = _bp.LoadFile(directory, filename);
            txtLoadResult.Text = noRecords + " records";
        }

        private void CleanEMGData()
        {
            if (_bp != null)
            {
                _bp.CleanData();
            }
            else
            {
                Console.WriteLine("No file loaded");
            }
        }



        #region Setup and Start Watchers

        private void Start_Watchers()
        {
            BleWatcher.Start();
            deviceWatcher.Start();
            isConnecting = false;
        }

        private void Setup_Watchers()
        {
            // Instantiate device watcher ..
            string myAqsFilter = "System.ItemNameDisplay:~~\"" + deviceFilterString;

            string[] aepProperies = new string[]
            {
            "System.ItemNameDisplay",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.IsPresent"
            };

            // Device Watcher
            deviceWatcher = DeviceInformation.CreateWatcher(
                myAqsFilter, aepProperies,
                DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // .. and BLE advert watcher
            BleWatcher = new BluetoothLEAdvertisementWatcher
            { ScanningMode = BluetoothLEScanningMode.Active };
            BleWatcher.SignalStrengthFilter.InRangeThresholdInDBm = -70;
            BleWatcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromSeconds(1);

            BleWatcher.Received += WatcherOnReceived;
            BleWatcher.Stopped += Watcher_Stopped;
        }
        #endregion Setup and Start Watchers


        #region Connect and Disconnect


        private Guid AddMyoArmbandFromDevice(BluetoothLEDevice _device)
        {
            MyoArmband newMyo = new MyoArmband();

            newMyo.Id = Guid.NewGuid();
            newMyo.Name = _device.Name;
            newMyo.Device = _device;
            newMyo.emgCharacs = new GattCharacteristic[4];
            newMyo.EmgConnStat = new GattCommunicationStatus[4];
            newMyo.Device.ConnectionStatusChanged += deviceConnChanged;

            newMyo.myDataHandler = new DataHandler();

            bool alreadyFound = false;
            foreach (MyoArmband m in connectedMyos)
            {
                if (m.Name == _device.Name)
                {
                    alreadyFound = true;
                }
            }

            if (connectedMyos.Count <= 2 && !alreadyFound)
            { connectedMyos.Add(newMyo); }

            Console.WriteLine(newMyo.Name + " is connected");

            return newMyo.Id;
        }

        public void ConnectToArmband(Guid armbandGuid)
        {
            // identify myo
            MyoArmband myo = connectedMyos.Where(g => (g.Id == armbandGuid)).FirstOrDefault();
            int myoIndex = connectedMyos.IndexOf(myo);

            // hook control service, establishing a connection
            GattDeviceServicesResult controlserv = Task.Run(() => GetServiceAsync(myo.Device, myoGuids["MYO_SERVICE_GCS"])).Result;
            myo.controlService = controlserv.Services.FirstOrDefault();

            // ensure the control service is ready
            if (myo.controlService != null)
            {
                GattCharacteristicsResult fwCh = GetCharac(myo.controlService, myoGuids["MYO_FIRMWARE_CH"]).Result;
                myo.FW_charac = fwCh.Characteristics.FirstOrDefault();

                GattCharacteristicsResult cmdCharac = GetCharac(myo.controlService, myoGuids["COMMAND_CHARACT"]).Result;
                myo.cmdCharac = cmdCharac.Characteristics.FirstOrDefault();

                // read firmware characteristic to establish a connection
                if (myo.FW_charac != null)
                {
                    GattReadResult readData = Read(myo.FW_charac).Result;

                    if (readData != null)
                    {
                        ushort[] fwData = new UInt16[readData.Value.Length / 2];
                        System.Buffer.BlockCopy(readData.Value.ToArray(), 0, fwData, 0, (int)(readData.Value.Length));
                        myo.myFirmwareVersion = ($"{fwData[0]}.{fwData[1]}.{fwData[2]} rev.{fwData[3]}");

                        vibrate_armband(myo);

                        // update data object
                        connectedMyos[myoIndex] = myo;
                        int errCode = SetupMyo(myo.Name);

                        Console.WriteLine("Setup of " + myo.Name + "(" + myo.myFirmwareVersion + ") returned code: " + errCode);
                    }
                }
            }
        }



        private void Disconnect_Myo(Guid armbandGuid)
        {
            if (btnStopStream.Visibility == System.Windows.Visibility.Visible)
            {
                StopDataStream(true);
            }

            MyoArmband myo = connectedMyos.Where(g => (g.Id == armbandGuid)).FirstOrDefault();
            if (myo == null) { return; }

            if (myo.controlService != null)
            {
                myo.controlService.Dispose();
                myo.imuService.Dispose();
                myo.emgService.Dispose();
                myo.Device.Dispose();
            }

            myo.FW_charac = null;
            myo.cmdCharac = null;
            myo.imuCharac = null;
            myo.emgCharacs = null;
            connectedMyos.Remove(myo);

            deviceList.Clear();
            addressBook.Clear();
            captureDuration = 0;

            GC.Collect();

            Dispatcher.Invoke(() =>
            {
                btnStartStream.Visibility = System.Windows.Visibility.Visible;
                btnStopStream.Visibility = System.Windows.Visibility.Hidden;
                txtDeviceLt.Text = "None";
                txtDeviceRt.Text = "None";
                txtBTAddrLt.Text = "None";
                txtBTAddrRt.Text = "None";
                txtDevConnStatLt.Text = "--";
                txtDevConnStatRt.Text = "--";
                txtTimer.Text = "0";
            });

            Setup_Watchers();
            Start_Watchers();
        }


        private void deviceConnChanged(BluetoothLEDevice sender, object args)
        {
            if (sender == null || sender.Name == "<null>") { return; }

            MyoArmband modifiedMyo = null;
            int indexOfModifiedMyo = 0;

            foreach (MyoArmband myo in connectedMyos.Where(a => a.Name == sender.Name).ToList())
            {
                modifiedMyo = myo;
                indexOfModifiedMyo = connectedMyos.IndexOf(myo);
            }

            if (modifiedMyo == null) { return; }

            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                connectedMyos[indexOfModifiedMyo].IsReady = false;
                connectedMyos[indexOfModifiedMyo].IsConnected = false;

                foreach (string s in bondedMyos.ToList())
                {
                    if (s == modifiedMyo.Name) { bondedMyos.Remove(s); } 
                    }

                connectedMyos.Remove(modifiedMyo);
                Disconnect_Myo(modifiedMyo.Id);

                currentDevice = null;
                isConnecting = false;

            }

            else if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                connectedMyos[indexOfModifiedMyo].IsConnected = true;

                while (!connectedMyos[indexOfModifiedMyo].IsReady)
                {
                    //int errCode = SetupMyo(connectedMyos[indexOfModifiedMyo].Id);
                    //Console.WriteLine("setup of " + modifiedMyo.Name + " attempted, returned code: " + errCode);
                }
            }
        }

        private void bondedMyos_Changed(object sender, NotifyCollectionChangedEventArgs e)
        {
            // manage watchers based on number of items in list
            if (e.NewItems != null)
            {
                if (readyMyos == 2 && BleWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopped)
                {
                    deviceWatcher.Stop();
                    BleWatcher.Stop();
                    Console.WriteLine("watchers stopped");
                    //btnStartStream.IsEnabled = true;
                }
            }
            if (e.OldItems != null)
            {
                if (readyMyos < 2 && BleWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Started)
                {
                    deviceWatcher.Start();
                    BleWatcher.Start();
                    Console.WriteLine("Watchers started");
                }
            }
            UpdateConnectionStatus();
        }

        #endregion Connect and Disconnect


        #region Prep, Start and Stop Data Streams

        private void Setup_Data_Channels()
        {
            myoGuids = new Dictionary<string, Guid>();

            // enumerate myo guids
            myoGuids.Add("MYO_DEVICE_NAME", new Guid("D5062A00-A904-DEB9-4748-2C7F4A124842")); // Device Name 
            myoGuids.Add("BATTERY_SERVICE", new Guid("0000180f-0000-1000-8000-00805f9b34fb")); // Battery Service
            myoGuids.Add("BATTERY_LEVLL_C", new Guid("00002a19-0000-1000-8000-00805f9b34fb")); // Battery Level Characteristic

            myoGuids.Add("MYO_SERVICE_GCS", new Guid("D5060001-A904-DEB9-4748-2C7F4A124842")); // Control Service
            myoGuids.Add("MYO_FIRMWARE_CH", new Guid("D5060201-A904-DEB9-4748-2C7F4A124842")); // Firmware Version Characteristic (read)
            myoGuids.Add("COMMAND_CHARACT", new Guid("D5060401-A904-DEB9-4748-2C7F4A124842")); // Command Characteristic (write)

            myoGuids.Add("MYO_EMG_SERVICE", new Guid("D5060005-A904-DEB9-4748-2C7F4A124842")); // raw EMG data service
            myoGuids.Add("EMG_DATA_CHAR_0", new Guid("D5060105-A904-DEB9-4748-2C7F4A124842")); // ch0 : EMG data characteristics (notify)
            myoGuids.Add("EMG_DATA_CHAR_1", new Guid("D5060205-A904-DEB9-4748-2C7F4A124842")); // ch1
            myoGuids.Add("EMG_DATA_CHAR_2", new Guid("D5060305-A904-DEB9-4748-2C7F4A124842")); // ch2
            myoGuids.Add("EMG_DATA_CHAR_3", new Guid("D5060405-A904-DEB9-4748-2C7F4A124842")); // ch3

            myoGuids.Add("IMU_DATA_SERVIC", new Guid("D5060002-A904-DEB9-4748-2C7F4A124842")); // IMU service
            myoGuids.Add("IMU_DATA_CHARAC", new Guid("D5060402-A904-DEB9-4748-2C7F4A124842")); // IMU characteristic

            myoGuids.Add("CLASSIFR_SERVIC", new Guid("D5060003-A904-DEB9-4748-2C7F4A124842")); // Classifier event service.
            myoGuids.Add("CLASSIFR_CHARAC", new Guid("D5060103-A904-DEB9-4748-2C7F4A124842")); // Classifier event data characteristic (indicate)     
        }

        /*
        private async void Init_Data_Channels()
        {
            // Establish connection to EMG data characteristics (4)
            GattCharacteristicsResult emgServ = await gatt.Services.Single
                (s => s.Uuid == myoGuids["MYO_EMG_SERVICE"]).GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

            emgStat = new List<GattCommunicationStatus>(4) { 0, 0, 0, 0 };
            _emgChars = new List<GattCharacteristic>(4)
            {
                emgServ.Characteristics.Single(c => c.Uuid == myoGuids["EMG_DATA_CHAR_0"]), ///ch0
                emgServ.Characteristics.Single(c => c.Uuid == myoGuids["EMG_DATA_CHAR_1"]), ///ch1
                emgServ.Characteristics.Single(c => c.Uuid == myoGuids["EMG_DATA_CHAR_2"]), ///ch2
                emgServ.Characteristics.Single(c => c.Uuid == myoGuids["EMG_DATA_CHAR_3"]), ///ch3
            };

            for (int k = 0; k < 4; k++)
            {
                emgStat[k] = await Notify(_emgChars[k], charDesVal_notify);
            }

            _emgChars[0].ValueChanged += _dh.EMG0_ValueChanged;
            _emgChars[1].ValueChanged += _dh.EMG1_ValueChanged;
            _emgChars[2].ValueChanged += _dh.EMG2_ValueChanged;
            _emgChars[3].ValueChanged += _dh.EMG3_ValueChanged;


            GattCharacteristicsResult imuCharac = await gatt.Services.Single
                (s => s.Uuid == myoGuids["IMU_DATA_SERVIC"]).GetCharacteristicsAsync();

            _IMUcharac = imuCharac.Characteristics.Single(c => c.Uuid == myoGuids["IMU_DATA_CHARAC"]);
            _IMUcharac.ValueChanged += _dh.IMU_ValueChanged;
            GattCommunicationStatus imuStatus = await Notify(_IMUcharac, charDesVal_notify);


            // Check that all notifications have succeeded
            if ((int)emgStat[0] + (int)emgStat[1] + (int)emgStat[2] + (int)emgStat[3] == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    lblDevStatus.Text = "EMG Ready";
                    
                    BtnDisconnect.Visibility = System.Windows.Visibility.Visible;
                    btnStreamEMG.IsEnabled = true;
                    //btnConnect.IsEnabled = true;
                });
            }

            if (imuStatus == GattCommunicationStatus.Success)
            {
                lblDevStatus.Text = "EMG and IMU Ready";
                _dh.Prep_Datastream();
            }
            else
            {
                lblDevStatus.Text = "IMU Error";
            }
        }

        */

        public int SetupMyo(string myoName)
        {
            MyoArmband myo = connectedMyos.Where(g => (g.Name == myoName)).FirstOrDefault();
            int myoIndex = connectedMyos.IndexOf(myo);

            if (myo.Device == null) { return 1; }

            try
            {
                // Establishing an IMU data connection
                var myServices = Task.Run(() => GetServiceAsync(myo.Device, myoGuids["IMU_DATA_SERVIC"])).Result;
                myo.imuService = myServices.Services.FirstOrDefault();
                if (myo.imuService == null) { return 2; }

                var imuDataChar = Task.Run(async () => await GetCharac(myo.imuService, myoGuids["IMU_DATA_CHARAC"])).Result;
                myo.imuCharac = imuDataChar.Characteristics.FirstOrDefault();


                if (myo.imuCharac == null) { return 3; }

                myo.imuCharac.ValueChanged += myo.myDataHandler.IMU_ValueChanged;


                // Establishing an EMG data connection
                var myservs = Task.Run(() => GetServiceAsync(myo.Device, myoGuids["MYO_EMG_SERVICE"])).Result;
                myo.emgService = myservs.Services.FirstOrDefault();
                if (myo.emgService == null) { return 4; }

                Task<GattCommunicationStatus>[] EmgNotificationTasks = new Task<GattCommunicationStatus>[4];
                for (int t = 0; t < 4; t++)
                {
                    string currEMGChar = "EMG_DATA_CHAR_" + t.ToString();
                    var tempCharac = Task.Run(() => GetCharac(myo.emgService, myoGuids[currEMGChar])).Result;
                    myo.emgCharacs[t] = tempCharac.Characteristics.FirstOrDefault();

                    EmgNotificationTasks[t] = Notify(myo.emgCharacs[t], charDesVal_notify);
                }
                Task.WaitAll(EmgNotificationTasks);

                myo.emgCharacs[0].ValueChanged += myo.myDataHandler.EMG0_ValueChanged;
                myo.emgCharacs[1].ValueChanged += myo.myDataHandler.EMG1_ValueChanged;
                myo.emgCharacs[2].ValueChanged += myo.myDataHandler.EMG2_ValueChanged;
                myo.emgCharacs[3].ValueChanged += myo.myDataHandler.EMG3_ValueChanged;

                int emgErrCode = (int)myo.EmgConnStat[0] + (int)myo.EmgConnStat[1] + (int)myo.EmgConnStat[2] + (int)myo.EmgConnStat[3];
                if (emgErrCode != 0) { return 5; }

                // signify readiness
                vibrate_armband(myo);
                myo.IsReady = true;
                myo.DevConnStat = BluetoothConnectionStatus.Connected;
                myo.myDataHandler.Prep_Datastream(myo.Name, "");


                if (!bondedMyos.Contains(myo.Name))
                { bondedMyos.Add(myo.Name); }

                // update data objects
                connectedMyos[myoIndex] = myo;
                currentDevice = null;
                isConnecting = false;
            }

            catch { return 9; }

            return 0;
        }



        public void vibrate_armband(MyoArmband myo)
        {
            if (myo.cmdCharac != null)
            {
                byte[] vibeShort = new byte[] { 0x03, 0x01, 0x01 };
                Write(myo.cmdCharac, vibeShort);
            }
        }



        public void StartDataStream() // check which armbands are connected and send start commands to each
        {
            // Switching on (selected) data streams with the following key:
            //{ SetModes, Payload=0x03, EMG[0=off,2=filtered,3=raw], IMU[0=off, 1=data, 2=events, 3=data&events, 4=raw], Classifier[off,on] }
            byte[] startStreamCommand = new byte[] { 0x01, 0x03, 0x02, 0x01, 0x00 };

            foreach (MyoArmband myo in connectedMyos.Where(x => x.IsReady == true).ToList())
            {
                // final check to make sure we are not writing to nowhere
                if (myo.cmdCharac != null)
                {
                    Write(myo.cmdCharac, startStreamCommand);
                    dispatcherTimer.Start();

                }
            }

            btnStopStream.Visibility = System.Windows.Visibility.Visible;
            btnStartStream.Visibility = System.Windows.Visibility.Hidden;
            

        }

        /*
        private async void Stop_Data_Stream()
        {

            // stop EMG and IMU
            byte[] stopEMGdata = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00 }; 

            if (commandCharacteristic != null)
            { GattCommunicationStatus result = await Write(commandCharacteristic, stopEMGdata); }

            _dh.Stop_Datastream();

            btnStopEMGStream.Visibility = System.Windows.Visibility.Hidden;
            dispatcherTimer.Stop();
        }
        */

        public void StopDataStream(bool targetConnectedDevices = false)
        {
            byte[] stopStreamCommand = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00 };
            foreach (MyoArmband myo in connectedMyos.Where(x => x.IsReady == targetConnectedDevices).ToList())
            {
                // final check to make sure we are not writing to nowhere
                if (myo.cmdCharac != null)
                {
                    Write(myo.cmdCharac, stopStreamCommand);
                    dispatcherTimer.Stop();
                    
                }
                if (myo.myDataHandler != null)
                {
                    myo.myDataHandler.IsRunning = false;
                    myo.myDataHandler.Stop_Datastream();
                }
            }

            btnStopStream.Visibility = System.Windows.Visibility.Hidden;
            btnStartStream.Visibility = System.Windows.Visibility.Visible;
        }


        #endregion Prep, Start and Stop Data Streams


        #region Update Functions

        private void Update_Timer()
        {
            Dispatcher.Invoke(() =>
            {
                captureDuration += dispatcherTimer.Interval.TotalMilliseconds;
                txtTimer.Text = (captureDuration / 1000).ToString();
            });
        }


        private void UpdateAddressBook(DeviceInformation args)
        {
            List<String> keys = args.Properties.Keys.ToList();
            List<Object> vals = args.Properties.Values.ToList();

            var dictionary = keys.Zip(vals, (k, v) => new { Key = k, Value = v })
                     .ToDictionary(x => x.Key, x => x.Value);

            // Set a variable to the My Documents path.
            string mydocpath = Environment.CurrentDirectory + @"\Devices\";
            string fileName = args.Name + ".txt";

            // Append text to an existing file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(mydocpath + fileName, append: false))
            {
                foreach (KeyValuePair<String, Object> kvp in dictionary)
                {
                    outputFile.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                outputFile.Flush();
                outputFile.Close();
            }

            var macaddr = dictionary.Single(k => k.Key.Contains("DeviceAddress")).Value.ToString();
            addressBook.Add(macaddr);
            Console.WriteLine($"{args.Name} added to Address Book: {macaddr}");
        }



        private void UpdateConnectionStatus()
        {
            MyoArmband foundLeft = connectedMyos.Where(g => (g.Name == "MyoL")).FirstOrDefault();
            MyoArmband foundRight = connectedMyos.Where(g => (g.Name == "MyoR")).FirstOrDefault();
            readyMyos = 0;

            Dispatcher.Invoke(() =>
            {
                if (foundLeft != null)
                {
                    txtDevConnStatLt.Text = foundLeft.DevConnStat.ToString();
                    if (foundLeft.DevConnStat == BluetoothConnectionStatus.Connected)
                    {
                        readyMyos++;
                        btnStartStream.IsEnabled = true;
                    }
                }
                if (foundRight != null)
                {
                    txtDevConnStatRt.Text = foundRight.DevConnStat.ToString();
                    if (foundRight.DevConnStat == BluetoothConnectionStatus.Connected)
                    {
                        readyMyos++;
                        btnStartStream.IsEnabled = true;
                    }
                }
                if (readyMyos == 2)
                {
                    //btnStartStream.IsEnabled = true;
                }
            });
        }


        #endregion Update Functions


        #region Watcher Functions


        private bool watcherLocked = false;

        private async Task<BluetoothLEDevice> getDevice(ulong btAddr)
        {
            watcherLocked = true;   
            return await BluetoothLEDevice.FromBluetoothAddressAsync(btAddr);
        }

        private void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // Address book checking to ensure correct device selection
            string tempMac = args.BluetoothAddress.ToString("X");
            string regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            string replace = "$1:$2:$3:$4:$5:$6";
            string macaddress = System.Text.RegularExpressions.Regex.Replace(tempMac, regex, replace);

            if (connectedMyos.Count < 2 && currentDevice == null) // myos yet to be found && not busy finding one
            {
                Task devChk = getDevice(args.BluetoothAddress).ContinueWith((antecedant) =>
                {
                    if (antecedant.Status == TaskStatus.RanToCompletion && antecedant.Result != null)   // if the result is confirmed
                    {
                        if (deviceList.Contains(antecedant.Result.Name) && !bondedMyos.Contains(antecedant.Result.Name)) // and it's not already connected       
                        {
                            isConnecting = true;
                            currentDevice = antecedant.Result;
                            Guid myoId = AddMyoArmbandFromDevice(currentDevice);

                            Dispatcher.Invoke(() =>
                            {
                                if (currentDevice.Name == "MyoL")
                                {
                                    txtDeviceLt.Text = currentDevice.Name;
                                    txtBTAddrLt.Text = macaddress;
                                }
                                else if (currentDevice.Name == "MyoR")
                                {
                                    txtDeviceRt.Text = currentDevice.Name;
                                    txtBTAddrRt.Text = macaddress;
                                }
                            });

                            if (connectedMyos.Count <= 2) { ConnectToArmband(myoId); }
                        }
                    }
                });
                devChk.Wait();
            }
        }


        private void Watcher_Stopped(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementWatcherStoppedEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                string btError;
                switch (eventArgs.Error)
                {
                    case BluetoothError.RadioNotAvailable:
                        btError = "Bluetooth not available";
                        break;

                    case BluetoothError.ResourceInUse:
                        btError = "Device is busy elsewhere";
                        break;
                }

            });
        }


        private void DeviceWatcher_Updated(DeviceWatcher watcher, DeviceInformationUpdate update)
        {
            List<String> keys = update.Properties.Keys.ToList();
            List<Object> vals = update.Properties.Values.ToList();
            List<String> updates = new List<string>();

            for (int x = 0; x < keys.Count(); x++)
            {
                updates.Add($"Update {x}: {keys[x].ToString()}: {vals[x].ToString()}");
            }
        }


        private void DeviceWatcher_Removed(DeviceWatcher watcher, DeviceInformationUpdate args)
        {
            var toRemove = (from a in deviceList where a == args.ToString() select a).FirstOrDefault();
            if (toRemove != null)
            {
                deviceList.Remove(toRemove);
            }
        }


        private void DeviceWatcher_Added(DeviceWatcher watcher, DeviceInformation args)
        {
            deviceList.Add(args.Name);
            UpdateAddressBook(args);
        }


        #endregion Watcher Functions


        #region GATT Functions


        private async Task<GattDeviceServicesResult> GetServiceAsync(BluetoothLEDevice dev, Guid my_Guid)
        {
            var tcs = new TaskCompletionSource<GattDeviceServicesResult>();
            tcs.SetResult(await dev.GetGattServicesForUuidAsync(my_Guid));
            var waiter = tcs.Task.GetAwaiter();
            tcs.Task.Wait();

            if (waiter.IsCompleted)
            {
                return tcs.Task.Result;
            }
            else { return null; }
        }


        private async Task<GattCharacteristicsResult> GetCharac(GattDeviceService gds, Guid characGuid)
        {
            var tcs = new TaskCompletionSource<GattCharacteristicsResult>();
            tcs.SetResult(await gds.GetCharacteristicsForUuidAsync(characGuid));
            var waiter = tcs.Task.GetAwaiter();
            tcs.Task.Wait();

            if (waiter.IsCompleted)
            {
                return tcs.Task.Result;
            }
            else { return null; }
        }


        public static Task<GattReadResult> Read(GattCharacteristic characteristic)
        {
            var tcs = new TaskCompletionSource<GattReadResult>();
            Task.Run(async () =>
            {
                var _myres = await characteristic.ReadValueAsync(BluetoothCacheMode.Cached);
                tcs.SetResult(_myres);
            });
            return tcs.Task;
        }

        public static Task<GattCommunicationStatus> Write(GattCharacteristic charac, byte[] msg)
        {
            var tcs = new TaskCompletionSource<GattCommunicationStatus>();
            Task.Run(async () =>
            {
                await charac.WriteValueAsync(msg.AsBuffer());
            });
            return tcs.Task;
        }

        public static Task<GattCommunicationStatus> Notify(GattCharacteristic charac, GattClientCharacteristicConfigurationDescriptorValue value)
        {
            var tcs = new TaskCompletionSource<GattCommunicationStatus>();
            Task.Run(async () =>
            {
                var _myres = await charac.WriteClientCharacteristicConfigurationDescriptorAsync(value);
                tcs.SetResult(_myres);
            });
            tcs.Task.Wait();
            return tcs.Task;
        }

        #endregion GATT Functions
    }
}


