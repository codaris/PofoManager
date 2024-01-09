using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioSync
{
    /// <summary>
    /// Portfolio command bytes
    /// </summary>
    public enum PortfolioCommand
    {
        Abort = 0,
        RetrieveFile = 0x02,
        SendFile = 0x03,
        Overwrite = 0x05,
        FileList = 0x06,
        Success = 0x20
    }

    /// <summary>
    /// Portfolio response bytes
    /// </summary>
    public enum PortfolioResponse
    {
        FileExists = 0x20,
        FileNotFound = 0x21
    }

    /// <summary>
    /// Portfolio constants and extension methods
    /// </summary>
    public static class Portfolio
    {
        /// <summary>The maximum block size</summary>
        public const int MaxBlockSize = 0x7000;

        /// <summary>
        /// Adds the portfolio command byte to the list
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="command">The command.</param>
        public static void AddCommand(this List<byte> list, PortfolioCommand command)
        {
            list.Add((byte)command);
        }

        /// <summary>
        /// Reads the portfolio response.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>Portfolio response</returns>
        public static PortfolioResponse ReadResponse(this Stream stream)
        {
            return (PortfolioResponse)stream.ReadByte();
        }
    }
}
