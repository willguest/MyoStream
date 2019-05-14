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

        public List<string> WaveletNames = new List<string>();
        public List<object[]> ListOfWavelets = new List<object[]>();

        private MotherWavelet currentWavelet;
        public string chosenWavelet;

        List<DecompositionLevel> dwt = new List<DecompositionLevel>();

        public BatchProcessor()
        {

        }

        public List<string> IdentifyWavelets()
        {
            foreach (KeyValuePair<string, object[]> kvp in CommonMotherWavelets.Wavelets)
            {
                WaveletNames.Add(kvp.Key);
                ListOfWavelets.Add(kvp.Value);
            }
            return WaveletNames;
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



        public double[][] LoadFileFromDir(string directory, string filename)
        {
            

            currentDirectory = directory;
            currentFilename = filename;
            filePath = Path.Combine(directory, currentFilename);

            try
            {
                var contents = File.ReadAllText(filePath).Split('\n');
                var titles = contents[0];
                var csv = from line in contents
                          select line.Split(',').ToArray();

                numChannels = csv.First().Count() - 1;  // discount header
                currentDataLength = csv.Count() - 1;    // discount timestamp

                rawData = new double[numChannels][];
                rawData = StructureData(numChannels, currentDataLength, csv.ToArray());

                


                //FlowingTensors myTF = new FlowingTensors();
                //myTF.DoSomeWork(rawData);


                return rawData;

            }
            catch (IOException e)
            {
                Console.WriteLine("File is open.  Please close it and try again");
                return null;
            }

        }


        public double[][] StructureData(int noChannels, int dataLength, string[][] inputArray)
        {
            string[] titles = inputArray[0];
            double[][] currData = new double[noChannels][];

            for (int i = 0; i < noChannels; i++)
            {
                currData[i] = new double[dataLength];
            }

            try
            {
                // remove time stamp and header for ease of use, sort into columns instead of rows
                for (int x = 1; x < dataLength; x++)
                {
                    double[] _dubs = Array.ConvertAll(inputArray[x], double.Parse);

                    for (int y = 0; y < noChannels; y++)
                    {
                        currData[y][x] = _dubs[y + 1];
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

        public async Task<double[,]> Get_Features(double[] signal, double[][][] dwtResults)
        {
            
            double detailWAMPthreshold = 0.05;
            double approxWAMPthreshold = 0.05;
            double signalWAMPthreshold = 0.05;
            double reconsWAMPthreshold = 0.05;

            // separate myop threshold...

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

                    // update maximal values (LMS)
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
                double meanAbsVal = 0;
                double sigEnergy = 0;
                double maxSigSqrd = 0;
                double maxRecSqrd = 0;

                int reconsWAMPCounter = 0;
                double reconsMYOPCounter = 0;

                for (int n = 0; n < signal.Length; n++)
                {
                    double signalAbs = Math.Abs(signal[n]);
                    double signalSqrd = signal[n] * signal[n];
                    double reconsSqrd = dwtResults[2][lvl][n] * dwtResults[2][lvl][n];

                    signalRmsCounter = signalRmsCounter + Math.Abs(signalSqrd);
                    reconsRmsCounter = reconsRmsCounter + Math.Abs(reconsSqrd);

                    // MAV and SSI counters (not used currently)
                    meanAbsVal = meanAbsVal + signalAbs;
                    sigEnergy = sigEnergy + signalSqrd;

                    // update maximal values for LMS
                    if (signalSqrd > maxSigSqrd)
                    {
                        maxSigSqrd = signalSqrd;
                        featDWT[0, 4] = (n + 1) / (double)signal.Length;
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
                } // next data point

                // reconstructed signal RMS, WL, WAMP, MYOP
                featDWT[1, 5 + lvl] = Math.Sqrt(reconsRmsCounter / dwtResults[2][lvl].Length);
                featDWT[2, 5 + lvl] = reconsWL;
                featDWT[3, 5 + lvl] = reconsWAMPCounter;
                featDWT[4, 5 + lvl] = reconsMYOPCounter / dwtResults[2][lvl].Length;

            } //  next level

            // raw signal RMS, WL, WAMP, MYOP
            featDWT[1, 4] = Math.Sqrt(signalRmsCounter / signal.Length);
            featDWT[2, 4] = rawSigWL;
            featDWT[3, 4] = signalWAMPCounter;
            featDWT[4, 4] = signalMYOPCounter / signal.Length;

            return featDWT;
        }




        public string Flatten_EMG_Features(double[][,] features)
        {
            string[] channelFeatures = new string[numChannels];
            double[][] allFeatureVectors = new double[8][];
            for (int ch = 0; ch < features.Length; ch++)
            {
                allFeatureVectors[ch] = new double[35];

                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 7; j++)
                    {
                        allFeatureVectors[ch][(i * 7) + j] = features[ch][i, j];
                    }
                }
                channelFeatures[ch] = string.Join(",", allFeatureVectors[ch]);
            }


            return string.Join(",", channelFeatures);
        }


        public double[][][] Do_DWT_on_Signal(double[][] rawD)
        {
            int maxDecompLev = 2;

            double[][][] DWTResults = new double[3][][];

            // prepare arrays for dwt data
            double[][][] allRecData = new double[maxDecompLev][][];
            double[][][] allDetails = new double[maxDecompLev][][];
            double[][][] allApproxs = new double[maxDecompLev][][];

            for (int d = 0; d < maxDecompLev; d++)
            {
                allRecData[d] = new double[numChannels][];
                allDetails[d] = new double[numChannels][];
                allApproxs[d] = new double[numChannels][];

                for (int ch = 0; ch < numChannels; ch++)
                {
                    allDetails[d][ch] = new double[currentDataLength];
                    allApproxs[d][ch] = new double[currentDataLength];
                    allRecData[d][ch] = new double[currentDataLength];
                }
            }

            if (currentWavelet == null)
            {
                Console.WriteLine("Please choose a wavelet to use for DWT");
                return null;
            }


            // perform DWT
            for (int x = 0; x < numChannels; x++)
            {
                double[] signal = rawData[x];
                DWTResults = Task.Run(() => PerformDWT(signal, currentWavelet, maxDecompLev)).Result;

                for (int y = 0; y < maxDecompLev; y++)
                {
                    allDetails[y][x] = DWTResults[0][y];
                    allApproxs[y][x] = DWTResults[1][y];
                    allRecData[y][x] = DWTResults[2][y];
                }
            }

            return DWTResults;
        }

        public async Task<double[][,]> Extract_Features_From_DWT_Results(double[][] rawData, double[][][] DWTResults)
        {
            int nChan = rawData.Length;
            double[][,] allFeatures = new double[nChan][,];

            for (int x = 0; x < nChan; x++)
            {
                double[] signal = rawData[x];
                allFeatures[x] = Task.Run(() => Get_Features(signal, DWTResults)).Result;
            }

            return allFeatures;
        }




        public void PlotEMGData(string session, string directory)
        {
            //Plotter myPlotter = new Plotter();
            //myPlotter.BuildDWTChart(session, directory + "/Images", currentWavelet.Name, maxDecompLev, rawData, allDetails, allApproxs, allRecData, allFeatures, showGraph);
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

            int numberOfUnusedDataPoint = currentDataLength - n;
            //Console.WriteLine(n + " DWT values (-" + numberOfUnusedDataPoint + ")");
        }


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
                reconstrData[r - 1] = DWT.ExecuteIDWT(dwt, wavelet, r, WaveletStudio.Functions.ConvolutionModeEnum.Normal);
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
