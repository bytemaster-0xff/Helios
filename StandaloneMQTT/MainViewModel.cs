using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace StandaloneMQTT
{
    public class ConnectedClient : INotifyPropertyChanged
    {
        public ConnectedClient(string clientId)
        {
            ClientId = clientId;
        }

        public string ClientId { get; }
        public string ConnectionTimeStamp { get; set; }
        public string DisconnectedTimeStamp { get; set; }
        public bool IsConnected { get; set; }
        public int Reconnects { get; set; }

        public void Raise()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionTimeStamp)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisconnectedTimeStamp)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reconnects)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal class MainViewModel
    {
        Dispatcher _dispatcher;
        static MQTTnet.Server.MqttServer _mqttServer;

        public MainViewModel(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task Init()
        {
            var mqttFactory = new MqttFactory();
            var mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().Build();
            _mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);
            await _mqttServer.StartAsync();
            _mqttServer.InterceptingPublishAsync += _mqttServer_InterceptingPublishAsync;
            _mqttServer.ClientConnectedAsync += _mqttServer_ClientConnectedAsync;
            _mqttServer.ClientDisconnectedAsync += _mqttServer_ClientDisconnectedAsync;
        }

        public async Task ShutDown()
        {
            await _mqttServer.StopAsync();
            _mqttServer.Dispose();

        }

        private Task _mqttServer_ClientDisconnectedAsync(ClientDisconnectedEventArgs arg)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var client = Clients.Single(clnt => clnt.ClientId == arg.ClientId);
                client.IsConnected = false;
                client.DisconnectedTimeStamp = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                client.Raise();
            }));

            return Task.CompletedTask;

        }

        private Task _mqttServer_ClientConnectedAsync(ClientConnectedEventArgs arg)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var client = Clients.SingleOrDefault(clnt => clnt.ClientId == arg.ClientId);
                if (client != null)
                {
                    client.DisconnectedTimeStamp = String.Empty;
                    client.IsConnected = true;
                    client.ConnectionTimeStamp = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
                    client.Reconnects++;
                    client.Raise();
                }
                else
                {
                    Clients.Add(new ConnectedClient(arg.ClientId)
                    {
                        IsConnected = true,
                        ConnectionTimeStamp = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString()
                    });
                }
            }));

            return Task.CompletedTask;
        }

        private Task _mqttServer_InterceptingPublishAsync(InterceptingPublishEventArgs arg)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Messages.Insert(0, $"{DateTime.Now.ToLongTimeString()} {arg.ApplicationMessage.Topic}");
                while (Messages.Count > 500)
                    Messages.RemoveAt(Messages.Count - 1);
            }));

            return Task.CompletedTask;
        }

        public ObservableCollection<ConnectedClient> Clients { get; } = new ObservableCollection<ConnectedClient>();

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();
    }
}
