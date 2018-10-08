using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using CenterSpace.NMath.Core;

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
        public int noPoints = 64;

        #region Private Variables

        public delegate void CallbackStack(IMUDataFrame myoIMUFrame);
        public CallbackStack OnMyoIMUUpdate;

        public delegate void CallbackStack2(EMGDataFrame myoEMGFrame);
        public CallbackStack2 OnMyoEMGUpdate;

        private sbyte[][] EMGChannel0;
        private sbyte[][] EMGChannel1;
        private sbyte[][] EMGChannel2;
        private sbyte[][] EMGChannel3;

        private Int16[][] _IMUdata;

        private StreamWriter sWriter;
        //private DWTCalc _dwt;

        // EMG data storage
        private double[][] rawdubs = new double [9][] { new double[64], new double[64], new double[64], new double[64], new double[64], new double[64], new double[64], new double[64], new double[64]};

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        // IMU data storage
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

        //private double myoRoll;
        //private double myoYaw;
        //private double myoPitch;

        private string _tStamp0;
        private string _tStamp1;

        private string ortData = "";
        private string accData = "";
        private string IMUstring = "";

        #endregion Private Variables


        public DataHandler()
        {
            IsRunning = false;

            NMathConfiguration.Init();
            Console.WriteLine("NMathConfiguration initiated successfully");
        }


        #region EMG Data Capture

        // Need to check that using a single event handler for all channels is not compromising data quality!
        public void EMG0_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel0 = GetEMGData(args.CharacteristicValue);
            WrangleData(EMGChannel0);
        }
        public void EMG1_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel1 = GetEMGData(args.CharacteristicValue);
            WrangleData(EMGChannel1);
        }
        public void EMG2_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel2 = GetEMGData(args.CharacteristicValue);
            WrangleData(EMGChannel2);
        }
        public void EMG3_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            EMGChannel3 = GetEMGData(args.CharacteristicValue);
            WrangleData(EMGChannel3);
        }


        private sbyte[][] GetEMGData(IBuffer characVal)
        {
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
            _IMUdata = GetIMUData(args.CharacteristicValue);
            //ortData = string.Join(",", _IMUdata[0]);
            //accData = string.Join(",", _IMUdata[1]);
            //IMUstring = ortData + "," + accData;
     
            orientationX = _IMUdata[0][0];
            orientationY = _IMUdata[0][1];
            orientationZ = _IMUdata[0][2];
            orientationW = _IMUdata[0][3];
            _myoQuaternion = new Quaternion(orientationX, orientationY, orientationZ, orientationW);
            
            accelerationX = _IMUdata[1][0];
            accelerationY = _IMUdata[1][1];
            accelerationZ = _IMUdata[1][2];

        }

        private Int16[][] GetIMUData(IBuffer characVal)
        {
            DataReader reader = DataReader.FromBuffer(characVal);
            byte[] fileContent = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(fileContent);

            var rawIMUdata = new Int16[][] { new Int16[4], new Int16[3] };

            // Orientation (quat.) data
            System.Buffer.BlockCopy(fileContent, 0, rawIMUdata[0], 0, 8);

            // Acceleration data
            System.Buffer.BlockCopy(fileContent, 8, rawIMUdata[1], 0, 6);

            // Scale data as needed
            for (int u = 0; u < 4; u++)
            { rawIMUdata[0][u] /= (short)182.044; }

            for (int v = 0; v < 3; v++)
            { rawIMUdata[1][v] /= (short)22.756; }

            return rawIMUdata;
        }

        #endregion IMU Data Capture


        #region Prep and Stop Datastream

        public void Prep_Datastream(string deviceName, string sessionId)
        {
            bool newFile = false;
            var _now = DateTime.Now.ToString();
            string localFolder = Environment.CurrentDirectory;
            string fileName = (deviceName + "_Testing.csv");

            string headers = "Timestamp 0, raw_EMG_0, raw_EMG_1, raw_EMG_2, raw_EMG_3, raw_EMG_4, raw_EMG_5, raw_EMG_6, raw_EMG_7," +
                "Timestamp 1, cln_EMG_0, cln_EMG_1, cln_EMG_2, cln_EMG_3, cln_EMG_4, cln_EMG_5, cln_EMG_6, cln_EMG_7";


            if (!File.Exists(localFolder + "/" + fileName))
            {
                newFile = true;
            }

            sWriter = new StreamWriter(localFolder + "/" + fileName, append: true);

            if (newFile)
            {
                sWriter.WriteLine(headers);
            }

            sWriter.BaseStream.Seek(0, SeekOrigin.End);
            sWriter.Flush();
            sWriter.AutoFlush = true;

        }

        

        public void Stop_Datastream()
        {
            IsRunning = false;
            sWriter.Flush();
            sWriter.Close();
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
        private int debugNo = 0;
        private int cnt = 0;

        private void WrangleData(sbyte[][] rawData) 
        {
            int u = rawdubs[0].Length;
            if (cnt >= 64) {
                //cnt = 0;
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            rawdubs[0][0] = now;

            for (int x = 1; x < 9; x++)
            {
                rawdubs[x][cnt] = rawData[0][x-1];
                rawdubs[x][cnt + 1] = rawData[1][x-1];
            }


            if (cnt + 2 == noPoints)
            {
                //double[][] currVals = rawdubs;
                
                Task.Run(async() => await MainAsyncThread(rawdubs));
                //Console.WriteLine(debugNo + " Data arrays full, wrote " + (cnt + 2) + " records to dwt..." + now);
                //debugNo++;
                cnt = 0;

            }
            else
            {
                cnt += 2;
            }
            
        }


        private async Task MainAsyncThread(double[][] data)
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];

            double[][] _reconstructedData = new double[9][];
            _reconstructedData[0] = new double[64];

            for (int y = 1; y < 9; y++)
            {
                _reconstructedData[y] = new double[64];
                _tasks[y-1] = WorkerThread(data[y]);
            }

            await Task.WhenAll(_tasks);

            long now = DateTime.UtcNow.Ticks;
            _reconstructedData[0][0] = now;

            for (int z = 1; z < 9; z++)
            {
                _reconstructedData[z] = _tasks[z-1].Result;
            }

            await StoreData(data, _reconstructedData).ConfigureAwait(true);
        }


        private async Task<double[]> WorkerThread(double[] _input0) 
        {
            return await DiscreetWaveletTransform(_input0).ConfigureAwait(false);
        }


        private async Task<double[]> DiscreetWaveletTransform(double[] _input)
        {
            var data = new DoubleVector(_input);

            var wavelet = new DoubleWavelet(Wavelet.Wavelets.D2);
            //var coif5wavelet = new DoubleWavelet(Wavelet.Wavelets.C5);

            var dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            if (_input.Length == noPoints)
            {
                // Decompose signal to level 5
                //Console.WriteLine("decomposing signal");
                dwt.Decompose(4);
                
            }

            // Find Universal threshold
            double lambdaU = dwt.ComputeThreshold(DoubleDWT.ThresholdMethod.Universal, 1);

            // Threshold all detail levels with lambdaU
            dwt.ThresholdAllLevels(DoubleDWT.ThresholdPolicy.Soft,
                new double[] {lambdaU, lambdaU, lambdaU, lambdaU, lambdaU});

            // Rebuild signal to level 2
            double[] reconstructedData2 = dwt.Reconstruct(2);

            // testing this: change wavelet type to coif5 in order to get "prefect reconstruction"
            //dwt.Wavelet = coif5wavelet;

            // Rebuild the signal to level 1 - the original (filtered) signal.
            double[] rebuiltSignal = dwt.Reconstruct();

            // add timestamp to end of array
            //rebuiltSignal[rebuiltSignal.Length] = DateTime.UtcNow.Ticks;


            //Console.WriteLine("finished rebuilding signal");
            return rebuiltSignal;
        }


        private async Task StoreData(double [][] rawEMG, double[][] resEMG)
        {

            if (sWriter.BaseStream != null)
            {
                // use only complete data (truncate)
                for (int j = 0; j < noPoints; j++)
                {
                    for (int k = 0; k < 9; k++)
                    {
                        //ciao
                        startEMG[k] = rawEMG[k][j];
                        cleanEMG[k] = resEMG[k][j];
                    }

                    string atfrst = string.Join(",", startEMG);
                    string atlast = string.Join(",", cleanEMG);

                    sWriter.WriteLine(atfrst + "," + atlast);
                }
                sWriter.Flush();

                
            }
        }




        #endregion Data Wrangling
    }
}
