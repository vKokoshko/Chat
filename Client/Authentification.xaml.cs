using Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for Authentification.xaml
    /// </summary>
    public partial class Authentification : Window
    {
        MainWindow mainWindow;

        public Authentification(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            txtMessage.Opacity = 0;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if(txtLogin.Text.Count() == 0)
            {
                MessageBox.Show("Enter login!");
                return;
            }

            TcpClient newClient = new TcpClient();
            try
            {
                newClient.Connect(new IPAddress(new byte[] { 127, 0, 0, 1 }), 12345);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Error");
                newClient.Close();
                this.DialogResult = false;
                this.Close();
                return;
            }
            var bw = new BinaryWriter(newClient.GetStream());
            bw.Write((byte)Command.CheckLogin);

            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(newClient.GetStream(), txtLogin.Text);
            bool result = Convert.ToBoolean(bf.Deserialize(newClient.GetStream()));
            
            if (result)
            {
                mainWindow.SetTcpClient(newClient);
                mainWindow.SetLogin(txtLogin.Text);
                mainWindow.Title = "Chat Client: " + txtLogin.Text;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                bw.Close();
                newClient.Close();
                txtMessage.Opacity = 1;
                DoubleAnimation da = new DoubleAnimation(1, 0, new TimeSpan(0, 0, 0, 3));
                txtMessage.BeginAnimation(TextBlock.OpacityProperty, da);
            }
        }
    }
}
