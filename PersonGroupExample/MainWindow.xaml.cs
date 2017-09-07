using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Linq;
using System.Globalization;
using PersonGroupExample;

namespace FaceTutorial
{
    public partial class MainWindow : Window
    {
        // Replace the first parameter with your valid subscription key.
        //
        // Replace or verify the region in the second parameter.
        //
        // You must use the same region in your REST API call as you used to obtain your subscription keys.
        // For example, if you obtained your subscription keys from the westus region, replace
        // "westcentralus" in the URI below with "westus".
        //
        // NOTE: Free trial subscription keys are generated in the westcentralus region, so if you are using
        // a free trial subscription key, you should not need to change this region.
        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("00d7358854144900955ef88f7f0b190b", "https://westus.api.cognitive.microsoft.com/face/v1.0");

        Face[] faces;                   // The list of detected faces.

        public MainWindow()
        {
            InitializeComponent();
            PopulateComboBox();
        }

        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
        }

        // Displays the image and calls Detect Faces.
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            double resizeX;
            double resizeY;
            // Get the image file to scan from the user.
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            // Return if canceled.
            if (!(bool)result)
            {
                return;
            }

            // Display the image file.
            string testImageFile = openDlg.FileName;

            Uri fileUri = new Uri(testImageFile);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Detect any faces in the image.
            Title = "Detecting...";
            faces = await UploadAndDetectFaces(testImageFile);
            Title = String.Format("Detection Finished. {0} face(s) detected", faces.Length);

            if (faces.Length > 0)
            {
                string personGroupId = "";

                if (comboBoxGroups.Text!="")
                    personGroupId = comboBoxGroups.Text;

                // Prepare to draw rectangles around the faces.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));

                resizeX = 96.0/bitmapSource.DpiX;
                resizeY = 96.0/bitmapSource.DpiY;

                for (int i = 0; i < faces.Length; ++i)
                {
                    Face face = faces[i];
                    var person = new Person();

                    // Draw a rectangle on the face.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2 * (96 / bitmapSource.DpiX)),
                        new Rect(
                            face.FaceRectangle.Left * resizeX,
                            face.FaceRectangle.Top * resizeY,
                            face.FaceRectangle.Width * resizeX,
                            face.FaceRectangle.Height * resizeY
                            )
                    );
                    
                    try
                    {

                        Guid[] thisFace = new Guid[1];
                        thisFace[0] = face.FaceId;

                        var identifyResult = await faceServiceClient.IdentifyAsync(personGroupId, thisFace);
                        if (identifyResult.Length != 0 && identifyResult[0].Candidates.Length != 0)
                        {
                            var candidateId = identifyResult[0].Candidates[0].PersonId;
                            person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);
                            drawingContext.DrawText(new FormattedText(person.Name, CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Klavika"), 25 * (96 / bitmapSource.DpiX), Brushes.Red), new Point(face.FaceRectangle.Left * resizeX, face.FaceRectangle.Top * resizeY));
                        }


                    }
                    catch (Exception ex)
                    {
                        Console.Write("Person not found, " + ex.Message);
                    }
                }

                drawingContext.Close();

                // Display the image with the rectangle around the face.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)((bitmapSource.PixelWidth / 96d) * bitmapSource.DpiX * resizeX),
                    (int)((bitmapSource.PixelHeight / 96d) * bitmapSource.DpiY * resizeY),
                    bitmapSource.DpiX,
                    bitmapSource.DpiY,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;
            }
        }

        // Uploads the image file and calls Detect Faces.

        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes =
                new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
                    return faces;
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }

        private void openForm_Click(object sender, RoutedEventArgs e)
        {
            StartupWindow startup = new StartupWindow();
            startup.Show();
            this.Close();
        }

        private async void PopulateComboBox()
        {
            string[] groups;

            PersonGroup[] personGroups = await faceServiceClient.ListPersonGroupsAsync();

            groups = personGroups.Select(groupId => groupId.PersonGroupId).ToArray();

            comboBoxGroups.ItemsSource = groups;
        }
    }
}