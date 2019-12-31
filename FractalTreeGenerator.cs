using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Events;
using System.IO;
using System.Windows.Media.Imaging;

namespace FractalTreeGenerator
{
    //class to store variables as globals 
    public class GlobVars
    {
        //Default tree depth
        public static int treeDepth = 5;

        //Hardcoded tree values
        public static double rotAngle = 28;
        public static double lenFactor = 0.78;
        public static double lenFactorChristmas = lenFactor / 2;
        public static double rndCoof = 0.15;

        //setting a randomizer
        public static Random random = new Random();

        //Line list to fill
        public static CurveArray treeCurves = new CurveArray();

        //marker for move and rotate functions
        public static string orientation = "";
    }

    //class with UI generations, which calls the main treegeneration class
    public class FractalPanel : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {

            //Addin tab data
            string RIBBON_PANEL = "Generate fractal tree";

            //Add a tab
            RibbonPanel panel = app.CreateRibbonPanel(RIBBON_PANEL);

            string assemblyName = Assembly.GetExecutingAssembly().Location;

            //add button for command trigger
            PushButtonData buttonTree = new PushButtonData(
                "Generate a fractal tree from a line", "Regular Tree", assemblyName,
                "FractalTreeGenerator.GenerateFractalTree");

            PushButtonData buttonChristmasTree = new PushButtonData(
                "Generate a fractal christmas tree from a line", "Christmas Tree", assemblyName,
                "FractalTreeGenerator.GenerateFractalChristmasTree");

            PushButton pushButton1 = panel.AddItem(buttonTree) as PushButton;
            pushButton1.ToolTip = "Click on a line to turn into a fractal tree";
            pushButton1.LargeImage = BmpImageSource("FractalTreeGenerator.Resources.TreeRegular.bmp");

            PushButton pushButton2 = panel.AddItem(buttonChristmasTree) as PushButton;
            pushButton2.ToolTip = "Click on a line to turn into a christmas fractal tree";
            pushButton2.LargeImage = BmpImageSource("FractalTreeGenerator.Resources.ChristmasTree.bmp");

            //add text input
            TextBoxData itemDepth = new TextBoxData("treeDepth");
            itemDepth.Name = "Tree depth";

            //add text input2
            TextBoxData itemAngle = new TextBoxData("treeAngle");
            itemAngle.Name = "Branches rotation angle";

            //add text input3
            TextBoxData itemRnd = new TextBoxData("treeRandomness");
            itemRnd.Name = "Randomness cooficient";

            IList<RibbonItem> stackedItems = panel.AddStackedItems(itemAngle, itemDepth, itemRnd);
            if (stackedItems.Count > 1)
            {
                TextBox item2 = stackedItems[0] as TextBox;
                item2.Value = GlobVars.rotAngle;
                item2.ToolTip = "Branches rotation angle";
                item2.EnterPressed += Refresh2;

                //refreshes value picked from textbox on enter press
                void Refresh2(object sender, TextBoxEnterPressedEventArgs args)
                {
                    try
                    {
                        TextBox textBoxRefresher = sender as TextBox;
                        GlobVars.rotAngle = Convert.ToInt32(item2.Value.ToString());
                    }
                    catch
                    {
                    }
                }

                TextBox item1 = stackedItems[1] as TextBox;
                item1.Value = GlobVars.treeDepth;
                item1.ToolTip = "Tree depth (do not use more than 10)";
                item1.EnterPressed += Refresh;

                //refreshes value picked from textbox on enter press
                void Refresh(object sender, TextBoxEnterPressedEventArgs args)
                {
                    try
                    {
                        TextBox textBoxRefresher = sender as TextBox;
                        GlobVars.treeDepth = Convert.ToInt32(item1.Value.ToString());
                    }
                    catch
                    {
                    }
                }

                TextBox item3 = stackedItems[2] as TextBox;
                item3.Value = GlobVars.rndCoof*100;
                item3.ToolTip = "Randomness coof, should be between 0 and 50, if more or less will be pinned at 15";
                item3.EnterPressed += Refresh3;

                //refreshes value picked from textbox on enter press
                void Refresh3(object sender, TextBoxEnterPressedEventArgs args)
                {
                    try
                    {
                        TextBox textBoxRefresher = sender as TextBox;
                        int rndVal = Convert.ToInt32(item3.Value.ToString());
                        if (rndVal >= 0 && rndVal <= 50)
                        {
                            GlobVars.rndCoof = rndVal/100;
                        }
                    }
                    catch
                    {
                    }
                }
            }

