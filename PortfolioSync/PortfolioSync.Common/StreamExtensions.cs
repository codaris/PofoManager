using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioSync
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Reads a short integer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns></returns>
        public static int ReadShort(this Stream stream)
        {
            int low = stream.ReadByte();
            if (low == -1) return -1;
            int high = stream.ReadByte();
            if (high == -1) return -1;
            return low + (high << 8);
        }

        /// <summary>
        /// Reads an integer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns></returns>
        public static int ReadInt(this Stream stream)
        {
            int lowlow = stream.ReadByte();
            if (lowlow == -1) return -1;
            int lowhigh = stream.ReadByte();
            if (lowhigh == -1) return -1;
            int highlow = stream.ReadByte();
            if (highlow == -1) return -1;
            int highhigh = stream.ReadByte();
            if (highhigh == -1) return -1;
            return lowlow + (lowhigh << 8) + (highlow << 16) + (highhigh << 24);
        }
    }
}
