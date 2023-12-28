#include "Portfolio.h"
#include "Manager.h"
#include "Result.h"

namespace Portfolio
{
    /** Portfolio pins */
    const int  PIN_INPUT_CLOCK = 2;     // pin sub D25 pin 12 (White -> White)
    const int  PIN_INPUT_DATA = 3;      // pin sub D25 pin 13 (Blue -> Gray)
    const int  PIN_OUTPUT_CLOCK = 4;    // pin sub D25 pin 3 (Yellow -> Purple)
    const int  PIN_OUTPUT_DATA = 5;     // pin sub D25 pin 2 (Green -> Blue)

    /** @brief The checksum */
    byte checksum = 0;
  
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
     * @brief Wait for the specified pin to go low
     * 
     * @param pin       The pin to wait on
     * @param timeout   The timeout amount in milliseconds
     * @return True if success or false for timeout
     */
    bool WaitForLow(uint8_t pin, int timeout) 
    {
        unsigned long startTime = millis();
        while(digitalRead(PIN_INPUT_CLOCK)) {
            if ((millis() - startTime) > timeout) return false;
        }
        return true;
    }

    /**
     * @brief Wait for the specified pin to go high
     * 
     * @param pin       The pin to wait on
     * @param timeout   The timeout amount in milliseconds
     * @return True if success or false for timeout
     */
    bool WaitForHigh(uint8_t pin, int timeout) 
    {
        unsigned long startTime = millis();
        while(!digitalRead(PIN_INPUT_CLOCK)) {
            if ((millis() - startTime) > timeout) return false;
        }
        return true;
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
     * @brief Synchronize
     */
    void WaitForServer()
    {
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
        }        

        Manager::SendSuccess();
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
            return;
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
}