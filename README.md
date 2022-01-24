# Video Compresser
This is a personal project of mine, I needed to compress a lot of videos to make it easier to upload them to the cloud and this was just perfect to leave it over night compressing. The way it works is it converts all the videos to .mp4 using h.265 as the enconder, which is the best enconder I've found so far, it normally reduces the file size by half.

## Usage
Here's a gif of how the current version is used.
![image](./readmeimgs/howToUse.gif)


## Installation
### Windows
Just download the compiled binary from the releases section, uncompress with 7zip or whatever you want and run the VideoCompresser.exe file.

### MacOS
1) Download the compiled binary from the releases section and uncompress it.
2) You need to give permission to ffmpeg to run, before you can run the program for the first time. For this step you'll need to enter the folder you just uncompressed and search for a ffmpeg folder, then double click one of the files and go to the Mac security tab, you'll need to give permission for this file to run on your computer, do the same with the other files in the ffmpeg folder.
3) Double click the file named VideoCompresser inside, you may need to give it permission for running like you did with the ffmpeg files, but this will only happen the first time you open the program.

### Linux
The only way I've found to run the app on linux is installing the .net runtime and using the command "dotnet run {pathOfDll}". So just download the runtime, and the program, the go inside the uncompressed folder and write "dotnet run VideoCompresser.dll".
