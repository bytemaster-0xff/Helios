﻿// Copyright 2020 Ammo Goettsch
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ToolsCommon;

namespace GenerateSimulatorViewportTemplates
{
    internal class GenerateSimulatorViewportTemplates
    {
        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                throw new Exception("JSON path, output path, and usesPatches boolean parameters are required");
            }

            string json = File.ReadAllText(args[0]);
            List<ViewportTemplate> templates = JsonConvert.DeserializeObject<ViewportTemplate[]>(json).ToList();
            Generate(templates, args[1], args[2] == "true");
        }

        private static readonly string[] _colors =
        {
            "DB2929",
            "D92762",
            "9545D8",
            "4C4CE0",
            "2F9FE0",
            "20BAA3",
            "1FC06F",
            "81D42F",
            "E3A322",
            "E05B2B",
            "598296",
            "596387"
        };

        private static void Generate(IList<ViewportTemplate> templates, string templatePath, bool usesPatches = false)
        {
            foreach (ViewportTemplate template in templates)
            {
                // assign a stable color based on the UI name of this viewport template
                int colorIndex = Math.Abs(template.TemplateDisplayName.GetHashCode()) % _colors.Length;

                // generate all valid viewports as templates
                foreach (Viewport viewport in template.Viewports.Where(v => v.IsValid || !usesPatches))
                {
                    List<string> lines = new List<string>();
                    string viewportName = viewport.ViewportName;
                    string category = "Simulator Viewports";
                    if (usesPatches)
                    {
                        viewportName = $"{template.ViewportPrefix}_{viewport.ViewportName}";
                        category = $"{template.TemplateCategory} Simulator Viewports";
                    }

                    lines.Add("<ControlTemplate>");
                    lines.Add($"    <Name>{template.DisplayName(viewport)}</Name>");
                    lines.Add($"    <Category>{category}</Category>");
                    lines.Add("    <TypeIdentifier>Helios.Base.ViewportExtent</TypeIdentifier>");
                    lines.Add("    <Template>");
                    lines.Add("        <TemplateValues>");
                    lines.Add("            <FillBackground>True</FillBackground>");
                    lines.Add($"            <BackgroundColor>#80{_colors[colorIndex]}</BackgroundColor>");
                    lines.Add("            <FontColor>#FFFFFFFF</FontColor>");
                    lines.Add("            <Font>");
                    lines.Add("                <FontFamily>Franklin Gothic</FontFamily>");
                    lines.Add("                <FontStyle>Normal</FontStyle>");
                    lines.Add("                <FontWeight>Normal</FontWeight>");
                    lines.Add("                <FontSize>12</FontSize>");
                    lines.Add("                <HorizontalAlignment>Center</HorizontalAlignment>");
                    lines.Add("                <VerticalAlignment>Center</VerticalAlignment>");
                    lines.Add("                <Padding>");
                    lines.Add("                    <Left>0</Left>");
                    lines.Add("                    <Top>0</Top>");
                    lines.Add("                    <Right>0</Right>");
                    lines.Add("                    <Bottom>0</Bottom>");
                    lines.Add("                </Padding>");
                    lines.Add("            </Font>");
                    lines.Add($"            <Text>{viewportName}</Text>");
                    lines.Add($"            <Location>{viewport.X},{viewport.Y}</Location>");
                    int width = viewport.Width;
                    if (width < 1)
                    {
                        width = 200;
                    }

                    int height = viewport.Height;
                    if (height < 1)
                    {
                        height = 200;
                    }

                    lines.Add($"            <Size>{width},{height}</Size>");
                    lines.Add("            <Hidden>False</Hidden>");
                    lines.Add($"            <ViewportName>{viewportName}</ViewportName>");
                    if (usesPatches)
                    {
                        lines.Add("            <RequiresPatches>true</RequiresPatches>");
                    }

                    lines.Add("        </TemplateValues>");
                    lines.Add("    </Template>");
                    lines.Add("</ControlTemplate>");

                    string outputDirectoryPath = Path.Combine(templatePath,
                        usesPatches ? "Additional Simulator Viewports" : "Simulator Viewports");
                    if (!Directory.Exists(outputDirectoryPath))
                    {
                        Directory.CreateDirectory(outputDirectoryPath);
                    }

                    File.WriteAllLines(Path.Combine(outputDirectoryPath, $"{viewportName}.htpl"), lines);
                }
            }
        }
    }
}