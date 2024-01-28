![](docs/images/PofoManagerBanner.png)

**Portfolio Manager** is an application for interfacing an [Atari Portfolio](https://en.wikipedia.org/wiki/Atari_Portfolio) vintage portable computer with your desktop PC.  It works together with an [Arduino](https://store.arduino.cc/pages/nano-family) to connect the Portfolio Parallel Interface Adapter to a USB port on your computer.  With this hardware, Portfolio Manger supports the internal file transfer protocol of the Portfolio.

## Requirements

To use this software you will need the following:

* An Atari Portfolio
* An Atari Parallel Interface Adapter
* An Arduino (Arduino Nano)
* A DB25 breakout board

## Arduino Connection Diagram

![](docs/images/PofoMangerNanoConnect.png)

For compatibiltiy with the Atari Portfolio file transfer server it is only necessary to connect to pins 2, 3, 12, 13, and 25 on the DB25 connector.

| Arduino Pin | DB25 Pin | 
|:-----------:|:--------:|
|      2      |  **2**   |
|      3      |  **3**   |
|      4      |    4     |
|      5      |    5     |
|      6      |    6     |
|      7      |  **12**  |
|      8      |  **13**  |
|      9      |    15    |
|      10     |    11    |
|      11     |    10    |
|      GND    |  **25**  | 

## Features

Support for all built-in Portfolio file transfer server features:
* Sending files to Portfolio
* Receiving files from Portfolio
* Listing directors on the Portfolio

## Documentation

Internal documentation for the projet is available by clicking the link below:

* [Portfolio Manager Documentation](https://codaris.github.io/PofoManager/)

## Getting Help

You can get help with this application by using the [Issues](https://github.com/codaris/PofoManager/issues) tab in this project.