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
        string filePath = "";

        private int numChannels = 0;
        private int currentDataLength = 0;

        public double[][] rawData;
        private double[][] clnData;

        

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        private StreamWriter sWriter;
        public List<string> WaveletNames = new List<string>();
        public List<object[]> ListOfWavelets = new List<object[]>();

        private MotherWavelet currentWavelet;
        public string chosenWavelet;

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
                chosenWavelet = currentWavelet.Name;
            }
        }



        public int LoadFileFromDir(string directory, string filename)
        {
            long startTime = DateTime.UtcNow.Ticks;

            currentDirectory = directory;
            currentFilename = filename + ".csv";
            filePath = directory + "/" + currentFilename;

            try
            {
                var contents = File.ReadAllText(filePath).Split('\n');
                var titles = contents[0];
                var csv = from line in contents
                          select line.Split(',').ToArray();

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

        private async Task<double[,]> Get_Features(int channelNo, double[] signal, double[][][] dwtResults)
        {
            
            double detailWAMPthreshold = 0.05;
            double approxWAMPthreshold = 0.05;
            double signalWAMPthreshold = 0.05;
            double reconsWAMPthreshold = 0.05;

            int noLevels = dwtResults[0].Length;

            // feature matrix 
            int noFeatures = 5;
            double[,] featDWT = new double[noFeatures, (3 * noLevels) + 1]; // 2 levels only implemented

            // signal features
            double rawSigWL = 0;
            double signalRmsCounter = 0;
            int signalWAMPCounter = 0;
            double signalMYOPCounter = 0;

            for (int lvl = 0; lvl < noLevels; lvl++)
            {
                double detailRmsCounter = 0;
                double approxRmsCounter = 0;
                double reconsRmsCounter = 0;

                double detailWL = 0;
                double approxWL = 0;

                int detailWAMPCounter = 0;
                int approxWAMPCounter = 0;

                double detailMYOPCounter = 0;
                double approxMYOPCounter = 0;
                
                double reconsWL = 0;

                double maxDetail = 0;
                double maxApprox = 0;

                // frequency domain
                for (int n = 0; n < dwtResults[0][lvl].Length; n++)
                {
                    double detailSqrd = dwtResults[0][lvl][n] * dwtResults[0][lvl][n];
                    double approxSqrd = dwtResults[1][lvl][n] * dwtResults[1][lvl][n];

                    // sum of squares
                    detailRmsCounter = detailRmsCounter + Math.Abs(detailSqrd);
                    approxRmsCounter = approxRmsCounter + Math.Abs(approxSqrd);

                    // update maximal values
                    if (detailSqrd > maxDetail)
                    {
                        maxDetail = detailSqrd;
                        featDWT[0, lvl] = (n + 1) / (double)dwtResults[0][lvl].Length;
                    }

                    if (approxSqrd > maxApprox)
                    {
                        maxApprox = approxSqrd;
                        featDWT[0, 2 + lvl] = (n + 1) / (double)dwtResults[1][lvl].Length;
                    }

                    if (n != 0)
                    {
                        double detailDelta = Math.Abs((dwtResults[0][lvl][n] - dwtResults[0][lvl][n - 1]));
                        double approxDelta = Math.Abs((dwtResults[1][lvl][n] - dwtResults[1][lvl][n - 1]));

                        // update waveform length
                        detailWL = detailWL + detailDelta;
                        approxWL = approxWL + approxDelta;

                        // WAMP
                        if (detailDelta > detailWAMPthreshold)
                        {
                            detailWAMPCounter++;
                        }
                        if (approxDelta > approxWAMPthreshold)
                        {
                            approxWAMPCounter++;
                        }
                    }

                    //MYOP
                    if (detailSqrd > detailWAMPthreshold)
                    {
                        detailMYOPCounter++;
                    }
                    if (approxSqrd > approxWAMPthreshold)
                    {
                        approxMYOPCounter++;
                    }

                }

                // RMS
                featDWT[1, lvl] = Math.Sqrt(detailRmsCounter / dwtResults[0][lvl].Length);
                featDWT[1, 2 + lvl] = Math.Sqrt(approxRmsCounter / dwtResults[1][lvl].Length);

                // WL
                featDWT[2, lvl] = detailWL;
                featDWT[2, 2 + lvl] = approxWL;

                // WAMP
                featDWT[3, lvl] = detailWAMPCounter;
                featDWT[3, 2 + lvl] = approxWAMPCounter;

                // MYOP
                featDWT[4, lvl] = detailMYOPCounter / dwtResults[0][lvl].Length;
                featDWT[4, 2 + lvl] = approxMYOPCounter / dwtResults[1][lvl].Length;


                // time domain features
                double maxSigSqrd = 0;
                double maxRecSqrd = 0;

                int reconsWAMPCounter = 0;
                double reconsMYOPCounter = 0;

                for (int n = 0; n < signal.Length; n++)
                {
                    double signalSqrd = signal[n] * signal[n];
                    double reconsSqrd = dwtResults[2][lvl][n] * dwtResults[2][lvl][n];

                    signalRmsCounter = signalRmsCounter + Math.Abs(signalSqrd);
                    reconsRmsCounter = reconsRmsCounter + Math.Abs(reconsSqrd);

                    // update maximal values for LMS
                    if (signalSqrd > maxSigSqrd)
                    {
                        maxDetail = signalSqrd;
                        featDWT[0, 4] = (n + 1) / (double)dwtResults[0][lvl].Length;
                    }

                    if (reconsSqrd > maxRecSqrd)
                    {
                        maxRecSqrd = reconsSqrd;
                        featDWT[0, 5 + lvl] = (n + 1) / (double)dwtResults[2][lvl].Length;
                    }

                    if (n != 0)
                    {
                        double signalDelta = Math.Abs(signal[n] - signal[n - 1]);
                        double reconsDelta = Math.Abs((dwtResults[2][lvl][n] - dwtResults[2][lvl][n - 1]));

                        // update waveform lengths
                        rawSigWL = rawSigWL + signalDelta;
                        reconsWL = reconsWL + reconsDelta;

                        // WAMP
                        if (signalDelta > signalWAMPthreshold)
                        {
                            signalWAMPCounter++;
                        }
                        if (reconsDelta > reconsWAMPthreshold)
                        {
                            reconsWAMPCounter++;
                        }
                    }

                    // MYOP
                    if (reconsSqrd > reconsWAMPthreshold)
                    {
                        reconsMYOPCounter++;
                    }
                    if (signalSqrd > signalWAMPthreshold)
                    {
                        signalMYOPCounter++;
                    }
                }

                // reconstructed signal RMS, WL, WAMP, MYOP
                featDWT[1, 5 + lvl] = Math.Sqrt(reconsRmsCounter / dwtResults[2][lvl].Length);
                featDWT[2, 5 + lvl] = reconsWL;
                featDWT[3, 5 + lvl] = reconsWAMPCounter;
                featDWT[4, 5 + lvl] = reconsMYOPCounter / dwtResults[2][lvl].Length;
            }

            // raw signal RMS, WL, WAMP, MYOP
            featDWT[1, 4] = Math.Sqrt(signalRmsCounter / signal.Length);
            featDWT[2, 4] = rawSigWL;
            featDWT[3, 4] = signalWAMPCounter;
            featDWT[4, 4] = signalMYOPCounter / signal.Length;

            return featDWT;
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



        public void PlotEMGData(string session, string directory, bool showGraph = true)
        {
            int maxDecompLev = 2;
            int noChannels = 8;

            double[][,] allFeatures = new double[noChannels][,];

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
                    allDetails[d][ch] = new double[currentDataLength];
                    allApproxs[d][ch] = new double[currentDataLength];
                    allRecData[d][ch] = new double[currentDataLength];
                }
            }

            if (chosenWavelet == null)
            {
                currentWavelet = CommonMotherWavelets.GetWaveletFromName(chosenWavelet);
            }

            

            // perform DWT
            for (int x = 0; x < noChannels; x++)
            {
                double[][][] DWTResults = Task.Run(() => PerformDWT(rawData[x], currentWavelet, maxDecompLev)).Result;

                for (int y = 0; y < maxDecompLev; y++)
                {
                    allDetails[y][x] = DWTResults[0][y];
                    allApproxs[y][x] = DWTResults[1][y];
                    allRecData[y][x] = DWTResults[2][y];
                }

                allFeatures[x] = Task.Run (() => Get_Features(x, rawData[x], DWTResults)).Result;
            }


            Plotter myPlotter = new Plotter();
            myPlotter.BuildDWTChart(session, directory + "/Images", currentWavelet.Name, maxDecompLev, rawData, allDetails, allApproxs, allRecData, allFeatures, showGraph);
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

        public async Task<double[][][]> PerformDWT(double[] inputData, MotherWavelet inputWavelet, int maxDecompLevel)
        {
            Signal signal = new Signal(inputData);
            MotherWavelet wavelet = inputWavelet;

            double[][][] output = new double[3][][];

            double[][] reconstrData;
            double[][] detailData;
            double[][] approxData;

            detailData = new double[maxDecompLevel][];
            approxData = new double[maxDecompLevel][];
            reconstrData = new double[maxDecompLevel][];

            for (int r = 1; r <= maxDecompLevel; r++)
            {
                dwt = DWT.ExecuteDWT(signal, wavelet, r, SignalExtension.ExtensionMode.SymmetricWholePoint, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

                detailData[r - 1] = new double[signal.SamplesCount];
                approxData[r - 1] = new double[signal.SamplesCount];

                reconstrData[r - 1] = new double[signal.SamplesCount];
                reconstrData[r - 1] = DWT.ExecuteIDWT(dwt, wavelet, dwt.Count, WaveletStudio.Functions.ConvolutionModeEnum.Normal);
            }

            for (int d = 0; d < dwt.Count; d++)
            {
                approxData[d] = dwt[d].Approximation;
                detailData[d] = dwt[d].Details;
            }

            output[0] = detailData;
            output[1] = approxData;
            output[2] = reconstrData;

            return output;
        }

    }
}
