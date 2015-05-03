# Introduction #

The entire application can be configured through the use of an XML file. Everything from the background of the main window to the placement of the images of each of the image strips can be configured.


# Details #
The XML structure should be enclosed with XML tags and have each of the following main sections:
  * window
  * imgStrip1
  * imgStrip2
  * imgStrip3
  * imgStrip4

The following is the default XML config that is generated if no other config file is found.

```
<xml>
  <window>
    <width value="800" />
    <height value="600" />
    <background value="" />
  </window>
  <imgStrip1>
    <enabled value="True" />
    <locationX value="123" />
    <locationY value="73" />
    <backgroundcolor value="Black" />
  </imgStrip1>
  <imgStrip2>
    <enabled value="True" />
    <locationX value="123" />
    <locationY value="189" />
    <backgroundcolor value="Black" />
  </imgStrip2>
  <imgStrip3>
    <enabled value="True" />
    <locationX value="123" />
    <locationY value="305" />
    <backgroundcolor value="Black" />
  </imgStrip3>
  <imgStrip4>
    <enabled value="True" />
    <locationX value="123" />
    <locationY value="421" />
    <backgroundcolor value="Black" />
  </imgStrip4>
</xml>
```

## Config Sections ##

Each config section that is available is outlined here

### window ###
The window section currently has three available configurations and is what sets up the main interface window.

  * width - The width of the main window
> > Default:800
  * height - The height of the main window
> > Default:600
  * background - The path to the image to load for the background window
> > Default to nothing (""). When no file is specified the application will load the default monobooth background from the applications resources.

### imgStrip1 ###
This is the configuration for the first image strip picture box.

  * enabled - [or False](True.md) Weather the image box for the first picture in the film strip is displayed or not
> > default: true
  * locationX - The horizontal location of the top left corner of the image location
> > default: 123
  * locationY - The vertical location of the top left corner of the image location
> > default: 73
  * backgroundcolor - The string color value for the background of the image box
> > default: black

### imgStrip2 ###
This is the configuration for the second image strip picture box.

  * enabled - [or False](True.md) Weather the image box for the first picture in the film strip is displayed or not
> > default: true
  * locationX - The horizontal location of the top left corner of the image location
> > default: 123
  * locationY - The vertical location of the top left corner of the image location
> > default: 189
  * backgroundcolor - The string color value for the background of the image box
> > default: black

### imgStrip3 ###
This is the configuration for the third image strip picture box.

  * enabled - [or False](True.md) Weather the image box for the first picture in the film strip is displayed or not
> > default: true
  * locationX - The horizontal location of the top left corner of the image location
> > default: 123
  * locationY - The vertical location of the top left corner of the image location
> > default: 305
  * backgroundcolor - The string color value for the background of the image box
> > default: black

### imgStrip4 ###
This is the configuration for the fourth image strip picture box.

  * enabled - [or False](True.md) Weather the image box for the first picture in the film strip is displayed or not
> > default: true
  * locationX - The horizontal location of the top left corner of the image location
> > default: 123
  * locationY - The vertical location of the top left corner of the image location
> > default: 421
  * backgroundcolor - The string color value for the background of the image box
> > default: black