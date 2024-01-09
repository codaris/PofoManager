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

    const int TIMEOUT = 1000;           // 1 second timeout

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
    bool WaitForLow(uint8_t pin, int timeout = TIMEOUT) 
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
    bool WaitForHigh(uint8_t pin, int timeout = TIMEOUT) 
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
    Result ReadByte(int timeout = TIMEOUT)
    {
        int value = 0;
        for (int i = 0; i < 4; i++) {
            if (!WaitForLow(PIN_INPUT_CLOCK, timeout)) goto timeout;
            value = (value << 1) | digitalRead(PIN_INPUT_DATA);     // Get bit
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);                    // Clear clock 
            if (!WaitForHigh(PIN_INPUT_CLOCK, timeout)) goto timeout;
            value = (value << 1) | digitalRead(PIN_INPUT_DATA);     // Get bit
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);                   // Set clock
        }
        return value;
        
        // If timeout error return timeout
        timeout:
        digitalWrite(PIN_OUTPUT_CLOCK, LOW);                    // Clear clock 
        return ResultType::Timeout;
    }

    /**
     * @brief Send a byte to the Portfolio
     * @param value     The value to send
     */
    ResultType SendByte(int value, int timeout = TIMEOUT)
    {
        // Delay before sending
        delayMicroseconds(50);

        for (int i = 0; i < 4; i++) {
            if (value & 0x80) digitalWrite(PIN_OUTPUT_DATA, HIGH);  // B
            else digitalWrite(PIN_OUTPUT_DATA, LOW);  
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);    
            value <<= 1;
            if (!WaitForLow(PIN_INPUT_CLOCK)) goto timeout;
            if (value & 0x80) digitalWrite(PIN_OUTPUT_DATA, HIGH);
            else digitalWrite(PIN_OUTPUT_DATA, LOW);          
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);    
            value = value << 1;
            if (!WaitForHigh(PIN_INPUT_CLOCK)) goto timeout;
        }

        return ResultType::Ok;

        timeout:
        digitalWrite(PIN_OUTPUT_CLOCK, LOW);    
        digitalWrite(PIN_OUTPUT_DATA, LOW);          
        return ResultType::Timeout;
    }

    /**
     * @brief   Read a byte from the Portfolio and update checksum
     * @return  A byte from the input
     */    
    Result ReadByteChecksum(int timeout = TIMEOUT) 
    {
        auto value = ReadByte(timeout);
        if (value.HasValue()) checksum += value.Value();
        return value;
    }

    /** 
     * @brief Send a byte to the Portfolio and update checksum
     * @param value     The value to send
     */
    ResultType SendByteChecksum(int value, int timeout = TIMEOUT) 
    {
        checksum -= value; 
        return SendByte(value, timeout);
    }

    /**
     * @brief Synchronize
     */
    void WaitForServer()
    {
        // Wait for start byte from Portfolio
        WaitForHigh(PIN_INPUT_CLOCK, 5000);     // Wait for high
        auto data = ReadByte(5000);
        if (Manager::Error(data)) return;

        // Synchronization 
        while (data.Value() != 90)   // While != 90
        {
            if (!WaitForLow(PIN_INPUT_CLOCK)) goto timeout;
            digitalWrite(PIN_OUTPUT_CLOCK, LOW);
            if (!WaitForHigh(PIN_INPUT_CLOCK)) goto timeout;
            digitalWrite(PIN_OUTPUT_CLOCK, HIGH);
            data = ReadByte();
            if (Manager::Error(data)) return;
        }        

        // Success
        Manager::SendSuccess();
        return;

        // Timeout error
        timeout:
        digitalWrite(PIN_OUTPUT_CLOCK, LOW);
        Manager::SendFailure(ResultType::Timeout);
        return;
    }

    /**
     * @brief Send a block from the serial port to the portfolio
     */
    void SendBlock()
    {
        // Read the block information
        Result length = Manager::WaitReadWord();            // 2 bytes, total length of block
        if (Manager::Error(length)) return;

        // Initialize the buffer with the length
        Manager::InitializeBuffer(length);

        // Reset checksum
        checksum = 0;

        if (length.Value() == 0) return;

        // Wait for Portfolio sync
        while (true) {
            auto data = ReadByte();
            if (Manager::Error(data)) return;
            if (data.Value() == 0x5A) break;
            if (data.Value() == 0x69) continue;
            if (data.Value() == 0xA5) continue;
            if (data.Value() == 0x96) continue;
            Manager::SendFailure(ResultType::Unexpected);
            return;
        }

        // Parsing the header was a success
        Manager::SendSuccess();            

        delay(50);
    
        // Send start of block
        if (Manager::Error(SendByte(0xA5))) return;

        // Send block length
        int lenH = length.Value() >> 8;     
        int lenL = length.Value() & 255;
        if (Manager::Error(SendByteChecksum(lenL))) return;
        if (Manager::Error(SendByteChecksum(lenH))) return;

        // Send the data from serial frames
        while (true) 
        {
            auto data = Manager::ReadBufferByte(1000);
            if (data.IsDone()) break;
            if (Manager::Error(data)) return;
            if (Manager::Error(SendByteChecksum(data.Value()))) return;
        }

        if (Manager::Error(SendByte(checksum))) return;
        auto checkData = ReadByte();
        if (Manager::Error(checkData)) return;

        if (checksum == checkData.Value()) {
            // Block send was success
            Manager::SendSuccess();                 
        } else {
            Manager::SendFailure(ResultType::ChecksumError);
        }
    }

    /**
     * @brief Retreive a block from portfolio and send to serial port
     */
    void RetrieveBlock()
    {
        // Reset checksum
        checksum = 0;

        // Send retrieve command value
        if (Manager::Error(SendByte(0x5A))) return;

        // Retreive the start of block value
        auto startData = ReadByte(5000);
        if (Manager::Error(startData)) return;
        if (startData.Value() != 0xA5) {
            Manager::SendFailure(ResultType::Unexpected);
            return;
        }

        // Get the block length
        auto lenL = ReadByteChecksum();
        if (Manager::Error(lenL)) return;
        auto lenH = ReadByteChecksum();
        if (Manager::Error(lenH)) return;
        int length = (lenH.Value() << 8) | lenL.Value();

        // Start a frame
        Manager::StartFrame();

        for (int i = 0; i < length; i++) 
        {
            auto value = ReadByteChecksum();
            if (Manager::Error(value)) return;
            if (Manager::ReadCancel()) return;
            Manager::SendFrameByte(value.Value());
        }

        // Get the checksum value
        auto checkSumResult = ReadByte();
        if (Manager::Error(checkSumResult)) return;
        byte blockChecksum = (byte)(256 - checkSumResult.Value());

        // If frames match end frame otherwise error
        if (blockChecksum == checksum) 
        {
            Manager::EndFrame();
        } else {
            Manager::SendFailure(ResultType::ChecksumError);
            return;
        }

        // Send the checksum back
        delayMicroseconds(100);
        Manager::Error(SendByte((byte)(256 - checksum)));
    }
}