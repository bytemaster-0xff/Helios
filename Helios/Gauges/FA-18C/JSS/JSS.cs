using GadrocsWorkshop.Helios.ComponentModel;
using GadrocsWorkshop.Helios.Controls;
using GadrocsWorkshop.Helios.Gauges.FA18C;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GadrocsWorkshop.Helios.Gauges.FA_18C.JSS
{
    [HeliosControl("Helios.FA18C.JSS", "Jetison Station Select", "F/A-18C", typeof(BackgroundImageRenderer))]
    class JSS : FA18CDevice
    {
        private HeliosValue _noseGear;
        private HeliosValue _leftGear;
        private HeliosValue _rightGear;
        private HeliosValue _halfFlaps;
        private HeliosValue _fullFlaps;
        private HeliosValue _flaps;

        private string _interfaceDeviceName = "JSS";
        private string _ufcCueing = "!=È";

        private string IndicatorName(string name, bool isOn)
        {
            return "{FA-18C}/Images/" + name + (isOn ? " On" : " Off") + ".png";
             
        }

        public JSS()
        : base("JSS", new Size(140, 470))
        {
            AddButton("CTR", (70-24),140, new Size(48, 48), "Jettison Center");
            AddButton("LI", 18, 200, new Size(48, 48), "Jettison Left Inner");
            AddButton("LO", 18, 260, new Size(48, 48), "Jettison Left Outer");
            AddButton("RI", 80, 200, new Size(48, 48), "Jettison Right Inner");
            AddButton("RO", 80, 260, new Size(48, 48), "Jettison Right Outer");



            AddIndicator("GEAR_NOSE", new Point(45, 320), new Size(50, 27), IndicatorName("Gear Nose", true), IndicatorName("Gear Nose", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName, "Nose Gear", false);
            AddIndicator("GEAR_LEFT", new Point(20, 355), new Size(50, 27), IndicatorName("Gear Left", true), IndicatorName("Gear Left", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName, "Left Gear", false);
            AddIndicator("GEAR_RIGHT", new Point(70, 355), new Size(50, 27), IndicatorName("Gear Right", true), IndicatorName("Gear Right", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName, "Right Gear", false);


            AddIndicator("FLAPS_FULL", new Point(20, 390), new Size(50, 27), IndicatorName("Flaps Full", true), IndicatorName("Flaps Full", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName, "Full Flaps", false);

            AddIndicator("FLAPS_HALF", new Point(70, 390), new Size(50, 27), IndicatorName("Flaps Half", true), IndicatorName("Flaps Half", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName,"Half Flaps", false);

            AddIndicator("FLAPS", new Point(45, 425), new Size(50, 27), IndicatorName("Flaps", true), IndicatorName("Flaps", false), Colors.Transparent, Colors.Transparent,
                "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller", false, _interfaceDeviceName, "Flaps", false);
        }


        private void AddButton(string name, double x, double y, Size size, string interfaceElementName)
        {
            Point pos = new Point(x, y);
            AddButton(
                name: name,
                posn: pos,
                size: size,
                image: "{FA-18C}/Images/JSS Button Up " + name + ".png",
                pushedImage: "{FA-18C}/Images/JSS Button Dn " + name + ".png",
                buttonText: "",
                interfaceDeviceName: _interfaceDeviceName,
                interfaceElementName: interfaceElementName,
                fromCenter: false
                );
        }


        private void AddTextDisplay(string name, double x, double y, Size size,
            string interfaceElementName, double baseFontsize, string testDisp, TextHorizontalAlignment hTextAlign, string ufcDictionary)
        {
                TextDisplay display = AddTextDisplay(
                name: name,
                posn: new Point(x, y),
                size: size,
                font: "Helios Virtual Cockpit F/A-18C_Hornet-Up_Front_Controller",
                baseFontsize: baseFontsize,
                horizontalAlignment: hTextAlign,
                verticalAligment: TextVerticalAlignment.Center,
                testTextDisplay: testDisp,
                textColor: Color.FromArgb(0xff, 0x7e, 0xde, 0x72),
                backgroundColor: Color.FromArgb(0x00, 0x26, 0x3f, 0x36),
                useBackground: false,
                interfaceDeviceName: _interfaceDeviceName,
                interfaceElementName: interfaceElementName,
                textDisplayDictionary: ufcDictionary
                );
        }


        public override string DefaultBackgroundImage
        {
            get { return "{FA-18C}/Images/JSS Faceplate.png"; }
        }

        public override void MouseDown(Point location)
        {
        }

        public override void MouseDrag(Point location)
        {
        }

        public override void MouseUp(Point location)
        {
        }
    }
}

