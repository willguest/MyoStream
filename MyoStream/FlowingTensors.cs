using System;

using TensorFlow;


using Accord;
using Accord.Math;
using CNTK;
using KerasSharp;
using KerasSharp.Activations;
using KerasSharp.Backends;
using KerasSharp.Initializers;
using KerasSharp.Losses;
using KerasSharp.Metrics;
using KerasSharp.Models;
using KerasSharp.Optimizers;

using static KerasSharp.Backends.Current;

namespace MyoStream
{
    public class FlowingTensors
    {
        public FlowingTensors()
        {

        }



        public void DoSomeWork (double[][] myoData)
        {
            using (var session = new TFSession())
            {

                float[,] x = myoData.ToMatrix().ToSingle();
                float[] y = myoData.GetColumn(0).ToSingle();

                var inputDim = x.GetLength(1); 


                KerasSharp.Backends.Current.Switch("KerasSharp.Backends.TensorFlowBackend");
                

                // Create the model
                var model = new Sequential();
                model.Add(new Dense(512, input_dim: inputDim, activation: new ReLU()));
                model.Add(new Dense(8, activation: new Softmax()));







                // Compile the model (for the moment, only the mean square 
                // error loss is supported, but this should be solved soon)
                model.Compile(loss: new CategoricalCrossEntropy(),
                    optimizer: new SGD(),
                    metrics: new[] { new Accuracy() });

                // Fit the model for 150 epochs
                model.fit(x, y, epochs: 150, batch_size: 32);

                // Use the model to make predictions
                float[] pred = model.predict(x)[0].To<float[]>();

                // Evaluate the model
                double[] scores = model.evaluate(x, y);
                Console.WriteLine($"{model.metrics_names[1]}: {scores[1] * 100}");

            }


            /*
            using (var session = new TFSession())   
            {
                var graph = session.Graph;

                var a = graph.Const(2);
                var b = graph.Const(3);

                TFTensor addingTensor = session.GetRunner().Run(graph.Add(a, b));
                object TResVal = addingTensor.GetValue();

            }
            */
        }

        int data1, data2, data3, data4, data5, data6;
        const int nTrainFiles = 1895;
        const int nTestFiles = 100;
        const int nGestures = 6;
        const int nfileLines = 50;
        const int nChannels = 8;
        const int nTrainSamples = nTrainFiles * nGestures;
        const int nTestSamples = nTestFiles * nGestures;


        void loadData(bool usePickle = true, bool loadFromFile = false)
        {

            //Function to load the data into matrix[n_files * 50][8 < channels >]
            //3/4 of the files will used for trainning and 1/4 to test
            //Number of files per gesture and train/test ratio hardcoded for now.

            


            float[][][][] trainData = new float[nTrainSamples][][][];

            for (int i = 0; i < nTrainSamples; i++)
            {
                trainData[i] = new float[1][][];
                trainData[i][1] = new float[nfileLines][];
                for (int j = 0; j < nChannels; j++)
                {
                    trainData[i][1][j] = new float[nChannels];
                }
            }

            float[][][][] testData = new float[nTestSamples][][][];

            for (int i = 0; i < nTrainSamples; i++)
            {
                trainData[i] = new float[1][][];
                trainData[i][1] = new float[nfileLines][];
                for (int j = 0; j < nChannels; j++)
                {
                    trainData[i][1][j] = new float[nChannels];
                }
            }

            float[] yTrain = new float[nTrainSamples];
            float[] yTest = new float[nTestSamples];

            int counter = 0;

            for (int x = 0; x < nTrainSamples; x++)
            {
                if (0 <= x && x < nTrainFiles)
                { yTrain[counter] = 0; }
                else if (nTrainFiles <= x && x < nTrainFiles * 2)
                { yTrain[counter] = 1; }
                else if (nTrainFiles * 2 <= x && x < nTrainFiles * 3)
                { yTrain[counter] = 2; }
                else if (nTrainFiles * 3 <= x && x < nTrainFiles * 4)
                { yTrain[counter] = 3; }
                else if (nTrainFiles * 4 <= x && x < nTrainFiles * 5)
                { yTrain[counter] = 4; }
                else if (nTrainFiles * 5 <= x && x < nTrainFiles * 6)
                { yTrain[counter] = 5; }

                counter++;
            }

            counter = 0;

            for (int x = 0; x < nTestSamples; x++)
            {
                if (0 <= x && x < nTestFiles)
                { yTrain[counter] = 0; }
                else if (nTestFiles <= x && x < nTestFiles * 2)
                { yTest[counter] = 1; }
                else if (nTestFiles * 2 <= x && x < nTestFiles * 3)
                { yTest[counter] = 2; }
                else if (nTestFiles * 3 <= x && x < nTestFiles * 4)
                { yTest[counter] = 3; }
                else if (nTestFiles * 4 <= x && x < nTestFiles * 5)
                { yTest[counter] = 4; }
                else if (nTestFiles * 5 <= x && x < nTestFiles * 6)
                { yTest[counter] = 5; }

                counter++;
            }

            counter = 0;




        }

    }
}
