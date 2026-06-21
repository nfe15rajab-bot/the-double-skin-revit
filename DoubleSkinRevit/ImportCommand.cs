using System;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DoubleSkinRevit
{
    [Transaction(TransactionMode.Manual)]
    public class ImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {   
            // select json file 

            OpenFileDialog jsonDialog = new OpenFileDialog();
            jsonDialog.Title = "Select JSON file from the double skin";
            jsonDialog.Filter = "JSON files|*.json";
            if (jsonDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;
            string jsonPath = jsonDialog.FileName;

            // select DXF
            OpenFileDialog dxfDialog = new OpenFileDialog();
            dxfDialog.Title = "Select DXF file for the double skin";
            dxfDialog.Filter = "DXF files|*.dxf";
            if (dxfDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;
            string dxfPath = dxfDialog.FileName;


            // read json file 
            string jsonContent = File.ReadAllText(jsonPath);

            //confirm to user 
            TaskDialog.Show("Double skin importer",
                $"JSON loaded : {Path.GetFileName(jsonPath)}\n" +
                $"DXF loaded : {Path.GetFileName(dxfPath)}\n\n" +
                "Files loaded successfully. Geometry creation coming next!:))"
                );
            return Result.Succeeded;
        }
    }
}
