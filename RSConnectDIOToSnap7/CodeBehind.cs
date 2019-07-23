/*=============================================================================|
|  PROJECT RSConnectDIOToSnap7                                           1.0.1 |
|==============================================================================|
|  Copyright (C) 2018 Denis FRAIPONT                                           |
|  All rights reserved.                                                        |
|==============================================================================|
|  RSConnectDIOToSnap7 is free software: you can redistribute it and/or modify |
|  it under the terms of the Lesser GNU General Public License as published by |
|  the Free Software Foundation, either version 3 of the License, or           |
|  (at your option) any later version.                                         |
|                                                                              |
|  It means that you can distribute your commercial software which includes    |
|  RSConnectDIOToSnap7 without the requirement to distribute the source code   |
|  of your application and without the requirement that your application be    |
|  itself distributed under LGPL.                                              |
|                                                                              |
|  RSConnectDIOToSnap7 is distributed in the hope that it will be useful,      |
|  but WITHOUT ANY WARRANTY; without even the implied warranty of              |
|  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the               |
|  Lesser GNU General Public License for more details.                         |
|                                                                              |
|  You should have received a copy of the GNU General Public License and a     |
|  copy of Lesser GNU General Public License along with RSConnectDIOToSnap7.   |
|  If not, see  http://www.gnu.org/licenses/                                   |
|                                                                              |
|  This project uses Sharp7 from Snap7 project: http://snap7.sourceforge.net/  |
|=============================================================================*/

using System;
using System.Collections.Generic;

using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;

using Sharp7;

namespace RSConnectDIOToSnap7
{
    /// <summary>
    /// Code-behind class for the RSConnectDIOToSnap7 Smart Component.
    /// </summary>
    /// <remarks>
    /// The code-behind class should be seen as a service provider used by the 
    /// Smart Component runtime. Only one instance of the code-behind class
    /// is created, regardless of how many instances there are of the associated
    /// Smart Component.
    /// Therefore, the code-behind class should not store any state information.
    /// Instead, use the SmartComponent.StateCache collection.
    /// </remarks>
    public class CodeBehind : SmartComponentCodeBehind
    {
        public enum IO { Input = 0, Output = 1 };

        private S7Client client;
        private bool bCanConnect = false;
        private bool bPLC_AddrIsValid = false;
        private bool[][] bDIO_AddressIsValid = new bool[Enum.GetNames(typeof(IO)).Length][];
        private double timePassed = 0;

        /// <summary>
        /// Called from [!:SmartComponent.InitializeCodeBehind]. 
        /// </summary>
        /// <param name="component">Smart Component</param>
        public override void OnInitialize(SmartComponent component)
        {
            base.OnInitialize(component);
            CheckClientAndValues(component);

            UpdateIOCount(component, 0, IO.Input);
            UpdateIOCount(component, 0, IO.Output);
        }

        /// <summary>
        /// Called when the library or station containing the SmartComponent has been loaded 
        /// </summary>
        /// <param name="component">Smart Component</param>
        public override void OnLoad(SmartComponent component)
        {
            base.OnLoad(component);

            bDIO_AddressIsValid[(int)IO.Input] = new bool[0];
            bDIO_AddressIsValid[(int)IO.Output] = new bool[0];

            CheckClientAndValues(component);
            Disconnect(component);

            //component.Properties not initialized yet;
            UpdateIOCount(component, 0, IO.Input);
            UpdateIOCount(component, 0, IO.Output);
        }

        /// <summary>
        /// Called when the value of a dynamic property value has changed.
        /// </summary>
        /// <param name="component"> Component that owns the changed property. </param>
        /// <param name="changedProperty"> Changed property. </param>
        /// <param name="oldValue"> Previous value of the changed property. </param>
        public override void OnPropertyValueChanged(SmartComponent component, DynamicProperty changedProperty, Object oldValue)
        {
            base.OnPropertyValueChanged(component, changedProperty, oldValue);

            if (changedProperty.Name == "DI_Number")
            {
                UpdateIOCount(component, (int)oldValue, IO.Input);
            }
            if (changedProperty.Name == "DO_Number")
            {
                UpdateIOCount(component, (int)oldValue, IO.Output);
            }

            //Make sure client is initialized before connect.
            CheckClientAndValues(component);
        }

