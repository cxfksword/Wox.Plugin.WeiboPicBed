using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wox.Plugin.WeiboPicBed
{
    /// <summary>
    /// Settings.xaml 的交互逻辑
    /// </summary>
    public partial class Settings : UserControl
    {
        private IPublicAPI woxAPI;

        public Settings(IPublicAPI woxAPI)
        {
            this.woxAPI = woxAPI;
            InitializeComponent();
        }

        private void Settings_Loaded(object sender, RoutedEventArgs e)
        {
            tbAccount.Text = SettingStorage.Instance.Account ?? "";
            tbPassword.Password = Utils.Decrypt(SettingStorage.Instance.Password ?? "");
        }

        private void tbAccount_LostFocus(object sender, RoutedEventArgs e)
        {
            SettingStorage.Instance.Account = tbAccount.Text;
            SettingStorage.Instance.Save();
        }

        private void tbPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            SettingStorage.Instance.Password = Utils.Encrypt(tbPassword.Password);
            SettingStorage.Instance.Save();
        }


    }
}
