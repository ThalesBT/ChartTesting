using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Input;
using System.IO.Ports;
using System.Threading;
using System.Collections.ObjectModel;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System.ComponentModel;
using System.Windows.Media;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;

namespace ChartTesting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public event PropertyChangedEventHandler PropertyChanged;

        SerialPort sp;
        string[] availableCOMPorts;
        bool isSerialConnected;

        private readonly SynchronizationContext _syncContext;
        public ObservableCollection<KeyValuePair<int, float>> valueList { get; private set; }


        int valueCounter;

        EnumerableDataSource<Point> m_d3DataSource;

        //List<Point> points;
        public List<Point> points;

        
        private LineGraph line;

        //public DynamicDataDisplay.Markers.DataSources.EnumerableDataSource D3DataSource
        //{
        //    get
        //    {
        //        return m_d3DataSource;
        //    }
        //    set
        //    {
        //        //you can set your mapping inside the set block as well             
        //        m_d3DataSource = value;
        //        OnPropertyChanged("D3DataSource");
        //    }
        //}

        //protected void OnPropertyChanged(PropertyChangedEventArgs e)
        //{
        //    PropertyChangedEventHandler handler = PropertyChanged;
        //    if (handler != null)
        //    {
        //        handler(this, e);
        //    }
        //}

        //protected void OnPropertyChanged(string propertyName)
        //{
        //    OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        //}

        public MainWindow()
        {
            InitializeComponent();

            _syncContext = SynchronizationContext.Current;

            sp = new SerialPort();
        
            baudRateComboBox.ItemsSource = new List<int>{9600,14400,19200,28800,38400,56000,57600,115200};

            isSerialConnected = false;

            points = new List<Point>();
            m_d3DataSource = new EnumerableDataSource<Point>(points);
            m_d3DataSource.SetXMapping(x => x.X);
            m_d3DataSource.SetYMapping(y => y.Y);

            line = new LineGraph(m_d3DataSource);
            line.LinePen = new Pen(Brushes.Black, 2);

            myChart.LegendVisible = false;
            myChart.Children.Add(line);

            requestButton.IsEnabled = false;

        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isSerialConnected)
            {
                try
                {
                    sp.PortName = COMComboBox.SelectedItem.ToString();
                    sp.BaudRate = (int)baudRateComboBox.SelectedItem;
                    sp.Open();

                    isSerialConnected = true;
                    requestButton.IsEnabled = true;
                    connectButton.Content = "Disconnect";

                }
                catch (Exception)
                {
                    MessageBox.Show("Please give a valid port number and baud rate");
                }
            }
            else
            {
                sp.Close();
                isSerialConnected = false;
                connectButton.Content = "Connect";
            }


        }


        private void COMComboBox_GotMouseCapture(object sender, MouseEventArgs e)
        {
            availableCOMPorts = SerialPort.GetPortNames();
            COMComboBox.ItemsSource = availableCOMPorts;
        }

        private void requestButton_Click(object sender, RoutedEventArgs e)
        {

            double MaxVoltage = 0, MinVoltage = 0;

            int inp;
            int i;
            bool receiving_data;
            bool transmission_error = false;
            int n_samples = int.Parse(numberOfSamples.Text);

            char[] inData = new char[5];

            sp.DiscardInBuffer();
            sp.Write("A" + numberOfSamples.Text.PadLeft(5,'0'));

            points.Clear();

            valueCounter = 0;
            receiving_data = true;

            while (valueCounter < n_samples && receiving_data == true)
            {
                inp = sp.ReadChar();

                if (inp == 'd')
                {
                    while (sp.BytesToRead < 4) { }

                    for(i = 0; i < 4; i++)
                    {
                        inp = sp.ReadChar();
                        if (inp > 47 && inp < 58)
                            inData[i] = (char)inp;
                        else if (inp == 'E')
                            receiving_data = false;
                        else if(inp == 'd')
                            transmission_error = true;
                    }

                    if(!transmission_error && receiving_data)
                    {
                        inData[4] = '\0';
                        double input = int.Parse(new string(inData)) * 0.5 / 4096.0;

                        if (valueCounter == 0)
                        {
                            MaxVoltage = input;
                            MinVoltage = input;
                        }
                        else
                        {
                            if (input > MaxVoltage)
                                MaxVoltage = input;
                            if (input < MinVoltage)
                                MinVoltage = input;
                        }
                        points.Add(new Point(Convert.ToDouble(valueCounter)*1.2987E-6, input));
                        valueCounter++;
                    }

                    transmission_error = false;

                }
                else if(inp == 'E')
                {
                    receiving_data = false;
                }
            }

            double Amplitude = MaxVoltage - MinVoltage;

            AmplitudeBox.Text = Amplitude.ToString("F") + " V";
            m_d3DataSource.RaiseDataChanged();
            myChart.FitToView();
        }

    }
}