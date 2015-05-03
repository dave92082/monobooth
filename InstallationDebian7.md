# Introduction #

This will be the step by step guide for installing and configuring Monobooth on Debian 7


# Base System #

## Requirements During Debian Installation ##

  * Desktop
  * Print Server
  * Standard System Utilities
  * File Server (Optional if you want to share the picture folder)
  * SSH Server (Optional if you want to administer remotely)


## After Debian is installed ##

In order for Monobooth to run, you need to have mono installed on your system as well as a couple other requirements. The steps below will get your system ready and then we can proceed with setting up Monobooth.

  1. Install Mono  (Required for .net)
> ` apt-get install mono-complete`
  1. Install OpenCV (Required for WebCam Video)
> ` apt-get install libopencv-*`
  1. Now that all the requirements are set up, we need to do some linking to get the expected library names to match EMGU-CV. You may need to replace the version number to whatever version apt-get installs in step 2.
> ` ln -s /usr/lib/libopencv_core.so.2.3.1 /usr/lib/opencv_core231 `

> ` ln -s /usr/lib/libopencv_highgui.so.2.3.1 /usr/lib/opencv_highgui231`

That is it for software requirements. Now all that is left is to add a printer to the system. Mono booth will be configured to not print in case you want to save only however right now (as of 1.0.0.1) it will crash if it tries to print and there is no default printer. You can configure a printer and set it to disabled in Debian if you want to run the photobooth without printing but there has to be a printer.

Go to System Tools > Preferences > System Settings > Printers and configure a printer.

# Setting up Monobooth #

  1. Download the monobooth for your cpu type (32-bit or 64-bit) from the Downloads section.
  1. Unzip the contents to a folder of your choice
  1. From a terminal, you can now go the directory where you extracted monobooth and run:
> ` mono monobooth.exe`

## Monobooth configuration ##
Monobooth will default to 800x600. If you set your resolution to 800x600, the entire screen will be taken up for a nice appearance. If you want to use a higher resolution, you can change the settings in the config.xml file, but you will have to also manually change the position of the filmstrip boxes and preview window. See the [Configuration](Configuration.md) Wiki page

If you webcam was detected by Debian, you should now be up and running. to exit out of monobooth (If you have it taking up your full screen) click anywhere inside the monobooth window and press the X key on your keyboard.

# Helpful Information #
  * The filmstrips will be saved in the same folder that you ran monobooth in. I will be making this configurable in a later version
  * The individual images (4 per filmstrip) will be named a GUID and the filmstrip with all 4 images will be named GUID-fs
  * Monobooth will automatically print the filmstrip to fill up a 4x6 photo paper. If you have your printer loaded with photo paper, you should automatically get 2 copies of every filmstrip that you can cut in half