#include "Portfolio.h"
#include "Manager.h"

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
}