using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PofoManager
{
    /// <summary>
    /// File transfer progress interface
    /// </summary>
    public interface IFileProgress
    {
        /// <summary>
        /// Starts the transfer progress with the specified total
        /// </summary>
        /// <param name="total">The total.</param>
        void Start(int total);

        /// <summary>
        /// Increments the progress by the specified number of bytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        void Increment(int bytes);
    }
}
