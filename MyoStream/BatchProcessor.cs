using CenterSpace.NMath.Charting.Microsoft;
using CenterSpace.NMath.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;

namespace MyoStream
{
    public class BatchProcessor
    {
        private string currentDirectory = "";
        private string currentFilename = "";
        private int currentDataLength = 0;
        private int noPoints = 128;

        private double[][] rawData = new double[9][];
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
            //WaveletNames = Enum.GetValues(typeof(Wavelet.Wavelets)).Cast<string>().ToList();
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
            currentFilename = filename;
            string filePath = directory + "/" + filename;
            
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

                return csv.Count() - 2;
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
            Console.WriteLine("Performing DWT using " + WaveletNames[SelectedWaveletIndex] + " wavelet");

            try
            {
                Task cleanTask = Task.Run(async () => await MainAsyncThread());
                cleanTask.Wait();
            }
            catch
            {

                Console.WriteLine("Error doing DWT using " + WaveletNames[SelectedWaveletIndex] + " wavelet");
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


        private async Task MainAsyncThread()
        {
            Task<double[]>[] _tasks = new Task<double[]>[8];

            int numberOfPackets = (int)(currentDataLength / noPoints);

            for (int packetNo = 0; packetNo < numberOfPackets; packetNo++)
            {
                int startIndex = packetNo * noPoints;
                midData[0] = new double[noPoints];
                clnData[0] = new double[noPoints];
                Array.Copy(rawData[0], startIndex, midData[0], 0, noPoints);

                for (int z = 1; z < 9; z++)
                {
                    midData[z] = new double[noPoints];
                    clnData[z] = new double[noPoints];

                    Array.Copy(rawData[z], startIndex, midData[z], 0, noPoints);

                    _tasks[z - 1] = WorkerThread(midData[z]);

                }

                await Task.WhenAll(_tasks);

                long now = DateTime.UtcNow.Ticks;
                clnData[0][0] = now;

                for (int w = 1; w < 9; w++)
                {
                    clnData[w] = _tasks[w - 1].Result;
                }

                await StoreData(midData, clnData).ConfigureAwait(true);

            }
            sWriter.Close();
            sWriter.Dispose();
            sWriter = null;

        }


        private async Task<double[]> WorkerThread(double[] workerData)
        {
            return await DiscreetWaveletTransform(workerData).ConfigureAwait(false);
            
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




        private async Task<double[]> DiscreetWaveletTransform(double[] _input)
        {
            DoubleVector data = new DoubleVector(_input);
            DoubleWavelet wavelet = new DoubleWavelet(Wavelet.Wavelets.D2);
            DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

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


        private async Task StoreData(double[][] rawEMG, double[][] resEMG)
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


        public void createSignal(int signalLength)
        {
            for (int z = 1; z < 9; z++)
            {
                midData[z] = new double[signalLength];
                clnData[z] = new double[signalLength];

                Array.Copy(rawData[z], 0, midData[z], 0, signalLength);
            }

            DoubleVector data = new DoubleVector(midData[1]);
            DoubleWavelet wavelet = new DoubleWavelet(Wavelet.Wavelets.D2);
            DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            // Decompose signal with DWT to level 5
            dwt.Decompose(5);

            // Find Universal threshold & threshold all detail levels with lambdaU
            double lambdaU = dwt.ComputeThreshold(FloatDWT.ThresholdMethod.Universal, 1);
            dwt.ThresholdAllLevels(FloatDWT.ThresholdPolicy.Soft, new double[] { lambdaU, lambdaU, lambdaU, lambdaU, lambdaU });

            // Rebuild the signal to level 1 - the original (filtered) signal.
            double[] reconstructedData = dwt.Reconstruct();

            BuildCharts(dwt, data, reconstructedData);
        }

        public void BuildCharts(DoubleDWT dwt, DoubleVector signal, double[] ReconstructedData)
        {

            // Plot out approximations at various levels of decomposition.
            var approxAllLevels = new DoubleVector();
            for (int n = 5; n > 0; n--)
            {
                var approx = new DoubleVector(dwt.WaveletCoefficients(DiscreteWaveletTransform.WaveletCoefficientType.Approximation, n));
                approxAllLevels.Append(new DoubleVector(approx));
            }

            var detailsAllLevels = new DoubleVector();
            for (int n = 5; n > 0; n--)
            {
                var approx = new DoubleVector(dwt.WaveletCoefficients(DiscreteWaveletTransform.WaveletCoefficientType.Details, n));
                detailsAllLevels.Append(new DoubleVector(approx));
            }

            // Create and display charts.
            Chart chart = new Chart() { Size = new System.Drawing.Size(1200, 700), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = "Debauchies-2 DWT",
                Font = new System.Drawing.Font("Trebuchet MS", 12F, FontStyle.Bold),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 7, 50, 45) });
            chart.ChartAreas[0].AxisX.Title = "5-Level Decomposition Details";
            Series series0 = new Series();
            series0.Points.DataBindY(detailsAllLevels);
            series0.ChartArea = "0";
            chart.Series.Add(series0);

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(50, 7, 50, 45) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximation";
            Series series1 = new Series();
            series1.Points.DataBindY(approxAllLevels);
            series1.ChartArea = "1";
            chart.Series.Add(series1);

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(0, 55, 50, 45) });
            chart.ChartAreas[2].AxisX.Title = "EMG Signal";
            Series series2 = new Series();
            series2.Points.DataBindY(new DoubleVector(signal));
            series2.ChartArea = "2";
            chart.Series.Add(series2);
            
            chart.ChartAreas.Add(new ChartArea("3") { Position = new ElementPosition(50, 55, 50, 45) });
            chart.ChartAreas[3].AxisX.Title = "Reconstructed signal";
            Series series3 = new Series();
            series3.Points.DataBindY(new DoubleVector(ReconstructedData));
            series3.ChartArea = "3";
            chart.Series.Add(series3);

            NMathChart.Show(chart);
        }


    }
}
