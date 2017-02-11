using Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
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

namespace ServerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpListener listener;
        BinaryFormatter binaryFormatter;
        Clients clients;
        State state;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            listener = new TcpListener(new IPAddress(new byte[] { 127, 0, 0, 1 }), 12345);
            binaryFormatter = new BinaryFormatter();
            clients = new Clients();
            state = State.Stopped;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(state != State.Stopped)
                Stop();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => { Start(); });
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        public void Start()
        {
            state = State.Started;
            listener.Start();
            Application.Current.Dispatcher.Invoke(() => {
                lstSystemConsole.Items.Add("Server started");
            });
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Factory.StartNew(ClientManager, client);
            }
        }

        public void Stop()
        {
            state = State.Stopped;
            listener.Stop();
            for (int i = 0; i < clients.GetListOfClients().Count; i++)
            {
                TcpClient cur = clients.GetTcpClient(i);
                var bw = new BinaryWriter(cur.GetStream());
                bw.Write((byte)Command.Exit);
            }
            lstSystemConsole.Items.Add("Server stopped");
            Initialize();
        }

        private void ClientManager(Object obj)
        {
            Tuple<TcpClient, Client> client = new Tuple<TcpClient, Client>(obj as TcpClient, new Client());
            Application.Current.Dispatcher.Invoke(() => {
                lstSystemConsole.Items.Add("New client manager started");
            });
            bool isContinue = true;
            BinaryFormatter bf = new BinaryFormatter();
            while (isContinue)
            {
                var r = new BinaryReader(client.Item1.GetStream());
                switch ((Command)r.ReadByte())
                {
                    case Command.CheckLogin:
                        string login = bf.Deserialize(client.Item1.GetStream()) as string;
                        client.Item2.id = Guid.NewGuid();
                        client.Item2.Login = login;
                        bool result = clients.AddClient(client);
                        bf.Serialize(client.Item1.GetStream(), result);
                        if (result)
                            RefreshClientList();
                        else
                        {
                            client.Item1.Close();
                            isContinue = false;
                        }
                        Application.Current.Dispatcher.Invoke(() => {
                            lstSystemConsole.Items.Add("Command.CheckLogin executed [returned value: " + result + "]");
                        });
                        break;
                    case Command.Send:

                        ChatMessage message = bf.Deserialize(client.Item1.GetStream()) as ChatMessage;

                        if (message.Receiver == string.Empty)
                        {
                            SendAll(message);
                        }
                        else
                        {
                            Tuple<TcpClient, Client> receiver = clients.GetClientByLogin(message.Receiver);
                            if (receiver == null)
                            {
                                var bw = new BinaryWriter(client.Item1.GetStream());
                                bw.Write((byte)Command.Error);
                                binaryFormatter.Serialize(client.Item1.GetStream(), "Recipient not found");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    lstSystemConsole.Items.Add("Command.Send executed with error [Recipient not found]");
                                });
                            }
                            else
                            {
                                var bw = new BinaryWriter(receiver.Item1.GetStream());
                                message.Content.Insert(0, "[private] ");
                                bw.Write((byte)Command.Receive);
                                binaryFormatter.Serialize(receiver.Item1.GetStream(), message);
                                bw = new BinaryWriter(client.Item1.GetStream());
                                if (message.Sender != message.Receiver)
                                {
                                    bw.Write((byte)Command.Receive);
                                    binaryFormatter.Serialize(client.Item1.GetStream(), message);
                                }
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    lstSystemConsole.Items.Add("Command.SendTo executed");
                                });
                            }
                        }
                        break;
                    case Command.SendFile:

                        string fileReceiver = bf.Deserialize(client.Item1.GetStream()) as string;
                        string ext = bf.Deserialize(client.Item1.GetStream()) as string;
                        byte[] fileTmp = bf.Deserialize(client.Item1.GetStream()) as byte[];
                        Tuple<TcpClient, Client> freceiver = clients.GetClientByLogin(fileReceiver);
                        BinaryWriter bwr = new BinaryWriter(freceiver.Item1.GetStream());
                        bwr.Write((byte)Command.ReceiveFile);
                        bf.Serialize(freceiver.Item1.GetStream(), client.Item2.Login);
                        bf.Serialize(freceiver.Item1.GetStream(), ext);
                        bf.Serialize(freceiver.Item1.GetStream(), fileTmp);
                        break;
                    case Command.Exit:
                        client.Item1.Close();
                        isContinue = false;
                        clients.RemoveClient(client.Item2.id);

                        List<string> list = clients.GetListOfClients();
                        foreach (var i in list)
                            Console.WriteLine(i);

                        RefreshClientList();
                        Application.Current.Dispatcher.Invoke(() => {
                            lstSystemConsole.Items.Add("Command.Exit executed");
                        });
                        break;
                }
            }
            Application.Current.Dispatcher.Invoke(() => {
                lstSystemConsole.Items.Add("Client manager closed");
            });
        }

        private void RefreshClientList()
        {
            for (int i = 0; i < clients.GetListOfClients().Count; i++)
            {
                TcpClient cur = clients.GetTcpClient(i);
                List<string> list = clients.GetListOfClients();
                var bw = new BinaryWriter(cur.GetStream());
                bw.Write((byte)Command.RefreshClientList);
                binaryFormatter.Serialize(cur.GetStream(), list);
            }
            Application.Current.Dispatcher.Invoke(() => {
                lstSystemConsole.Items.Add("Command.RefreshClientList executed");
            });
        }

        private void SendAll(ChatMessage message)
        {
            for (int i = 0; i < clients.GetListOfClients().Count; i++)
            {
                TcpClient cur = clients.GetTcpClient(i);
                var bw = new BinaryWriter(cur.GetStream());
                bw.Write((byte)Command.Receive);
                binaryFormatter.Serialize(cur.GetStream(), message);
            }
            Application.Current.Dispatcher.Invoke(() => {
                lstSystemConsole.Items.Add("Command.SendAll executed");
            });
        }
    }

    class Clients
    {
        List<Tuple<TcpClient, Client>> clientList;

        public Clients()
        {
            clientList = new List<Tuple<TcpClient, Client>>();
        }

        public List<string> GetListOfClients()
        {
            List<string> lst = new List<string>();
            foreach (var item in clientList)
            {
                lst.Add(item.Item2.Login);
            }
            return lst;
        }

        public bool AddClient(Tuple<TcpClient, Client> client)
        {
            if (clientList.Find(t => t.Item2.Login.ToLower() == client.Item2.Login.ToLower()) == null)
            {
                clientList.Add(client);
                return true;
            }
            else
                return false;
        }

        public void RemoveClient(Guid id)
        {
            clientList.Remove(clientList.Where(t => t.Item2.id == id).FirstOrDefault());
        }

        public TcpClient GetTcpClient(int index)
        {
            return clientList.ElementAt(index).Item1;
        }

        public Tuple<TcpClient, Client> GetClientByLogin(string login)
        {
            return clientList.Find(t => t.Item2.Login.ToLower() == login.ToLower());
        }
    }
}