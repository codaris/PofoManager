#include "Portfolio.h"
#include "Manager.h"

namespace Portfolio
{
    /** @brief The checksum */
    byte checksum = 0;

    const int PAYLOAD_BUFSIZE = 60000;
    const int CONTROL_BUFSIZE = 100;
    const int LIST_BUFSIZE = 2000;
    const int MAX_FILENAME_LEN = 79;

    unsigned char DirectoryRequestBuffer[82] =
    { 
        0x06,         /* Offset 0: Funktion */
        0x00, 0x70,    /* Offset 2: Puffergroesse = 28672 Byte */
        'C', ':', '*','.','*', 0
    };    
    
    /**
     * @brief   Read a byte from the Portfolio and update checksum
     * @return  A byte from the input
     */    
    int ReadByteChecksum() 
    {
        int value = ReadByte();
        checksum += value;
        return value;        
    }

    /** 
     * @brief Send a byte to the Portfolio and update checksum
     * @param value     The value to send
     */
    int SendByteChecksum(int value) 
    {
        SendByte(value);
        checksum -= value; 
    }

    /**
     * @brief Initials the pins for the portfolio
     */
    void Enable()
    {
        pinMode(PIN_INPUT_CLOCK, INPUT); 
        pinMode(PIN_INPUT_DATA, INPUT); 
        pinMode(PIN_OUTPUT_CLOCK, OUTPUT); 
        pinMode(PIN_OUTPUT_DATA, OUTPUT); 
    }

    /**
     * @brief Disable the pins for the portfolio
     */
    void Disable()
    {
        pinMode(PIN_INPUT_CLOCK, INPUT); 
        pinMode(PIN_INPUT_DATA, INPUT); 
        pinMode(PIN_OUTPUT_CLOCK, INPUT); 
        pinMode(PIN_OUTPUT_DATA, INPUT); 
    }

