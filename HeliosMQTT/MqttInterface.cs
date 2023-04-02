﻿// Copyright 2014 Craig Courtney
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

        public MqttInterface() : base("MQTT Pub Sub Interface")
        {
            AddTopicCommand = new RelayCommand((obj) => AddTopic(obj));
            AddPublishedActionCommand = new RelayCommand((obj) => AddPublishedAction(obj));
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

        private void LoadActions()
        {
            Actions.Clear();
            foreach(var value in PublishedActions)
            {
                var valueUnit = BindingValueUnits.FetchUnitByName(value.UnitName);

                var action = new HeliosAction(this, value.Device, value.Topic, "publish",
                    $"Action to publish {value.Topic}", value.Description, valueUnit);
                action.Execute += Action_Execute;
                Actions.Add(action);
            }
        }

        private void Action_Execute(object action, HeliosActionEventArgs e)
        {
            var ha = action as HeliosAction;

            if(_mqttServer != null && _mqttServer.IsStarted)
            {
                _mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(new MqttApplicationMessage() { Topic = ha.Name } ));
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

        private void AddPublishedAction(Object obj)
        {
            ValidationErrors.Clear();

            var errors = SelectedPublishedAction.Validate();
            if(errors.Any())
            {
                foreach(var err in errors)
                    ValidationErrors.Add(err);
                return;
            }

            if (String.IsNullOrEmpty(SelectedPublishedAction.Id))
            {
                SelectedPublishedAction.Id = Guid.NewGuid().ToString();
                PublishedActions.Add(SelectedPublishedAction);
            }

            SelectedPublishedAction = new TopicAction();
            LoadActions();
        }

        private void AddTopic(Object obj)
        {
            ValidationErrors.Clear();

            var errors = SelectedTopic.Validate();
            if (errors.Any())
            {
                foreach (var err in errors)
                    ValidationErrors.Add(err);
                return;
            }

            if (String.IsNullOrEmpty(SelectedTopic.Id))
            {
                SelectedTopic.Id = Guid.NewGuid().ToString();
                Topics.Add(SelectedTopic);
            }

                SelectedTopic = new SubscribedTopic();
                LoadTriggers();
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
            reader.ReadStartElement("Content");
            reader.ReadStartElement("Topics");
            int buttonCount = int.Parse(reader.ReadElementString("TopicsCount"));

            var topics = new List<SubscribedTopic>();
            for (int i = 0; i < buttonCount; i++)
            {
                reader.ReadStartElement(nameof(SubscribedTopic));
     
                topics.Add(new SubscribedTopic()
                {
                    Id = reader.ReadElementString(nameof(TopicAction.Id)),
                    Device = reader.ReadElementString(nameof(TopicAction.Device)),
                    Description = reader.ReadElementString(nameof(TopicAction.Description)),
                    DefaultValue = reader.ReadElementString(nameof(TopicAction.DefaultValue)),
                    Topic = reader.ReadElementString(nameof(TopicAction.Topic)),
                    UnitName = reader.ReadElementString(nameof(TopicAction.UnitName)),
                });

                reader.ReadEndElement();
            }

            reader.ReadEndElement();

            reader.ReadStartElement("PublishedActions");
            var values = new List<TopicAction>();
            var valueCount = int.Parse(reader.ReadElementString("PublishedActionsCount"));
            for (int i = 0; i < valueCount; i++)
            {
                reader.ReadStartElement(nameof(TopicAction));
                values.Add(new TopicAction()
                {
                    Id = reader.ReadElementString(nameof(TopicAction.Id)),
                    Device = reader.ReadElementString(nameof(TopicAction.Device)),
                    Description = reader.ReadElementString(nameof(TopicAction.Description)),
                    DefaultValue = reader.ReadElementString(nameof(TopicAction.DefaultValue)),
                    Topic = reader.ReadElementString(nameof(TopicAction.Topic)),
                    UnitName = reader.ReadElementString(nameof(TopicAction.UnitName)),
                });

                reader.ReadEndElement();
            }

            reader.ReadEndElement();

            reader.ReadEndElement();

            foreach (var value in values.OrderBy(trg => trg.Topic))
            {
                PublishedActions.Add(value);
            }

            foreach (var topic in topics.OrderBy(trg=>trg.Topic))
            {
                Topics.Add(topic);
            }

            LoadTriggers();
            LoadActions();
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Content");

            writer.WriteStartElement("Topics");
            writer.WriteElementString("TopicsCount", Topics.Count.ToString());
            foreach (SubscribedTopic topic in Topics)
            {
                writer.WriteStartElement(nameof(SubscribedTopic));

                writer.WriteElementString(nameof(topic.Id), topic.Id);
                writer.WriteElementString(nameof(topic.Device), topic.Device);
                writer.WriteElementString(nameof(topic.Description), topic.Description);
                writer.WriteElementString(nameof(topic.DefaultValue), topic.DefaultValue);
                writer.WriteElementString(nameof(topic.Topic), topic.Topic);
                writer.WriteElementString(nameof(topic.UnitName), topic.UnitName);
             
                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            writer.WriteStartElement("PublishedActions");
            writer.WriteElementString("PublishedActionsCount", PublishedActions.Count.ToString());
            foreach (var pv in PublishedActions)
            {
                writer.WriteStartElement(nameof(TopicAction));

                writer.WriteElementString(nameof(pv.Id), pv.Id);
                writer.WriteElementString(nameof(pv.Device), pv.Device);
                writer.WriteElementString(nameof(pv.Description), pv.Description);
                writer.WriteElementString(nameof(pv.DefaultValue), pv.DefaultValue);
                writer.WriteElementString(nameof(pv.Topic), pv.Topic);
                writer.WriteElementString(nameof(pv.UnitName), pv.UnitName);
                
                writer.WriteEndElement();
            }

            writer.WriteEndElement();

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

        private SubscribedTopic _selectedTopic = new SubscribedTopic();
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

        private TopicAction _selectedPublishedAction = new TopicAction();
        public TopicAction SelectedPublishedAction
        {
            get => _selectedPublishedAction;
            set
            {
                var oldValue = _selectedPublishedAction;
                _selectedPublishedAction = value;
                OnPropertyChanged(nameof(SelectedPublishedAction), oldValue, value, false);
            }
        }

        public ObservableCollection<TopicAction> PublishedActions { get; } = new ObservableCollection<TopicAction>();
        public ObservableCollection<SubscribedTopic> Topics { get; } = new ObservableCollection<SubscribedTopic>();
        public ObservableCollection<string> ReceivedMessages { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectedClients { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> UnitNames { get; } = new ObservableCollection<string>(BindingValueUnits.UnitNames);

        public ObservableCollection<string> ValidationErrors { get; } = new ObservableCollection<string>();


        public ICommand AddTopicCommand { get; }
        public ICommand RemoveTopicCommand { get; }

        public ICommand AddPublishedActionCommand { get; }
        public ICommand RemovePublishedValueCommand { get; }



    }
}
