#pragma once
#ifndef __PORTFOLIO_H__
#define __PORTFOLIO_H__

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
     * @brief Wait for server
     */
    void WaitForServer();    

    /**
     * @brief Sends a block to the portfolio
     */
    void SendBlock();

    /**
     * @brief Retrieves a block from the portfolio
     */
    void RetrieveBlock();
}

#endif
