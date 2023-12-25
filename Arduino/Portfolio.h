#pragma once
#ifndef __PORTFOLIO_H__
#define __PORTFOLIO_H__

/** Portfolio pins */
const int  PIN_INPUT_CLOCK = 2;     // pin sub D25 pin 12 (White -> White)
const int  PIN_INPUT_DATA = 3;      // pin sub D25 pin 13 (Blue -> Gray)
const int  PIN_OUTPUT_CLOCK = 4;    // pin sub D25 pin 3 (Yellow -> Purple)
const int  PIN_OUTPUT_DATA = 5;     // pin sub D25 pin 2 (Green -> Blue)


namespace Portfolio
{
    /**
     * @brief Initialize the pins for the portfolio
     */
    void Enable();

    /**
     * @brief Disable the pins for the portfolio
     */
    void Disable();

    /**
     * @brief   Read a byte from the Portfolio
     * @return  A byte from the input
     */
    int ReadByte();

    /**
     * @brief Send a byte to the Portfolio
     * @param value     The value to send
     */
    void SendByte(int value);

    /**
     * @brief Synchronize
     */
    void WaitForServer();    

    /**
     * @brief Request file list
     */
    void RequestFileList();

    /**
     * @brief List the files
     */
    void ListFiles();

    void SendFile();

    void RetreiveFile();
}

#endif
