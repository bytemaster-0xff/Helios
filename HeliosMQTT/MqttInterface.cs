// Copyright 2014 Craig Courtney
// Copyright 2022 Helios Contributors
// 
// Helios is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Helios is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.


using GadrocsWorkshop.Helios.Collections;
using GadrocsWorkshop.Helios.Interfaces.Capabilities;
using System.ComponentModel;
using System.Xml;
using GadrocsWorkshop.Helios.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GadrocsWorkshop.Helios.Windows;
using System;
using MQTTnet;
using MQTTnet.Server;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows;
using System.Linq;
using System.Collections.Generic;

namespace GadrocsWorkshop.Helios.Interfaces.HeliosMQTT
{

    [HeliosInterface("Helios.Base.MqttInterface", "MQTT Pub Sub Interace",
        typeof(MqttInterfaceEditor),
        typeof(UniqueHeliosInterfaceFactory), AutoAdd = false)]

    public class MqttInterface : HeliosInterface, IExtendedDescription
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static MqttServer _mqttServer;

        // currently registered Helios triggers for this object
        private readonly NoResetObservablecollection<IBindingTrigger> _pressedTriggers =
            new NoResetObservablecollection<IBindingTrigger>();

        // currently registered Helios triggers for this object
        private readonly NoResetObservablecollection<IBindingTrigger> _releasedTriggers =
            new NoResetObservablecollection<IBindingTrigger>();


        public MqttInterface() : base("MQTT Pub Sub Interface")
        {
            AddTopicCommand = new RelayCommand((obj) => AddTopic(obj));
            RemoveTopicCommand = new RelayCommand((obj) => RemoveTopic(obj));
        }

        private void LoadTriggers()
        {
            Triggers.Clear();

            foreach (var topic in Topics)
            {
                var receivedTrigger = new HeliosTrigger(this, $"Topic: {topic.Topic}",
                topic.Topic, "received", "Fired when a topic is received.",
                   "Always returns true.", BindingValueUnits.Boolean);
                Triggers.Add(receivedTrigger);
            }
        }

        private async void OpenMQTTConnection()
        {
            var mqttFactory = new MqttFactory();
            var mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().Build();
            _mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);
            await _mqttServer.StartAsync();
            _mqttServer.InterceptingPublishAsync += _mqttServer_InterceptingPublishAsync;
            _mqttServer.ClientConnectedAsync += _mqttServer_ClientConnectedAsync;
            _mqttServer.ClientDisconnectedAsync += _mqttServer_ClientDisconnectedAsync;
        }

        private System.Threading.Tasks.Task _mqttServer_ClientDisconnectedAsync(ClientDisconnectedEventArgs arg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ConnectedClients.Contains(arg.ClientId))
                    ConnectedClients.Remove(arg.ClientId);
            });
            
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task _mqttServer_ClientConnectedAsync(ClientConnectedEventArgs arg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectedClients.Add(arg.ClientId);
            });

            Debug.WriteLine(arg.ClientId);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task _mqttServer_InterceptingPublishAsync(InterceptingPublishEventArgs arg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReceivedMessages.Add(arg.ApplicationMessage.Topic);
            });

            var trigger = Triggers.FirstOrDefault(trg => trg.Name == arg.ApplicationMessage.Topic) as HeliosTrigger;
            if(trigger != null)
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        trigger?.FireTrigger(new BindingValue(true))));
                }
                else
                {
                    trigger?.FireTrigger(new BindingValue(true));
                }

            }

            Debug.WriteLine(arg.ApplicationMessage.Topic);
            if (arg.ApplicationMessage.Payload != null)
                Debug.WriteLine(System.Text.ASCIIEncoding.ASCII.GetString(arg.ApplicationMessage.Payload));

            return System.Threading.Tasks.Task.CompletedTask;
        }

        private async void CloseMQTTConnection()
        {
            if (_mqttServer != null)
            {
                await _mqttServer.StopAsync();
                _mqttServer.Dispose();
                _mqttServer = null;
            }
        }

        protected override void AttachToProfileOnMainThread()
        {
            if(Application.Current.GetType().FullName != "GadrocsWorkshop.Helios.ProfileEditor.App" && _mqttServer == null)
                OpenMQTTConnection();

            PropertyChanged += MqttInterface_PropertyChanged; ;
        }

        private void MqttInterface_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        protected override void DetachFromProfileOnMainThread(HeliosProfile oldProfile)
        {
            PropertyChanged -= MqttInterface_PropertyChanged;

            CloseMQTTConnection();
        }

        private void AddTopic(Object obj)
        {
            if (!String.IsNullOrEmpty(TopicName))
            {
                Topics.Add(new SubscribedTopic() { Topic = TopicName, PayloadType = SubscribedTopic.PayloadTypes.None });
                TopicName = String.Empty;
                LoadTriggers();
            }
        }

        private void RemoveTopic(Object obj)
        {
            if(SelectedTopic != null)
            {
                Topics.Remove(SelectedTopic);
                SelectedTopic = null;
                LoadTriggers();
            }
        }

        public override void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement("Topics");
            int buttonCount = int.Parse(reader.ReadElementString("TopicsCount"));

            var topics = new List<SubscribedTopic>();
            for (int i = 0; i < buttonCount; i++)
            {
                reader.ReadStartElement("Topic");
                string topicName = reader.ReadElementString("Name");
                string payloadType = reader.ReadElementString("PayloadType");
                topics.Add(new SubscribedTopic()
                {
                    Topic = topicName,
                    PayloadType = (SubscribedTopic.PayloadTypes)Enum.Parse(typeof(SubscribedTopic.PayloadTypes), payloadType)
                });

                reader.ReadEndElement();
            }

            foreach(var topic in topics.OrderBy(trg=>trg.Topic))
            {
                Topics.Add(topic);
            }

            reader.ReadEndElement();

            LoadTriggers();
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Topics");
            writer.WriteElementString("TopicsCount", Topics.Count.ToString());
            foreach (SubscribedTopic topic in Topics)
            {
                writer.WriteStartElement("Topic");
                writer.WriteElementString("Name", topic.Topic);
                writer.WriteElementString("PayloadType", topic.PayloadType.ToString());
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        public string Description { get; }

        public string RemovalNarrative => "Removes the MQTT Interface.";

        private string _topicName;
        public string TopicName
        {
            get => _topicName;
            set
            {
                var oldValue = _topicName;
                _topicName = value;
                OnPropertyChanged(nameof(TopicName), oldValue, value, false);
            }
        }

        private SubscribedTopic _selectedTopic;
        public SubscribedTopic SelectedTopic
        {
            get => _selectedTopic;
            set
            {
                var oldValue = _selectedTopic;
                _selectedTopic = value;
                OnPropertyChanged(nameof(SelectedTopic), oldValue, value, false);
            }
        }

        public ObservableCollection<SubscribedTopic> Topics { get; } = new ObservableCollection<SubscribedTopic>();
        public ObservableCollection<string> ReceivedMessages { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectedClients { get; } = new ObservableCollection<string>();

        public ICommand AddTopicCommand { get; }
        public ICommand RemoveTopicCommand { get; }

    }
}
