using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using CenterSpace.NMath.Charting.Microsoft;
using CenterSpace.NMath.Core;
using Windows.UI.Xaml.Controls;

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
                Text = session + "'s IMU Data",
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


        public void BuildDWTChart(string session, string dir, string waveletUsed, int maxDecompLevel, double[][] signal, double[][][] details, double[][][] approxs, double[][][] ReconstructedData, double[][,] features, bool showGraph = true)
        {
            string suffix = " DWT Using " + waveletUsed + " Wavelet";
            Color[] myPalette = new Color[] { Color.Green, Color.Red, Color.DarkBlue, Color.Peru, Color.Pink, Color.Purple, Color.MediumAquamarine, Color.YellowGreen };

            // add chart
            Chart chart = new Chart() { Size = new Size(1880, 1000), };

            // add chart title
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = session + suffix,
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            #region Signal Charts (raw and reconstructed)

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 7, 50, 35) });
            chart.ChartAreas[0].AxisX.Title = "Original EMG Signal";
            chart.ChartAreas[0].AxisX.TitleAlignment = StringAlignment.Center;
            chart.ChartAreas[0].AxisY.Maximum = 1;
            chart.ChartAreas[0].AxisY.Minimum = -1;

            for (int ch = 0; ch < signal.Length; ch++)
            {
                Series ssig = new Series();
                ssig.ChartType = SeriesChartType.Line;
                ssig.Points.DataBindY(signal[ch]);
                ssig.ChartArea = "0";
                ssig.Color = myPalette[ch];
                chart.Series.Add(ssig);
            }

            for (int l = 0; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, (l * 17) + 7, 48, 18) });
                chart.ChartAreas[1 + l].AxisX.Title = "Reconstruction, from level " + (l + 1);
                chart.ChartAreas[1 + l].AxisX.TitleAlignment = StringAlignment.Center;
                chart.ChartAreas[1 + l].AxisY.Maximum = 1;
                chart.ChartAreas[1 + l].AxisY.Minimum = -1;

                for (int ch = 0; ch < ReconstructedData[l].Length; ch++)
                {
                    Series series = new Series();
                    series.ChartType = SeriesChartType.Line;
                    series.Points.DataBindY(ReconstructedData[l][ch]);
                    series.ChartArea = "r" + l;
                    series.Color = myPalette[ch];
                    chart.Series.Add(series);
                }
            }

            #endregion Signal Charts (raw and reconstructed)

            #region DWT Chart Titles

            Title detailLevel1Title = new Title()
            {
                Name = "L1 details",
                Text = "L1 details",
                Font = new Font("Trebuchet MS", 10F),
                TextOrientation = TextOrientation.Rotated270,
                Position = new ElementPosition(-1, 40, 5, 20),

            };
            Title approxLevel1Title = new Title()
            {
                Name = "L1 approximations",
                Text = "L1 approx.",
                Font = new Font("Trebuchet MS", 10F),
                TextOrientation = TextOrientation.Rotated270,
                Position = new ElementPosition(-1, 52, 5, 20),
            };
            Title detailLevel2Title = new Title()
            {
                Name = "L2 details",
                Text = "L2 details",
                Font = new Font("Trebuchet MS", 10F),
                TextOrientation = TextOrientation.Rotated270,
                Position = new ElementPosition(-1, 65, 5, 20),
            };
            Title approxLevel2Title = new Title()
            {
                Name = "L2 approximations",
                Text = "L2 approx.",
                Font = new Font("Trebuchet MS", 10F),
                TextOrientation = TextOrientation.Rotated270,
                Position = new ElementPosition(-1, 78, 5, 20),
            };
            chart.Titles.Add(detailLevel1Title);
            chart.Titles.Add(approxLevel1Title);
            chart.Titles.Add(detailLevel2Title);
            chart.Titles.Add(approxLevel2Title);

            #endregion DWT Chart Titles

            #region DWT Charts

            for (int ch = 0; ch < details[0].Length; ch++)
            {
                // titles (channel 'ch')
                Title chTitle = new Title()
                {
                    Name = chart.Titles.NextUniqueName(),
                    Text = "Channel " + (ch + 1),
                    Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                    Position = new ElementPosition((ch * 12) + 1, 41, 13, 5),
                };
                chart.Titles.Add(chTitle);

                // detail and approximation charts
                chart.ChartAreas.Add(new ChartArea("dwt-d1" + ch) { Position = new ElementPosition((ch * 12), 45, 13, 13) });
                //double[] cD1sqrd = new double[details[0][ch].Length]; // option to square values 
                //for (int x = 0; x < cD1sqrd.Length; x++)
                //{ cD1sqrd[x] = details[0][ch][x] * details[0][ch][x]; }
                Series sd1 = new Series();
                sd1.ChartType = SeriesChartType.Bar;
                sd1.Points.DataBindY(details[0][ch]);
                sd1.ChartArea = "dwt-d1" + ch;
                sd1.Color = myPalette[ch];
                chart.Series.Add(sd1);

                chart.ChartAreas.Add(new ChartArea("dwt-a1" + ch) { Position = new ElementPosition((ch * 12), 58, 13, 13) });
                Series sa1 = new Series();
                sa1.ChartType = SeriesChartType.Bar;
                sa1.Points.DataBindY(approxs[0][ch]);
                sa1.ChartArea = "dwt-a1" + ch;
                sa1.Color = myPalette[ch];
                chart.Series.Add(sa1);

                chart.ChartAreas.Add(new ChartArea("dwt-d2" + ch) { Position = new ElementPosition((ch * 12), 71, 13, 13) });
                Series sd2 = new Series();
                sd2.ChartType = SeriesChartType.Bar;
                sd2.Points.DataBindY(details[1][ch]);
                sd2.ChartArea = "dwt-d2" + ch;
                sd2.Color = myPalette[ch];
                chart.Series.Add(sd2);

                chart.ChartAreas.Add(new ChartArea("dwt-a2" + ch) { Position = new ElementPosition((ch * 12), 84, 13, 13) });
                Series sa2 = new Series();
                sa2.ChartType = SeriesChartType.Bar;
                sa2.Points.DataBindY(approxs[1][ch]);
                sa2.ChartArea = "dwt-a2" + ch;
                sa2.Color = myPalette[ch];
                chart.Series.Add(sa2);
            }

            #endregion DWT Charts

            // Save and display the chart
            string savePath = dir + "/" + session + suffix + ".png";
            chart.SaveImage(savePath, ChartImageFormat.Png);

            if (showGraph)
            {
                NMathChart.Show(chart);
            }
        }


        #endregion EMG - DWT Plotting


    }
}
