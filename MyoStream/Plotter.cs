using LiveCharts;
using LiveCharts.Defaults;

using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Controls;

namespace MyoStream
{
    public class Plotter : UserControl
    {
        public SeriesCollection SeriesCollection { get; set; }
        public CartesianChart CartChart;

        private Func<double, string> _yFormatter;
        public Func<double, string> YFormatter
        {
            get { return _yFormatter; }
            set
            {
                _yFormatter = value;
                OnPropertyChanged("YFormatter");
            }
        }

        public Plotter()
        {
            SeriesCollection = new SeriesCollection();
            CartChart = new CartesianChart();

            for (int cs = 0; cs < 8; cs++)
            {
                //modifying the series collection will animate and update the chart
                SeriesCollection.Add(new LineSeries
                {
                    Title = "Channel " + cs.ToString(),
                    Values = new ChartValues<double> { cs*10, cs*10, cs * 5, cs * 2, cs * 10, cs * 4, cs * 2, cs * 10, cs *6, cs * 9, cs * 2, cs * 10, cs * 12, cs * 2, cs * 10, cs * 4, cs * 2, cs * 10, cs * 14, cs *8 },
                    LineSmoothness = 0, //0: straight lines, 1: really smooth lines
                    PointGeometry = null, // Geometry.Parse("m 25 70.36218 20 -28 -20 22 -8 -6 z"),
                    PointGeometrySize = 5,
                    PointForeground = Brushes.DarkGreen
                });
            }

            Console.WriteLine("Plotter Initialised");
        }





        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
