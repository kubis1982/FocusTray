# MSIX Package Assets

This directory contains the visual assets required for the MSIX package in Microsoft Store.

## Required Assets

### Store Logo (StoreLogo.png)
- Size: 50x50 pixels
- Format: PNG
- Purpose: Displayed in Partner Center and Store admin pages

### Square 44x44 Logo (Square44x44Logo.png)
- Size: 44x44 pixels
- Format: PNG
- Purpose: App tile in Start menu and taskbar

### Square 150x150 Logo (Square150x150Logo.png)
- Size: 150x150 pixels
- Format: PNG
- Purpose: Medium tile size in Start menu

### Splash Screen (SplashScreen.png)
- Size: 620x300 pixels
- Format: PNG
- Purpose: Shown while app is launching

## Creating Assets from favicon.ico

```powershell
# choco install imagemagick

# Extract and resize from ICO
magick convert src/FocusTray/Resources/favicon.ico -resize 50x50 build/msix/Assets/StoreLogo.png
magick convert src/FocusTray/Resources/favicon.ico -resize 44x44 build/msix/Assets/Square44x44Logo.png
magick convert src/FocusTray/Resources/favicon.ico -resize 150x150 build/msix/Assets/Square150x150Logo.png
magick convert src/FocusTray/Resources/favicon.ico -resize 620x300 -background white build/msix/Assets/SplashScreen.png
```