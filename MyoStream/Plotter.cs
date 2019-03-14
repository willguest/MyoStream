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


        public void BuildDWTChart(string session, string dir, string waveletUsed, int maxDecompLevel, double[][] signal, double[][][] details, double[][][] approxs, double[][][] ReconstructedData, double[][,] features, bool showGraph = true)
        {

            Color[] myPalette = new Color[] { Color.Green, Color.Red, Color.DarkBlue, Color.Peru, Color.Pink, Color.Purple, Color.MediumAquamarine, Color.YellowGreen };


            // Create and display charts.
            Chart chart = new Chart() { Size = new Size(1880, 1000), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = session + "- DWT Using " + waveletUsed + " Wavelet",
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 52, 25, 23) });
            chart.ChartAreas[0].AxisX.Title = "Decomposition Details (Sqrd), Lev.1";
            chart.ChartAreas[0].AxisY.Maximum = 1.5;
            chart.ChartAreas[0].AxisY.Minimum = 0;

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(25, 52, 25, 23) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximations (Sqrd), Lev.1";
            chart.ChartAreas[1].AxisY.Maximum = 1.5;
            chart.ChartAreas[1].AxisY.Minimum = 0;

            for (int ch = 0; ch < details[0].Length; ch++)
            {
                double[] newArray0 = new double[details[0][ch].Length];
                for (int x = 0; x < newArray0.Length; x++)
                { newArray0[x] = details[0][ch][x] * details[0][ch][x]; }

                Series sd1 = new Series();
                sd1.ChartType = SeriesChartType.Line;
                sd1.Points.DataBindY(newArray0);
                sd1.ChartArea = "0";
                sd1.Color = myPalette[ch];
                chart.Series.Add(sd1);

                double[] newArray1 = new double[approxs[0][ch].Length];
                for (int x = 0; x < newArray1.Length; x++)
                { newArray1[x] = approxs[0][ch][x] * approxs[0][ch][x]; }

                Series sa1 = new Series();
                sa1.ChartType = SeriesChartType.Line;
                sa1.Points.DataBindY(newArray1);
                sa1.ChartArea = "1";
                sa1.Color = myPalette[ch];
                chart.Series.Add(sa1);
            }

            chart.ChartAreas.Add(new ChartArea("2") { Position = new ElementPosition(0, 75, 25, 23) });
            chart.ChartAreas[2].AxisX.Title = "Decomposition Details (Sqrd), Lev.2";
            chart.ChartAreas[2].AxisY.Maximum = 1.5;
            chart.ChartAreas[2].AxisY.Minimum = 0;

            chart.ChartAreas.Add(new ChartArea("3") { Position = new ElementPosition(25, 75, 25, 23) });
            chart.ChartAreas[3].AxisX.Title = "DWT Approximations (Sqrd), Level 2";
            chart.ChartAreas[3].AxisY.Maximum = 1.5;
            chart.ChartAreas[3].AxisY.Minimum = 0;

            for (int ch = 0; ch < details[1].Length; ch++)
            {
                double[] newArray1 = new double[details[1][ch].Length];
                for (int x = 0; x < newArray1.Length; x++)
                { newArray1[x] = details[1][ch][x] * details[1][ch][x]; }

                Series sd2 = new Series();
                sd2.ChartType = SeriesChartType.Line;
                sd2.Points.DataBindY(newArray1);
                sd2.ChartArea = "2";
                sd2.Color = myPalette[ch];
                chart.Series.Add(sd2);

                double[] newArray2 = new double[approxs[1][ch].Length];
                for (int x = 0; x < newArray2.Length; x++)
                { newArray2[x] = approxs[1][ch][x] * approxs[1][ch][x]; }

                Series sa2 = new Series();
                sa2.ChartType = SeriesChartType.Line;
                sa2.Points.DataBindY(newArray2);
                sa2.ChartArea = "3";
                sa2.Color = myPalette[ch];
                chart.Series.Add(sa2);
            }


            
            chart.ChartAreas.Add(new ChartArea("4") { Position = new ElementPosition(0, 7, 50, 45) });
            //chart.ChartAreas[4].Area3DStyle.Enable3D = true;

            chart.ChartAreas[4].AxisX.Title = "Original EMG Signal";
            chart.ChartAreas[4].AxisY.Maximum = 1;
            chart.ChartAreas[4].AxisY.Minimum = -1;

            for (int ch = 0; ch < signal.Length; ch++)
            {
                Series ssig = new Series();
                ssig.ChartType = SeriesChartType.Line;
                ssig.Points.DataBindY(signal[ch]);
                ssig.ChartArea = "4";
                ssig.Color = myPalette[ch];
                chart.Series.Add(ssig);
            }


            

            for (int l = 0; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, (l * 25) + 2, 50, 25) });
                chart.ChartAreas[5 + l].AxisX.Title = "Reconstruction, from level " + (l + 1);
                chart.ChartAreas[5 + l].AxisY.Maximum = 1;
                chart.ChartAreas[5 + l].AxisY.Minimum = -1;

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

            // show table of features
            chart.ChartAreas.Add(new ChartArea("g") { Position = new ElementPosition(50, 55, 50, 45) });
            chart.ChartAreas[7].AxisX.Title = "grid of features";

            System.Windows.Forms.DataGrid featureGrid = new System.Windows.Forms.DataGrid
            {
                Location = new Point(700, 500),
                Size = new Size(300, 200),
                CaptionText = "grid of features",
                DataSource = features[0]
            };


            System.Data.DataTable feat = new System.Data.DataTable("features");
            feat.Columns.Add("LMS");
            feat.Columns.Add("RMS");
            feat.Columns.Add("WL");
            feat.Columns.Add("WAMP");
            feat.Columns.Add("MYOP");


            for (int row = 0; row < feat.Columns.Count; row++)
            {
                System.Data.DataRow dr = feat.NewRow();
                dr["LMS"] = features[0][row, 0];
                dr["RMS"] = features[0][row, 1];
                dr["WL"] = features[0][row, 2];
                dr["WAMP"] = features[0][row, 3];
                dr["MYOP"] = features[0][row, 4];

                feat.Rows.Add(dr);
            }

            //featureGrid.Show();



            DataGridTableStyle FeatTS = new DataGridTableStyle { MappingName = "feat" };

            // Add a second column style.
            DataGridColumnStyle TextCol = new DataGridTextBoxColumn();
            TextCol.MappingName = "LMS";
            TextCol.HeaderText = "Localised Max. Sq.";
            TextCol.Width = 250;
            FeatTS.GridColumnStyles.Add(TextCol);


            // Save and display the chart
            string savePath = dir + "/" + session + "_ EMG DWT using " + waveletUsed + " wavelet.png";
            chart.SaveImage(savePath, ChartImageFormat.Png);

            if (showGraph)
            {
                NMathChart.Show(chart);
                
            }
        }


        #endregion EMG - DWT Plotting


    }
}
