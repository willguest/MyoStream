using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TensorFlow;

namespace MyoStream
{
    class FlowingTensors
    {
        public FlowingTensors()
        {
            using (var session = new TFSession())
            {
                var graph = session.Graph;

                var a = graph.Const(2);
                var b = graph.Const(3);

                TFTensor addingTensor = session.GetRunner().Run(graph.Add(a, b));
                object TResVal = addingTensor.GetValue();

            }
        }


    }
}
