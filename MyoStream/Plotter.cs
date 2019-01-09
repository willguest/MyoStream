using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using CenterSpace.NMath.Charting.Microsoft;
using WaveletStudio;
using WaveletStudio.Wavelet;

namespace MyoStream
{
    public class Plotter
    {

            #region IMU Data Plotting

        public void BuildIMUChart(string session, string dir, double[][] IMUData)
        {
            // Create and display charts.
            Chart chart = new Chart() { Size = new Size(1880, 1000), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = session + "'s (R) IMU Data",
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(5, 5, 90, 30) });
            chart.ChartAreas[0].AxisX.Title = "Orientation";

            for (int x = 0; x < 4; x++)
            {
                Series series = new Series();
                series.Name = "o" + x;
                series.Points.DataBindY(IMUData[x]);
                series.ChartArea = "0";
                chart.Series.Add(series);
            }

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(5, 34, 90, 30) });
            chart.ChartAreas[1].AxisX.Title = "Acceleration";

            for (int x = 4; x < 7; x++)
            {
                Series series = new Series();
                series.Name = "a" + x;
                series.Points.DataBindY(IMUData[x]);
                series.ChartArea = "1";
                chart.Series.Add(series);
            }

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(5, 64, 90, 30) });
            chart.ChartAreas[2].AxisX.Title = "Gyroscope";

            for (int x = 7; x < 10; x++)
            {
                Series series = new Series();
                series.Name = "g" + x;
                series.Points.DataBindY(IMUData[x]);
                series.ChartArea = "2";
                chart.Series.Add(series);
            }

            // save and display
            string savePath = dir + "/" + session + "- IMU Data.png";
            chart.SaveImage(savePath, ChartImageFormat.Png);
            NMathChart.Show(chart);
        }



        #endregion IMU Data Plotting


        #region DWT Plotting

        List<DecompositionLevel> dwt = new List<DecompositionLevel>();

        public void InitialisePlot(double[] inputData, MotherWavelet inputWavelet, int channelNo)
        {
            //var data = new DoubleVector(inputData);
            //var wavelet = new DoubleWavelet(inputWavelet);
            //DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            Signal signal = new Signal(inputData);
            MotherWavelet wavelet = inputWavelet;
            int maxL = 5; // levels to decompose

            //dwt = DWT.ExecuteDWT(data, wavelet, maxL);

            double[][] reconstrData = new double [maxL][];

            for (int r = 0; r < maxL; r++)
            {
                dwt = DWT.ExecuteDWT(signal, wavelet, r, SignalExtension.ExtensionMode.SymmetricWholePoint, WaveletStudio.Functions.ConvolutionModeEnum.Normal);

                reconstrData[r] = new double[signal.SamplesCount];
                reconstrData[r] = DWT.ExecuteIDWT(dwt, wavelet, r, WaveletStudio.Functions.ConvolutionModeEnum.Normal);
            }


            /*
            int maxPossDecomp = dwt.MaximumDecompLevel();
            Console.WriteLine("Decomposition possible to level " + maxPossDecomp + " using this wavelet (" + inputWavelet.ToString() + ")");

            double[][] allReconData = new double[maxPossDecomp][];

            for (int r = 1; r <= maxPossDecomp; r++)
            {
                allReconData[r-1] = new double[inputData.Length];

                // Decompose signal with DWT
                dwt.Decompose(r);

                // Threshold all detail levels .... on-going work, through combining threshold logic seems to work well ("rigrSure" = sure+sure);
                double lambdaSure = dwt.ComputeThreshold(FloatDWT.ThresholdMethod.Sure, r);
                double lambdaUniv = dwt.ComputeThreshold(FloatDWT.ThresholdMethod.Universal, r);
                double lambdaComb = lambdaSure * lambdaUniv;
                double[] lambdaArray = Enumerable.Repeat(lambdaComb, r).ToArray();

                dwt.ThresholdAllLevels(FloatDWT.ThresholdPolicy.Soft, lambdaArray);
                Console.WriteLine("Level " + r + ": lambdas balanced are: " + lambdaSure + ", " + lambdaUniv + " -> " +  lambdaComb);

                // Rebuild the signal to level 1 - the original (filtered) signal.
                allReconData[r - 1] = dwt.Reconstruct();
            }
            */


            // Display chart
            BuildDWTChart(channelNo, inputWavelet.Name, maxL, inputData, reconstrData);


        }

        public Task BuildDWTChart(int channelNo, string waveletUsed, int maxDecompLevel, double[] signal, double[][] ReconstructedData)
        {
            double[] approxAllLevels = new double[0];
            double[] detailsAllLevels = new double[0];

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

                //approxAllLevels.Add(d.Approximation.ToArray());
                //detailsAllLevels.Add(d.Details);
                
            }

            /*
            // Plot out approximations at various levels of decomposition.
            var approxAllLevels = new DoubleVector();
            for (int n = maxDecompLevel; n > 0; n--)
            {
                var approx = new DoubleVector(dwt.WaveletCoefficients(DiscreteWaveletTransform.WaveletCoefficientType.Approximation, n));
                approxAllLevels.Append(new DoubleVector(approx));
            }

            var detailsAllLevels = new DoubleVector();
            for (int n = 5; n > 0; n--)
            {
                var detail = new DoubleVector(dwt.WaveletCoefficients(DiscreteWaveletTransform.WaveletCoefficientType.Details, n));
                detailsAllLevels.Append(new DoubleVector(detail));
            }

            */


            // Create and display charts.
            Chart chart = new Chart() { Size = new System.Drawing.Size(1880, 1000), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = "DWT Using " + waveletUsed + " Wavelet",
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 52, 25, 45) });
            chart.ChartAreas[0].AxisX.Title = "Decomposition Details for all (" + maxDecompLevel + ") levels";
            Series series0 = new Series();
            series0.Points.DataBindY(detailsAllLevels);
            series0.ChartArea = "0";
            chart.Series.Add(series0);

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(25, 52, 25, 45) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximations for all (" + maxDecompLevel + ") levels";
            Series series1 = new Series();
            series1.Points.DataBindY(approxAllLevels);
            series1.ChartArea = "1";
            chart.Series.Add(series1);

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(0, 7, 50, 45) });
            chart.ChartAreas[2].AxisX.Title = "Original EMG Signal";
            Series series2 = new Series();
            series2.Points.DataBindY(signal);
            series2.ChartArea = "2";
            chart.Series.Add(series2);

            float offset = 1.0f;
            float rP = (100 / (maxDecompLevel - 1));

            for (int l = 1; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, ((l - 1) * rP) + offset, 50, rP - offset) });
                chart.ChartAreas[2 + l].AxisX.Title = "Reconstructed signal, from level " + l;
                Series series = new Series();
                series.Points.DataBindY(ReconstructedData[l]);   // needs new data...
                series.ChartArea = "r" + l;
                chart.Series.Add(series);
            }

            // Save and/or display the chart
            //chart.SaveImage("C:/Users/16102434/Source/Repos/MyoStream/Images2/DWT_" + waveletUsed + "_to_Level_" + maxDecompLevel + "_Channel " + channelNo + ".png", ChartImageFormat.Png);
            NMathChart.Show(chart);
            
            return null;
        }


        #endregion DWT Plotting


    }
}
