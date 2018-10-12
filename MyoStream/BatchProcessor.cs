
using CenterSpace.NMath.Core;
using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyoStream
{
    public class BatchProcessor
    {
        private string currentDirectory = "";
        private string currentFilename = "";
        private int currentDataLength = 0;

        public double[][] rawData = new double[9][];
        private double[][] midData = new double[9][];
        private double[][] clnData = new double[9][];

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        private StreamWriter sWriter;
        public List<string> WaveletNames = new List<string>();
        public List<Wavelet.Wavelets> ListOfWavelets = new List<Wavelet.Wavelets>();
        public int SelectedWaveletIndex { get; set; }
        private Wavelet.Wavelets currentWavelet;

        public BatchProcessor()
        {
            
        }

        public void IdentifyWavelets()
        {
            ListOfWavelets = Enum.GetValues(typeof(Wavelet.Wavelets)).Cast<Wavelet.Wavelets>().ToList();

            foreach (Wavelet.Wavelets w in ListOfWavelets)
            {
                WaveletNames.Add(w.ToString());
            }
        }

        public void SelectWavelet(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string waveName = (sender as System.Windows.Controls.ComboBox).SelectedItem as string;
            SelectedWaveletIndex = WaveletNames.IndexOf(waveName);
            currentWavelet = ListOfWavelets[SelectedWaveletIndex];

            
        }



        public int LoadFile(string directory, string filename)
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

                StructureData(csv.Count() - 1, csv.ToArray());

                currentDataLength = rawData[0].Count() - 1;

                long duration = DateTime.UtcNow.Ticks - startTime;
                Console.WriteLine(currentDataLength + " data points loaded and arranged in " + (float)duration / 10000f + " milliseconds");

                return currentDataLength;
            }
            catch (IOException e)
            {
                Console.WriteLine("File is open.  Please close it and try again");
                return 0;
            }

        }


        public void StructureData(int dataLength, string[][] inputArray)
        {
            string[] titles = inputArray[0];
            for (int i = 0; i < 9; i++)
            {
                rawData[i] = new double[dataLength];     
            }


            for (int x = 1; x < dataLength; x++)
            {
                double[] _dubs = Array.ConvertAll(inputArray[x], double.Parse);
                for (int y=0; y < 9; y++)
                {
                    rawData[y][x - 1] = _dubs[y];
                }
            }
        }


        public void CleanData()
        {
            Prep_Datastream();
            Console.WriteLine("Performing DWT using " + currentWavelet.ToString() + " wavelet");

            Task cleanIt = Task.Run(async () => await MainAsyncThread());
            cleanIt.Wait();
            Task storeIt = Task.Run(async () => await StoreData(currentDataLength, midData, clnData).ConfigureAwait(true));

        }

        public void PlotData()
        {
            var myPlotter = new Plotter();
            Task showIt = Task.Run(() => myPlotter.InitialisePlot(midData[3], currentWavelet));

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
                while (n * 2 <= currentDataLength)
                { n *= 2; }
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
                midData[z] = new double[noPoints];
                clnData[z] = new double[noPoints];

                Array.Copy(rawData[z], 0, midData[z], 0, noPoints);
            }
        }



        private async Task MainAsyncThread()
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];

            for (int z = 1; z < 9; z++)
            {
                _tasks[z - 1] = WorkerThread(midData[z]);
            }

            await Task.WhenAll(_tasks);

            for (int w = 1; w < 9; w++)
            {
                clnData[w] = _tasks[w - 1].Result;
            }

            //await StoreData(noPoints, midData, clnData).ConfigureAwait(true);

            //sWriter.Close();
            //sWriter.Dispose();
            //sWriter = null;

        }








        private async Task<double[]> WorkerThread(double[] workerData)
        {
            return DiscreetWaveletTransform(workerData, currentWavelet);
            
            //return DWT_Test(workerData, currentWavelet);
        }


        private double[] DWT_Test(double[] _input, Wavelet.Wavelets _wavelet) // currently does not very much (removing zeros...)
        {
            DoubleVector data = new DoubleVector(_input);
            DoubleWavelet wavelet = new DoubleWavelet(_wavelet);
            DoubleDWT dwt = new DoubleDWT(wavelet);

            // Decompose signal with DWT
            double[] approx;
            double[] details;
            dwt.DWT(data.DataBlock.Data, out approx, out details);

            dwt.Signal = _input;
            int maxPossDecomp = dwt.MaximumDecompLevel();
            Console.WriteLine("max. decomposition level of " + WaveletNames[SelectedWaveletIndex] + " wavelet is " + maxPossDecomp);

            // Rebuild the signal
            double[] signal = dwt.IDWT(approx, details);

            return signal;
        }




        private double[] DiscreetWaveletTransform(double[] _input, Wavelet.Wavelets motherWavelet)
        {
            DoubleVector data = new DoubleVector(_input);
            DoubleWavelet wavelet = new DoubleWavelet(motherWavelet);
            DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            int maxPossDecomp = dwt.MaximumDecompLevel();
            Console.WriteLine("Decomposition possible to level " + maxPossDecomp + " using this wavelet (" + motherWavelet.ToString() + ")...");

            // Decompose signal
            dwt.Decompose(5);

            // Find Universal threshold
            double lambdaU = dwt.ComputeThreshold(DoubleDWT.ThresholdMethod.Universal, 1);

            // Threshold all detail levels with lambdaU
            dwt.ThresholdAllLevels(DoubleDWT.ThresholdPolicy.Soft,
                new double[] { lambdaU, lambdaU, lambdaU, lambdaU, lambdaU });

            // Rebuild signal to level 2
            double[] reconstructedData2 = dwt.Reconstruct(2);

            // Rebuild the signal to level 1 - the original (filtered) signal.
            double[] rebuiltSignal = dwt.Reconstruct();

            //Console.WriteLine("finished rebuilding signal");
            return rebuiltSignal;
        }


        private async Task StoreData(int noRecords, double[][] rawEMG, double[][] resEMG)
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









    }
}
