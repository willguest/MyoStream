using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using WaveletStudio;
using WaveletStudio.Wavelet;





namespace MyoStream
{
    public class BatchProcessor
    {
        private string currentDirectory = "";
        private string currentFilename = "";
        private int currentDataLength = 0;

        public double[][] rawData;
        //private double[][] midData = new double[9][];
        private double[][] clnData = new double[9][];

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        private StreamWriter sWriter;
        public List<string> WaveletNames = new List<string>();
        public List<object[]> ListOfWavelets = new List<object[]>();
        public int SelectedWaveletIndex { get; set; }

        private MotherWavelet currentWavelet;

        public BatchProcessor()
        {

        }

        public void IdentifyWavelets()
        {
            foreach (KeyValuePair<string, object[]> kvp in CommonMotherWavelets.Wavelets)
            {
                WaveletNames.Add(kvp.Key);
                ListOfWavelets.Add(kvp.Value);
            }
        }

        public void SelectWavelet(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string waveName = (sender as System.Windows.Controls.ComboBox).SelectedItem as string;
            currentWavelet = CommonMotherWavelets.GetWaveletFromName(waveName);
        }



        public int LoadFileFromDir(string directory, string filename)
        {
            long startTime = DateTime.UtcNow.Ticks;
            int numberOfChannels = 0;
            currentDirectory = directory;
            currentFilename = filename + ".csv";
            string filePath = directory + "/" + currentFilename;

            try
            {
                var contents = File.ReadAllText(filePath).Split('\n');
                var titles = contents[0];
                var csv = from line in contents
                          select line.Split(',').ToArray();

                if (filename.Contains("EMG")) { numberOfChannels = 9; }
                if (filename.Contains("IMU")) { numberOfChannels = 11; }

                rawData = new double[numberOfChannels][];

                StructureData(numberOfChannels, csv.Count() - 1, csv.ToArray());

                currentDataLength = rawData[0].Count() - 1;
                long duration = DateTime.UtcNow.Ticks - startTime;
                int dataCh = rawData.Length - 1;
                double[][] rawDataOut = new double[dataCh][];

                for (int x = 1; x < rawData.Length; x++)
                {
                    //rawData8[x-1] = new double[currentDataLength];
                    rawDataOut[x - 1] = rawData[x];
                }

                Console.WriteLine(currentDataLength + " data points loaded and arranged in " + (float)duration / 10000f + " milliseconds");


                //FlowingTensors myTF = new FlowingTensors();
                //myTF.DoSomeWork(rawData8);


                return currentDataLength;

            }
            catch (IOException e)
            {
                Console.WriteLine("File is open.  Please close it and try again");
                return 0;
            }

        }


        public void StructureData(int noChannels, int dataLength, string[][] inputArray)
        {
            currentDataLength = dataLength;
            string[] titles = inputArray[0];
            for (int i = 0; i < noChannels; i++)
            {
                rawData[i] = new double[dataLength];
            }


            for (int x = 1; x < dataLength; x++)
            {
                double[] _dubs = Array.ConvertAll(inputArray[x], double.Parse);

                for (int y = 1; y < noChannels; y++)
                {
                    rawData[y - 1][x - 1] = _dubs[y];
                }
            }
        }


        public void StoreData()
        {
            Prep_Datastream();
            Task storeIt = Task.Run(async () => await StoreEMGData(currentDataLength, rawData, clnData).ConfigureAwait(true));
            sWriter.Close();
            sWriter.Dispose();
            sWriter = null;
        }


        public void PlotEMGData(string session, string directory)
        {
            int noChannels = 8;
            int maxDecompLev = 5;

            // prepare arrays for decomposition data
            double[][][] allDWT = new double[maxDecompLev][][];
            double[][] allDetails = new double[noChannels][];
            double[][] allApproxs = new double[noChannels][];

            for (int d = 0; d < maxDecompLev; d++)
            {
                allDWT[d] = new double[noChannels][];
                for (int ch = 0; ch < noChannels; ch++)
                {
                    allDWT[d][ch] = new double[currentDataLength];
                }
            }
            for (int ch = 0; ch < noChannels; ch++)
            {
                allDetails[ch] = new double[detailsAllLevels.Length];
                allApproxs[ch] = new double[approxAllLevels.Length];
            }


            // perform DWT
            var myPlotter = new Plotter();

            for (int x = 0; x < noChannels; x++)
            {
                double[][] DWTData = Task.Run(() => PerformDWThere(rawData[x], currentWavelet, maxDecompLev)).Result;

                for (int y = 0; y < maxDecompLev; y++)
                {
                    allDWT[y][x] = DWTData[y];
                }

                allDetails[x] = detailsAllLevels;
                Array.Resize(ref detailsAllLevels, 0);
                allApproxs[x] = approxAllLevels;
                Array.Resize(ref approxAllLevels, 0);
            }

            myPlotter.BuildDWTChart(session, directory + "/Images", currentWavelet.Name, maxDecompLev, rawData, allDetails, allApproxs, allDWT);
        }

