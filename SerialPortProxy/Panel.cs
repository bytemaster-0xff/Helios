using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SerialPortProxy
{
    public class Panel : System.ComponentModel.INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string> ActionReceived;
        private Dispatcher _dispatcher;
        private DateTime _lastPing;

        public Panel(string portName, Dispatcher dispatcher)
        {
            PortName = portName;
            Port = new System.IO.Ports.SerialPort(portName, 115200);
            Port.DataReceived += Port_DataReceived;
            _dispatcher = dispatcher;
        }

        private void Port_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            if (Port.IsOpen)
            {
                var line = Port.ReadLine();
                while (line.Length > 0)
                {
                    Debug.WriteLine(line);
                    if (line.StartsWith("PONG"))
                    {
                        LastPing = DateTime.Now.ToLongTimeString();
                        Raise();
                    }
                    else if (line.StartsWith("IAM"))
                    {
                        DeviceId = line.Substring("IAM=".Length);
                        Raise();
                    }
                    else if (line.StartsWith("ACTION="))
                    {
                        ActionReceived(this, line.Substring("ACTION=".Length));
                    }

                    if (Port.IsOpen && Port.BytesToRead > 0)
                        line = Port.ReadLine();
                    else
                        line = String.Empty;
                }
            }
        }

        public string PortName { get; }
        public string DeviceId { get; private set; }
        public string ConnectionTimeStamp { get; set; }
        public string LastPing { get; set; }
        public string DisconnectedTimeStamp { get; set; }
        public bool IsConnected { get; set; }
        public int Reconnects { get; set; }
        public bool Missing { get; set; } = false;
        public System.IO.Ports.SerialPort Port { get; set; }

        public void TryOpen()
        {
            Missing = false;
            try
            {
                Port.Open();
                IsConnected = true;
                ConnectionTimeStamp = DateTime.Now.ToShortTimeString();
                DisconnectedTimeStamp = null;
                Port.Write("WHOIS\n");
                Port.Write("PING\n");
                Raise();
                _lastPing = DateTime.Now;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                if(DisconnectedTimeStamp == null)
                    DisconnectedTimeStamp = DateTime.Now.ToShortTimeString();

                Raise();
            }
        }

        public void Loop()
        {
            if(IsConnected && !Port.IsOpen)
            {
                IsConnected = false;
                DisconnectedTimeStamp = DateTime.Now.ToLongDateString();
                Raise();

                TryOpen();
            }
            else if (!IsConnected)
            {
                TryOpen();
            }
            else
            {
                if((DateTime.Now - _lastPing).TotalSeconds > 15)
                {
                    if (Port.IsOpen)
                    {
                        Port.Write("PING\n");
                        Port.Write("WHOIS\n");
                        _lastPing = DateTime.Now;
                    }
                    else
                    {
                        IsConnected = false;
                        DisconnectedTimeStamp = DateTime.Now.ToLongDateString();
                        Raise();
                    }
                }
            }
        }

        public void SendTopic(string topic)
        {
            if (Port.IsOpen)
                Port.Write(topic + "\n");
        }

        public void Raise()
        {
            _dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionTimeStamp)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisconnectedTimeStamp)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Missing)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastPing)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceId)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reconnects)));
            });
        }
    }
}
