using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace DoubleSkinRevit
{
    [Transaction(TransactionMode.Manual)]
    public class ImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // select json file 

            string jsonPath = null;
            using (System.Windows.Forms.OpenFileDialog jsonDialog = new System.Windows.Forms.OpenFileDialog()) ;
            {
                OpenFileDialog jsonDialog = new OpenFileDialog();
                jsonDialog.Title = "Select JSON file from the double skin";
                jsonDialog.Filter = "JSON files|*.json";
                if (jsonDialog.ShowDialog() != DialogResult.OK) 
                    return Result.Cancelled;
                jsonPath = jsonDialog.FileName;
            }
            // select DXF
            string dxfPath = null;
            using (System.Windows.Forms.OpenFileDialog jsonDialog = new System.Windows.Forms.OpenFileDialog())
            {
                OpenFileDialog dxfDialog = new OpenFileDialog();
                dxfDialog.Title = "Select DXF file for the double skin";
                dxfDialog.Filter = "DXF files|*.dxf";
                if (dxfDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                dxfPath = dxfDialog.FileName;
            }

            // read json file 
            string jsonContent = File.ReadAllText(jsonPath);


            // parse json data
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            string altitude=data.solar.altitude.ToString();
            string azimuth=data.solar.azimuth.ToString();
            string voidRatio=data.pattern.voidRatio.ToString();
            double radius =(double)data.pattern.radius;
            double sigma = (double)data.pattern.sigma;
            string mode=data.pattern.mode.ToString();
            double scaleMMperPX = (double)data.pattern.scaleMMperPX;
            double gridWidthM = double.Parse(data.pattern.gridWidthM.ToString());
            double gridHeightM = double.Parse(data.pattern.gridHeightM.ToString());



            //confirm to user 
            TaskDialog.Show("Double skin importer",
                $"JSON loaded : {Path.GetFileName(jsonPath)}\n" +
                $"DXF loaded : {Path.GetFileName(dxfPath)}\n\n" +
                $"Altitude : {altitude}° Azimuth: {azimuth}°\n" + 
                $"Mode : {mode} Radius: {radius}px\n" + 
                $"Grid: {gridWidthM}m x {gridHeightM}m\n" +
                $"Void ratio : {voidRatio}%"
                );

            return Result.Succeeded;
        }
    }
}
