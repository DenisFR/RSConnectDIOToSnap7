# RSConnectDIOToSnap7
This is a RobotStudio Smart Component to connect DI/DO to SIEMENS PLC using Snap7 library.
## What you have to do before compiling:
  - Update ABB.Robotics.* References to Good RobotStudio SDK Version path with ***Project*** - ***Add Reference*** - ***Browse***.
  - On Project Properties:
    - **Application**: Choose good .NET Framework version.
    - **Build Events**: *Post Build Events*: Replace with the good LibraryCompiler.exe Path.
    - **Debug**: *Start External Program*: Replace with the good RobotStudio.exe Path `This not work if project on network drive, let it clear.`
  - In *\RSConnectDIOToSnap7\RSConnectDIOToSnap7.en.xml*:
    - Replace **xsi:schemaLocation** value with good one.
  - Same for *\RSConnectDIOToSnap7\RSConnectDIOToSnap7.xml*.

### If your project path is on network drive:
##### To get RobotStudio load it:
  - In *$(RobotStudioPath)\Bin\RobotStudio.exe.config* file:
    - Add in section *`<configuration><runtime>`*
      - `<loadFromRemoteSources enable="true"/>`

##### To Debug it:
  - Start first RobotStudio to get RobotStudio.exe.config loaded.
  - Then attach its process in VisualStudio ***Debug*** - ***Attach to Process..***