    /**
     * @brief   Read a byte from the Portfolio
     * @return  A byte from the input
     */
    int ReadByte()
    {
        int value = 0;
        for (int i = 0; i < 4; i++) {
            while(digitalRead(PIN_INPUT_CLOCK));                    // Wait clock low
            value = (value << 1) | digitalRead(PIN_INPUT_DATA);     // Get bit
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);                    // Clear clock 
            while (!digitalRead(PIN_INPUT_CLOCK));                  // Wait clock high
            value = (value << 1) | digitalRead(PIN_INPUT_DATA);     // Get bit
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);                   // Set clock
        }
        return value;
    }

    /**
     * @brief Send a byte to the Portfolio
     * @param value     The value to send
     */
    void SendByte(int value)
    {
        /* Should be usleep(50), but smaller arguments than 1000 result in no delay */
        delayMicroseconds(50);
        // delay(1);

        for (int i = 0; i < 4; i++) {
            if (value & 0x80) digitalWrite(PIN_OUTPUT_DATA, HIGH);  // B
            else digitalWrite(PIN_OUTPUT_DATA, LOW);  
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);    
            value <<= 1;
            while(digitalRead(PIN_INPUT_CLOCK));
            if (value & 0x80) digitalWrite(PIN_OUTPUT_DATA, HIGH);
            else digitalWrite(PIN_OUTPUT_DATA, LOW);          
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);    
            value = value << 1;
            while(!digitalRead(PIN_INPUT_CLOCK));       
        }
    }

    /**
     * @brief Synchronize
     */
    void WaitForServer()
    {
        /*
        Wait for Portfolio to enter server mode
        */
        digitalWrite(PIN_OUTPUT_CLOCK, HIGH);  
        while (!digitalRead(PIN_INPUT_CLOCK));
        int value = ReadByte();

        // synchronization 
        while (value != 90)   // While != 90
        {
            while (digitalRead(PIN_INPUT_CLOCK));    
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);
            while (!digitalRead(PIN_INPUT_CLOCK)); 
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);
            value = ReadByte();
            Serial.print(value, 16);
            Serial.write(" ");
            Serial.println(value);
        }        
    }

    int SendBlock(const byte data[], const int length)
    {
        // Reset checksum
        checksum = 0;

        if (length == 0) return;

        // Wait for start
        while (true) {
            int value = ReadByte();
            if (value == 0x5A) break;
            if (value == 0x69) continue;
            if (value == 0xA5) continue;
            if (value == 0x96) continue;
            Serial.println("Cannot start");
            return -1;
        }

        delay(50);
    
        // Start of block
        SendByte(0xA5);

        int lenH = length >> 8;     
        int lenL = length & 255;
        SendByteChecksum(lenL); 
        SendByteChecksum(lenH); 

        for (int i = 0; i < length; i++) {
            SendByteChecksum(data[i]);
        }
  
        SendByte(checksum);
        int value = ReadByte();
        if (checksum == value) {
            Serial.println("Block sent");
            return 0;
        } else {
            Serial.print("Error: ");
            Serial.print(value, 16);
            Serial.println();
            return -1;
        }
    }

    int ReadBlockToSerial()
    {
        checksum = 0;
        SendByte(0x5A);     // Z start
        int value = ReadByte();
        if (value != 0xA5) {
            Serial.print("Acknowledge error, received: ");
            Serial.print(value, 16);
            Serial.println();
            return -1;
        }

        Serial.println("Acknowledge OK");
        
        int lenL = ReadByteChecksum();
        int lenH = ReadByteChecksum();
        int length = (lenH << 8) | lenL;

        /*
        if (length > maxLength) {
            Serial.print("Receive buffer too small: ");
            Serial.print(length);
            Serial.print(" > ");
            Serial.print(maxLength);
            Serial.println();
            return -1;
        }
        */

        for (int i = 0; i < length; i++) 
        {
            int dataByte = ReadByteChecksum();
            Serial.print(dataByte, 16);
            Serial.print(" ");
            if (i % 40 == 0 && i != 0) Serial.println();
        }
        Serial.println();

        int ackChecksum = ReadByte();

        if ((byte)(256 - ackChecksum) == checksum) 
        {
            Serial.println("Checksum OK");
        } else {
            Serial.print("Checksum error: ");
            Serial.print(256 - ackChecksum, 16);
            Serial.print(" != ");
            Serial.print(checksum, 16);
            Serial.println();
            return -1;
        }

        delayMicroseconds(100);
        SendByte((byte)(256 - checksum));
        return length;
    }

    /**
     * @brief Test request file list
     */
    void RequestFileList()
    {
        SendBlock(DirectoryRequestBuffer, sizeof(DirectoryRequestBuffer));
        ReadBlockToSerial();
    }

    /**
     * @brief Send a block from the serial port to the portfolio
     */
    void SendBlock()
    {
        // Read the block information
        Result length = Manager::WaitReadWord();            // 2 bytes, total length of block
        if (length.IsError()) {
            Manager::SendFailure(length.AsErrorCode());
            return;
        }
        // Initialize the buffer with the length
        Manager::InitializeBuffer(length);

        // Reset checksum
        checksum = 0;

        if (length.Value() == 0) return;

        // Wait for start
        while (true) {
            int value = ReadByte();
            if (value == 0x5A) break;
            if (value == 0x69) continue;
            if (value == 0xA5) continue;
            if (value == 0x96) continue;
            Manager::SendFailure(ResultType::Unexpected);
        }

        // Parsing the header was a success
        Manager::SendSuccess();            

        delay(50);
    
        // Start of block
        SendByte(0xA5);

        int lenH = length.Value() >> 8;     
        int lenL = length.Value() & 255;
        SendByteChecksum(lenL); 
        SendByteChecksum(lenH); 

        while (true) 
        {
            auto data = Manager::ReadBufferByte();
            if (data.IsDone()) break;
            if (data.IsError()) {
                Manager::SendFailure(data.AsErrorCode());
                return;
            }

            SendByteChecksum(data.Value());
        }

        SendByte(checksum);
        int value = ReadByte();
        if (checksum == value) {
            // Block send was success
            Manager::SendSuccess();                 
        } else {
            Manager::SendFailure(ResultType::Unexpected);
        }
    }

    /**
     * @brief Retreive a block from portfolio and send to serial port
     */
    void RetrieveBlock()
    {
        checksum = 0;
        SendByte(0x5A);     // Z start
        if (ReadByte() != 0xA5) {
            Manager::SendFailure(ResultType::Unexpected);
            return;
        }

        // Acknowledge the packet
        Manager::SendSuccess();

        int lenL = ReadByteChecksum();
        int lenH = ReadByteChecksum();
        int length = (lenH << 8) | lenL;

        // Start a frame
        Manager::StartFrame();

        for (int i = 0; i < length; i++) 
        {
            Manager::SendFrameByte(ReadByteChecksum());
        }

        byte blockChecksum = (byte)(256 - ReadByte());

        if (blockChecksum == checksum) 
        {
            Manager::EndFrame();
        } else {
            Manager::SendFailure(ResultType::Unexpected);
            return;
        }

        delayMicroseconds(100);
        SendByte((byte)(256 - checksum));
    }

    /**
     * @brief List the files
     */
    void ListFiles()
    {
        WaitForServer();
        SendBlock();
        RetrieveBlock();
    }

    /**
     * @brief Send a file
     */
    void SendFile()
    {

    }

    /**
     * @brief Retreive a file
     */
    void RetreiveFile()
    {

    }
}