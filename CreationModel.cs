using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
            GetLevels(doc, out level1, out level2); //фильтруем уровни

            //формирование списка точек где будут стены
            //зададим размеры домика
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(6000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            Transaction transaction = new Transaction(doc);
            //название транзакции обязательно. Указывается вторым арг-ом или в методе Start
            transaction.Start("Построение домика");

            CreateWall(doc, level1, level2, points, walls); //создаем 4 стены
            AddDoor(doc, level1, walls[0]);//создаем дверь в стене 0
            AddWindow(doc, level1, walls[1]);//создаем окно в стене 1
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);
            AddRoof(doc, level2, walls);

            transaction.Commit();

            return Result.Succeeded;
        }
        //метод для создания крыши
        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                       .OfClass(typeof(RoofType))
                       .OfType<RoofType>()
                       .Where(x => x.Name.Equals("Типовой - 400мм"))
                       .Where(x => x.FamilyName.Equals("Базовая крыша"))
                       .FirstOrDefault();


            //на документе получаем свойство Application
            Application application = doc.Application;

            LocationCurve curve1 = walls[0].Location as LocationCurve;
            LocationCurve curve2 = walls[2].Location as LocationCurve;

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            XYZ point = curve1.Curve.GetEndPoint(0);
            XYZ point1 = curve1.Curve.GetEndPoint(1);
            XYZ point2 = curve2.Curve.GetEndPoint(0);
            XYZ point3 = curve2.Curve.GetEndPoint(1);
            double xPoint = point.X-2;
            double xPoint1 = point1.X+2;


            //зададим смещение по высоте и по ширине от стен для начального положения крыши
            double dh = level2.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
            XYZ offSet = new XYZ(-3, -3, dh);
            XYZ offSet1 = new XYZ(-3, 3, dh);

            //зададим высоту крыши
            double hightRoof = UnitUtils.ConvertToInternalUnits(2000, UnitTypeId.Millimeters);
            double h = dh+hightRoof;
            //пустой массив для отрезков по которым строится крыша
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(point + offSet, new XYZ(0, 0, h)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, h), point3 + offSet1));

            //создадим опорную плоскость перпендикулярно крыше
            ReferencePlane plane = doc.Create.NewReferencePlane2(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);

            //создадим крышу по двум отрезкам и зададим смещения от опорной плоскости xPoint, xPoint1
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, xPoint, xPoint1);

        }

        //метод для добавления окна
        private static void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol winType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_Windows)
                        .OfType<FamilySymbol>()
                        .Where(x => x.Name.Equals("0915 x 1830 мм"))
                        .Where(x => x.FamilyName.Equals("Фиксированные"))
                        .FirstOrDefault();

            //добавляем дверь в стену св-во Location у двери-точка, у стены-отрезок
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            //дверь установим в среднюю точку
            XYZ point = (point1 + point2) / 2;



            //Create.NewFamilyInstance нуждается, что бы элемент был активен в модели если нет то активировать
            if (!winType.IsActive) winType.Activate();
            FamilyInstance window = doc.Create.NewFamilyInstance(point, winType, wall, level1, StructuralType.NonStructural);

            Parameter offset = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            offset.Set(UnitUtils.ConvertToInternalUnits(650, UnitTypeId.Millimeters));


        }

        //метод для добавления двери
        private static void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .OfType<FamilySymbol>() //отберем только FamilySymbol, что бы было доступно FamilyName или Family.Name
                        .Where(x => x.Name.Equals("0915 x 2134 мм"))
                        .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))//where всегда возвращает коллекцию
                        .FirstOrDefault();

            //добавляем дверь в стену св-во Location у двери-точка, у стены-отрезок
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            //дверь установим в среднюю точку
            XYZ point = (point1 + point2) / 2;

            //Create.NewFamilyInstance нуждается, что бы дверь была активна в модели если нет то активировать
            if (!doorType.IsActive) doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);

        }


        //метод для создания 4х стен
        private static void CreateWall(Document doc, Level level1, Level level2, List<XYZ> points, List<Wall> walls)
        {

            //создаем 4 отрезка и по ним стены
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);

                //для высоты стены можно использовать перегрузку метода, а можно
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

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
