# ![Icon](./src/Assets/32.png) Region to Share Ex

A Windows helper app to share only a part of a screen via video conference apps that only support either full screen or single window like e.g. Teams, WebEx, etc.

> Region to Share Ex is a fork of [tom-englert/RegionToShare](https://github.com/tom-englert/RegionToShare). As of v2 the screen capture is GPU-accelerated via Windows.Graphics.Capture and the app runs on .NET 10.

## How it works

This tool simply mirrors the content of a screen region into a hidden window. In your meeting app you then can just share the content of this hidden window.

**Region to Share is not aware of your meeting app nor what the meeting app is doing with the content of the window.**
It's up to your meeting app whether it properly shares this hidden windows content or not - if it's not working as expected, there is nothing Region to Share can do about this.

## Limitations

- The shared region must lie on a **single monitor**. Sharing a region that spans two monitors is currently not supported - the part outside the primary monitor will stay black.

## Prerequisites

- Windows 10, version 2004 (build 19041) or newer, or Windows 11

The released `.exe` is self-contained, so no separate .NET runtime needs to be installed. (Building from source requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).)

## Installation

- Download the latest `RegionToShareEx-*-win-x64.exe` from the [release page](https://github.com/MarcFauser/RegionToShareEx/releases) and run it.

## Usage

### Tutorial

Watch this great tutorial by James Montemagno

[![Watch the tutorial](https://img.youtube.com/vi/4WVY-mFPFNI/hqdefault.jpg)](https://www.youtube.com/embed/4WVY-mFPFNI)

### Quick Start

- Start the "RegionToShareEx" app.
- Move the window to the region you want to share.
- In your meeting app start sharing the window "Region to Share Ex".

![StartSharing](./src/Assets/StartSharing.gif)

- Now click the "Region to Share Ex" window to start sharing the selected region.
  The window will change to the region selection frame, and others are seeing what's inside this frame.
- Close the region frame to stop showing the region without stopping to share.

![ShowRegion](./src/Assets/ShowRegion.gif)

## Feedback 😄

If you like this tool, don't forget to ⭐ it.
