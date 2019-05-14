using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace MyoStream
{
    class dataUtils
    {

        public double[][] InterpretCsv(string filePath)
        {
            double[][] rawData = new double[9][];

            string[] inputArray = File.ReadAllText(filePath).Split('\n');
            IEnumerable<string[]> csv = from line in inputArray
                                        select line.Split(',').ToArray();
            int dataLength = csv.Count() - 1;
            string[][] stringData = csv.ToArray();

            string[] titles = stringData[0];
            for (int i = 0; i < 9; i++)
            {
                rawData[i] = new double[dataLength];
            }

            for (int x = 1; x < dataLength; x++)
            {
                double[] _dubs = Array.ConvertAll(stringData[x], double.Parse);
                for (int y = 0; y < 9; y++)
                {
                    rawData[y][x - 1] = _dubs[y];
                }
            }
            
            return rawData;
        }

/*
        def to_categorical(y, num_classes= None):
  """Converts a class vector (integers) to binary class matrix.
  E.g. for use with categorical_crossentropy.
  Arguments:
      y: class vector to be converted into a matrix
          (integers from 0 to num_classes).
      num_classes: total number of classes.
  Returns:
      A binary matrix representation of the input.The classes axis is placed
      last.
  """
  y = np.array(y, dtype = 'int')
  input_shape = y.shape
  if input_shape and input_shape[-1] == 1 and len(input_shape) > 1:
    input_shape = tuple(input_shape[:-1])
  y = y.ravel()
  if not num_classes:
    num_classes = np.max(y) + 1
  n = y.shape[0]
  categorical = np.zeros((n, num_classes), dtype = np.float32)
  categorical[np.arange(n), y] = 1
  output_shape = input_shape + (num_classes,)
  categorical = np.reshape(categorical, output_shape)
  return categorical
  */


        private static int[] NormalizeData(IEnumerable<double> data, int min, int max)
        {
            double dataMax = data.Max();
            double dataMin = data.Min();
            double range = dataMax - dataMin;

            return data
                .Select(d => (d - dataMin) / range)
                .Select(n => (int)((1 - n) * min + n * max))
                .ToArray();
        }

    }
}
