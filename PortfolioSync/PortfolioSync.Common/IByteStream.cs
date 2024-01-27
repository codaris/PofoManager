﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PortfolioSync
{
    /// <summary>
    /// Read and write stream interface
    /// </summary>
    /// <seealso cref="IReadByteStream" />
    /// <seealso cref="IWriteByteStream" />
    public interface IByteStream : IReadByteStream, IWriteByteStream
    {
    }
}
