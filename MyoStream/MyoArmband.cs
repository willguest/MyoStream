using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MyoStream
{
    public class MyoArmband
    {
        public System.Guid Id { get; set; }
        public string Name { get; set; }

        public bool IsConnected { get; set; } = false;
        public bool IsReady { get; set; } = false;

        public bool gotIMUCharac { get; set; } = false;
        public bool gotEMGcharacs { get; set; } = false;


        // bluetooth low energy device
        public BluetoothLEDevice Device { get; set; }

        // services and characteristics
        public GattDeviceService controlService { get; set; }
        public GattCharacteristic FW_charac { get; set; }
        public string myFirmwareVersion { get; set; }

        public GattDeviceService imuService { get; set; }
        public GattDeviceService emgService { get; set; }

        public GattCharacteristic cmdCharac { get; set; }
        public GattCharacteristic imuCharac { get; set; }
        public GattCharacteristic[] emgCharacs { get; set; }

        // BLE Stati and other configuration variables
        public BluetoothConnectionStatus DevConnStat { get; set; }
        public GattCommunicationStatus CharNotStat { get; set; }
        public GattCommunicationStatus[] EmgConnStat { get; set; }

        // data handler to receive and process incoming streams
        public DataHandler myDataHandler { get; set; }
    }
}