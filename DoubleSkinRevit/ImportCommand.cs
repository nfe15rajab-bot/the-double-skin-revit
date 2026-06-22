using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

//using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace DoubleSkinRevit
{
    [Transaction(TransactionMode.Manual)]
    public class ImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // select json file 

            string jsonPath = null;
            using (OpenFileDialog jsonDialog = new OpenFileDialog())
            {

                jsonDialog.Title = "Select JSON file from the double skin";
                jsonDialog.Filter = "JSON files|*.json";
                if (jsonDialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;
                jsonPath = jsonDialog.FileName;
            }
            // select DXF
            string dxfPath = null;
            using (OpenFileDialog dxfDialog = new OpenFileDialog())
            {
                dxfDialog.Title = "Select DXF file for the double skin";
                dxfDialog.Filter = "DXF files|*.dxf";
                if (dxfDialog.ShowDialog() != DialogResult.OK) return Result.Cancelled;
                dxfPath = dxfDialog.FileName;
            }

            // read json file 
            string jsonContent = File.ReadAllText(jsonPath);


            // parse json data
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            string altitude = data.solar.altitude.ToString();
            string azimuth = data.solar.azimuth.ToString();
            string voidRatio = data.pattern.voidRatio.ToString();
            double radius = (double)data.pattern.radius;
            double sigma = (double)data.pattern.sigma;
            string mode = data.pattern.mode.ToString();
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
            List<List<XYZ>> polylines = ParseDxfPolylines(dxfPath);
            if (polylines.Count == 0)
            {
                TaskDialog.Show("Double Skin", "No polylines found in DXF.");
                return Result.Failed;
            }
            TaskDialog.Show("Double skin", $"Parsed {polylines.Count} polylines successfully.");
            Document doc = commandData.Application.ActiveUIDocument.Document;
            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Double skin", "Please open a .rfa family file first");
                return Result.Failed;

            }
            using (Transaction tx = new Transaction(doc, "Double skin: import perforations"))
            {
                tx.Start();
                //Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
                SketchPlane sketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                double depthFeet = 50.0 * MM_TO_FEET;

                int created = 0;
                foreach (List<XYZ> verts in polylines)
                {
                    try
                    {
                        CurveLoop loop = VertsToCurveLoop(verts);
                        CurveArray curveArray = CurveLoopToCurveArray(loop);
                        CurveArrArray profile = new CurveArrArray();
                        profile.Append(curveArray);
                        doc.FamilyCreate.NewExtrusion(false, profile, sketchPlane, depthFeet);
                        created++;

                    }
                    catch { }


                }
                tx.Commit();

                TaskDialog.Show("Double Skin", $"Done +_+ Created {created} void extrusions.");

            }
            using (Transaction tx2 = new Transaction(doc, "double skin: apply cuts :{"))
            {
                tx2.Start();

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.WhereElementIsNotElementType();
                collector.OfClass(typeof(Extrusion));

                Element solid = null;
                List<Element> voids = new List<Element>();
                foreach(Element e in collector)
                {
                    Extrusion ext = e as Extrusion;
                    if (ext == null) continue;
                    if (ext.IsSolid)
                        solid = ext;
                    else
                        voids.Add(ext);
                }



                
                if (solid != null)
                {
                    int cuts = 0;
                    string lastError = "";
                   
                    foreach (Element v in voids)

                    {

                        try { SolidSolidCutUtils.AddCutBetweenSolids(doc, solid, v);
                            cuts++;

                        }
                        catch (Exception ex) { lastError = ex.Message; }
                    }
                    TaskDialog.Show("Cut result", $"Cuts applied: {cuts}\nLast error:{lastError}");
                }
                tx2.Commit();
            }



            return Result.Succeeded;
        }

   

        //parse dxf data
        private const double MM_TO_FEET = 1.0 / 304.8;
        private static List<List<XYZ>> ParseDxfPolylines(string path)
        {
            var result = new List<List<XYZ>>();
            var lines = File.ReadAllLines(path);

            bool inLWPoly = false;
            var verts = new List<XYZ>();
            double x = 0;
            bool hasX = false;

            for (int i = 0; i < lines.Length - 1; i++){
                string code = lines[i].Trim();
                string value = lines[i + 1].Trim();


            
            if (code == "0" && value == "LWPOLYLINE")
            {
                if (inLWPoly && verts.Count >= 3)
                    result.Add(new List<XYZ>(verts));
                verts.Clear();
                inLWPoly = true;
                hasX = false;
                i++; continue;

            }

            if (inLWPoly && code == "0" && value != "LWPOLYLINE")
            {
                if (verts.Count >= 3)
                    result.Add(new List<XYZ>(verts));
                verts.Clear();
                inLWPoly = false;
                continue;

            }
            if (!inLWPoly) { i++; continue; }
            if (code == "10")
            {
                if (double.TryParse(value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double px))
                {
                    x = px * MM_TO_FEET;
                    hasX = true;
                }
                i++; continue;
            }
            if (code == "20" && hasX)
                {
                    if (double.TryParse(value, 
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double py))
                    {
                        verts.Add(new XYZ(x, py * MM_TO_FEET, 0));
                        hasX = false;
                    }
                    i++; continue;
                }
            i++;

            }
            if (inLWPoly && verts.Count >= 3)
                result.Add(verts);

            return result;
            
            }
    private static CurveLoop VertsToCurveLoop(List<XYZ> verts){
            var loop = new CurveLoop();
            for (int i = 0; i < verts.Count; i++)
            {
                XYZ a = verts[i];
                XYZ b = verts[(i + 1) % verts.Count];
                loop.Append(Line.CreateBound(a, b));

            }
            return loop;
        }
        private static CurveArray CurveLoopToCurveArray(CurveLoop loop){
            var arr = new CurveArray();
            foreach (Curve c in loop) arr.Append(c);
            return arr;
        }

        }
}

