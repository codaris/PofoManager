using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PortfolioSync
{
    /// <summary>
    /// The error codes
    /// </summary>
    public enum ErrorCode
    {
        Ok = 0,
        Timeout = 1,
        Cancelled = 2,
        Unexpected = 3,
        Overflow = 4,
        SyncError = 5,
        ChecksumError = 6,
        End = 0xFF
    }

    /// <summary>
    /// The command types
    /// </summary>
    public enum Command
    {
        Init = 1,
        Ping = 2,
        WaitForServer = 3,
        SendBlock = 4,
        RetreiveBlock = 5
    }

    /// <summary>
    /// Arduino management class
    /// </summary>
    /// <seealso cref="PortfolioSync.NotifyObject" />
    /// <seealso cref="System.IDisposable" />
    public class Arduino : NotifyObject, IDisposable
    {
        /// <summary>The serial port</summary>
        private SerialPort? serialPort = null;

        /// <summary>The serial stream</summary>
        private SerialPortByteStream? serialStream = null;

        /// <summary>Cancel the current operation</summary>
        private CancellationTokenSource? cancellationTokenSource = null;

        /// <summary>The message log</summary>
        private readonly IDebugTarget messageTarget;

        /// <summary>Whether or not current processing a command</summary>
        private int commandCount = 0;

        /// <summary>The arduino buffer size</summary>
        private const int BufferSize = 60;

        /// <summary>The high version value</summary>
        private const int VersionHigh = 1;

        /// <summary>The low version value</summary>
        private const int VersionLow = 1;

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is in the process of connecting.
        /// </summary>
        public bool IsConnecting { get; private set; }  

        /// <summary>
        /// Gets a value indicating whether the current operation can be cancelled
        /// </summary>
        public bool CanCancel => cancellationTokenSource != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Arduino" /> class.
        /// </summary>
        /// <param name="messageTarget">The message target.</param>
        public Arduino(IDebugTarget messageTarget)
        {
            this.messageTarget = messageTarget;
        }

        /// <summary>
        /// Connects to the specified port name.
        /// </summary>
        /// <param name="portName">Name of the port.</param>
        /// <returns>A task representing the asynchronise operation</returns>
        public async Task Connect(string portName)
        {
            try
            {
                IsConnecting = true;
                OnPropertyChanged(nameof(IsConnecting));

                serialPort = new SerialPort(portName, 115200);
                serialPort.DtrEnable = false;
                serialPort.Open();
                serialStream = new SerialPortByteStream(serialPort);
                messageTarget.WriteLine($"Connected to {portName}.");

                // Wait for initialization
                await Initialize();
            }
            catch
            {
                // If initialize failed then disconnect
                Disconnect();
                throw;
            }
            finally
            {
                IsConnecting = false;
                OnPropertyChanged(nameof(IsConnecting));
            }

            // Change connected property
            IsConnected = true;
            OnPropertyChanged(nameof(IsConnected));

            // Begin the main loop
            _ = Task.Run(async () => { await Mainloop(); });
        }

        /// <summary>
        /// Disconnects this instance.
        /// </summary>
        public void Disconnect()
        {
            serialStream?.Dispose();
            serialStream = null;
            serialPort?.Close();
            serialPort?.Dispose();
            serialPort = null;

            IsConnected = false;
            OnPropertyChanged(nameof(IsConnected));
            messageTarget.WriteLine("Disconnected.");
        }

        /// <summary>
        /// The main loop of procesing incoming packets
        /// </summary>
        private async Task Mainloop()
        {
            while (serialStream != null)
            {
                // If data is available and not processing a command, check incoming packet
                if (commandCount == 0 && serialStream.DataAvailable)
                {
                    var data = serialStream.ReadByte();
                    // If sync then syn back
                    if (data == Ascii.SYN) serialStream.WriteByte(Ascii.SYN);
                    // Processing incoming packet
                    if (data == Ascii.SOH) await ProcessIncomingCommand().ConfigureAwait(false); ;
                    serialStream.WriteNak(ErrorCode.Unexpected);
                }
                await Task.Yield();
            }
        }

        /// <summary>
        /// Cancels the current operation
        /// </summary>
        public void Cancel()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancel));
        }

        /// <summary>
        /// Initializes the Arduino connection
        /// </summary>
        private async Task Initialize()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            // Stop processing incoming packets
            using var _ = StartCommandScope();

            // Empty the read buffer
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Try synchronizing
            if (!await Synchronize().ConfigureAwait(false)) throw new DataException("Synchronization failed.");

            // Send the init command
            serialStream.WriteByte(Ascii.SOH);
            serialStream.WriteByte((byte)Command.Init);
            
            // Read the header
            await serialStream.ExpectByteAsync(Ascii.SOH, 2500).ConfigureAwait(false);
            int versionHigh = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
            int versionLow = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
            if (versionHigh != VersionHigh || versionLow != VersionLow)
            {
                throw new DataException($"Unexpected Arduino version (Expected {VersionHigh}.{VersionLow} but received {versionHigh}.{versionLow}");
            }
            int bufferSize = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
            if (bufferSize != BufferSize) throw new DataException($"Received buffer size of '{bufferSize}' does not equal '{BufferSize}'.");

            // Read the text stream from the Arduino
            await serialStream.ExpectByteAsync(Ascii.STX, 1000).ConfigureAwait(false);
            while (true)
            {
                byte value = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
                if (value == Ascii.ETX) break;
                messageTarget.Write(char.ConvertFromUtf32(value));
            }
        }

        /// <summary>
        /// Pings the arduino
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Cannot test if not connected</exception>
        public async Task Ping()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            using var _ = StartCommandScope();

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream...");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Try synchronizing
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) throw new DataException("Synchronization failed.");

            messageTarget.Write("Pinging... ");
            serialStream.StartCommand(Command.Ping);
            var response = await serialStream.TryReadByteAsync(2500).ConfigureAwait(false); ;       // Wait for response
            if (response == Ascii.ACK)
            {
                messageTarget.WriteLine("Success.");
            }
            else if (response == Ascii.NAK)
            {
                response = await serialStream.TryReadByteAsync(2000).ConfigureAwait(false); ;
                var errorCode = ErrorCode.Timeout;
                if (response.HasValue) errorCode = (ErrorCode)response.Value;
                messageTarget.WriteLine($"Ping Failure.  Error {errorCode}");
            }
            else
            {
                messageTarget.WriteLine($"No response.");
            }
        }

        /// <summary>
        /// Lists the files.
        /// </summary>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        public async Task<IEnumerable<string>> ListFiles(string filePattern)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            // Send the file list packet
            List<byte> data = new List<byte>();
            data.AddCommand(PortfolioCommand.FileList);
            data.AddShort(Portfolio.MaxBlockSize);
            data.AddStringZ(filePattern);

            // Stop processing incoming packets
            using var _ = StartCommandScope();

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream...");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Try synchronizing
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) throw new DataException("Synchronization failed.");

            // Wait for server
            messageTarget.DebugWriteLine("Waiting for server...");
            await WaitForServer();

            messageTarget.DebugWriteLine("Requesting file list...");
            await SendBlock(data.ToArray());

            // Processing response
            var response = new MemoryStream(await RetreiveBlock());
            var files = new List<string>();
            int fileCount = response.ReadShort();
            messageTarget.DebugWriteLine($"File count: {fileCount}");
            var fileName = new StringBuilder();
            while (true)
            {
                int value = response.ReadByte(); 
                if (value == 0)
                {
                    files.Add(fileName.ToString());
                    fileName.Clear();
                    continue;
                }
                if (value == -1) break;
                fileName.Append((char)value);
            }
            foreach (var file in files) messageTarget.DebugWriteLine(file);
            messageTarget.DebugWriteLine($"Done.");
            return files;
        }

        /// <summary>
        /// Sends file to the portfolio
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <exception cref="System.InvalidOperationException">Cannot send file if not connected</exception>
        /// <exception cref="System.Exception">Unable to start file transfer</exception>
        public async Task SendFile(string fileName, string remoteFilePath, bool overwrite, IFileProgress progress)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            using var commandScope = StartCommandScope();
            using var cancelScope = StartCancellationScope();
            var cancellationToken = cancellationTokenSource?.Token ?? default;

            messageTarget.WriteLine($"Sendng '{fileName}' to Portfolio as '{remoteFilePath}'");
            if (overwrite) messageTarget.WriteLine("Overwriting file if exists");

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream... ");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Send Syn character and wait for syn
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) throw new ArduinoException("Unable to start file transfer");

            // Wait for server
            messageTarget.DebugWriteLine("Waiting for server...");
            await WaitForServer();

            var fileLength = (int)fileStream.Length;
            progress.Start(fileLength);

            List<byte> data = new List<byte>();
            data.AddCommand(PortfolioCommand.SendFile);
            data.AddShort(Portfolio.MaxBlockSize);                   
            data.AddShort(DOS.GenerateTime(DateTime.Now));
            data.AddShort(DOS.GenerateDate(DateTime.Now));
            data.AddInt(fileLength);
            data.AddStringZ(remoteFilePath);

            messageTarget.DebugWriteLine("Send file command...");
            await SendBlock(data.ToArray());
            var responseBlock = new MemoryStream(await RetreiveBlock());
            var response = responseBlock.ReadResponse();
            if (response == PortfolioResponse.FileNotFound) messageTarget.DebugWriteLine("File OK");
            else if (response == PortfolioResponse.FileExists)
            {
                if (overwrite) {
                    messageTarget.DebugWriteLine("File exists, overwriting");
                    data.Clear();
                    data.AddCommand(PortfolioCommand.Overwrite);
                    data.AddShort(Portfolio.MaxBlockSize); 
                    await SendBlock(data.ToArray());
                } 
                else
                {
                    messageTarget.WriteLine($"File '{remoteFilePath}' already exists.");
                    data.Clear();
                    data.AddCommand(PortfolioCommand.Abort);
                    data.AddShort(0);                           // Block size
                    await SendBlock(data.ToArray());
                    return;
                }
            }
            else
            {
                messageTarget.WriteLine($"Unexpected response from Portfolio: {(int)response:X2}");
                throw new ArduinoException($"Unexpected response from Portfolio: {(int)response:X2}");
            }

            var buffer = new byte[Portfolio.MaxBlockSize];          
            while (true)
            {
                int length = fileStream.Read(buffer, 0, buffer.Length);
                if (length == 0) break;
                messageTarget.DebugWriteLine($"Sending block {length} bytes");
                await SendBlock(buffer, length, progress, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    messageTarget.WriteLine("Cancelled.  Restart server on Portfolio to try again.");
                    // Send cancel packet (ineffective because in the middle of sending block)
                    data.Clear();
                    data.AddCommand(PortfolioCommand.Abort);
                    data.AddShort(0);       // Block size
                    await SendBlock(data.ToArray());
                    return;
                }

            }

            responseBlock = new MemoryStream(await RetreiveBlock());
            response = responseBlock.ReadResponse();
            if (response == PortfolioResponse.FileExists) messageTarget.WriteLine("Success.");
            else messageTarget.WriteLine("Failure.");
        }

        /// <summary>
        /// Retrieves a file from the portfolio
        /// </summary>
        /// <param name="remoteFilePath">The remote file path.</param>
        /// <param name="localFilePath">The local file path.</param>
        /// <returns>True if successfil</returns>
        public async Task<bool> RetreiveFile(string remoteFilePath, string localFilePath, IFileProgress progress)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            messageTarget.WriteLine($"Retrieving '{remoteFilePath}' from Portfolio as '{localFilePath}'");

            using var fileStream = new FileStream(localFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            using var commandScope = StartCommandScope();
            using var cancelScope = StartCancellationScope();
            var cancellationToken = cancellationTokenSource?.Token ?? default;

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream... ");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Send Syn character and wait for syn
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) throw new ArduinoException("Unable to start file transfer");

            // Wait for server
            messageTarget.DebugWriteLine("Waiting for server...");
            await WaitForServer();

            List<byte> data = new List<byte>();
            data.AddCommand(PortfolioCommand.RetrieveFile);
            data.AddShort(Portfolio.MaxBlockSize);                   
            data.AddStringZ(remoteFilePath);

            messageTarget.DebugWriteLine("Retrieve file command...");
            await SendBlock(data.ToArray());
            var responseBlock = new MemoryStream(await RetreiveBlock());
            var response = responseBlock.ReadResponse();
            if (response == PortfolioResponse.FileExists)
            {
                messageTarget.DebugWriteLine("File OK");
            }
            else if (response == PortfolioResponse.FileNotFound)
            {
                messageTarget.WriteLine("File was not found on Portfolio");
                throw new ArduinoException("File was not found on Portfolio");
            }
            else
            {
                messageTarget.WriteLine($"Unexpected response from Portfolio: {(int)response:X2}");
                throw new ArduinoException($"Unexpected response from Portfolio: {(int)response:X2}");
            }
            int blockSize = responseBlock.ReadShort();
            int fileTime = responseBlock.ReadShort();
            int fileDate = responseBlock.ReadShort();
            int remaining = responseBlock.ReadInt();

            progress.Start(remaining);

            var buffer = new byte[blockSize];
            while (true)
            {
                var block = await RetreiveBlock(progress, cancellationToken);
                remaining -= block.Length;
                fileStream.Write(block, 0, block.Length);
                if (remaining <= 0) break;
                if (cancellationToken.IsCancellationRequested) break;
            }

            // Send success
            data.Clear();
            if (cancellationToken.IsCancellationRequested)
            {
                // Empty the read buffer
                messageTarget.DebugWriteLine("Clearing stream... ");
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (stopwatch.Elapsed < TimeSpan.FromSeconds(1))
                {
                    while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);
                }
                messageTarget.WriteLine("Cancelled.  Restart server on Portfolio to try again.");
            }
            else
            {
                data.AddCommand(PortfolioCommand.Success);
                data.AddShort(3);
                await SendBlock(data.ToArray());
                messageTarget.WriteLine("Success.");
            }
            return true;
        }

        /// <summary>
        /// Reads and parses the response.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Not Connected</exception>
        /// <exception cref="System.Exception">Transmission Error {errorCode}</exception>
        private async Task ReadResponse()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            var response = await serialStream.ReadByteAsync(5000).ConfigureAwait(false);    // Wait for response
            if (response == Ascii.ACK) return;
            if (response == Ascii.NAK) throw new ArduinoException(await serialStream.ReadByteAsync(1000).ConfigureAwait(false));
            throw new ArduinoException($"Unexpected response received 0x{response:X2}");
        }

        /// <summary>
        /// Reads the cancel.
        /// </summary>
        private async Task ReadTimeout()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            var response = await serialStream.ReadByteAsync(5000).ConfigureAwait(false);    // Wait for response
            if (response != Ascii.NAK) throw new ArduinoException($"Unexpected response received 0x{response:X2}");
            response = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
            if (response != (int)ErrorCode.Timeout) throw new ArduinoException($"Unexpected response received 0x{response:X2}");
        }

        /// <summary>
        /// Synchronizes the serial connection
        /// </summary>
        /// <param name="byteStream">The byte stream.</param>
        /// <returns></returns>
        private async Task<bool> Synchronize()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            int tryCount = 0;
            while (true)
            {
                serialStream.WriteByte(Ascii.SYN);
                var response = await serialStream.TryReadByteAsync(1000).ConfigureAwait(false);       // Wait one second for response
                if (response == Ascii.SYN) break;
                if (response == Ascii.NAK)
                {
                    // Ignore error code
                    await serialStream.TryReadByteAsync(1000).ConfigureAwait(false);
                    continue;
                }
                tryCount++;
                if (tryCount > 10) return false;
            }
            return true;
        }

        /// <summary>
        /// Processes the incoming packet.
        /// </summary>
        private async Task ProcessIncomingCommand()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            var command = await serialStream.TryReadByteAsync(1000).ConfigureAwait(false);
            if (!command.HasValue) return;
            switch ((Command)command.Value)
            {
                case Command.Ping:
                    serialStream.WriteByte(Ascii.ACK);
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// Waits for portfolio server
        /// </summary>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        private async Task WaitForServer()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.StartCommand(Command.WaitForServer);
            await ReadResponse().ConfigureAwait(false);     // Wait for acknowledge
        }

        /// <summary>
        /// Retreives a block from the portfolio
        /// </summary>
        /// <returns></returns>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        private async Task<byte[]> RetreiveBlock(IFileProgress? progress = null, CancellationToken cancellationToken = default)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.StartCommand(Command.RetreiveBlock);
            return await ReadFrame(progress, cancellationToken);
        }

        /// <summary>
        /// Reads a data frame.
        /// </summary>
        /// <returns>Byte array of data</returns>
        private async Task<byte[]> ReadFrame(IFileProgress? progress, CancellationToken cancellationToken = default)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            // Wait for start value
            var startValue = await serialStream.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (startValue == Ascii.NAK)
            {
                throw new ArduinoException(await serialStream.ReadByteAsync(2000, CancellationToken.None).ConfigureAwait(false));
            }
            else if (startValue != Ascii.STX)
            {
                throw new ArduinoException($"Expecting transmisson start (STX) received 0x{startValue:X} instead.");
            }

            // The data result
            var result = new List<byte>();

            while (true)
            {
                var data = await serialStream.ReadByteAsync(1000, CancellationToken.None).ConfigureAwait(false);
                switch (data)
                {
                    case Ascii.DLE:
                        data = await serialStream.ReadByteAsync(1000, CancellationToken.None).ConfigureAwait(false);
                        break;
                    case Ascii.NAK:
                        throw new ArduinoException(await serialStream.ReadByteAsync(1000, CancellationToken.None).ConfigureAwait(false));
                    case Ascii.CAN:
                        throw new ArduinoException(ErrorCode.Cancelled);
                    case Ascii.ETX:
                        messageTarget.Dump(result);
                        return result.ToArray();
                }
                result.Add(data);
                progress?.Increment(1);
                if (cancellationToken.IsCancellationRequested)
                {
                    // Send cancel
                    for (int i = 0; i < 5; i++) serialStream.WriteByte(Ascii.CAN);
                    return Array.Empty<byte>();
                }
            }
        }

        /// <summary>
        /// Sends a block to the portfolio
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        private Task SendBlock(byte[] data, IFileProgress? progress = null, CancellationToken cancellationToken = default) => SendBlock(data, data.Length, progress, cancellationToken);

        /// <summary>
        /// Sends a block to the portfolio
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="length">The length.</param>
        private async Task SendBlock(byte[] data, int length, IFileProgress? progress = null, CancellationToken cancellationToken = default)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            if (length > data.Length) throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be larger than buffer size");
            serialStream.StartCommand(Command.SendBlock);
            serialStream.WriteWord(length);
            await ReadResponse().ConfigureAwait(false);     // Wait for header acknowledge
            await SendBuffer(data, length, progress, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;
            await ReadResponse().ConfigureAwait(false);     // Wait for final acknowledge
            await ReadResponse().ConfigureAwait(false);     // Wait for final acknowledge TODO why?
        }

        /// <summary>
        /// Sends the buffer by breaking into BufferSize sized groups
        /// </summary>
        /// <param name="data">The data.</param>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        private async Task SendBuffer(byte[] data, int length, IFileProgress? progress = null, CancellationToken cancellationToken = default)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            int offset = 0;
            while (true)
            {
                int size = Math.Min(BufferSize, length - offset);
                if (size == 0) break;
                messageTarget.DebugWriteLine($"Sending {size} bytes:");
                messageTarget.Dump(new ArraySegment<byte>(data, offset, size));
                for (int i = 0; i < size; i++)
                {
                    serialStream.WriteByte(data[offset++]);
                }
                await ReadResponse().ConfigureAwait(false);
                progress?.Increment(size);
                if (cancellationToken.IsCancellationRequested)
                {
                    await ReadTimeout();
                    break;
                }
            }
        }

        /// <summary>
        /// The disposed value
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Disconnect();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts the command.
        /// </summary>
        internal ScopeGuard StartCommandScope()
        {
            commandCount++;
            return new ScopeGuard(() => { if (commandCount > 0) commandCount--; });
        }

        /// <summary>
        /// Starts the cancellation scope.
        /// </summary>
        internal ScopeGuard StartCancellationScope()
        {
            cancellationTokenSource = new CancellationTokenSource();
            return new ScopeGuard(() =>
            {
                cancellationTokenSource = null;
                OnPropertyChanged(nameof(CanCancel));
            });
        }
    }
}
