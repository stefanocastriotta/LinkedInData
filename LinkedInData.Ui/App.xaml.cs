using System.Windows;
using GalaSoft.MvvmLight.Threading;
using CefSharp;
using System.Net;
using System.Net.Security;

namespace LinkedInData.Ui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            DispatcherHelper.Initialize();

            var settings = new CefSettings();
            settings.IgnoreCertificateErrors = true;

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);

            ServicePointManager.ServerCertificateValidationCallback =
           new RemoteCertificateValidationCallback(
                delegate
                { return true; }
            );

        }
    }
}
