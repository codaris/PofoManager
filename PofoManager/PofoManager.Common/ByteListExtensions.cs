using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PofoManager
{
    /// <summary>
    /// Extensions for using a byte list as a transfer buffer to the Portfolio
    /// </summary>
    public static class ByteListExtensions
    {
        /// <summary>
        /// Adds the integer to the list as a byte
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="value">The value.</param>
        public static void AddByte(this List<byte> list, int value)
        {
            list.Add((byte)value);
        }

        /// <summary>
        /// Adds a string to the byte list
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="value">The value.</param>
        public static void AddString(this List<byte> data, string value)
        {
            foreach (char c in value) data.Add((byte)c);
        }

        /// <summary>
        /// Adds a zero-terminated string to the byte list
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="value">The value.</param>
        public static void AddStringZ(this List<byte> data, string value)
        {
            data.AddString(value);
            data.Add(0);
        }

        /// <summary>
        /// Adds the short.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="value">The size.</param>
        public static void AddShort(this List<byte> data, int value)
        {
            data.Add((byte)(value & 0xFF));
            data.Add((byte)(value >> 8 & 0xFF));
        }

        /// <summary>
        /// Adds a size to the byte array.  Sizes are 3 bytes long.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="size">The size.</param>
        public static void AddSize(this List<byte> data, int size)
        {
            data.Add((byte)(size & 0xFF));
            data.Add((byte)(size >> 8 & 0xFF));
            data.Add((byte)(size >> 16 & 0xFF));
        }

        /// <summary>
        /// Adds an integer to the byte array.  4 bytes long
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="value">The value.</param>
        public static void AddInt(this List<byte> data, int value)
        {
            data.Add((byte)(value & 0xFF));
            data.Add((byte)(value >> 8 & 0xFF));
            data.Add((byte)(value >> 16 & 0xFF));
            data.Add((byte)(value >> 24 & 0xFF));
        }
    }
}
