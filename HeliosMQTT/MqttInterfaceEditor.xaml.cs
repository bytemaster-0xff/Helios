using GadrocsWorkshop.Helios.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GadrocsWorkshop.Helios.Interfaces.HeliosMQTT
{
    /// <summary>
    /// Interaction logic for MqttInterfaceEditor.xaml
    /// </summary>
    public partial class MqttInterfaceEditor : HeliosInterfaceEditor
    {
        public MqttInterfaceEditor()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
