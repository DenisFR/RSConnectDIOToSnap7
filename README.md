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
  
## Usage
![RSConnectDIOToSnap7](https://raw.githubusercontent.com/DenisFR/RSConnectDIOToSnap7/master/RSConnectDIOToSnap7/RSConnectDIOToSnap7.jpg)
### Properties
  - ***PLC_Addr***:\
IP Address of PLC IPV4.\
For PLCSim, you have to run [NetToPLCSim](http://nettoplcsim.sourceforge.net/) and enter PC IP.
  - ***PLC_Rack***:\
S7_300 Rack=0\
S7_400 See HW Config\
S7_12xx/15xx Rack=0
  - ***PLC_Slot***:
S7_300 Slot=2\
S7_400 See HW Config\
S7_12xx/15xx Slot=1
  - ***DI_Number***:
Number of DI to connect to in Station Logic.\
Change it to get DI appearing.
  - ***DO_Number***:
Number of DO to connect to in Station Logic.\
Change it to get DO appearing.
  - ***DI_Address_x***:
You can use: Ax.y Qx.y Mx.y DBx.DBXy.z
  - ***DO_Address_x***:
You can use: Ex.y Ix.y Mx.y DBx.DBXy.z
### Signals
  - ***Connect***:
Start to connect to PLC.\
If an error occurs, you can check if it is related to [Sharp7](https://sourceforge.net/projects/snap7/files/Sharp7/) or [Snap7](https://sourceforge.net/projects/snap7/) in their [Forum](https://sourceforge.net/p/snap7/discussion/)
  - ***Read***:
Read DI on PLC one time. (Hidden if not connected)\
In simulation running, they are read continuously.\
DO are written each time they change.