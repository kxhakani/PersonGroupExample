using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using FaceTutorial;
using System.Data.SqlClient;

namespace PersonGroupExample
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class createGroup : Window
    {
        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("00d7358854144900955ef88f7f0b190b", "https://westus.api.cognitive.microsoft.com/face/v1.0");

        public createGroup()
        {
            InitializeComponent();
        }

        private async void btnCreateGroup_Click(object sender, RoutedEventArgs e)
        {
            // Create an empty person group

            try
            {
                await faceServiceClient.CreatePersonGroupAsync(txtPersonGroupID.Text, txtPersonGroupName.Text);
            }
            catch (FaceAPIException f)
            {
                txtMessage.Text = "Response status: " + f.ErrorMessage;
            }
            catch (Exception ex)
            {
                txtMessage.Text = ex.Message;
            }
            

        }

        private async void btnCreatePerson_Click(object sender, RoutedEventArgs e)
        {
            CreatePersonResult person = await faceServiceClient.CreatePersonAsync(
                // Id of the person group that the person belonged to
                txtExisitingPersonGroupID.Text,
                // Name of the person
                txtPersonName.Text
            );

            foreach (string imagePath in Directory.GetFiles(txtFolderURL.Text, "*.jpg"))
            {
                using (Stream s = File.OpenRead(imagePath))
                {

                    // Detect faces in the image and add to the Person
                    await faceServiceClient.AddPersonFaceAsync(txtExisitingPersonGroupID.Text, person.PersonId, s);
                }
            }

            //Call the training method
            await faceServiceClient.TrainPersonGroupAsync(txtExisitingPersonGroupID.Text);


            TrainingStatus trainingStatus = null;
            while (true)
            {
                trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(txtExisitingPersonGroupID.Text);

                if (trainingStatus.Status.ToString() != "running")
                {
                    break;
                }

                await Task.Delay(1000);
            }

            addToDatabase(txtPersonName.Text, txtPassword.Text, txtExisitingPersonGroupID.Text, person.PersonId.ToString());

            txtMessage2.Text = "Person: " + txtPersonName.Text + " was Successfully Created!";
        }

        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            //dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // Display the image file.
                txtFolderURL.Text = dialog.FileName;
            }
        }

        private void btnBackMain_Click(object sender, RoutedEventArgs e)
        {
            StartupWindow mainWindow = new StartupWindow();
            mainWindow.Show();
            this.Close();
        }

        private async void PopulateComboBox()
        {
            string[] groups;

            PersonGroup[] personGroups = await faceServiceClient.ListPersonGroupsAsync();

            groups = personGroups.Select(groupId => groupId.PersonGroupId).ToArray();

            txtExisitingPersonGroupID.ItemsSource = groups;
        }

        private void txtExisitingPersonGroupID_DropDownOpened(object sender, EventArgs e)
        {
            PopulateComboBox();
        }

        private void addToDatabase(string userName, string password, string PersonGroupId, string PersonId)
        {
            string connectionString = null;
            connectionString = "Data Source=BLAISNIWORK;Initial Catalog=TestingImageRec;Integrated Security=SSPI;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                String query = "INSERT INTO dbo.UserInformation (userName,userPassword,PersonGroupId,PersonId) VALUES (@userName, @userPassword, @PersonGroupId, @PersonId)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userName", userName);
                    command.Parameters.AddWithValue("@userPassword", password);
                    command.Parameters.AddWithValue("@PersonGroupId", PersonGroupId);
                    command.Parameters.AddWithValue("@PersonId", PersonId);

                    connection.Open();
                    int result = command.ExecuteNonQuery();

                    // Check Error
                    if (result < 0)
                        Console.WriteLine("Error inserting data into Database!");
                }
            }
            
        }
    }
}
