using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SerialPortProxy
{
    public class MainViewModel : INotifyPropertyChanged
    {
        System.Timers.Timer _timer = new System.Timers.Timer();

        System.Windows.Threading.Dispatcher _dispatcher;

        public const string CLIENT_ID = "Serial Proxy ClientT";
        public const string SERVER_IP = "10.1.1.116";

        IMqttClient _mqttClient;

        public event PropertyChangedEventHandler? PropertyChanged;

        public System.Collections.ObjectModel.ObservableCollection<Panel> Panels { get; } = new System.Collections.ObjectModel.ObservableCollection<Panel>();


        public MainViewModel(System.Windows.Threading.Dispatcher dispatcher)
        {
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 1000;
            _dispatcher = dispatcher;
        }
        private async Task TryReconnectAsync(CancellationToken cancellationToken)
        {
            if (_mqttClient == null)
                return;

            var connected = _mqttClient.IsConnected;
            while (_mqttClient != null && !connected && !cancellationToken.IsCancellationRequested)
            {
                LastAttempt = DateTime.Now.ToLongTimeString();

                try
                {
                    Console.WriteLine($"Connection established to...{SERVER_IP}");

                    var options = new MqttClientOptionsBuilder()
                                .WithClientId(CLIENT_ID)
                                .WithTcpServer(SERVER_IP)
                                .WithCleanSession()
                                .Build();

                    await _mqttClient.ConnectAsync(options, CancellationToken.None);
                    
                    OnlineSince = DateTime.Now.ToLongTimeString();
                    MQTTConnected = true;
                    _dispatcher.Invoke(() =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MQTTConnected"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LastAttempt"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("OnlineSince"));
                    });

                    await _mqttClient.SubscribeAsync("#");
                }
                catch
                {
                    MQTTConnected = false;
                    OnlineSince = null;
                    _dispatcher.Invoke(() =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MQTTConnected"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LastAttempt"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("OnlineSince"));
                    });
                }
                if (_mqttClient != null)
                {
                    connected = _mqttClient.IsConnected;
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        private async void OpenMQTTConnection()
        {
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += _mqttClient_ApplicationMessageReceivedAsync;
            _mqttClient.DisconnectedAsync += _mqttClient_DisconnectedAsync;
            
            await TryReconnectAsync(CancellationToken.None);            
        }

        private async void CloseMQTTConnection()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }


        private Task _mqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            foreach (var panel in Panels)
            {
                if (panel.IsConnected)
                {
                    panel.SendTopic(arg.ApplicationMessage.Topic);
                    Debug.WriteLine($"Send => {arg.ApplicationMessage.Topic} to {panel.PortName}");
                }
            }
            return Task.CompletedTask;
        }

        private Task _mqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            MQTTConnected = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MQTTConnected"));

            TryReconnectAsync(CancellationToken.None);
            return Task.CompletedTask;
        }

        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var ports = System.IO.Ports.SerialPort.GetPortNames();
            _dispatcher.Invoke(() =>
            {
                foreach (var port in ports)
                {
                    var existing = Panels.SingleOrDefault(pnl => pnl.PortName == port);
                    if (existing == null)
                    {
                        var panel = new Panel(port, _dispatcher);
                        panel.ActionReceived += Panel_ActionReceived;
                        panel.TryOpen();
                        Panels.Add(panel);
                    }
                    else
                    {
                        existing.Loop();
                    }
                }

                foreach (var panel in Panels)
                {
                    panel.Loop();

                    if (ports.SingleOrDefault(prt => prt == panel.PortName) == null)
                    {
                        panel.Missing = true;                        
                    }
                }
            });
        }

        private async void Panel_ActionReceived(object? sender, string e)
        {
            Debug.WriteLine("Action Received: " + (sender as Panel).DeviceId + " -> " + e);
            if(_mqttClient.IsConnected)
            {
                await _mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = e.Trim() });
            }
        }

        public void Init()
        {
            _timer.Start();
           OpenMQTTConnection();
        }

        public void ShutDown()
        {
            _timer.Start();
            _timer.Dispose();
            CloseMQTTConnection();

        }

        public string LastAttempt { get; set; }
        public string OnlineSince { get; set; }
        public bool MQTTConnected { get; set; }
    }
}
