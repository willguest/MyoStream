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
            chart.Palette = new ChartColorPalette();
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


        public void BuildDWTChart(string session, string dir, string waveletUsed, int maxDecompLevel, double[][] signal, double[][][] details, double[][][] approxs, double[][][] ReconstructedData)
        {

            Color[] myPalette = new Color[] { Color.Green, Color.Red, Color.DarkBlue, Color.Peru, Color.Pink, Color.Purple, Color.MediumAquamarine, Color.YellowGreen };


            // Create and display charts.
            Chart chart = new Chart() { Size = new System.Drawing.Size(1880, 1000), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = session + "- DWT Using " + waveletUsed + " Wavelet",
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 52, 25, 23) });
            chart.ChartAreas[0].AxisX.Title = "Decomposition Details for for level 1";
            chart.ChartAreas[0].AxisY.Maximum = 0.5;
            chart.ChartAreas[0].AxisY.Minimum = -0.5;

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(25, 52, 25, 23) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximations for level 1";
            chart.ChartAreas[1].AxisY.Maximum = 1.5;
            chart.ChartAreas[1].AxisY.Minimum = 0;

            for (int ch = 0; ch < details[0].Length; ch++)
            {
                Series sd1 = new Series();
                sd1.ChartType = SeriesChartType.Column;
                sd1.Points.DataBindY(details[0][ch]);
                sd1.ChartArea = "0";
                sd1.Color = myPalette[ch];
                chart.Series.Add(sd1);

                Series sa1 = new Series();
                sa1.ChartType = SeriesChartType.Line;
                sa1.Points.DataBindY(approxs[0][ch]);
                sa1.ChartArea = "1";
                sa1.Color = myPalette[ch];
                chart.Series.Add(sa1);
            }

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(0, 75, 25, 23) });
            chart.ChartAreas[2].AxisX.Title = "Decomposition Details for for level 2";
            chart.ChartAreas[2].AxisY.Maximum = 0.5;
            chart.ChartAreas[2].AxisY.Minimum = -0.5;

            chart.ChartAreas.Add(new ChartArea("3") { Position = new ElementPosition(25, 75, 25, 23) });
            chart.ChartAreas[3].AxisX.Title = "DWT Approximations for level 2";
            chart.ChartAreas[3].AxisY.Maximum = 1.5;
            chart.ChartAreas[3].AxisY.Minimum = 0;

            for (int ch = 0; ch < details[1].Length; ch++)
            {
                Series sd2 = new Series();
                sd2.ChartType = SeriesChartType.Column;
                sd2.Points.DataBindY(details[1][ch]);
                sd2.ChartArea = "2";
                sd2.Color = myPalette[ch];
                chart.Series.Add(sd2);

                Series sa2 = new Series();
                sa2.ChartType = SeriesChartType.Line;
                sa2.Points.DataBindY(approxs[1][ch]);
                sa2.ChartArea = "3";
                sa2.Color = myPalette[ch];
                chart.Series.Add(sa2);
            }



            chart.ChartAreas.Add(new ChartArea("4") { Position = new ElementPosition(0, 7, 50, 45) });
            chart.ChartAreas[4].AxisX.Title = "Original EMG Signal";
            chart.ChartAreas[4].AxisY.Maximum = 1;
            chart.ChartAreas[4].AxisY.Minimum = 0;

            for (int ch = 0; ch < signal.Length; ch++)
            {
                Series ssig = new Series();
                ssig.ChartType = SeriesChartType.FastLine;
                ssig.Points.DataBindY(signal[ch]);
                ssig.ChartArea = "4";
                ssig.Color = myPalette[ch];
                chart.Series.Add(ssig);
            }
            
            float offset = 1.0f;
            float rP = (100 / (maxDecompLevel - 1));

            for (int l = 1; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, ((l - 1) * rP) + offset, 50, rP - offset) });
                chart.ChartAreas[4 + l].AxisX.Title = "Reconstructed signal, from level " + (l + 0);
                chart.ChartAreas[4 + l].AxisY.Maximum = 1;
                chart.ChartAreas[4 + l].AxisY.Minimum = 0;

                for (int ch = 0; ch < ReconstructedData[l].Length; ch++)
                {
                    Series series = new Series();
                    series.ChartType = SeriesChartType.FastLine;
                    series.Points.DataBindY(ReconstructedData[l][ch]); 
                    series.ChartArea = "r" + l;
                    series.Color = myPalette[ch];
                    chart.Series.Add(series);
                }
            }

            // Save and display the chart
            string savePath = dir + "/" + session + "_ EMG DWT using " + waveletUsed + " wavelet.png";
            chart.SaveImage(savePath, ChartImageFormat.Png);
            NMathChart.Show(chart);  
        }


        #endregion EMG - DWT Plotting


    }
}
