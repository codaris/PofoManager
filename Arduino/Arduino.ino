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
    // Serial.println("Portfolio Interface Test");
}

/**
 * @brief The main loop
 */
void loop() 
{  
    // Process manager operations
    Manager::Task();    

    /*
    Serial.println("Waiting for server...");
    Portfolio::WaitForServer();
    Serial.println("Requesting file list...");
    Portfolio::RequestFileList();
    Serial.println("Wait for key");
    Serial.read();
    /*
    Wait for Portfolio to enter server mode
    digitalWrite(PIN_OUTPUT_CLOCK, HIGH);  
    while (!digitalRead(PIN_INPUT_CLOCK));
    byte value = receiveByte();

    // synchronization 
    while (true) 
    {
        while (digitalRead(PIN_INPUT_CLOCK));    
        digitalWrite(PIN_OUTPUT_CLOCK, LOW);
        while (!digitalRead(PIN_INPUT_CLOCK)); 
        digitalWrite(PIN_OUTPUT_CLOCK, HIGH);
        value = receiveByte();
        Serial.print(value, 16);
        Serial.write(" ");
        Serial.println(value);
    }
    */
}