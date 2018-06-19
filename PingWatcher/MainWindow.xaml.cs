using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

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
        public static System.Windows.Controls.TextBox m_TbNotification;
        public static System.Windows.Controls.TextBox m_TbAvgPingOut;
        public static System.Windows.Controls.TextBox m_TbTimeOutsOut;
        public static System.Windows.Controls.TextBox m_TbAvgPingGateway;
        public static System.Windows.Controls.TextBox m_TbTimeOutsGateway;
        public static long m_Interval;
        public static ManualResetEvent _suspendEvent = new ManualResetEvent(true);
        public static IPAddress m_GatewayAddress;
        public static IPAddress m_targetAddress;
        public static NotifyIcon ni;
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            ni = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(@"C:\Users\Computer\Documents\Visual Studio 2017\Projects\PingWatcher\PingWatcher\Images\147227.ico"),
                Visible = true
            };
            ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };
            ni.MouseClick += new MouseEventHandler(TrayIconClicked);
            
            m_GatewayAddress = GetDefaultGateway();
            tbGatewayIp.Text = m_GatewayAddress.ToString();
            m_targetAddress = IPAddress.Parse(tbTargetIp.Text);
            m_MainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            m_TbNotification = m_MainWindow.tbNotifications;
            m_TbAvgPingOut = m_MainWindow.tbAvgPingOut;
            m_TbTimeOutsOut = m_MainWindow.tbTimeOutsOut;
            m_TbAvgPingGateway = m_MainWindow.tbAvgPingGateway;
            m_TbTimeOutsGateway = m_MainWindow.tbTimeOutsGateway;
            m_Interval = Convert.ToInt16(tbInterval.Text) * 1000;
        }

        void TrayIconClicked(object sender, MouseEventArgs e)
        {
            if (e.Button==MouseButtons.Right)
            {
                //System.Windows.Controls.ContextMenu menu = this.FindResource("TrayIconContextMenu") as ContextMenu;
                
            }
        }

        private void Click_Exit(object sender, RoutedEventArgs e)
        {
            _suspendEvent.Reset();
            WindowState = WindowState.Normal; 
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) this.Hide();
            base.OnStateChanged(e);
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
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbNotification.AppendText(message + "\r\n"));
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbNotification.ScrollToEnd());
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
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbAvgPingGateway.Text = info.GatewayPing.ToString());
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbTimeOutsGateway.Text = info.GatewayTimeouts.ToString());
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbAvgPingOut.Text = info.TargetPing.ToString());
            System.Windows.Application.Current.Dispatcher.Invoke(() => m_TbTimeOutsOut.Text = info.TargetTimeouts.ToString());
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
