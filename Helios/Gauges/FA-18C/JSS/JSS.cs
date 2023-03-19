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


        public JSS()
        : base("JSS", new Size(140, 470))
        {
            AddButton("CTR", 50,200, new Size(32, 32), "Jettison Center");
            AddButton("RI", 30, 250, new Size(32, 32), "Jettison Right Inner");
            AddButton("RO", 80, 250, new Size(32, 32), "Jettison Right Outer");
            AddButton("LI", 30, 300, new Size(32, 32), "Jettison Left Inner");
            AddButton("LO", 80, 300, new Size(32, 32), "Jettison Left Outer");

            AddTextDisplay("JSS_GEAR_NOSE", 40, 370, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
            AddTextDisplay("JSS_GEAR_LEFT", 10, 400, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
            AddTextDisplay("JSS_GEAR_RIGHT", 70, 400, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
            AddTextDisplay("JSS_FLAPS_FULL", 10, 430, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
            AddTextDisplay("JSS_FLAPS_HALF", 70, 430, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
            AddTextDisplay("JSS_FLAPS", 50, 460, new Size(50, 20), "Option Display 1 Selected", 32, "~", TextHorizontalAlignment.Left, _ufcCueing);
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

