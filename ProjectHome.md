# monobooth #

The goal of the monobooth project is to create a highly customizable Photo Booth application for use in any number of professionally built booths and DIY built booths. Most of the photo booth software out there now is either expensive, limited, or ugly (or sometimes all of the above). This project hopes to change that and let anyone run a full featured booth with great features.

Rather than re-inventing the wheel on some items, other open source projects will be leveraged to keep development time low and hopefully reduce bugs in the common tasks.

This project is written in C# and is meant to be cross platform on Windows and Linux (using mono). All builds are tested on both Operating systems to make sure everything works across the board.

Currently monobooth only supports webcam's. The project (read I) currently doesn't have a DSLR camera with a USB 2.0 port. Eventually any remote controllable camera over USB AND all webcams supported by Open CV will be supported. For the DIY photo booths, it's a lot cheaper and easier to plug in a $60 Logitech HD webcam rather than a $600 DSLR.

## Features ##
  * Fully Configurable UI through xml config
  * Support for any webcam supported by OpenCV
  * Captures 4 images per film stip
  * Live Preview
  * Countdown overlays on the live preview
  * Printing of filmstrip to default printer
  * Uploading of filmstrip to FTP server
  * Option to save filmstrips for re-printing later
  * Runs on both Linux (Using Mono) and Windows
  * NEW - Ability to change background through config.

## To Do List ##
  * Ability to email finished filmstrip
  * Ability to choose number of copies
  * QR code displayed with link to uploaded filmstrip.
  * Control of software through serial port. (for Arduino integration)
  * Key bindings for people who want to emulate a keyboard with buttons
  * Second screen detection and display of recent film strips
  * Video Messages
  * Facebook/Twitter/Google+ integration