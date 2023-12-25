using System;
using System.Collections.Generic;
using System.Text;

namespace PortfolioSync
{
    /// <summary>
    /// Read and write stream interface
    /// </summary>
    /// <seealso cref="Common.IReadByteStream" />
    /// <seealso cref="Common.IWriteByteStream" />
    public interface IByteStream : IReadByteStream, IWriteByteStream
    {
    }
}
