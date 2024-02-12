using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PofoManager.ViewModels
{
    /// <summary>
    /// View model for the send file dialog
    /// </summary>
    /// <seealso cref="PofoManager.ViewModels.TransferViewModel" />
    public class SendViewModel : TransferViewModel
    {
        /// <summary>
        /// The default destination path
        /// </summary>
        private static string DefaultDestinationPath = "C:\\";

        /// <summary>
        /// Gets the file to send
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string DestinationPath
        {
            get => GetProperty<string>();
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets or sets a value indicating whether to overwrite the file on the Portfolio.
        /// </summary>
        public bool OverwriteFile
        {
            get => GetProperty<bool>(true);
            set => SetProperty(value);
        }

        /// <summary>
        /// Gets a value indicating whether the send is not running.
        /// </summary>
        public bool IsEnabled
        {
            get => GetProperty<bool>(true);
            private set => SetProperty(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SendViewModel" /> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        /// <param name="filePath">The file path.</param>
        public SendViewModel(Arduino arduino, string filePath) : base(arduino)
        {
            this.SourcePath = filePath;
            this.DestinationPath = DefaultDestinationPath + Path.GetFileNameWithoutExtension(filePath).ToUpper().Truncate(8) + Path.GetExtension(filePath).ToUpper().Truncate(4);
        }

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public async Task<bool> SendFile(Window owner)
        {
            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                MessageBox.Show(owner, "Destination file name must be specified.", "Send File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!DestinationPath.IsValidFileName())
            {
                MessageBox.Show(owner, "Destination file name is not valid.", "Send File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Update default destination path
            DefaultDestinationPath = Path.GetDirectoryName(DestinationPath) ?? "C:\\";

            try
            {
                IsEnabled = false;
                await arduino.SendFile(SourcePath, DestinationPath, OverwriteFile, this).ConfigureAwait(false);
                return true;
            }
            finally
            {
                IsEnabled = true;
            }
        }
    }
}
