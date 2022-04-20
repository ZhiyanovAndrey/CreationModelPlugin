using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<Wall> walls = new List<Wall>(); //чистый список для внесения стен
            Level level1, level2;


            //формирование списка точек где будут стены
            //зададим размеры домика
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            GetLevels(doc, out level1, out level2); //фильтруем уровни
            CreateWall(doc, level1, level2, points, walls); //создаем 4 стены



            return Result.Succeeded;
        }

        //метод для создания 4х стен
        private static void CreateWall(Document doc, Level level1, Level level2, List<XYZ> points, List<Wall> walls)
        {
            Transaction transaction = new Transaction(doc);
            //название транзакции обязательно. Указывается вторым арг-ом или в методе Start
            transaction.Start("Построение стен");
            //создаем 4 отрезка и по ним стены
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);

                //для высоты стены можно использовать перегрузку метода, а можно
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            transaction.Commit();
        }


        private static void GetLevels(Document doc, out Level level1, out Level level2)
        {
            //общая часть фильтрации уровней
            List<Level> listLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .OfType<Level>()
                    .ToList();

            //фильтр для уровня 1
            level1 = listLevels
                     .Where(x => x.Name.Equals("Уровень 1"))
                     .FirstOrDefault();

            //фильтр для уровня 2
            level2 = listLevels
                     .Where(x => x.Name.Equals("Уровень 2"))
                     .FirstOrDefault();
        }
    }
}
