using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;


namespace MyoStream
{
    
    [Serializable]
    public class IMUDataFrame
    {
        public DateTime Timestamp;
        public Quaternion Orientation;
        public Vector3D Acceleration;
        public Vector3D HandPosition;
    }

    [Serializable]
    public class EMGDataFrame
    {
        public DateTime Timestamp;
        public sbyte[] EMG;
    }
    

    public class DataHandler
    {
        public bool IsRunning { get; set; }
        public int segment = 64;

        private string thisDeviceName = "";

        //public delegate void CallbackStack(IMUDataFrame myoIMUFrame);
        //public CallbackStack OnMyoIMUUpdate;

        //public delegate void CallbackStack2(EMGDataFrame myoEMGFrame);
        //public CallbackStack2 OnMyoEMGUpdate;

        public int totalEMGRecords = 0;
        public int totalIMURecords = 0;


        #region Private Variables

        private StreamWriter emgWriter;
        private StreamWriter imuWriter;

        // EMG data storage
        private sbyte[][] EMGChannel0;
        private sbyte[][] EMGChannel1;
        private sbyte[][] EMGChannel2;
        private sbyte[][] EMGChannel3;

        private double[][] rawdubs;
        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];
        private int cnt = 0;

        private int sessionNo = 1;
        private string date = DateTime.Now.Date.ToString("yyyyMMdd");

        // IMU data storage
        //private Int16[][] _IMUdata;
        private float[][] _fltIMUd;
        private Quaternion _myoQuaternion { get; set; }

        private float orientationW = 0;
        private float orientationX = 0;
        private float orientationY = 0;
        private float orientationZ = 0;
        private float accelerationX = 0;
        private float accelerationY = 0;
        private float accelerationZ = 0;
        private float gyroscopeX = 0;
        private float gyroscopeY = 0;
        private float gyroscopeZ = 0;

        private string ortData = "";
        private string accData = "";
        private string gyrData = "";
        private string IMUString = "";

        #endregion Private Variables


        public DataHandler()
        {
            IsRunning = false;

            rawdubs = new double[9][];
            for (int dArr = 0; dArr < 9; dArr++)
            {
                rawdubs[dArr] = new double[segment];
            }
        }


        #region EMG Data Capture

        public void EMG_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel0 = GetEMGData(args.CharacteristicValue);

