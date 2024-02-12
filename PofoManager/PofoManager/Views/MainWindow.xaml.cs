using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

using PofoManager.ViewModels;

namespace PofoManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMessageTarget
    {
        /// <summary>
        /// The view model
        /// </summary>
        private readonly MainViewModel viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Application.Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            viewModel = new MainViewModel(this);
            DataContext = viewModel;

            // Apppend newline after version text
            Log.AppendText(" " + App.Version.ToString(3) + "\r\n");
            Log.ScrollToEnd();
        }

        /// <summary>
        /// Tasks the scheduler unobserved task exception.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnobservedTaskExceptionEventArgs"/> instance containing the event data.</param>
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            if (e.Exception.Flatten().InnerExceptions.All(ex => ex is TaskCanceledException)) return;
            Dispatcher.InvokeAsync(() =>
            {
                foreach (var exception in e.Exception.Flatten().InnerExceptions)
                {
                    if (exception is TaskCanceledException) continue;
                    MessageBox.Show(this, e.Exception.Message, "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        /// <summary>
        /// Handles the DispatcherUnhandledException event of the Application control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Threading.DispatcherUnhandledExceptionEventArgs"/> instance containing the event data.</param>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (e.Exception is TaskCanceledException) return;
            Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(this, e.Exception.Message, "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        /// <summary>
        /// Handles the Click event of the OpenFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                SendDialog.ShowDialog(this, viewModel.Arduino, openFileDialog.FileName); 
            }
        }

        /// <summary>
        /// Handles the Click event of the OpenFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void RetrieveFile_Click(object sender, RoutedEventArgs e)
        {
            RetrieveDialog.ShowDialog(this, viewModel.Arduino);
        }

        /// <summary>
        /// Handles the Click event of the Clear control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Log.Clear();
        }

        /// <summary>
        /// Handles the Click event of the Exit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Handles the Click event of the Connect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.Connect().ConfigureAwait(false);
        }

        /// <summary>
        /// Handles the Click event of the Disconnect control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Disconnect();
        }

        /// <summary>
        /// Handles the Click event of the Ping control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void Ping_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.Arduino.Ping().ConfigureAwait(false);
        }

        /// <summary>
        /// Write the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        void IMessageTarget.Write(string message)
        {
            Dispatcher.BeginInvoke(() => {
                Log.AppendText(message);
                Log.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Handles the Click event of the MenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void ListFiles_Click(object sender, RoutedEventArgs e)
        {
            ListDialog.ShowDialog(this, viewModel.Arduino);
        }

        /// <summary>
        /// Handles the Drop event of the Window control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!viewModel.IsConnected) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                SendDialog.ShowDialog(this, viewModel.Arduino, files[0]);
            }
        }

        /// <summary>
        /// Handles the PreviewDragOver event of the Log control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        private void Log_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (!viewModel.IsConnected) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled |= true;
            }
        }

        /// <summary>
        /// Handles the Click event of the About control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var about = new About();
            about.Owner = this;
            about.ShowDialog();
        }

        /// <summary>
        /// Handles the Click event of the UploadFirmware control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void UploadFirmware_Click(object sender, RoutedEventArgs e)
        {
            UploadFirmware.ShowDialog(this, viewModel);
        }
    }
}
