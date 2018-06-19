using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace PingWatcher
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields
        public static Thread t = new Thread(() => PingThread());
        public static MainWindow m_MainWindow;
        public static TextBox m_TbNotification;
        public static TextBox m_TbAvgPingOut;
        public static TextBox m_TbTimeOutsOut;
        public static TextBox m_TbAvgPingGateway;
        public static TextBox m_TbTimeOutsGateway;
        public static Int32 m_Interval;
        public static ManualResetEvent _suspendEvent = new ManualResetEvent(true);
        public static IPAddress m_GatewayAddress;
        public static IPAddress m_targetAddress;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            m_GatewayAddress = GetDefaultGateway();
            tbGatewayIp.Text = m_GatewayAddress.ToString();
            m_targetAddress = IPAddress.Parse(tbTargetIp.Text);
            m_MainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            m_TbNotification = m_MainWindow.tbNotifications;
            m_TbAvgPingOut = m_MainWindow.tbAvgPingOut;
            m_TbTimeOutsOut = m_MainWindow.tbTimeOutsOut;
            m_TbAvgPingGateway = m_MainWindow.tbAvgPingGateway;
            m_TbTimeOutsGateway = m_MainWindow.tbTimeOutsGateway;
            m_Interval = Convert.ToInt16(tbInterval.Text) * 1000;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            if (t.ThreadState == ThreadState.Unstarted)
            {
                t.Start();
            }
            else
            {
                _suspendEvent.Set();
                tbNotifications.Text += "Ping thread resumed\r\n";
            }            
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStop.IsEnabled = false;
            btnStart.IsEnabled = true;

            _suspendEvent.Reset();
            WriteToNotificationWindow("Ping thread stopped");
        }

        public static IPAddress GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .Where(a => a != null)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                .FirstOrDefault();
        }

        public static IPAddress ParseTargetIp(string address)
        {
            return IPAddress.Parse(address);
        }

        public static bool IsIpAddress(string address)
        {
            if (IPAddress.TryParse(address, out IPAddress ipAddress))
            {
                if (address.Count(x => x == '.') == 3) return true;
            }
            return false;
        }

        public static bool IsValidInterval(string interval)
        {
            if(Int16.TryParse(interval,out short result))
            {
                if (Convert.ToInt16(interval) < 1 | Convert.ToInt16(interval) > 60) return false;
                else return true;
            }

            return false;
        }

        private void tbTargetIp_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsIpAddress(tbTargetIp.Text))
            {
                btnStart.IsEnabled = false;
                tbNotifications.Text += "Entered Target IP is no valid IPv4 address";
            }
            else
            {
                btnStart.IsEnabled = true;
                tbNotifications.Clear();
                m_targetAddress = IPAddress.Parse(tbTargetIp.Text);
            }
        }

        private void tbGatewayIp_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsIpAddress(tbGatewayIp.Text))
            {
                btnStart.IsEnabled = false;
                tbNotifications.Text += "Entered Gateway IP is no valid IPv4 address";
            }
            else
            {
                btnStart.IsEnabled = true;
                tbNotifications.Clear();
                m_GatewayAddress = IPAddress.Parse(tbGatewayIp.Text);
            }
        }

        private void tbInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsValidInterval(tbInterval.Text))
            {                
                btnStart.IsEnabled = false;
                tbNotifications.Text += "Enter ping interval between 1 and 60 seconds.";
            }
            else
            {
                btnStart.IsEnabled = true;
                tbNotifications.Clear();
            }       
        }

        public static void WriteToNotificationWindow(string message)
        {
            Application.Current.Dispatcher.Invoke(() => m_TbNotification.AppendText(message + "\r\n"));
            Application.Current.Dispatcher.Invoke(() => m_TbNotification.ScrollToEnd());
        }

        public static void PingThread()
        {
            WriteToNotificationWindow("Ping thread started");
            while (true)
            {
                _suspendEvent.WaitOne(Timeout.Infinite);
                long interval = m_Interval;
                PingReply target = PingTarget();
                PingReply gateway = PingGateway();
                interval = interval - target.RoundtripTime - gateway.RoundtripTime;
                if (interval < 0) interval = 0;
                string message = DateTime.Now.ToString("G") + "\r\nTarget:\r\nStatus: " + target.Status.ToString() + "\tPing: " + target.RoundtripTime.ToString() + "\r\nGateway:\r\nStatus: " + gateway.Status.ToString() + "\tPing: " + gateway.RoundtripTime.ToString();
                try
                {
                    DbConnection.WriteToDb(target, gateway);
                    WriteToNotificationWindow(message);
                    UpdateMainWindow();
                }
                catch(Exception ex)
                {
                    WriteToNotificationWindow(ex.Message);
                }
                Thread.Sleep(Convert.ToInt32(interval));
            }
        }

        public static PingReply PingTarget()
        {
            Ping sender = new Ping();
            PingReply reply = sender.Send(m_targetAddress);
            return reply;
        }

        public static PingReply PingGateway()
        {
            Ping sender = new Ping();
            PingReply reply = sender.Send(m_GatewayAddress);
            return reply;
        }

        public static void UpdateMainWindow()
        {
            Commons.PingInformation info = DbConnection.PingInformation(m_targetAddress.ToString(), m_GatewayAddress.ToString());
            Application.Current.Dispatcher.Invoke(() => m_TbAvgPingGateway.Text = info.GatewayPing.ToString());
            Application.Current.Dispatcher.Invoke(() => m_TbTimeOutsGateway.Text = info.GatewayTimeouts.ToString());
            Application.Current.Dispatcher.Invoke(() => m_TbAvgPingOut.Text = info.TargetPing.ToString());
            Application.Current.Dispatcher.Invoke(() => m_TbTimeOutsOut.Text = info.TargetTimeouts.ToString());
        }
    }
}
