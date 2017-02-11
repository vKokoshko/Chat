using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract
{

    [Serializable]
    public class ChatMessage
    {
        public string DateAndTime { set; get; }
        public string Sender { set; get; }
        public string Receiver { set; get; }
        public List<string> Content { set; get; }
    }

    [Serializable]
    public class Client
    {
        public Guid id { get; set; }
        public string Login { get; set; }

        public Client()
        {
            id = Guid.Empty;
            Login = String.Empty;
        }
        
        public override string ToString()
        {
            return String.Format("id=\"{0}\"; login=\"{1}\"", id, Login);
        }

        public Stream GetStream()
        {
            throw new NotImplementedException();
        }
    }

    public enum Command : byte
    {
        CheckLogin,
        GetClients,
        RefreshClientList,
        Send,
        SendFile,
        ConfirmFileDelivery,
        ReceiveFile,
        Receive,
        Error,
        Exit
    }

    public enum State : byte
    {
        Started,
        Stopped
    }
}