            Task wrangleIt = Task.Factory.StartNew(() => WrangleEMGData(EMGChannel0));
            wrangleIt.Wait();
        }
        public void EMG1_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel1 = GetEMGData(args.CharacteristicValue);
            //Task.Run(() => WrangleEMGData(EMGChannel1));
        }
        public void EMG2_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel2 = GetEMGData(args.CharacteristicValue);
            //Task.Run(() => WrangleEMGData(EMGChannel2));
        }
        public void EMG3_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel3 = GetEMGData(args.CharacteristicValue);
            //Task.Run(() => WrangleEMGData(EMGChannel3));
        }


        private sbyte[][] GetEMGData(IBuffer characVal)
        {
            totalEMGRecords += 2;
            DataReader reader = DataReader.FromBuffer(characVal);
            byte[] fileContent = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(fileContent);

            sbyte[][] _data = new sbyte[][] { new sbyte[8], new sbyte[8] };

            System.Buffer.BlockCopy(fileContent, 0, _data[0], 0, 8);
            System.Buffer.BlockCopy(fileContent, 8, _data[1], 0, 8);

            return _data;
        }

        #endregion EMG Data Capture


        #region IMU Data Capture

        public void IMU_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            _fltIMUd = GetIMUData(args.CharacteristicValue);
            ortData = string.Join(",", _fltIMUd[0]);
            accData = string.Join(",", _fltIMUd[1]);
            gyrData = string.Join(",", _fltIMUd[2]);

            long nowTicks = DateTime.UtcNow.Ticks;
            IMUString = nowTicks + "," + ortData + "," + accData + "," + gyrData;

            Task.Run (async () => await StoreIMUData(IMUString).ConfigureAwait(true));

            /*
            orientationX = _IMUdata[0][0];
            orientationY = _IMUdata[0][1];
            orientationZ = _IMUdata[0][2];
            orientationW = _IMUdata[0][3];
            _myoQuaternion = new Quaternion(orientationX, orientationY, orientationZ, orientationW);
            
            accelerationX = _IMUdata[1][0];
            accelerationY = _IMUdata[1][1];
            accelerationZ = _IMUdata[1][2];

            gyroscopeX = _IMUdata[2][0];
            gyroscopeY = _IMUdata[2][1];
            gyroscopeZ = _IMUdata[2][2];
            */
        }

        private float[][] GetIMUData(IBuffer characVal)
        {
            DataReader reader = DataReader.FromBuffer(characVal);
            byte[] fileContent = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(fileContent);

            var rawIMUdata = new Int16[][] { new Int16[4], new Int16[3], new Int16[3] };
            var fltIMUdata = new float[][] { new float[4], new float[3], new float[3] };

            // Orientation (quat.) data
            System.Buffer.BlockCopy(fileContent, 0, rawIMUdata[0], 0, 8);

            // Acceleration data
            System.Buffer.BlockCopy(fileContent, 8, rawIMUdata[1], 0, 6);

            // Gyroscope data
            System.Buffer.BlockCopy(fileContent, 14, rawIMUdata[2], 0, 6);


            // Normalise
            for (int u = 0; u < 4; u++)
            { fltIMUdata[0][u] = ((float)(rawIMUdata[0][u] / 32768.0f)) + 0.5f; }

            for (int v = 0; v < 3; v++)
            { fltIMUdata[1][v] = ((float)(rawIMUdata[1][v] / 8192.0f)) + 0.5f; }

            for (int w = 0; w < 3; w++)
            { fltIMUdata[2][w] = ((float)(rawIMUdata[2][w] / 32768.0f)) + 0.5f; }
            

            /* Scaling (old)
            for (int u = 0; u < 4; u++)
            { rawIMUdata[0][u] = (short)(rawIMUdata[0][u] / 182.044f); }

            for (int v = 0; v < 3; v++)
            { rawIMUdata[1][v] = (short)(rawIMUdata[1][v]) / 22.756f; }

            for (int w = 0; w < 3; w++)
            { rawIMUdata[2][w] = (short)(rawIMUdata[2][w]); }
            */

            return fltIMUdata;
        }

        #endregion IMU Data Capture

        
        #region Prep and Stop Datastream

        public async void Prep_EMG_Datastream(string deviceName, string sessionId)
        {
            thisDeviceName = deviceName;

            string localFolder = "C:/Users/16102434/Desktop/Current Work/Myo/testData";  //Environment.CurrentDirectory;
            string fileName = (sessionId + "_" + date + "_" + sessionNo.ToString("D3") + "-" + deviceName + "_EMG_Data.csv");

            string headers = "Timestamp 0, raw_EMG_0, raw_EMG_1, raw_EMG_2, raw_EMG_3, raw_EMG_4, raw_EMG_5, raw_EMG_6, raw_EMG_7,";

            while (File.Exists(localFolder + "/" + fileName))
            {
                sessionNo++;
                fileName = (sessionId + "_" + date + "_" + sessionNo.ToString("D3") + "-" + deviceName + "_EMG_Data.csv");                              // incorp. number earlier
            }

            emgWriter = new StreamWriter(localFolder + "/" + fileName, append: true, encoding: System.Text.Encoding.UTF8, bufferSize: 1024);

            await Task.Run(() => emgWriter.WriteLine(headers));
            emgWriter.BaseStream.Seek(0, SeekOrigin.End);
            emgWriter.Flush();
            emgWriter.AutoFlush = false;
        }


        public void Prep_IMU_Datastream(string deviceName, string sessionId)
        {
            string localFolder = "C:/Users/16102434/Desktop/Current Work/Myo/testData";  // Environment.CurrentDirectory;
            string fileName = (sessionId + "_" + date + "_" + sessionNo.ToString("D3") + "-" + deviceName + "_IMU_Data.csv");

            while (File.Exists(localFolder + "/" + fileName))
            {
                sessionNo++;
                fileName = (sessionId + "_" + date + "_" + sessionNo.ToString("D3") + "-" + deviceName + "_IMU_Data.csv");
            }

            string headers = "Timestamp 0, orientationW, orientationX, orientationY, orientationZ," +
                "accelerationX, accelerationY, accelerationZ," +
                "gyroscopeX, gyroscopeY, gyroscopeZ";

            imuWriter = new StreamWriter(localFolder + "/" + fileName, append: true);

            imuWriter.WriteLine(headers);
            imuWriter.BaseStream.Seek(0, SeekOrigin.End);
            imuWriter.Flush();
            imuWriter.AutoFlush = false;
        }

        

        public void Stop_Datastream()
        {
            IsRunning = false;

            imuWriter.Flush();
            emgWriter.Flush();
            imuWriter.Close();
            emgWriter.Close();

            Console.WriteLine(totalEMGRecords + " EMG records received on all channels from " + thisDeviceName);
            Console.WriteLine(totalIMURecords + " IMU records received from " + thisDeviceName);
        }

        #endregion Prep and Stop Datastream


        #region (Unused) Send to IP via Socket
        /*
        private void SendData()
        {
            var rmsCopy = rms;
            string result = string.Join(",", rmsCopy);

            string s = "{ \"sensorName\":\"Myo\",\"attributes\":[{\"attributeName\":\"EMG\",\"attributteValue\":\"" + result +
                    "\"},{\"attributeName\":\"orientationW\",\"attributteValue\":\"" + orientationW +
                    "\" }, { \"attributeName\":\"orientationX\", \"attributteValue\":\"" + orientationX +
                    "\"},{\"attributeName\":\"orientationY\",\"attributteValue\":\"" + orientationY +
                    "\" },{\"attributeName\":\"orientationZ\",\"attributteValue\":\"" + orientationZ +
                    "\" },{\"attributeName\":\"myoRoll\",\"attributteValue\":\"" + myoRoll +
                    "\" },{\"attributeName\":\"myoPitch\",\"attributteValue\":\"" + myoPitch +
                    "\" },{\"attributeName\":\"myoYaw\",\"attributteValue\":\"" + myoYaw +
                    "\" }] }";

            byte[] send_buffer = Encoding.UTF8.GetBytes(s);

            sending_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            send_to_address = IPAddress.Parse("127.0.0.1");
            IPEndPoint sending_end_point = new IPEndPoint(send_to_address, 11002);

            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = sending_end_point;

            try
            {
                socketEventArg.SetBuffer(send_buffer, 0, send_buffer.Length);
                sending_socket.SendToAsync(socketEventArg);
                Console.WriteLine(s);
            }
            catch
            {
                Console.WriteLine("there was a problem with setbuffer or sendtoasync");
            }
        }
        */

        #endregion (Unused) Send to IP via Socket


        #region Data Wrangling
        

        private Task WrangleEMGData(sbyte[][] rawData) // receives a 9x2 array of EMG data, sends [segment]x9 array to streamwriter, when full
        {
            
            if (cnt >= segment) {
                Console.WriteLine("data arrays overloaded");
                return null;
            }

            long now = DateTime.UtcNow.Ticks;
            rawdubs[0][cnt] = now;
            rawdubs[0][cnt + 1] = now;

            for (int x = 1; x < 9; x++)
            {
                rawdubs[x][cnt] = rawData[0][x-1];
                rawdubs[x][cnt + 1] = rawData[1][x-1];
            }


            // only write out when we hit the segment size 
            if (cnt + 2 == segment)
            {
                Task.Run(() => StoreEmgData(rawdubs)).ConfigureAwait(true);
                cnt = 0;
            }
            else
            {
                cnt += 2;
            }
            return null;
        }

        /*
        private async Task MainAsyncThread(double[][] data)
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];

            double[][] _reconstructedData = new double[9][];
            _reconstructedData[0] = new double[noPoints];

            for (int y = 1; y < 9; y++)
            {
                _reconstructedData[y] = new double[noPoints];
                _tasks[y-1] = WorkerThread(data[y]);
            }

            await Task.WhenAll(_tasks);

            long now = DateTime.UtcNow.Ticks;
            _reconstructedData[0][0] = now;

            for (int z = 1; z < 9; z++)
            {
                _reconstructedData[z] = _tasks[z-1].Result;
            }

            await StoreEMGData(data, _reconstructedData).ConfigureAwait(true);
        }

        
        private async Task<double[]> WorkerThread(double[] _input0) 
        {
            return await DiscreetWaveletTransform(_input0).ConfigureAwait(false);
        }

        
        private async Task<double[]> DiscreetWaveletTransform(double[] _input)
        {
            var data = new DoubleVector(_input);
            var wavelet = new DoubleWavelet(Wavelet.Wavelets.D2);
            var dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            // Decompose the raw signal
            dwt.Decompose(4);

            // Find Universal threshold
            double lambdaU = dwt.ComputeThreshold(DoubleDWT.ThresholdMethod.Universal, 1);

            // Threshold all detail levels with lambdaU
            dwt.ThresholdAllLevels(DoubleDWT.ThresholdPolicy.Soft,
                new double[] {lambdaU, lambdaU, lambdaU, lambdaU, lambdaU});

            // Rebuild signal to level 2
            double[] reconstructedData2 = dwt.Reconstruct(2);

            // Rebuild the signal to level 1 - the original (filtered) signal.
            double[] rebuiltSignal = dwt.Reconstruct();

            //Console.WriteLine("finished rebuilding signal");
            return rebuiltSignal;
        }
        */

        private async void StoreEmgData(double[][] rawData)
        {
            // save EMG data
            if (emgWriter.BaseStream != null)
            {
                for (int j = 0; j < segment; j++)
                {
                    for (int k = 0; k < 9; k++)
                    {
                        startEMG[k] = rawData[k][j];
                    }

                    string atfrst = string.Join(",", startEMG);
                    emgWriter.WriteLine(atfrst);
                    emgWriter.Flush();
                }
            }
        }

        private async Task StoreEMGDataOld(double [][] rawEMG, double[][] resEMG)
        {
            if (emgWriter.BaseStream != null)
            {
                // use only complete data (truncate)
                for (int j = 0; j < segment; j++)
                {
                    for (int k = 0; k < 9; k++)
                    {
                        //ciao
                        startEMG[k] = rawEMG[k][j];
                        cleanEMG[k] = resEMG[k][j];
                    }

                    string atfrst = string.Join(",", startEMG);
                    string atlast = string.Join(",", cleanEMG);

                    await Task.Run(() => emgWriter.WriteLineAsync(atfrst + "," + atlast));
                    totalEMGRecords++;
                }
            }
        }

        
        private async Task StoreIMUData(string imuString)
        {
            if (imuWriter.BaseStream != null)
            {
                imuWriter.WriteLine(imuString);
                imuWriter.Flush();
                totalIMURecords++;
            }
        }
        

        #endregion Data Wrangling
    }
}
