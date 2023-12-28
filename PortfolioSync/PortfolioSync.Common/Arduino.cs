using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
        End = 0xFF
    }

    /// <summary>
    /// The packet types
    /// </summary>
    public enum Command
    {
        Init = 1,
        Ping = 2,
        ListFiles = 3,
        SendBlock = 4,
        RetreiveBlock = 5,
        WaitForServer = 6,
        Data = 7
    }

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

        /// <summary>The maximum block size</summary>
        private const int BlockSize = 0x7000;

        /// <summary>The high version value</summary>
        private const int VersionHigh = 1;

        /// <summary>The low version value</summary>
        private const int VersionLow = 1;

        private enum FileFormat
        {
            Basic = 0x70,
            BasicPassword = 0x71,
            ExtBasic = 0x72,
            ExtBasicPassword = 0x73,
            Data = 0x74,
            Binary = 0x76
        }

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance can cancel.
        /// </summary>
        public bool CanCancel => cancellationTokenSource != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Arduino" /> class.
        /// </summary>
        /// <param name="messageTarget">The message log.</param>
        public Arduino(IDebugTarget messageTarget)
        {
            this.messageTarget = messageTarget;
        }

        /// <summary>
        /// Connects the specified port name.
        /// </summary>
        /// <param name="portName">Name of the port.</param>
        /// <returns></returns>
        public async Task Connect(string portName)
        {
            cancellationTokenSource = new();
            serialPort = new SerialPort(portName, 115200);
            serialPort.DtrEnable = false;
            serialPort.Open();
            serialStream = new SerialPortByteStream(serialPort);
            IsConnected = true;
            OnPropertyChanged(nameof(IsConnected));
            messageTarget.WriteLine($"Connected to {portName}.");

            // Empty the read buffer
            await Initialize();

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
                    serialStream.WriteByte(Ascii.NAK);
                    serialStream.WriteByte((byte)ErrorCode.Unexpected);
                }
                await Task.Yield();
            }
        }

        /// <summary>
        /// Cancels the current operation
        /// </summary>
        public void Cancel()
        {
            serialStream?.WriteByte(Ascii.CAN);
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;
            OnPropertyChanged(nameof(CanCancel));
        }

        /// <summary>
        /// Initializes the Arduino 
        /// </summary>
        private async Task Initialize()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            using var _ = StartCommandScope();

            // Empty the read buffer
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Try synchronizing
            if (!await Synchronize().ConfigureAwait(false)) messageTarget.WriteLine("Initialize failed.");

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
            if (bufferSize < 16) throw new DataException($"Received buffer size of '{bufferSize}' is too small.");

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
            if (!await Synchronize().ConfigureAwait(false)) messageTarget.WriteLine("Synchronization failed.");

            messageTarget.Write("Pinging... ");
            serialStream.WriteByte(Ascii.SOH);
            serialStream.WriteByte((byte)Command.Ping);
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

        public async Task ListFiles()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            List<byte> data = new List<byte>();
            data.Add(0x06);
            data.Add(0x00);
            data.Add(0x70);
            data.AddString("C:*.*");
            data.Add(0);

            using var _ = StartCommandScope();

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream...");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Try synchronizing
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) messageTarget.WriteLine("Synchronization failed.");

            // Wait for server
            messageTarget.DebugWriteLine("Waiting for server...");
            await WaitForServer();

            messageTarget.DebugWriteLine("Requesting file list...");
            await SendBlock(data.ToArray());
            // await ReadResponse().ConfigureAwait(false);

            // Processing response
            var response = new MemoryStream(await RetreiveBlock());
            var files = new List<string>();
            int fileCount = response.ReadByte() + (response.ReadByte() << 8);
            messageTarget.WriteLine($"File count: {fileCount}");
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
            foreach (var file in files) messageTarget.WriteLine(file);
            messageTarget.WriteLine($"Done.");
        }

        /// <summary>
        /// Sends the tape file.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <exception cref="System.InvalidOperationException">Cannot send file if not connected</exception>
        /// <exception cref="System.Exception">Unable to start file transfer</exception>
        public async Task SendFile(Stream fileStream)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            using var _ = StartCommandScope();

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
            data.Add(0x03);                             // Send file
            data.AddShort(BlockSize);                   // Block size
            data.AddShort(DOS.GenerateTime(DateTime.Now));
            data.AddShort(DOS.GenerateDate(DateTime.Now));
            data.AddInt((int)fileStream.Length);
            data.AddString("C:\\TESTFILE.TXT");
            data.Add(0x00);

            messageTarget.DebugWriteLine("Send file command...");
            await SendBlock(data.ToArray());
            var response = new MemoryStream(await RetreiveBlock());
            var result = response.ReadByte();
            if (result == 0x21) messageTarget.DebugWriteLine("File OK");
            if (result == 0x20)
            {
                messageTarget.DebugWriteLine("File exists, overwriting");
                data.Clear();
                data.Add(0x05);
                data.AddShort(BlockSize); // Block size
                await SendBlock(data.ToArray());
            }

            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            var fileData = memoryStream.ToArray();
            await SendBlock(fileData);

            response = new MemoryStream(await RetreiveBlock());
            result = response.ReadByte();
            if (result == 0x20) messageTarget.WriteLine("Success.");
            else messageTarget.WriteLine("Failure.");

            /*
            serialStream.WriteByte(Ascii.SOH);    // Start of packet 
            serialStream.WriteByte((byte)Command.SendFile);
            serialStream.WriteWord((ushort)fileStream.Length);
            serialStream.WriteByte(HeaderSize);
            await ReadResponse().ConfigureAwait(false);
            await SendBuffer(data).ConfigureAwait(false);
            messageTarget.WriteLine($"Done.");
            */
        }

        /// <summary>
        /// Reads the tape file.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> RetreiveFile()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            using var _ = StartCommandScope();

            // Empty the read buffer
            messageTarget.DebugWriteLine("Clearing stream... ");
            while (serialStream.DataAvailable) await serialStream.ReadByteAsync().ConfigureAwait(false);

            // Send Syn character and wait for syn
            messageTarget.DebugWriteLine("Synchronizing...");
            if (!await Synchronize().ConfigureAwait(false)) throw new ArduinoException("Unable to start file transfer");

            return new byte[0];
            /*
            messageTarget.WriteLine($"Waiting for CSAVE on pocket computer...");
            serialStream.WriteByte(Ascii.SOH);    // Start of packet 
            serialStream.WriteByte((byte)Command.RetreiveFile);
            await ReadResponse().ConfigureAwait(false);

            try
            {
                cancellationTokenSource = new();
                OnPropertyChanged(nameof(CanCancel));
                return await ReadFrame(cancellationTokenSource.Token).ConfigureAwait(false);
            }
            finally
            {
                cancellationTokenSource = null;
                OnPropertyChanged(nameof(CanCancel));
            }
            */
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
                case Command.Data:
                    var value = await serialStream.TryReadByteAsync(1000).ConfigureAwait(false);
                    if (!value.HasValue) return;
                    messageTarget.WriteLine($"Data: {value:X2}");
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// Sends the disk packet.
        /// </summary>
        /// <param name="response">The data.</param>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        private async Task SendResponse(DiskResponse response)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.WriteByte(Ascii.SOH);
            // serialStream.WriteByte((byte)Command.Disk);
            serialStream.WriteByte(response.Capture ? 0xFF : 0);
            serialStream.WriteWord(response.Data.Length);
            await ReadResponse().ConfigureAwait(false);     // Wait for acknowledge
            await SendBuffer(response.Data).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a data frame.
        /// </summary>
        /// <returns>Byte array of data</returns>
        private async Task<byte[]> ReadFrame(CancellationToken cancellationToken = default)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");

            // Wait for start value
            var startValue = await serialStream.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (startValue == Ascii.NAK)
            {
                throw new ArduinoException(await serialStream.ReadByteAsync(2000).ConfigureAwait(false));
            }
            else if (startValue != Ascii.STX)
            {
                throw new ArduinoException($"Expecting transmisson start (STX) received 0x{startValue:X} instead.");
            }

            // The data result
            var result = new List<byte>();

            while (true)
            {
                var data = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
                switch (data)
                {
                    case Ascii.DLE:
                        data = await serialStream.ReadByteAsync(1000).ConfigureAwait(false);
                        break;
                    case Ascii.NAK:
                        throw new ArduinoException(await serialStream.ReadByteAsync(1000).ConfigureAwait(false));
                    case Ascii.CAN:
                        throw new ArduinoException(ErrorCode.Cancelled);
                    case Ascii.ETX:
                        if (result.Count % 40 != 0) messageTarget.WriteLine();
                        messageTarget.Dump(result);
                        return result.ToArray();
                }
                result.Add(data);
                messageTarget.Write(".");
                // messageTarget.Write(" " + data.ToString("X2"));
                if (result.Count % 80 == 0) messageTarget.WriteLine();
            }
        }

        private async Task SendBlock(byte[] data)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.WriteByte(Ascii.SOH);    // Start of packet 
            serialStream.WriteByte((byte)Command.SendBlock);
            serialStream.WriteWord(data.Length);
            await ReadResponse().ConfigureAwait(false);     // Wait for acknowledge
            await SendBuffer(data).ConfigureAwait(false);
            await ReadResponse().ConfigureAwait(false);     // Wait for final acknowledge
            await ReadResponse().ConfigureAwait(false);     // Wait for final acknowledge
        }

        private async Task WaitForServer()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.WriteByte(Ascii.SOH);    // Start of packet 
            serialStream.WriteByte((byte)Command.WaitForServer);
            await ReadResponse().ConfigureAwait(false);     // Wait for acknowledge
        }

        private async Task<byte[]> RetreiveBlock()
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            serialStream.WriteByte(Ascii.SOH);    // Start of packet 
            serialStream.WriteByte((byte)Command.RetreiveBlock);
            return await ReadFrame();
        }

        /// <summary>
        /// Sends the buffer by breaking into BufferSize sized groups
        /// </summary>
        /// <param name="data">The data.</param>
        /// <exception cref="PortfolioSync.ArduinoException">Arduino is not connected</exception>
        private async Task SendBuffer(byte[] data)
        {
            if (serialStream == null) throw new ArduinoException("Arduino is not connected");
            int offset = 0;
            while (true)
            {
                int size = Math.Min(BufferSize, data.Length - offset);
                if (size == 0) break;
                messageTarget.DebugWriteLine($"Sending {size} bytes:");
                messageTarget.Dump(new ArraySegment<byte>(data, offset, size));
                for (int i = 0; i < size; i++) serialStream.WriteByte(data[offset++]);
                await ReadResponse().ConfigureAwait(false);
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

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
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
    }
}