        /// <summary>
        /// Called when the value of an I/O signal value has changed.
        /// </summary>
        /// <param name="component"> Component that owns the changed signal. </param>
        /// <param name="changedSignal"> Changed signal. </param>
        public override void OnIOSignalValueChanged(SmartComponent component, IOSignal changedSignal)
        {
            if (changedSignal.Name == "Connect")
            {
                //Make sure client is initialized before connect.
                CheckClientAndValues(component);

                if (((int)changedSignal.Value != 0) && bCanConnect)
                    Connect(component);
                else
                    Disconnect(component);
            }
            if ((changedSignal.Name == "Read") && ((int)changedSignal.Value != 0))
            {
                Read(component);
            }
            if (changedSignal.Name.Contains("DI_"))
            {
                if (client.Connected)
                {
                    int diNumber = -1;
                    int.TryParse(Right(changedSignal.Name, changedSignal.Name.Length - 3), out diNumber);
                    if (diNumber >= 0)
                    {
                        S7Client.S7DataItem item = new S7Client.S7DataItem();
                        if (GetS7DataItem((string)component.Properties["DI_Address_" + diNumber.ToString()].Value, ref item))
                        {
                            byte[] b = new byte[1];
                            byte.TryParse(changedSignal.Value.ToString(), out b[0]);
                            int err = S7Consts.errCliInvalidBlockType;
                            err = client.WriteArea(item.Area, item.DBNumber, item.Start, item.Amount, item.WordLen, b);
                            LogError(component, err);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called during simulation.
        /// </summary>
        /// <param name="component"> Simulated component. </param>
        /// <param name="simulationTime"> Time (in ms) for the current simulation step. </param>
        /// <param name="previousTime"> Time (in ms) for the previous simulation step. </param>
        /// <remarks>
        /// For this method to be called, the component must be marked with
        /// simulate="true" in the xml file.
        /// </remarks>
        public override void OnSimulationStep(SmartComponent component, double simulationTime, double previousTime)
        {
            timePassed += simulationTime - previousTime;
            if (timePassed > 10)
            {
                timePassed = 0;
                Read(component);
            }
        }

        /// <summary>
        /// Called to validate the value of a dynamic property with the CustomValidation attribute.
        /// </summary>
        /// <param name="smartComponent">Component that owns the changed property.</param>
        /// <param name="property">Property that owns the value to be validated.</param>
        /// <param name="newValue">Value to validate.</param>
        /// <returns>Result of the validation. </returns>
        public override ValueValidationInfo QueryPropertyValueValid(SmartComponent smartComponent, DynamicProperty property, object newValue)
        {
            bCanConnect = false;
            if (property.Name == "PLC_Addr")
            {
                bPLC_AddrIsValid = false;
                System.Net.IPAddress ip;
                if (!System.Net.IPAddress.TryParse((string)newValue, out ip))
                    return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);
                bPLC_AddrIsValid = true;
            }
            if (property.Name.StartsWith("DI_Address_"))
            {
                ValueValidationInfo vvi = DIValidationInfo(smartComponent, property, newValue);
                if (!vvi.Equals(ValueValidationInfo.Valid))
                {
                    return vvi;
                }
            }
            if (property.Name.StartsWith("DO_Address_"))
            {
                int doNumber = -1;
                int.TryParse(Right(property.Name, property.Name.Length - 11), out doNumber);
                if ((doNumber >= 0) && (doNumber < bDIO_AddressIsValid[(int)IO.Output].Length))
                {
                    bDIO_AddressIsValid[(int)IO.Output][doNumber] = false;
                    S7Client.S7DataItem item = new S7Client.S7DataItem();
                    if (!GetS7DataItem((string)newValue, ref item))
                        return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);
                    if (item.WordLen != S7Consts.S7WLBit)
                        return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);
                    if ((item.Area != S7Consts.S7AreaPE)
                            && (item.Area != S7Consts.S7AreaMK)
                            && (item.Area != S7Consts.S7AreaDB)
                            )
                        return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);

                    bDIO_AddressIsValid[(int)IO.Output][doNumber] = true;
                }
                else
                    return new ValueValidationInfo(ValueValidationResult.InvalidProject);
            }

            bCanConnect = bPLC_AddrIsValid;
            for (int i = 0; i < bDIO_AddressIsValid[(int)IO.Input].Length; ++i)
                bCanConnect &= bDIO_AddressIsValid[(int)IO.Input][i] || ((int)smartComponent.Properties["DI_Number"].Value == 0);
            for (int i = 0; i < bDIO_AddressIsValid[(int)IO.Output].Length; ++i)
                bCanConnect &= bDIO_AddressIsValid[(int)IO.Output][i] || ((int)smartComponent.Properties["DO_Number"].Value == 0);
            smartComponent.IOSignals["Connect"].UIVisible = bCanConnect;
            return ValueValidationInfo.Valid;
        }
        private ValueValidationInfo DIValidationInfo(SmartComponent smartComponent, DynamicProperty property, object newValue)
        {
            int diNumber = -1;
            int.TryParse(Right(property.Name, property.Name.Length - 11), out diNumber);
            if ((diNumber >= 0) && (diNumber < bDIO_AddressIsValid[(int)IO.Input].Length))
            {
                bDIO_AddressIsValid[(int)IO.Input][diNumber] = false;
                S7Client.S7DataItem item = new S7Client.S7DataItem();
                if (!GetS7DataItem((string)newValue, ref item))
                    return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);
                if (item.WordLen != S7Consts.S7WLBit)
                    return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);
                if ((item.Area != S7Consts.S7AreaPA)
                        && (item.Area != S7Consts.S7AreaMK)
                        && (item.Area != S7Consts.S7AreaDB)
                        )
                    return new ValueValidationInfo(ValueValidationResult.InvalidSyntax);

                bDIO_AddressIsValid[(int)IO.Input][diNumber] = true;
                return ValueValidationInfo.Valid;
            }
            else
                return new ValueValidationInfo(ValueValidationResult.InvalidProject);
        }

