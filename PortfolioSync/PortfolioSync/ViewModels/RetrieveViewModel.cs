using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioSync.ViewModels
{
    public class RetrieveViewModel : BaseViewModel, IFileProgress
    {
        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string SourcePath
        {
            get => GetProperty<string>();
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string DestinationPath
        {
            get => GetProperty<string>();
            set
            {
                SetProperty(value);
                OnPropertyChanged(nameof(DestinationPathVisibility));
            }
        }

        /// <summary>
        /// Gets the destination path visibility.
        /// </summary>
        public Visibility DestinationPathVisibility => string.IsNullOrWhiteSpace(DestinationPath) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Gets a value indicating whether the send is not running.
        /// </summary>
        public bool IsNotRunning
        {
            get => GetProperty<bool>(true);
            private set => SetProperty(value);
        }

        /// <summary>
        /// Gets the transfer percentage text.
        /// </summary>
        public string TransferPercentageText => TransferPercentage.HasValue ? TransferPercentage.Value.ToString("n0") + "%" : string.Empty;

        /// <summary>
        /// Gets the transfer percentage.
        /// </summary>
        public int? TransferPercentage => fileSize == 0 ? null : (int)((double)fileProgress / (double)fileSize * 100);

        /// <summary>
        /// The arduino instance
        /// </summary>
        private readonly Arduino arduino;

        /// <summary>
        /// The file size
        /// </summary>
        private int fileSize = 0;

        /// <summary>
        /// The file progress
        /// </summary>
        private int fileProgress = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrieveViewModel"/> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        public RetrieveViewModel(Arduino arduino)
        {
            this.arduino = arduino;
        }

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public async Task<bool> RetrieveFile(Window owner)
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                MessageBox.Show(owner, "Source file path must be specified.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!SourcePath.IsValidFileName())
            {
                MessageBox.Show(owner, "Source file path is not valid.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                MessageBox.Show(owner, "Destination file path must be selected.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                IsNotRunning = false;
                await arduino.RetreiveFile(SourcePath, DestinationPath, this).ConfigureAwait(false);
                return true;
            }
            finally
            {
                IsNotRunning = true;
            }
        }

        /// <summary>
        /// Cancels the transfer
        /// </summary>
        public void Cancel()
        {
            arduino.Cancel();
        }

        /// <summary>
        /// Starts the transfer progress with the specified total
        /// </summary>
        /// <param name="total">The total.</param>
        void IFileProgress.Start(int total)
        {
            this.fileSize = total;
            this.fileProgress = 0;
        }

        /// <summary>
        /// Increments the progress by the specified number of bytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        void IFileProgress.Increment(int bytes)
        {
            this.fileProgress += bytes;
            OnPropertyChanged(nameof(TransferPercentage));
            OnPropertyChanged(nameof(TransferPercentageText));
        }
    }
}
