#include "Portfolio.h"
#include "Manager.h"

#ifndef SERIAL_RX_BUFFER_SIZE
#error "SERIAL_RX_BUFFER_SIZE is not defined."
#endif

#if SERIAL_RX_BUFFER_SIZE < 64
#error "Serial RX buffer is too small. Must be at least 64 bytes."
#endif

#ifndef SERIAL_TX_BUFFER_SIZE
#error "SERIAL_TX_BUFFER_SIZE is not defined."
#endif

#if SERIAL_TX_BUFFER_SIZE < 64
#error "Serial TX buffer is too small. Must be at least 64 bytes."
#endif


// For blinking the LCD while running
unsigned long ledMillis = 0;        // will store last time LED was updated
const long ledInterval = 1000;      // LED blink interval


/**
 * @brief Setup the Ardunio
 */
void setup() 
{
    Serial.begin(115200);
    Portfolio::Enable();
    pinMode(LED_BUILTIN, OUTPUT);
}

/**
 * @brief The main loop
 */
void loop() 
{  
    // Process manager operations
    Manager::Task();    

    unsigned long currentMillis = millis();
    if (currentMillis - ledMillis >= ledInterval) {
        // save the last time you blinked the LED
        ledMillis = currentMillis;
        // Flip the LED
        digitalWrite(LED_BUILTIN, digitalRead(LED_BUILTIN) == LOW);
    }    
}