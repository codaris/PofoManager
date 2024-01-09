using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioSync.ViewModels
{
    /// <summary>
    /// Base class for file transfer view models
    /// </summary>
    /// <seealso cref="PortfolioSync.ViewModels.BaseViewModel" />
    /// <seealso cref="PortfolioSync.IFileProgress" />
    public abstract class TransferViewModel : BaseViewModel, IFileProgress
    {
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
        protected readonly Arduino arduino;

        /// <summary>
        /// The file size
        /// </summary>
        private int fileSize = 0;

        /// <summary>
        /// The file progress
        /// </summary>
        private int fileProgress = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransferViewModel"/> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        public TransferViewModel(Arduino arduino)
        {
            this.arduino = arduino;
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
