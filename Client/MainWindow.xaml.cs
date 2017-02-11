using Contract;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpClient tcpClient;
        string login;
        Dictionary<string, string> smiles;
        byte[] file;
        string fileExt;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }
        
        private void Initialize()
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            tcpClient = null;
            login = string.Empty;
            smiles = new Dictionary<string, string>();
            for (int i = 1; i <= 9; i++)
            {
                Button button = new Button();
                button.Height = 34;
                button.Background = Brushes.White;
                button.BorderThickness = new Thickness() { Left = 0, Right = 0, Top = 0, Bottom = 0 };
                button.BorderBrush = Brushes.White;
                smiles.Add("smile_0" + i, Directory.GetCurrentDirectory() + @"\smiles\smile_0" + i + ".png");
                button.Content = new Image() { Source = new ImageSourceConverter().ConvertFrom(smiles.Last().Value) as ImageSource, Tag = smiles.Last().Key };
                button.Click += Smile_Click;
                wrapSmilesInner.Children.Add(button);
            }
            btnSmile.Content = new Image() { Source = new ImageSourceConverter().ConvertFrom(Directory.GetCurrentDirectory() + @"\smiles\smile.png") as ImageSource };
            (lstvClients.ContextMenu.Items[0] as MenuItem).Icon = new Image() { Source = new ImageSourceConverter().ConvertFrom(Directory.GetCurrentDirectory() + @"\smiles\sendfile.png") as ImageSource, Width = 16 };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Authentification auth = new Authentification(this);
            auth.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            auth.ShowDialog();
            if (auth.DialogResult == false)
                this.Close();
            else
                Task.Factory.StartNew(ListenToServer, tcpClient);
        }

        public void SetTcpClient(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
        }

        public void SetLogin(string login)
        {
            this.login = login;
        }

        private void ListenToServer(Object obj)
        {
            var client = obj as TcpClient;
            bool isContinue = true;
            BinaryFormatter bf = new BinaryFormatter();
            while (isContinue)
            {
                var r = new BinaryReader(client.GetStream());
                switch ((Command)r.ReadByte())
                {
                    case Command.RefreshClientList:
                        List<string> lst = bf.Deserialize(tcpClient.GetStream()) as List<string>;
                        Application.Current.Dispatcher.Invoke(() => {
                            lstvClients.ItemsSource = lst;
                        });
                        break;
                    case Command.Receive:
                        ChatMessage message = bf.Deserialize(tcpClient.GetStream()) as ChatMessage;
                        Application.Current.Dispatcher.Invoke(() => {
                            FlowDocument newDoc = new FlowDocument();
                            newDoc.FontFamily = new FontFamilyConverter().ConvertFrom("Segoe UI") as FontFamily;
                            newDoc.FontSize = 12;
                            newDoc.PagePadding = new Thickness() { Left = 0, Right = 0, Bottom = 0, Top = 0 };
                            newDoc.Blocks.Add(new Paragraph());
                            newDoc.LineHeight = 20;
                            var inlines = (newDoc.Blocks.FirstBlock as Paragraph).Inlines;
                            for (int i = 0; i < message.Content.Count; i++)
                            {
                                if(message.Content[i].StartsWith("::smile::"))
                                {
                                    inlines.Add(new Image());
                                    var newImage = ((inlines.LastInline as InlineUIContainer).Child as Image);
                                    newImage.Source = new ImageSourceConverter().ConvertFrom(smiles[message.Content[i].Substring(9)]) as ImageSource;
                                    newImage.Height = 16;
                                    Thickness thick = new Thickness() { Left = 3, Top = 0, Right = 3, Bottom = -4 };
                                    newImage.Margin = thick;
                                    newImage.VerticalAlignment = VerticalAlignment.Bottom;
                                }
                                else
                                {
                                    inlines.Add(new Run(message.Content[i]));
                                }
                            }
                            lstChat.Items.Add(
                                new ChatListItem()
                                {
                                    messageDate = message.DateAndTime,
                                    messageSender = message.Sender,
                                    messageContent = newDoc
                                }
                            );
                        });
                        break;
                    case Command.ReceiveFile:
                        string fileSender = bf.Deserialize(tcpClient.GetStream()) as string;
                        fileExt = bf.Deserialize(tcpClient.GetStream()) as string;
                        file = bf.Deserialize(tcpClient.GetStream()) as byte[];
                        Application.Current.Dispatcher.Invoke(() => {
                            ChatListItem newItem = new ChatListItem()
                            {
                                messageDate = DateTime.Now.ToString(),
                                messageSender = fileSender,
                                messageContent = SendFileFlowDoc("sendfile")
                            };
                            lstChat.Items.Add(newItem);
                        });
                        break;
                    case Command.Error:
                        string error = bf.Deserialize(tcpClient.GetStream()) as string;
                        Application.Current.Dispatcher.Invoke(() => {
                            MessageBox.Show(error);
                        });
                        break;
                    case Command.Exit:
                        MessageBox.Show("Connection to Server terminated");
                        tcpClient.Close();
                        tcpClient = null;
                        Application.Current.Dispatcher.Invoke(() => {
                            Close();
                        });
                        break;
                }
            }
        }

        private void Content_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if ((((((sender as ListView).SelectedItem as ChatListItem).messageContent.Blocks.FirstBlock as Paragraph).Inlines.FirstInline as InlineUIContainer).Child as Image).Tag.ToString() == "sendfile")
                {
                    SaveFileDialog dlgSave = new SaveFileDialog();
                    dlgSave.CheckPathExists = true;
                    if (dlgSave.ShowDialog() != true)
                        return;
                    string fileName = dlgSave.FileName + fileExt;
                    using (FileStream fileStream = new FileStream(fileName, FileMode.Create, System.IO.FileAccess.Write))
                    {
                        fileStream.Write(file, 0, file.Length);
                    }
                }
            }
            catch(Exception exc)
            {
                return;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(tcpClient != null)
            {
                var bw = new BinaryWriter(tcpClient.GetStream());
                bw.Write((byte)Command.Exit);
            }
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            
            if ((txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines.Count == 0)
                return;
            
            var bw = new BinaryWriter(tcpClient.GetStream());
            bw.Write((byte)Command.Send);
            BinaryFormatter bf = new BinaryFormatter();
            
            var inlines = (txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines;
            string receiver = string.Empty;
            int i = 0;

            if (inlines.FirstInline.GetType() == typeof(Run))
            {
                string str = (inlines.FirstInline as Run).Text;
                Regex reg = new Regex(@"^\[to\s.*\]\:.*");
                if (reg.IsMatch(str))
                {
                    receiver = str.Substring(4, str.IndexOf("]:") - 4);
                    i = 1;
                }
            }
            
            List<string> lst = new List<string>();

            for (int block = 0; block < txtMessage.Document.Blocks.Count; block++)
            {
                if (txtMessage.Document.Blocks.ElementAt(block).GetType() != typeof(Paragraph))
                    continue;
                inlines = (txtMessage.Document.Blocks.ElementAt(block) as Paragraph).Inlines;
                for (; i < inlines.Count; i++)
                {
                    if (inlines.ElementAt(i).GetType() == typeof(Run))
                    {
                        if ((inlines.ElementAt(i) as Run).Text != "")
                            lst.Add((inlines.ElementAt(i) as Run).Text);
                    }
                    else if (inlines.ElementAt(i).GetType() == typeof(InlineUIContainer))
                    {
                        if ((inlines.ElementAt(i) as InlineUIContainer).Child.GetType() == typeof(Image))
                        {
                            if (((inlines.ElementAt(i) as InlineUIContainer).Child as Image).Tag == null)
                                continue;
                            lst.Add("::smile::" + ((inlines.ElementAt(i) as InlineUIContainer).Child as Image).Tag);
                        }
                    }
                    else if (inlines.ElementAt(i).GetType() == typeof(Span))
                    {
                        for (int j = 0; j < (inlines.ElementAt(i) as Span).Inlines.Count; j++)
                        {
                            lst.Add(((inlines.ElementAt(i) as Span).Inlines.ElementAt(j) as Run).Text);
                        }
                    }
                    else
                    {
                        continue;
                    }

                }
                i = 0;
                lst.Add("\n");
            }

            ChatMessage item = new ChatMessage()
            {
                DateAndTime = DateTime.Now.ToString(),
                Sender = login,
                Receiver = receiver,
                Content = lst
            };
            
            bf.Serialize(tcpClient.GetStream(), item);
            txtMessage.Document = new FlowDocument();
            txtMessage.Document.Blocks.Add(new Paragraph());
        }

        private void lstvClients_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines.Count == 0)
                (txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines.Add(new Run("[to " + lstvClients.SelectedItem.ToString() + "]: "));
            else
                (txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines.InsertBefore((txtMessage.Document.Blocks.FirstBlock as Paragraph).Inlines.FirstInline, new Run("[to " + lstvClients.SelectedItem.ToString() + "]: "));
        }

        private void btnSmile_Click(object sender, RoutedEventArgs e)
        {
            wrapSmiles.Visibility = (wrapSmiles.Visibility == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
        }

        private void Smile_Click(object sender, RoutedEventArgs e)
        {
            Image newImage = new Image();
            newImage.Source = ((sender as Button).Content as Image).Source;
            newImage.Tag = ((sender as Button).Content as Image).Tag;
            newImage.Height = 16;
            Thickness thick = new Thickness();
            thick.Left = 3;
            thick.Top = 0;
            thick.Right = 3;
            thick.Bottom = -4;
            newImage.Margin = thick;
            newImage.VerticalAlignment = VerticalAlignment.Bottom;
            new InlineUIContainer(newImage, txtMessage.CaretPosition.GetInsertionPosition(LogicalDirection.Forward));
            wrapSmiles.Visibility = Visibility.Hidden;
            if(txtMessage.CaretPosition.GetNextInsertionPosition(LogicalDirection.Forward) != null)
                txtMessage.CaretPosition = txtMessage.CaretPosition.GetNextInsertionPosition(LogicalDirection.Forward);
            txtMessage.Focus();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (lstvClients.SelectedItem == null)
                return;
            OpenFileDialog dlgOpen = new OpenFileDialog();
            dlgOpen.CheckFileExists = dlgOpen.CheckPathExists = true;
            if(dlgOpen.ShowDialog() != true)
                return;
            var bw = new BinaryWriter(tcpClient.GetStream());
            bw.Write((byte)Command.SendFile);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(tcpClient.GetStream(), lstvClients.SelectedItem);
            byte[] bytes;
            using (FileStream file = new FileStream(dlgOpen.FileName, FileMode.Open, FileAccess.Read))
            {
                bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
            }
            string ext = dlgOpen.FileName.Substring(dlgOpen.FileName.LastIndexOf("."));
            bf.Serialize(tcpClient.GetStream(), ext);
            bf.Serialize(tcpClient.GetStream(), bytes);

            ChatListItem newItem = new ChatListItem()
            {
                messageDate = DateTime.Now.ToString(),
                messageSender = login,
                messageContent = SendFileFlowDoc("")
            };
            lstChat.Items.Add(newItem);
        }

        private FlowDocument SendFileFlowDoc(string imageTag)
        {
            FlowDocument newDoc = new FlowDocument();
            newDoc.FontFamily = new FontFamilyConverter().ConvertFrom("Segoe UI") as FontFamily;
            newDoc.FontSize = 12;
            newDoc.PagePadding = new Thickness() { Left = 0, Right = 0, Bottom = 0, Top = 0 };
            newDoc.Blocks.Add(new Paragraph());
            newDoc.LineHeight = 30;
            var inlines = (newDoc.Blocks.FirstBlock as Paragraph).Inlines;

            Image newImage = new Image();
            inlines.Add(newImage);
            newImage.Source = new ImageSourceConverter().ConvertFrom(Directory.GetCurrentDirectory() + @"\smiles\sendfile.png") as ImageSource;
            newImage.Tag = imageTag;
            newImage.Height = 30;
            newImage.Margin = new Thickness() { Left = 3, Top = 0, Right = 3, Bottom = 0 };
            newImage.VerticalAlignment = VerticalAlignment.Top;
            return newDoc;
        }
    }

    public class ChatListItem
    {
        public string messageDate { set; get; }
        public string messageSender { set; get; }
        public FlowDocument messageContent { set; get; }
    }
}


