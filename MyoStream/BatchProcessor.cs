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
        private int noPoints = 64;

        private double[][] rawData = new double[9][];
        private double[][] midData = new double[9][];
        private double[][] clnData = new double[9][];

        private double[] startEMG = new double[9];
        private double[] cleanEMG = new double[9];

        private StreamWriter sWriter;
        private DWTCalc _dwt;


        public BatchProcessor()
        {
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

            Task.Run(async () => await MainAsyncThread());

            Console.WriteLine("cleaning complete");
            
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
            sWriter = null;

        }


        private async Task<double[]> WorkerThread(double[] _input0)
        {
            return await DiscreetWaveletTransform(_input0).ConfigureAwait(false);
        }


        private async Task<double[]> DiscreetWaveletTransform(double[] _input)
        {
            var data = new DoubleVector(_input);

            var wavelet = new DoubleWavelet(Wavelet.Wavelets.Harr);

            var dwt = new DoubleDWT(data.DataBlock.Data, wavelet);

            if (_input.Length == noPoints)
            {
                // Decompose signal to level 5
                //Console.WriteLine("decomposing signal");
                dwt.Decompose(4);

            }

            // Find Universal threshold
            double lambdaU = dwt.ComputeThreshold(DoubleDWT.ThresholdMethod.Universal, 1);

            // Threshold all detail levels with lambdaU
            dwt.ThresholdAllLevels(DoubleDWT.ThresholdPolicy.Soft,
                new double[] { lambdaU, lambdaU, lambdaU, lambdaU, lambdaU });

            // testing this: change wavelet type to coif5 in order to get "prefect reconstruction"
            //dwt.Wavelet = coif5wavelet;

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

    }
}
