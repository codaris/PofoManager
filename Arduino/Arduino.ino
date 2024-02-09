#include "Portfolio.h"
#include "Manager.h"

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