using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LuckDraw
{
    public class NetworkStatusIndicator : TextBlock
    {
        public NetworkStatusIndicator()
        {
            Text = "无网络";
            if (!NetworkInterface.GetIsNetworkAvailable())
                this.Visibility = System.Windows.Visibility.Visible;
            else
                this.Visibility = System.Windows.Visibility.Collapsed;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;

        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => {
                if (e.IsAvailable)
                {
                    this.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                    this.Visibility = System.Windows.Visibility.Visible;
            }));

        }
    }
}
