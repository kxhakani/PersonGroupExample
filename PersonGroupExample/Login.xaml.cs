using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PersonGroupExample
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("00d7358854144900955ef88f7f0b190b", "https://westus.api.cognitive.microsoft.com/face/v1.0");

        public Login()
        {
            InitializeComponent();
        }

        private async void searchImage_Click(object sender, RoutedEventArgs e)
        {
            double resizeX;
            double resizeY;

            Title = "Authenticating..";

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
            txtLoginImage.Text = testImageFile;

            Uri fileUri = new Uri(testImageFile);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            displayImage.Source = bitmapSource;

            Face[] faces = await UploadAndDetectFaces(testImageFile);

            //Draw Box on Face?
            // Prepare to draw rectangles around the faces.
            DrawingVisual visual = new DrawingVisual();
            DrawingContext drawingContext = visual.RenderOpen();
            drawingContext.DrawImage(bitmapSource,
                new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));

            resizeX = 96.0 / bitmapSource.DpiX;
            resizeY = 96.0 / bitmapSource.DpiY;

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
            displayImage.Source = faceWithRectBitmap;

            //Only one face can be tested against the API
            VerifyResult autheticated = null;
            if (faces.Count() == 1)
            {
                autheticated = await verifyUser(txtUserName.Text, faces[0]);
            }

            if (autheticated != null)
            {
                if (autheticated.IsIdentical)
                {
                    Title = "Is Authenticated";
                    MessageBox.Show("User is Verified");
                }
            }
        }

        private async Task<VerifyResult> verifyUser(string userName, Face face)
        {
            string PersonId = "";
            string connectionString = null;
            connectionString = "Data Source=BLAISNIWORK;Initial Catalog=TestingImageRec;Integrated Security=SSPI;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                String query = "SELECT * FROM dbo.UserInformation WHERE userName=@userName;";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userName", userName);
                   
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PersonId = reader["PersonId"].ToString();
                        }
                    }
                }
            }

            VerifyResult result = null;
            if (PersonId!="")
                result = await faceServiceClient.VerifyAsync(face.FaceId, comboLoginGroup.Text, Guid.Parse(PersonId));

            return result;

        }

        private async void PopulateComboBox()
        {
            string[] groups;

            PersonGroup[] personGroups = await faceServiceClient.ListPersonGroupsAsync();

            groups = personGroups.Select(groupId => groupId.PersonGroupId).ToArray();

            comboLoginGroup.ItemsSource = groups;
        }

        private void comboLoginGroup_DropDownOpened(object sender, EventArgs e)
        {
            PopulateComboBox();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            StartupWindow startup = new StartupWindow();
            startup.Show();
            this.Close();
        }

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
    }
}