        public void PlotIMUData(string session, string directory)
        {
            Plotter newPlotter = new Plotter();
            newPlotter.BuildIMUChart(session, directory + "/Images", rawData);
        }


        private void Prep_Datastream()
        {
            bool newFile = false;
            var _now = DateTime.Now.ToString();

            string fileName = (currentFilename + "_Clean.csv");

            string headers = "Timestamp 0, raw_EMG_0, raw_EMG_1, raw_EMG_2, raw_EMG_3, raw_EMG_4, raw_EMG_5, raw_EMG_6, raw_EMG_7," +
                "Timestamp 1, cln_EMG_0, cln_EMG_1, cln_EMG_2, cln_EMG_3, cln_EMG_4, cln_EMG_5, cln_EMG_6, cln_EMG_7";

            if (!File.Exists(currentDirectory + "/" + fileName))
            {
                newFile = true;
            }

            sWriter = new StreamWriter(currentDirectory + "/" + fileName, append: true);

            if (newFile)
            {
                sWriter.WriteLine(headers);
            }

            sWriter.BaseStream.Seek(0, SeekOrigin.End);
            sWriter.Flush();
            sWriter.AutoFlush = true;

        }


        public void PrepareDataArrays(int dataLength)
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];
            int n = 0;

            if (currentDataLength < 1024)
            {
                n = 2;
                while (n + 2 <= currentDataLength)
                { n += 2; }
            }
            else
            {
                n = 1024;
                while (n + 256 <= currentDataLength)
                { n += 256; }
            }

            Console.WriteLine("max n value used: " + n);
            int noPoints = n;

            int numberOfUnusedDataPoint = currentDataLength - noPoints;
            Console.WriteLine(numberOfUnusedDataPoint + " data points were trimmed from this data set");

            for (int z = 0; z < 9; z++)
            {
                clnData[z] = new double[noPoints];

            }
        }


        private async Task StoreEMGData(int noRecords, double[][] rawEMG, double[][] resEMG)
        {

            if (sWriter.BaseStream != null)
            {
                // use only complete data (truncate)
                for (int j = 0; j < noRecords; j++)
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
        double[] approxAllLevels = new double[0];
        double[] detailsAllLevels = new double[0];

        List<DecompositionLevel> dwt = new List<DecompositionLevel>();

        public async Task<double[][]> PerformDWThere(double[] inputData, MotherWavelet inputWavelet, int maxDecompLevel)
        {
            Signal signal = new Signal(inputData);
            MotherWavelet wavelet = inputWavelet;

            double[][] reconstrData = new double[maxDecompLevel][];

            for (int r = 1; r <= maxDecompLevel; r++)
            {
                dwt = DWT.ExecuteDWT(signal, wavelet, r, SignalExtension.ExtensionMode.SymmetricWholePoint, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

                reconstrData[r - 1] = new double[signal.SamplesCount];
                reconstrData[r - 1] = DWT.ExecuteIDWT(dwt, wavelet, r, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

            }

            foreach (DecompositionLevel d in dwt)
            {
                int oldSizeA = approxAllLevels.Length;
                int newSizeA = oldSizeA + d.Approximation.Length;
                Array.Resize(ref approxAllLevels, newSizeA);
                Array.Copy(d.Approximation, 0, approxAllLevels, oldSizeA, d.Approximation.Length);

                int oldSizeD = detailsAllLevels.Length;
                int newSizeD = oldSizeD + d.Details.Length;
                Array.Resize(ref detailsAllLevels, newSizeD);
                Array.Copy(d.Details, 0, detailsAllLevels, oldSizeA, d.Details.Length);

            }

            return reconstrData;
        }

    }
}
