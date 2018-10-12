using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using CenterSpace.NMath.Charting.Microsoft;
using CenterSpace.NMath.Core;

namespace MyoStream
{
    public class Plotter
    {

        public void InitialisePlot(double[] inputData, Wavelet.Wavelets inputWavelet)
        {

            DoubleVector data = new DoubleVector(inputData);
            DoubleWavelet wavelet = new DoubleWavelet(inputWavelet);
            DoubleDWT dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            int maxPossDecomp = dwt.MaximumDecompLevel();
            Console.WriteLine("Decomposition possible to level " + maxPossDecomp + " using this wavelet (" + inputWavelet.ToString() + ")");

            double[][] allReconData = new double[maxPossDecomp][];

            for (int r = 1; r <= maxPossDecomp; r++)
            {
                allReconData[r-1] = new double[inputData.Length];

                // Decompose signal with DWT
                dwt.Decompose(r);

                // Find Universal threshold & threshold all detail levels with lambdaU (rigrSure = soft+sure);
                double lambdaU1 = dwt.ComputeThreshold(FloatDWT.ThresholdMethod.Sure, r);
                double lambdaU2 = dwt.ComputeThreshold(FloatDWT.ThresholdMethod.Universal, r);
                double lambdaU = lambdaU1 * lambdaU2;

                dwt.ThresholdAllLevels(FloatDWT.ThresholdPolicy.Soft, new double[] { lambdaU, lambdaU, lambdaU, lambdaU, lambdaU, lambdaU, lambdaU, lambdaU, lambdaU, lambdaU });
                Console.WriteLine("Level " + r + ": lambdaU is " + lambdaU);

                // Rebuild the signal to level 1 - the original (filtered) signal.
                allReconData[r - 1] = dwt.Reconstruct();
            }

            // Display chart
            BuildChart(inputWavelet.ToString(), maxPossDecomp, dwt, data, allReconData);
        }



        public Task BuildChart(string waveletUsed, int maxDecompLevel, DoubleDWT dwt, DoubleVector signal, double[][] ReconstructedData)
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
                var detail = new DoubleVector(dwt.WaveletCoefficients(DiscreteWaveletTransform.WaveletCoefficientType.Details, n));
                detailsAllLevels.Append(new DoubleVector(detail));
            }

            // Create and display charts.
            Chart chart = new Chart() { Size = new System.Drawing.Size(1880, 1000), };
            Title title = new Title()
            {
                Name = chart.Titles.NextUniqueName(),
                Text = "DWT Using " + waveletUsed + " Wavelet, over " + maxDecompLevel + " levels",
                Font = new Font("Trebuchet MS", 12F, FontStyle.Bold),
                Position = new ElementPosition(15, 1.5f, 20, 5),
            };
            chart.Titles.Add(title);

            chart.ChartAreas.Add(new ChartArea("0") { Position = new ElementPosition(0, 7, 25, 45) });
            chart.ChartAreas[0].AxisX.Title = "Decomposition Details for all (" + maxDecompLevel + ") levels";
            Series series0 = new Series();
            series0.Points.DataBindY(detailsAllLevels);
            series0.ChartArea = "0";
            chart.Series.Add(series0);

            chart.ChartAreas.Add(new ChartArea("1") { Position = new ElementPosition(25, 7, 25, 45) });
            chart.ChartAreas[1].AxisX.Title = "DWT Approximations for all (" + maxDecompLevel + ") levels";
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

            float offset = 1.0f;
            float rP = (100 / maxDecompLevel);

            for (int l = 0; l < maxDecompLevel; l++)
            {
                chart.ChartAreas.Add(new ChartArea("r" + l) { Position = new ElementPosition(50, (l * rP) - offset, 50, rP + offset)});
                chart.ChartAreas[3+l].AxisX.Title = "Reconstructed signal, from level " + (l + 1);
                Series series = new Series();
                series.Points.DataBindY(new DoubleVector(ReconstructedData[l]));   // needs new data...
                series.ChartArea = "r" + l;
                chart.Series.Add(series);
            }

            NMathChart.Show(chart);
            return null;
        }

    }
}
