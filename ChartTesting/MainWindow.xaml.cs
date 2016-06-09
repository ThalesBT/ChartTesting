using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Collections.ObjectModel;

namespace ChartTesting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort sp;
        string[] availableCOMPorts;
        bool isSerialConnected;

        private readonly SynchronizationContext _syncContext;
        public ObservableCollection<KeyValuePair<int, float>> valueList { get; private set; }

        int valueCounter;

        public MainWindow()
        {
            InitializeComponent();

            _syncContext = SynchronizationContext.Current;

            sp = new SerialPort();
            sp.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            baudRateComboBox.ItemsSource = new List<int>{9600,14400,19200,28800,38400,56000,57600,115200};

            isSerialConnected = false;
            valueCounter = 0;

            valueList = new ObservableCollection<KeyValuePair<int, float>>();

            
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

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if(sp.ReadChar()=='f')
            {
                while (sp.BytesToRead < 5) { }

                sp.ReadChar();

                byte[] inData = new byte[4];

                sp.Read(inData, 0, 4);

                float input = BitConverter.ToSingle(inData,0);

                /* _syncContext.Post(o => terminalTextBox.AppendText(input.ToString()), null);
                 _syncContext.Post(o => terminalTextBox.ScrollToEnd(), null);*/

                /*if (valueCounter == 255)
                {
                    _syncContext.Post(o => valueList.Move(0, 255), null);
                    _syncContext.Post(o => valueList.Add(new KeyValuePair<int, float>(valueCounter, input)), null);

                }
                else
                {
                    _syncContext.Post(o => valueList.Add(new KeyValuePair<int, float>(valueCounter, input)), null);
                    _syncContext.Post(o => valueCounter++, null);
                }*/
                _syncContext.Post(o => valueList.Add(new KeyValuePair<int, float>(valueCounter, input)), null);
                _syncContext.Post(o => ((LineSeries)chartTest.Series[0]).ItemsSource = valueList, null);
                if(valueCounter == 255)
                {
                    _syncContext.Post(o => valueCounter = 0, null);
                    _syncContext.Post(o => valueList.Clear(), null);
                    _syncContext.Post(o => ((LineSeries)chartTest.Series[0]).ItemsSource = valueList, null);
                }
                else
                    _syncContext.Post(o => valueCounter++, null);
                //_syncContext.Post(o => ((LineSeries)chartTest.Series[0]).ItemsSource = valueList, null);

            }

            /*if (sp.BytesToRead == 6)
            {

                byte[] inData = new byte[4];

                sp.
                
                string indata = sp.ReadExisting();
                _syncContext.Post(o => terminalTextBox.AppendText(indata), null);
                _syncContext.Post(o => terminalTextBox.ScrollToEnd(), null);

            Debug.Write(indata);
            */
        }
    }
}
/*
if(myPort.available() > 0) {
    char inByte = myPort.readChar();
    if(inByte == 'f') {
      // we expect data with this format f:XXXX
      myPort.readChar(); // discard ':'
      byte[] inData = new byte[4];
myPort.readBytes(inData);
      
      int intbit = 0;

      intbit = (inData[3] << 24) | ((inData[2] & 0xff) << 16) | ((inData[1] & 0xff) << 8) | (inData[0] & 0xff);
      
      float f = Float.intBitsToFloat(intbit);
      println(f);
    }
    */