        /// <summary>
        /// Mark sure client is initialized.
        /// </summary>
        /// <param name="smartComponent"></param>
        private void CheckClientAndValues(SmartComponent smartComponent)
        {
            if (client == null)
            {
                client = new S7Client();
                Disconnect(smartComponent);
            }

            int diCount = (int)smartComponent.Properties["DI_Number"].Value;
            if (diCount != bDIO_AddressIsValid[(int)IO.Input].Length)// When just loaded array is not initialized
                UpdateIOCount(smartComponent, diCount, IO.Input);
            int doCount = (int)smartComponent.Properties["DO_Number"].Value;
            if (doCount != bDIO_AddressIsValid[(int)IO.Output].Length)// When just loaded array is not initialized
                UpdateIOCount(smartComponent, diCount, IO.Output);

            for (int i = 0; i < smartComponent.Properties.Count; ++i)
            {
                smartComponent.Properties[i].ValidateValue(smartComponent.Properties[i].Value);
            }
        }

        /// <summary>
        /// Connect component to PLC
        /// </summary>
        /// <param name="smartComponent"></param>
        private void Connect(SmartComponent smartComponent)
        {
            int result;
            string ip = (string)smartComponent.Properties["PLC_Addr"].Value;
            int rack = (int)smartComponent.Properties["PLC_Rack"].Value;
            int slot = (int)smartComponent.Properties["PLC_Slot"].Value;
            result = client.ConnectTo(ip, rack, slot);
            LogError(smartComponent, result);
            LockUi(smartComponent, result == 0);
        }

        /// <summary>
        /// Disconnect component from PLC
        /// </summary>
        /// <param name="component"></param>
        private void Disconnect(SmartComponent component)
        {
            client.Disconnect();
            component.IOSignals["Connect"].Value = 0;
            LockUi(component, false);
        }

