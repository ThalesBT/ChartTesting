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
using System.Numerics;

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

        private double timeBetweenSamples = 1.04E-6;

        //List<Point> points;
        public List<Point> points;

        private LineGraph line;

        public MainWindow()
        {
            InitializeComponent();

            _syncContext = SynchronizationContext.Current;

            sp = new SerialPort();

            baudRateComboBox.ItemsSource = new List<int> { 9600, 14400, 19200, 28800, 38400, 56000, 57600, 115200 };
            averagingComboBox.ItemsSource = new List<int> { 0, 16, 64, 256 };
            averagingComboBox.SelectedIndex = 1;

            isSerialConnected = false;

            points = new List<Point>();
            m_d3DataSource = new EnumerableDataSource<Point>(points);
            m_d3DataSource.SetXMapping(x => x.X);
            m_d3DataSource.SetYMapping(y => y.Y);

            line = new LineGraph(m_d3DataSource);
            line.LinePen = new Pen(Brushes.DarkMagenta, 2);

            timeDomainChart.LegendVisible = false;
            timeDomainChart.Children.Add(line);

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

            double mean = 0, variance = 0;

            Complex[] inputPoints = new Complex[] { };

            char[] inData = new char[5];

            sp.DiscardInBuffer();
            sp.Write("B" + averagingComboBox.SelectedIndex.ToString());
            Thread.Sleep(300);
            if (averagingComboBox.SelectedIndex != 0)
                n_samples = n_samples / (int)averagingComboBox.SelectedItem;
            sp.Write("A" + n_samples.ToString().PadLeft(7, '0'));

            points.Clear();

            valueCounter = 0;
            receiving_data = true;

            while (valueCounter < n_samples && receiving_data == true)
            {
                inp = sp.ReadChar();

                if (inp == 'd')
                {
                    while (sp.BytesToRead < 4) { }

                    for (i = 0; i < 4; i++)
                    {
                        inp = sp.ReadChar();
                        if (inp > 47 && inp < 58)
                            inData[i] = (char)inp;
                        else if (inp == 'E')
                            receiving_data = false;
                        else if (inp == 'd')
                            transmission_error = true;
                    }

                    if (!transmission_error && receiving_data)
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
                        //points.Add(new Point(Convert.ToDouble(valueCounter), input));

                        inputPoints[valueCounter] = new Complex(input,0);
                        
                        if (averagingComboBox.SelectedIndex == 0)
                            points.Add(new Point(Convert.ToDouble(valueCounter) * timeBetweenSamples, input));
                        else
                            points.Add(new Point(Convert.ToDouble(valueCounter) * timeBetweenSamples * (int)averagingComboBox.SelectedItem, input)); //* 1.2987E-6 * 1.111111E-6 --- 0*/

                        valueCounter++;

                        mean = mean + input / n_samples;
                    }

                    transmission_error = false;

                }
                else if (inp == 'E')
                {
                    receiving_data = false;
                }
            }

            double Amplitude = MaxVoltage - MinVoltage;

            AmplitudeBox.Text = (Amplitude*1E3).ToString("F") + " mV";
            m_d3DataSource.RaiseDataChanged();
            timeDomainChart.FitToView();

            foreach (Point point in points)
            {
                variance = variance + (Math.Pow(point.Y - mean, 2.0)) / n_samples;
            }

            List<double> autocorrelation = new List<double>();
            List<int> autocorrelationPeaks = new List<int>();

            double autocorr;

            for (i = 0; i < n_samples - n_samples * 0.1; i++)
            {
                autocorr = compute_autoc(n_samples, i, variance, mean);
                autocorrelation.Add(autocorr);
            }

            bool ascending = false;

            for(i=0;i<autocorrelation.Count-1;i++)
                if(autocorrelation[i] < autocorrelation[i+1])
                    ascending = true;
                else
                {
                    if (ascending)
                        autocorrelationPeaks.Add(i);
                    ascending = false;
                }

            double meanPeriod = 0;

            for(i=0;i<autocorrelationPeaks.Count -1;i++)
                meanPeriod = meanPeriod + autocorrelationPeaks[i + 1] - autocorrelationPeaks[i];

            meanPeriod = meanPeriod / i;

            displayPeriodFrequency(meanPeriod, (int)averagingComboBox.SelectedItem);

            Complex[] frequencySpectrum = new Complex[] { };

            frequencySpectrum = DFT.Transform(inputPoints);
        }

        private void displayPeriodFrequency(double meanPeriod, int averaging)
        {
            if (averaging == 0)
                averaging = 1;

            double period = meanPeriod * timeBetweenSamples * averaging;

            if(period > 0 && period < 1.5E-5)
            {
                PeriodBox.Text = (period*1E6).ToString("F") + " μs";
                FrequencyBox.Text = (1 / (period*1E6)).ToString("F") + " MHz";
            }
            else if(period < 1.5E-3)
            {
                PeriodBox.Text = (period * 1E3).ToString("F") + " ms";
                FrequencyBox.Text = (1 / (period * 1E3)).ToString("F") + " kHz";
            }
            else
            {
                PeriodBox.Text = (period*1E3).ToString("F") + " ms";
                FrequencyBox.Text = (1 / period).ToString("F") + " Hz";
            }
            
        }

        private double compute_autoc(int n_samples, int lag, double Variance, double Mean)
        {
            double autocv;      // Autocovariance value
            double ac_value;    // Computed autocorrelation value to be returned
            int i;           // Loop counter

            // Loop to compute autovariance
            autocv = 0.0;
            for (i = 0; i < (n_samples - lag); i++)
                autocv = autocv + ((points[i].Y - Mean) * (points[i + lag].Y - Mean));

            autocv = (1.0 / (n_samples - lag)) * autocv;

            // Autocorrelation is autocovariance divided by variance
            ac_value = autocv / Variance;

            return (ac_value);
        }

    }
}

public class DFT
{
    public static Complex[] Transform(Complex[] input)
    {
        int N = input.Length;

        Complex[] output = new Complex[N];

        double arg = -2.0 * Math.PI / (double)N;
        for (int n = 0; n < N; n++)
        {
            output[n] = new Complex();
            for (int k = 0; k < N; k++)
                output[n] += input[k] * Complex.FromPolarCoordinates(1, arg * (double)n * (double)k);
        }
        return output;
    }
}