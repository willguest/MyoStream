using System;
using System.Globalization;
using System.Threading;
using System.Text;

using CenterSpace.NMath.Core;
using System.Collections.Generic;

namespace MyoStream
{

    public class DWTCalc
    {

        public DWTCalc()
        {
            NMathConfiguration.Init();
            Console.WriteLine("NMathConfiguration initiated successfully");
        }


        public double[] CalculateDWT(double[] arrsize26)
        {
            
            #region DWT of a signal using the Harr wavelet.
            /*
            // Do a simple DWT on a signal
            var data = new DoubleVector(12, new RandGenNormal(0.0, 2.0));

            // Choose wavelet
            var wavelet = new DoubleWavelet(Wavelet.Wavelets.Harr);

            // Build DWT object
            var dwt = new DoubleDWT(wavelet);

            // Decompose signal with DWT
            double[] approx;
            double[] details;
            dwt.DWT(data.DataBlock.Data, out approx, out details);

            // Rebuild the signal
            double[] signal = dwt.IDWT(approx, details);

            // Print results
            Console.WriteLine();
            Console.WriteLine("DWT signal decomposition and reconstruction example using the Harr wavelet.");
            Console.WriteLine(String.Format("Original Signal: {0}", data.ToString("#.##")));
            Console.WriteLine(String.Format("DWT Approximation: {0}", new DoubleVector(approx).ToString("#.##")));
            Console.WriteLine(String.Format("DWT Details: {0}", new DoubleVector(details).ToString("#.##")));
            Console.WriteLine(String.Format("IDWT Reconstructed signal: {0}", new DoubleVector(signal).ToString("#.##")));
            */
            #endregion

            #region DWT using a Daubeachies wavelet, then thresholding, and finally reconstucting the signal.

            var data = new DoubleVector(arrsize26);

            // Choose wavelet
            var wavelet = new DoubleWavelet(Wavelet.Wavelets.D2);

            // Build DWT object
            var dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            // Decompose signal with DWT
            dwt.Decompose(3);

            // Find Universal threshold
            double lambdaU = dwt.ComputeThreshold(DoubleDWT.ThresholdMethod.Universal, 1);

            // Threshold all detail levels with lambdaU
            dwt.ThresholdAllLevels(DiscreteWaveletTransform.ThresholdPolicy.Soft, new double[] { lambdaU, lambdaU, lambdaU, lambdaU, lambdaU });

            // Rebuild signal to level 2
            double[] reconstructedData2 = dwt.Reconstruct(2);

            // Rebuild the signal to level 1 - the original (filtered) signal.
            reconstructedData1 = dwt.Reconstruct();

            endOfCalc = DateTime.UtcNow.Ticks;
            return reconstructedData1;

            // Print results
            //Console.WriteLine();
            //Console.WriteLine("A DWT signal thresholding and reconstruction example using a Daubeachies wavelet.");
            //Console.WriteLine(String.Format("Original Signal: "));
            //Console.WriteLine(" {0}", data.ToString("#.##"));
            //Console.WriteLine();
            //Console.WriteLine(String.Format("IDWT Reconstructed signal: "));
            //Console.WriteLine(" {0}", new DoubleVector(reconstructedData1).ToString("#.##"));

            #endregion
        }

        public double[] reconstructedData1 { get; private set; }
        public long endOfCalc { get; private set; }

    }
}