        /// <summary>
        /// Lock UI-Elements depending on connection status
        /// </summary>
        /// <param name="component"></param>
        /// <param name="bConnected"></param>
        private void LockUi(SmartComponent component, Boolean bConnected)
        {
            component.Properties["Status"].Value = bConnected ? "Connected" : "Disconnected";

            component.Properties["PLC_Addr"].ReadOnly = bConnected;
            component.Properties["PLC_Rack"].ReadOnly = bConnected;
            component.Properties["PLC_Slot"].ReadOnly = bConnected;

            component.Properties["DI_Number"].ReadOnly = bConnected;
            for (int i = 0; i < bDIO_AddressIsValid[(int)IO.Input].Length; ++i)
                component.Properties["DI_Address_" + i.ToString()].ReadOnly = bConnected;

            component.Properties["DO_Number"].ReadOnly = bConnected;
            for (int i = 0; i < bDIO_AddressIsValid[(int)IO.Output].Length; ++i)
                component.Properties["DO_Address_" + i.ToString()].ReadOnly = bConnected;

            component.IOSignals["Read"].UIVisible = (bDIO_AddressIsValid[(int)IO.Input].Length > 0) && bConnected;
        }

        /// <summary>
        /// Read all DI from PLC.
        /// </summary>
        /// <param name="smartComponent"></param>
        private void Read(SmartComponent smartComponent)
        {
            if (client.Connected)
            {
                S7MultiVar reader = new S7MultiVar(client);

                bool allDO_OK = true;
                int doCount = (int)smartComponent.Properties["DO_Number"].Value;
                List<byte[]> listBuffers = new List<byte[]>();
                for (int i = 0; i < doCount; ++i)
                {
                    S7Client.S7DataItem item = new S7Client.S7DataItem();
                    allDO_OK &= GetS7DataItem((string)smartComponent.Properties["DO_Address_" + i.ToString()].Value, ref item);
                    if (allDO_OK)
                    {
                        byte[] buffer = new byte[1];
                        listBuffers.Add(buffer);
                        if (!reader.Add(item.Area, item.WordLen, item.DBNumber, item.Start, item.Amount, ref buffer))
                            LogError(smartComponent, S7Consts.errCliInvalidParams);
                    }
                }

                if (allDO_OK && (doCount > 0))
                {
                    int err = S7Consts.errCliInvalidBlockType;
                    err = reader.Read();
                    LogError(smartComponent, err);
                    if (err == 0)
                    {
                        for (int i = 0; i < doCount; ++i)
                        {
                            LogError(smartComponent, reader.Results[i]);
                            string diName = "DO_" + i.ToString();
                            if (smartComponent.IOSignals.Contains(diName))
                            {
                                byte[] buffer = listBuffers[i];
                                smartComponent.IOSignals[diName].Value = ((int)buffer[0] != 0 ? 1 : 0);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update DIO list depends DIO_Number
        /// </summary>
        /// <param name="component">Component that owns signals. </param>
        /// <param name="oldCount">Old DIO count</param>
        private void UpdateIOCount(SmartComponent component, int oldCount, IO io)
        {
            String prefix = "";
            IOSignalType iOSignalType = IOSignalType.DigitalInput;
            if (io == IO.Input)
            {
                prefix = "DI";
            }
            else
            {
                prefix = "DO";
                iOSignalType = IOSignalType.DigitalOutput;
            }

            int newIOCount = (int)component.Properties[prefix + "_Number"].Value;
            if (newIOCount > oldCount)
            {
                Array.Resize(ref bDIO_AddressIsValid[(int)io], newIOCount);
                for (int i = oldCount; i < newIOCount; i++)
                {
                    string dioName = prefix + "_" + i.ToString();
                    if (!component.IOSignals.Contains(dioName))
                    {
                        IOSignal ios = new IOSignal(dioName, iOSignalType)
                        {
                            ReadOnly = true,
                            UIVisible = false
                        };
                        component.IOSignals.Add(ios);
                    }
                    string dioAddress = prefix + "_Address_" + i.ToString();
                    if (!component.Properties.Contains(dioAddress))
                    {
                        DynamicProperty idp = new DynamicProperty(dioAddress, "System.String")
                        {
                            Value = "M0.0",
                            ReadOnly = false,
                            UIVisible = true
                        };
                        idp.Attributes["AutoApply"] = "true";
                        idp.Attributes["CustomValidation"] = "true";
                        component.Properties.Add(idp);
                        bDIO_AddressIsValid[(int)io][i] = false;
                    }
                }
            }
            else
            {
                for (int i = oldCount - 1; i >= newIOCount; i--)
                {
                    string dioName = prefix + "_" + i.ToString();
                    if (component.IOSignals.Contains(dioName))
                        component.IOSignals.Remove(dioName);
                    string dioAddress = prefix + "_Address_" + i.ToString();
                    if (component.Properties.Contains(dioAddress))
                        component.Properties.Remove(dioAddress);
                }
                Array.Resize(ref bDIO_AddressIsValid[(int)io], newIOCount);
            }
        }


        /// <summary>
        /// This function logs a textual explanation of the error code
        /// </summary>
        /// <param name="component"></param>
        /// <param name="result"></param>
        private void LogError(SmartComponent component, int result)
        {
            if (result != 0)
            {
                Disconnect(component);
                component.Properties["Status"].Value = client.ErrorText(result);
                Logger.AddMessage(new LogMessage("WW: " + component.Name + " error: " + client.ErrorText(result), LogMessageSeverity.Warning));
            }
        }

        /// <summary>
        /// Returns a String containing a specified number of characters from the left side of a string.
        /// </summary>
        /// <param name="value">String expression from which the leftmost characters are returned. If string contains Null, Empty is returned.</param>
        /// <param name="length">Numeric expression indicating how many characters to return. If 0, a zero-length string ("") is returned. If greater than or equal to the number of characters in string, the entire string is returned.</param>
        /// <returns></returns>
        private string Left(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || (length <= 0)) return string.Empty;

            return value.Length <= length ? value : value.Substring(0, length);
        }

        /// <summary>
        /// Returns a String containing a specified number of characters from the right side of a string.
        /// </summary>
        /// <param name="value">String expression from which the rightmost characters are returned. If string contains Null, Empty is returned.</param>
        /// <param name="length">Numeric expression indicating how many characters to return. If 0, a zero-length string ("") is returned. If greater than or equal to the number of characters in string, the entire string is returned.</param>
        /// <returns></returns>
        private string Right(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || (length <= 0)) return string.Empty;

            return value.Length <= length ? value : value.Substring(value.Length - length);
        }

        /// <summary>
        /// Returns a Boolean value indicating whether an expression can be evaluated as an integer.
        /// </summary>
        /// <param name="value">String expression containing an integer expression or string expression.</param>
        /// <returns>IsInteger returns True if the entire expression is recognized as an integer; otherwise, it returns False.</returns>
        private bool IsInteger(string value)
        {
            int output;
            return int.TryParse(value, out output);
        }

        /// <summary>
        /// Populate S7DataItem struct depends name (ex: MB500).
        /// </summary>
        /// <param name="name">Name of data.</param>
        /// <param name="item">Struct to be populated.</param>
        /// <returns>True if name is in good syntax.</returns>
        private bool GetS7DataItem(string name, ref S7Client.S7DataItem item)
        {
            string strName = name.ToUpper();
            if (string.IsNullOrEmpty(strName))
                return false;

            if (strName.Substring(0, 1) == "M")
            {
                item.Area = S7Consts.S7AreaMK;
                if (strName.Length < 2) //Mx0 || M0.
                    return false;
                item.WordLen = GetWordLength(strName.Substring(1, 1));
                string strOffset = Right(strName, strName.Length - (item.WordLen == S7Consts.S7WLBit ? 1 : 2));
                int offset;
                if (!TryGetOffset(strOffset, item.WordLen, out offset))
                    return false;
                item.Start = offset;
                item.Amount = 1;
            }
            else if ((strName.Substring(0, 1) == "A") || (strName.Substring(0, 1) == "Q"))
            {
                item.Area = S7Consts.S7AreaPA;
                if (strName.Length < 2) //Ax0 || A0.
                    return false;
                item.WordLen = GetWordLength(strName.Substring(1, 1));
                string strOffset = Right(strName, strName.Length - (item.WordLen == S7Consts.S7WLBit ? 1 : 2));
                int offset;
                if (!TryGetOffset(strOffset, item.WordLen, out offset))
                    return false;
                item.Start = offset;
                item.Amount = 1;
            }
            else if ((strName.Substring(0, 1) == "E") || (strName.Substring(0, 1) == "I"))
            {
                item.Area = S7Consts.S7AreaPE;
                if (strName.Length < 2) //Ex0 || E0.
                    return false;
                item.WordLen = GetWordLength(strName.Substring(1, 1));
                string strOffset = Right(strName, strName.Length - (item.WordLen == S7Consts.S7WLBit ? 1 : 2));
                int offset;
                if (!TryGetOffset(strOffset, item.WordLen, out offset))
                    return false;
                item.Start = offset;
                item.Amount = 1;
            }
            else if ((strName.Length >= 2) && (strName.Substring(0, 2) == "DB"))
            {
                item.Area = S7Consts.S7AreaDB;
                if (strName.Length < 3) //DB0
                    return false;
                string strDBNumber = Right(strName, strName.Length - 2);
                strDBNumber = Left(strDBNumber, strDBNumber.IndexOf(".") == -1 ? strDBNumber.Length : strDBNumber.IndexOf("."));
                int dbNumber;
                if (!int.TryParse(strDBNumber, out dbNumber))
                    return false;
                item.DBNumber = dbNumber;
                int index = strName.IndexOf(".DB");
                if ((index < 0) || (strName.Length < (index + 4))) //.DBx
                    return false;
                item.WordLen = GetWordLength(strName.Substring(index + 3, 1));
                string strOffset = Right(strName, strName.Length - index - 4); //.DBx = 4
                int offset;
                if (!TryGetOffset(strOffset, item.WordLen, out offset))
                    return false;
                item.Start = offset;
                item.Amount = 1;
            }
            else
                return false;

            return true;
        }

        /// <summary>
        /// Return WordLength depends char
        /// </summary>
        /// <param name="word">Char to design type.</param>
        /// <returns>By default returns S7WLBit.</returns>
        private int GetWordLength(string word)
        {
            if (word.ToUpper() == "X")
                return S7Consts.S7WLBit;
            if (word.ToUpper() == "B")
                return S7Consts.S7WLByte;
            else if (word.ToUpper() == "W")
                return S7Consts.S7WLWord;
            else if (word.ToUpper() == "D")
                return S7Consts.S7WLDWord;
            return S7Consts.S7WLBit;
        }

        /// <summary>
        /// Try to get offset depends WordLenght
        /// </summary>
        /// <param name="strOffset">Address of memory.</param>
        /// <param name="wordLenght">Lenght of memory.</param>
        /// <param name="offset">Offset of data from address. M10.1 == 81 </param>
        /// <returns>True if success.</returns>
        private bool TryGetOffset(string strOffset, int wordLenght, out int offset)
        {
            offset = -1;
            if (wordLenght == S7Consts.S7WLBit)
            {
                if (!strOffset.Contains("."))
                    return false;
                string[] split = strOffset.Split('.');
                if (split.Length != 2)
                    return false;
                string strByt = split[0];
                int byt = -1;
                if (!int.TryParse(strByt, out byt))
                    return false;
                string strBit = split[1];
                int bit = -1;
                if (!int.TryParse(strBit, out bit))
                    return false;
                offset = (byt * 8) + bit;
            }
            else
            {
                if (!int.TryParse(strOffset, out offset))
                    return false;
            }
            return true;
        }
    }
}
