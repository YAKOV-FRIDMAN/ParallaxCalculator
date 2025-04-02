using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;


using System.Windows.Shapes;
using Emgu.CV.Reg;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ParallaxCalculator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point? point1 = null;
        private Point? point2 = null;

        private Point? detectedCenter1 = null;
        private Point? detectedCenter2 = null;


        private Point selectionStart1;
        private Rectangle selectionRect1;
        private bool isSelecting1 = false;

        private Point selectionStart2;
        private Rectangle selectionRect2;
        private bool isSelecting2 = false;


        private double currentZoom1 = 1.0;
        private double currentZoom2 = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.2;
        private const double MaxZoom = 5.0;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadImage1_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                Image1.Source = bitmap;
                Canvas1.Width = bitmap.PixelWidth;
                Canvas1.Height = bitmap.PixelHeight;
                Image1.Width = bitmap.PixelWidth;
                Image1.Height = bitmap.PixelHeight;
            }
        }

        private void LoadImage2_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                Image2.Source = bitmap;
                Canvas2.Width = bitmap.PixelWidth;
                Canvas2.Height = bitmap.PixelHeight;
                Image2.Width = bitmap.PixelWidth;
                Image2.Height = bitmap.PixelHeight;
            }
        }

        private void Image1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            selectionStart1 = e.GetPosition(Canvas1);


            // הסר ריבוע קודם
            if (selectionRect1 != null)
            {
                Canvas1.Children.Remove(selectionRect1);
                selectionRect1 = null;
            }

            // צור חדש תמיד
            selectionRect1 = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4 }
            };
            Canvas1.Children.Add(selectionRect1);

            Canvas.SetLeft(selectionRect1, selectionStart1.X);
            Canvas.SetTop(selectionRect1, selectionStart1.Y);
            selectionRect1.Width = 0;
            selectionRect1.Height = 0;
            isSelecting1 = true;
            Canvas1.CaptureMouse();
        }

        private void Canvas1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting1 || selectionRect1 == null) return;

            Point currentPoint = e.GetPosition(Canvas1);

            double x = Math.Min(currentPoint.X, selectionStart1.X);
            double y = Math.Min(currentPoint.Y, selectionStart1.Y);
            double w = Math.Abs(currentPoint.X - selectionStart1.X);
            double h = Math.Abs(currentPoint.Y - selectionStart1.Y);

            Canvas.SetLeft(selectionRect1, x);
            Canvas.SetTop(selectionRect1, y);
            selectionRect1.Width = w;
            selectionRect1.Height = h;
        }

        private void Canvas1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting1) return;

            isSelecting1 = false;
            Canvas1.ReleaseMouseCapture();

            double x = Canvas.GetLeft(selectionRect1);
            double y = Canvas.GetTop(selectionRect1);
            double w = selectionRect1.Width;
            double h = selectionRect1.Height;

            point1 = new Point(x + w / 2, y + h / 2);

            Canvas1.Children.Clear();
            System.Windows.Shapes.Ellipse marker = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Red
            };
            Canvas.SetLeft(marker, point1.Value.X - 5);
            Canvas.SetTop(marker, point1.Value.Y - 5);
            Canvas1.Children.Add(marker);

            // המרת מקור התמונה ל־Bitmap
            if (Image1.Source is BitmapSource bmpSource)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmpSource));

                using var ms = new System.IO.MemoryStream();
                encoder.Save(ms);
                using var bmp = new System.Drawing.Bitmap(ms);

                // קריאה לזיהוי האובייקט
                var rect = new Rect(x, y, w, h);
                var resultBitmap = DetectObject(bmp, rect);

                // המרה חזרה ל־BitmapSource (WPF)
                Image1.Source = ConvertBitmapToImageSource(resultBitmap.Image);

                // מציאת מרכז כובד מתוך ShiftedPoints
                if (resultBitmap.ShiftedPoints?.Length > 0)
                {
                    double centerX = resultBitmap.ShiftedPoints.Average(p => p.X);
                    double centerY = resultBitmap.ShiftedPoints.Average(p => p.Y);
                    detectedCenter1 = new Point(centerX, centerY);

                    // עדכון קנבס
                    Canvas1.Children.Clear();
                    marker = new System.Windows.Shapes.Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.Blue
                    };
                    Canvas.SetLeft(marker, centerX - 5);
                    Canvas.SetTop(marker, centerY - 5);
                    Canvas1.Children.Add(marker);
                }

            }

        }

        private void Image2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            selectionStart2 = e.GetPosition(Canvas2);
            if (selectionRect2 != null)
            {
                Canvas2.Children.Remove(selectionRect2);
                selectionRect2 = null;
            }

            selectionRect2 = new Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4 }
            };
            Canvas2.Children.Add(selectionRect2);

            Canvas.SetLeft(selectionRect2, selectionStart2.X);
            Canvas.SetTop(selectionRect2, selectionStart2.Y);
            selectionRect2.Width = 0;
            selectionRect2.Height = 0;
            isSelecting2 = true;
            Canvas2.CaptureMouse();
        }

        private void Canvas2_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting2 || selectionRect2 == null) return;

            System.Windows.Point currentPoint = e.GetPosition(Canvas2);

            double x = Math.Min(currentPoint.X, selectionStart2.X);
            double y = Math.Min(currentPoint.Y, selectionStart2.Y);
            double w = Math.Abs(currentPoint.X - selectionStart2.X);
            double h = Math.Abs(currentPoint.Y - selectionStart2.Y);

            Canvas.SetLeft(selectionRect2, x);
            Canvas.SetTop(selectionRect2, y);
            selectionRect2.Width = w;
            selectionRect2.Height = h;
        }

        private void Canvas2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting2) return;

            isSelecting2 = false;
            Canvas2.ReleaseMouseCapture();

            double x = Canvas.GetLeft(selectionRect2);
            double y = Canvas.GetTop(selectionRect2);
            double w = selectionRect2.Width;
            double h = selectionRect2.Height;

            point2 = new System.Windows.Point(x + w / 2, y + h / 2);

            Canvas2.Children.Clear();
            System.Windows.Shapes.Ellipse marker = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = System.Windows.Media.Brushes.Red
            };
            Canvas.SetLeft(marker, point2.Value.X - 5);
            Canvas.SetTop(marker, point2.Value.Y - 5);
            Canvas2.Children.Add(marker);

            if (Image2.Source is BitmapSource bmpSource)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmpSource));

                using var ms = new System.IO.MemoryStream();
                encoder.Save(ms);
                using var bmp = new System.Drawing.Bitmap(ms);

                // קריאה לזיהוי האובייקט
                var rect = new Rect(x, y, w, h);
                var resultBitmap = DetectObject(bmp, rect);

                // המרה חזרה ל־BitmapSource (WPF)
                Image2.Source = ConvertBitmapToImageSource(resultBitmap.Image);

                if (resultBitmap.ShiftedPoints?.Length > 0)
                {
                    double centerX = resultBitmap.ShiftedPoints.Average(p => p.X);
                    double centerY = resultBitmap.ShiftedPoints.Average(p => p.Y);
                    detectedCenter2 = new Point(centerX, centerY);

                    Canvas2.Children.Clear();
                  marker = new System.Windows.Shapes.Ellipse
                    {
                      Width = 10,
                      Height = 10,
                      Fill = Brushes.Blue
                    };
                    Canvas.SetLeft(marker, centerX - 5);
                    Canvas.SetTop(marker, centerY - 5);
                    Canvas2.Children.Add(marker);
                }
            }

        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (detectedCenter1 == null || detectedCenter2 == null)
            {
                ResultText.Text = "זיהוי לא הושלם בשתי התמונות.";
                return;
            }

            if (!double.TryParse(BaseLengthInput.Text, out double baseLength))
            {
                ResultText.Text = "הכנס מרחק בין נקודות התצפית (בס\"מ).";
                return;
            }

            double pixelDistance = Math.Abs(detectedCenter1.Value.X - detectedCenter2.Value.X);
            //  double degreesPerPixel = 60.0 / 1000.0;
            if (!double.TryParse(FOVInput.Text, out double horizontalFOV))
            {
                ResultText.Text = "הכנס שדה ראייה תקין.";
                return;
            }

            int imageWidth = ((BitmapSource)Image1.Source).PixelWidth;
            double degreesPerPixel = horizontalFOV / imageWidth;

            double angleDegrees = pixelDistance * degreesPerPixel;
            double halfAngleRadians = (angleDegrees / 2.0) * Math.PI / 180.0;
            double halfBase = baseLength / 2.0;

            double distanceToObject = halfBase / Math.Tan(halfAngleRadians);
            ResultText.Text = $"המרחק לאובייקט: {distanceToObject:F2} ס\"מ";
        }


        private DetectObjectResult DetectObject(System.Drawing.Bitmap fullImage, Rect rect)
        {
            DetectObjectResult detectObjectResult = new DetectObjectResult();
            // כל התמונה
            Image<Bgr, byte> img = fullImage.ToImage<Bgr, byte>();

            // ROI – האזור המסומן
            System.Drawing.Rectangle roi = new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            var roiImage = new Mat(img.Mat, roi); // גישה לאזור החתוך

            // עיבוד על ה־ROI בלבד
            Mat gray = new Mat();
            CvInvoke.CvtColor(roiImage, gray, ColorConversion.Bgr2Gray);

            Mat blurred = new Mat();
            CvInvoke.GaussianBlur(gray, blurred, new System.Drawing.Size(5, 5), 0);

            Mat edges = new Mat();
            CvInvoke.Canny(blurred, edges, 100, 200);

            // קונטורים מתוך ה־ROI
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(edges, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            // ציור הקונטורים – על כל התמונה, אחרי התאמת הקואורדינטות למיקום בתוך התמונה
            for (int i = 0; i < contours.Size; i++)
            {
                using var originalContour = contours[i];

                // צור רשימה חדשה של נקודות עם תיקון מיקום לפי ה־ROI
                var shiftedPoints = originalContour.ToArray()
                    .Select(p => new System.Drawing.Point(p.X + roi.X, p.Y + roi.Y))
                    .ToArray();
                detectObjectResult.ShiftedPoints = shiftedPoints;
                var shiftedContour = new VectorOfPoint(shiftedPoints);

                CvInvoke.DrawContours(img, new VectorOfVectorOfPoint(shiftedContour), -1, new MCvScalar(0, 0, 255), 2);
            }

            detectObjectResult.Image = img.ToBitmap();
            return detectObjectResult; // מחזיר את כל התמונה עם קונטור מסומן
        }



        public static Mat BitmapToMat(System.Drawing.Bitmap bitmap)
        {
            return bitmap != null ? BitmapExtension.ToMat(bitmap) : null;
        }



        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        public static ImageSource ConvertBitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();

            try
            {
                ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                return wpfBitmap;
            }
            finally
            {
                DeleteObject(hBitmap); // Always delete the GDI object to avoid memory leaks
            }
        }

        private void btnZoomInnImg1_Click(object sender, RoutedEventArgs e)
        {
            currentZoom1 = Math.Min(currentZoom1 + ZoomStep, MaxZoom);
            ZoomTransform1.ScaleX = currentZoom1;
            ZoomTransform1.ScaleY = currentZoom1;
        }

        private void btnZoomOutImg1_Click(object sender, RoutedEventArgs e)
        {
            currentZoom1 = Math.Max(currentZoom1 - ZoomStep, MinZoom);
            ZoomTransform1.ScaleX = currentZoom1;
            ZoomTransform1.ScaleY = currentZoom1;
        }

        private void btnZoomInnImg2_Click(object sender, RoutedEventArgs e)
        {
            currentZoom2 = Math.Min(currentZoom2 + ZoomStep, MaxZoom);
            ZoomTransform2.ScaleX = currentZoom2;
            ZoomTransform2.ScaleY = currentZoom2;
        }

        private void btnZoomOutImg2_Click(object sender, RoutedEventArgs e)
        {
            currentZoom2 = Math.Max(currentZoom2 - ZoomStep, MinZoom);
            ZoomTransform2.ScaleX = currentZoom2;
            ZoomTransform2.ScaleY = currentZoom2;
        }




    }

    class DetectObjectResult : IDisposable
    {
        public System.Drawing.Bitmap Image { get; set; }

        public System.Drawing.Point[] ShiftedPoints { get; set; }

        public void Dispose()
        {
            Image?.Dispose();
        }
    }
}