                return Result.Succeeded;
        }

        //to embed image into dll
        public System.Windows.Media.ImageSource BmpImageSource(string embeddedPath)
        {
            Stream stream = this.GetType().Assembly.GetManifestResourceStream(embeddedPath);
            var decoder = new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

            return decoder.Frames[0];
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // nothing to clean up in this simple case
            return Result.Succeeded;
        }
    }


    //class for regular tree generation
    [TransactionAttribute(TransactionMode.Manual)]
    public class GenerateFractalTree : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get UIDocument
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            try
            {
                Logic logic = new Logic();
                logic.GenerateTree(logic.TreeRecursion, uidoc);
                return Result.Succeeded;
            }
            catch (Exception e)
            {
                message = e.Message;
                return Result.Failed;
            }
        }
    }


    //class for christmas tree generation
    [TransactionAttribute(TransactionMode.Manual)]
    public class GenerateFractalChristmasTree : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Get UIDocument
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            try
            {
                Logic logic = new Logic();
                logic.GenerateTree(logic.ChristmasRecursion, uidoc);
                return Result.Succeeded;
            }
            catch (Exception e)
            {
                message = e.Message;
                return Result.Failed;
            }
        }
    }

    public class Logic
    { 
        //tree generation wrapper to place it in Revit
        public void GenerateTree(Action<int, Curve, CurveArray, double, double> method, UIDocument uidoc)
        {
            //Get Document
            Document doc = uidoc.Document;

            //to mark if the generation should be executed
            bool flag = false;

            //check for appropriate views for generation, such a check is dictated by need to build sketchplanes
            if (doc.ActiveView.ViewType == ViewType.FloorPlan)
            {
                GlobVars.orientation = "horizontal";
                flag = true;
            }
            else if (doc.ActiveView.ViewType == ViewType.Section || doc.ActiveView.ViewType == ViewType.Elevation)
            {
                GlobVars.orientation = "vertical";
                flag = true;
            }
            else
            {
                GlobVars.orientation = "";
                TaskDialog.Show("FractalTreeGenerator", "Tree can be generated only on Floor plan, Elevation or Section");
            }

            if (flag == true)
            {
                //Pick referenceline
                Reference refLine = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element);

                //Retrieve Element
                ElementId lineId = refLine.ElementId;
                Element line = doc.GetElement(lineId);
                Curve crv = (line.Location as LocationCurve).Curve;

                //If an element was picked run tree generation
                if (refLine != null)
                {
                    //call recursive function
                    method(GlobVars.treeDepth, crv, GlobVars.treeCurves, GlobVars.rotAngle, GlobVars.lenFactor);

                    //Place generated lines in Revit
                    using (Transaction trans = new Transaction(doc, "Place Tree"))
                    {
                        trans.Start();

                        foreach (Curve i in GlobVars.treeCurves)
                        {
                            Plane plane = Plane.CreateByNormalAndOrigin(i.GetEndPoint(0).CrossProduct(i.GetEndPoint(1)), i.GetEndPoint(1));
                            SketchPlane sketchPlane = SketchPlane.Create(doc, plane);

                            //create geometry
                            doc.Create.NewModelCurve(i, sketchPlane);
                        }

                        trans.Commit();
                    }
                }
            }
            else
            {
                //reset all values that could be kept from before
                flag = false;
                GlobVars.treeCurves = new CurveArray();
                GlobVars.orientation = "";

                TaskDialog.Show("FractalTreeGenerator", "Please try in Floor plan, Section or Elevation");
            }
        }


        //Converts degree to radians
        public double ToRad(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        //generating a random val
        public int AddRandom(double value)
        {
            int result = GlobVars.random.Next(Convert.ToInt32(value - (value * GlobVars.rndCoof)),
                Convert.ToInt32(value + (value * GlobVars.rndCoof)));

            return result;
        }

        //get line end moved along it's axis
        public XYZ MoveAlongLine(double distance, Curve line)
        {
            //Get start and end points
            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);

            //Move line end point by distance
            XYZ result = end + (end - start).Normalize().Multiply(distance);
            return result;
        }

        //rotate a point along a reference point
        public XYZ RotatePoint(double angle, XYZ reference, XYZ toRotate)
        {
            XYZ result = null;
            //mathematical rotation in 2 dimentions
            if (GlobVars.orientation == "vertical")
            {
                result = new XYZ(
                    toRotate.X,
                    Math.Sin(angle) * (toRotate.Z - reference.Z) + Math.Cos(angle) * (toRotate.Y - reference.Y) + reference.Y,
                    Math.Cos(angle) * (toRotate.Z - reference.Z) - Math.Sin(angle) * (toRotate.Y - reference.Y) + reference.Z);
            }
            else
            {
                result = new XYZ(
                    Math.Cos(angle) * (toRotate.X - reference.X) - Math.Sin(angle) * (toRotate.Y - reference.Y) + reference.X,
                    Math.Sin(angle) * (toRotate.X - reference.X) + Math.Cos(angle) * (toRotate.Y - reference.Y) + reference.Y,
                    toRotate.Z);
            }

            return result;
        }

        //generate tree side branch
        public Curve SideBranch(double angle, Curve branch, double scaleFactor)
        {
            //Get length and adjust for branch
            double cLength = branch.Length;

            //Get end point of branch
            XYZ end = branch.GetEndPoint(1);

            //generate move distance
            double lengthRnd = AddRandom(cLength) * scaleFactor;

            //generate new branch
            XYZ pntMoved = MoveAlongLine(lengthRnd, branch);
            XYZ pntRotated = RotatePoint(angle, end, pntMoved);
            Line generatedBranch = Line.CreateBound(end, pntRotated);

            return generatedBranch;
        }

        //generate both side branches
        public void GenerateSideBranches(int depth, Curve branch, CurveArray listToFill, double rotationAngle, double scaleFactor,
            Action<int, Curve, CurveArray, double, double> method)
        {
            for (int i = 0; i < 2; i++)
            {
                //getting angle values
                double rotation = ToRad(AddRandom(rotationAngle));

                //generate a + and - rotations
                if (i == 1) rotation = -rotation;

                Curve newBranch = SideBranch(rotation, branch, scaleFactor);
                listToFill.Append(newBranch);

                //call next recursion
                method(depth - 1, newBranch, listToFill, rotationAngle, scaleFactor);
            }
        }


        //Tree recursive method
        public void TreeRecursion(int depth, Curve branch, CurveArray listToFill, double rotationAngle, double scaleFactor)
        {
            if (depth > 0)
            {
                //test for short length
                try
                {
                    branch.GetEndPoint(1);

                    //asssign recursion type
                    Action<int, Curve, CurveArray, double, double> recursionType = TreeRecursion;

                    GenerateSideBranches(depth, branch, listToFill, rotationAngle, scaleFactor, recursionType);
                }
                catch
                {
                    //simply return nothing
                }
            }
        }


        //Christmas tree recursive method
        public void ChristmasRecursion(int depth, Curve branch, CurveArray listToFill, double rotationAngle, double scaleFactor)
        {
            if (depth > 0)
            {
                try
                {
                    //Get end point of branch
                    XYZ end = branch.GetEndPoint(1);

                    //asssign recursion type
                    Action<int, Curve, CurveArray, double, double> recursionType = ChristmasRecursion;

                    //additional branch for christmas tree
                    //Get length and adjust for branch
                    double cLength = branch.Length;

                    //generate move distance
                    double lengthRnd = AddRandom(cLength) * GlobVars.lenFactor;

                    XYZ newPoint = MoveAlongLine(lengthRnd, branch);
                    Curve newBranch = Line.CreateBound(end, newPoint);

                    listToFill.Append(newBranch);

                    //call next recursion
                    ChristmasRecursion(depth - 1, newBranch, listToFill, rotationAngle, GlobVars.lenFactorChristmas);
                    GenerateSideBranches(depth, branch, listToFill, rotationAngle, GlobVars.lenFactorChristmas, recursionType);
                }
                catch
                { 
                    //simply return nothing
                }
            }
        }
    }
}