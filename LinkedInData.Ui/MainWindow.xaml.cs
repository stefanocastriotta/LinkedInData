using System.Windows;
using LinkedInData.Ui.ViewModel;
using GalaSoft.MvvmLight.Messaging;
using System;
using Microsoft.Win32;

namespace LinkedInData.Ui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += (s, e) => ViewModelLocator.Cleanup();
            Messenger.Default.Register<NotificationMessage>(this, (message) => NavigateMessageHandler(message));
            Messenger.Default.Register<NotificationMessage<OperationResult>>(this, (message) => OperationResultMessageHandler(message));
        }

        private void NavigateMessageHandler(NotificationMessage message)
        {
            Browser.Address = message.Notification;
        }

        private void OperationResultMessageHandler(NotificationMessage<OperationResult> message)
        {
            if (message.Content.Result)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.DefaultExt = ".xlsx"; // Default file extension
                saveFileDialog.Filter = "Excel document (.xlsx)|*.xlsx"; // Filter files by extension

                if (saveFileDialog.ShowDialog() == true)
                    System.IO.File.WriteAllBytes(saveFileDialog.FileName, message.Content.Content);
            }
            else
            {
                MessageBox.Show("Errore durante l'elaborazione", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}