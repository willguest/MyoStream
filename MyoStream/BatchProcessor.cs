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

        private int numChannels = 0;
        private int currentDataLength = 0;

        public double[][] rawData;
        private double[][] clnData;

        private double[][] detailData;
        private double[][] approxData;

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        private StreamWriter sWriter;
        public List<string> WaveletNames = new List<string>();
        public List<object[]> ListOfWavelets = new List<object[]>();
        public int SelectedWaveletIndex { get; set; }
        private MotherWavelet currentWavelet;

        List<double[]> approxFirst = new List<double[]>();
        List<double[]> approxSecnd = new List<double[]>();
        List<double[]> detailFirst = new List<double[]>();
        List<double[]> detailSecnd = new List<double[]>();

        List<DecompositionLevel> dwt = new List<DecompositionLevel>();

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
            if (waveName != null)
            {
                currentWavelet = CommonMotherWavelets.GetWaveletFromName(waveName);
            }
        }



        public int LoadFileFromDir(string directory, string filename)
        {
            long startTime = DateTime.UtcNow.Ticks;

            currentDirectory = directory;
            currentFilename = filename + ".csv";
            string filePath = directory + "/" + currentFilename;

            try
            {
                var contents = File.ReadAllText(filePath).Split('\n');
                var titles = contents[0];
                var csv = from line in contents
                          select line.Split(',').ToArray();

                //if (filename.Contains("EMG")) { numChannels = 8; }
                //if (filename.Contains("IMU")) { numChannels = 10; }

                numChannels = csv.First().Count();
                currentDataLength = csv.Count() - 1;

                rawData = new double[numChannels][];
                clnData = new double[numChannels][];
                rawData = StructureData(numChannels, csv.Count(), csv.ToArray());            // change this line for files without headers and/or timestamps

                long duration = DateTime.UtcNow.Ticks - startTime;

                Console.WriteLine(currentDataLength + " data points loaded and arranged in " + (float)duration / 10000f + " milliseconds");


                //FlowingTensors myTF = new FlowingTensors();
                //myTF.DoSomeWork(rawData);


                return currentDataLength;

            }
            catch (IOException e)
            {
                Console.WriteLine("File is open.  Please close it and try again");
                return 0;
            }

        }


        public double[][] StructureData(int noChannels, int dataLength, string[][] inputArray)
        {
            string[] titles = inputArray[0];
            double[][] currData = new double[noChannels - 1][];

            for (int i = 0; i < noChannels - 1; i++)
            {
                currData[i] = new double[dataLength - 2];
            }

            try
            {
                // remove time stamp and header for ease of use, sort into columns instead of rows
                for (int x = 1; x < dataLength - 1; x++)
                {
                    double[] _dubs = Array.ConvertAll(inputArray[x], double.Parse);

                    for (int y = 1; y < noChannels; y++)
                    {
                        currData[y - 1][x - 1] = _dubs[y];
                    }
                }
            }
            catch (FormatException f)
            {
                Console.WriteLine("Error thrown: " + f.Message);
                Console.WriteLine("Please check the file contents for errors");
                return null;
            }

            return currData;
        }

        /*
        public void StoreData()
        {
            Prep_Datastream();
            Task storeIt = Task.Run(async () => await StoreEMGData(currentDataLength, rawData, clnData).ConfigureAwait(true));
            sWriter.Close();
            sWriter.Dispose();
            sWriter = null;
        }
        */



        public void PlotEMGData(string session, string directory)
        {
            int maxDecompLev = 5;
            int noChannels = 8;

            // prepare arrays for dwt data
            double[][][] allRecData = new double[maxDecompLev][][];
            double[][][] allDetails = new double[maxDecompLev][][];
            double[][][] allApproxs = new double[maxDecompLev][][];

            for (int d = 0; d < maxDecompLev; d++)
            {
                allRecData[d] = new double[noChannels][];
                allDetails[d] = new double[noChannels][];
                allApproxs[d] = new double[noChannels][];

                for (int ch = 0; ch < noChannels; ch++)
                {
                    allRecData[d][ch] = new double[currentDataLength];
                    allDetails[d][ch] = new double[currentDataLength];
                    allApproxs[d][ch] = new double[currentDataLength];
                }
            }


            // perform DWT
            for (int x = 0; x < noChannels; x++)
            {
                double[][] DWTData = Task.Run(() => PerformDWT(rawData[x], currentWavelet, maxDecompLev)).Result;

                for (int y = 0; y < maxDecompLev; y++)
                {
                    allRecData[y][x] = DWTData[y];
                    allApproxs[y][x] = approxData[y];
                    allDetails[y][x] = detailData[y];
                }
            }

            Plotter myPlotter = new Plotter();
            myPlotter.BuildDWTChart(session, directory + "/Images", currentWavelet.Name, maxDecompLev, rawData, allDetails, allApproxs, allRecData);
        }

        public void PlotIMUData(string session, string directory)
        {
            Plotter newPlotter = new Plotter();
            newPlotter.BuildIMUChart(session, directory + "/Images", rawData);
        }




        public void SizeDataArrays(int dataLength)
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];
            int n = 0;

            if (currentDataLength < 1024)
            {
                n = 4;
                while (n + 4 <= currentDataLength)
                { n += 4; }
            }
            else
            {
                n = 1024;
                while (n + 256 <= currentDataLength)
                { n += 256; }
            }

            Console.WriteLine("max n value used: " + n);

            int numberOfUnusedDataPoint = currentDataLength - n;
            Console.WriteLine(numberOfUnusedDataPoint + " data points were trimmed from this data set");

            for (int z = 0; z < numChannels; z++)
            {
                clnData[z] = new double[n];
            }
        }

        /*
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
        */

        public async Task<double[][]> PerformDWT(double[] inputData, MotherWavelet inputWavelet, int maxDecompLevel)
        {
            Signal signal = new Signal(inputData);
            MotherWavelet wavelet = inputWavelet;

            double[][] reconstrData = new double[maxDecompLevel][];
            detailData = new double[maxDecompLevel][];
            approxData = new double[maxDecompLevel][];

            for (int r = 1; r <= maxDecompLevel; r++)
            {
                dwt = DWT.ExecuteDWT(signal, wavelet, r, SignalExtension.ExtensionMode.SymmetricWholePoint, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

                reconstrData[r - 1] = new double[signal.SamplesCount];
                reconstrData[r - 1] = DWT.ExecuteIDWT(dwt, wavelet, r, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

                detailData[r - 1] = new double[signal.SamplesCount];
                approxData[r - 1] = new double[signal.SamplesCount];
            }

            for (int d = 0; d < dwt.Count; d++)
            {
                approxData[d] = dwt[d].Approximation;
                detailData[d] = dwt[d].Details;
            }

            return reconstrData;
        }

    }
}
