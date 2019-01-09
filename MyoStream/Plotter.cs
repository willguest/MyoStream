using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using CenterSpace.NMath.Charting.Microsoft;

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
                series.ChartType = SeriesChartType.StepLine;
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
                series.ChartType = SeriesChartType.Spline;
                series.Name = "a" + x;
                series.Points.DataBindY(IMUData[x]);
                series.ChartArea = "1";
                chart.Series.Add(series);
            }

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(5, 64, 90, 30) });
            chart.ChartAreas[2].AxisX.Title = "Gyroscopic Rotation";

            for (int x = 7; x < 10; x++)
            {
                Series series = new Series();
                series.ChartType = SeriesChartType.FastLine;
                series.Name = "g" + x;
                series.Points.DataBindY(IMUData[x]);
                series.ChartArea = "2";
                chart.Series.Add(series);
            }

            // save and display
            string savePath = dir + "/" + session + " - IMU Data.png";
            chart.SaveImage(savePath, ChartImageFormat.Png);
            NMathChart.Show(chart);
        }


        #endregion IMU Data Plotting


        #region EMG - DWT Plotting
        
        public async Task<double[][]> PerformDWT_NMath(double[] inputData, int maxDecompLevel)
        {
            //var data = new DoubleVector(inputData);
            //var wavelet = new DoubleWavelet(inputWavelet);
            //DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);
            //dwt = DWT.ExecuteDWT(data, wavelet, maxL);

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

                // Rebuild the signal to level 1
                allReconData[r - 1] = dwt.Reconstruct();
            }
            */


            // Display chart
            //BuildDWTChart(session, dir, channelNo, inputWavelet.Name, maxL, inputData, reconstrData);
            return null;

        }


        public void BuildDWTChart(string session, string dir, string waveletUsed, int maxDecompLevel, double[][] signal, double[][] details, double[][] approxs, double[][][] AllDWTData)
        {
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

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(25, 52, 25, 45) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximations for all (" + maxDecompLevel + ") levels";

            for (int ch = 0; ch < details.Length; ch++)
            {
                Series series0 = new Series();
                series0.ChartType = SeriesChartType.Spline;
                series0.Points.DataBindY(details[ch]);
                series0.ChartArea = "0";
                chart.Series.Add(series0);

                Series series1 = new Series();
                series1.ChartType = SeriesChartType.FastLine;
                series1.Points.DataBindY(approxs[ch]);
                series1.ChartArea = "1";
                chart.Series.Add(series1);
            }

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(0, 7, 50, 45) });
            chart.ChartAreas[2].AxisX.Title = "Original EMG Signal";

            for (int ch = 0; ch < signal.Length; ch++)
            {
                Series series2 = new Series();
                series2.ChartType = SeriesChartType.FastLine;
                series2.Points.DataBindY(signal[ch]);
                series2.ChartArea = "2";
                chart.Series.Add(series2);
            }
            
            float offset = 1.0f;
            float rP = (100 / (maxDecompLevel - 1));

            for (int l = 1; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, ((l - 1) * rP) + offset, 50, rP - offset) });
                chart.ChartAreas[2 + l].AxisX.Title = "Reconstructed signal, from level " + (l + 1);

                for (int ch = 0; ch < AllDWTData[l].Length; ch++)
                {
                    Series series = new Series();
                    series.ChartType = SeriesChartType.FastLine;
                    series.Points.DataBindY(AllDWTData[l][ch]); 
                    series.ChartArea = "r" + l;
                    chart.Series.Add(series);
                }
            }

            // Save and display the chart
            string savePath = dir + "/" + session + "-EMG DWT using " + waveletUsed + " wavelet.png";
            chart.SaveImage(savePath, ChartImageFormat.Png);
            NMathChart.Show(chart);  
        }


        #endregion EMG - DWT Plotting


    }
}
