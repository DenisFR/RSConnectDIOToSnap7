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
		private S7Client client;
		private bool bCanConnect = false;
		private bool bPLC_AddrIsValid = false;
		private bool[] bDI_AddressIsValid = { };
		private bool[] bDO_AddressIsValid = { };

		/// <summary>
		/// Called from [!:SmartComponent.InitializeCodeBehind]. 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnInitialize(SmartComponent component)
		{
			///Never Called???
			base.OnInitialize(component);
			CheckClientAndValues(component);

			UpdateDICount(component, 0);
			UpdateDOCount(component, 0);
		}

		/// <summary>
		/// Called when the library or station containing the SmartComponent has been loaded 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnLoad(SmartComponent component)
		{
			base.OnLoad(component);
			CheckClientAndValues(component);
			Disconnect(component);

			//component.Properties not initialized yet;
			UpdateDICount(component, 0);
			UpdateDOCount(component, 0);
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
				UpdateDICount(component, (int)oldValue);
			}
			if (changedProperty.Name == "DO_Number")
			{
				UpdateDOCount(component, (int)oldValue);
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
			if (changedSignal.Name.Contains("DO_"))
			{
				if (client.Connected)
				{
					int doNumber = -1;
					int.TryParse(Right(changedSignal.Name, changedSignal.Name.Length - 3), out doNumber);
					if (doNumber >= 0)
					{
						S7Client.S7DataItem item = new S7Client.S7DataItem();
						if (GetS7DataItem((string)component.Properties["DO_Address_" + doNumber.ToString()].Value, ref item))
						{
							byte[] b = new byte[1];
							byte.TryParse(changedSignal.Value.ToString(), out b[0]);
							int result = 0x01700000;// S7Consts.errCliInvalidBlockType
							result = client.WriteArea(item.Area, item.DBNumber, item.Start, item.Amount, item.WordLen, b);
							ShowResult(component, result);
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
			Read(component);
		}

		/// <summary>
		/// Called to validate the value of a dynamic property with the CustomValidation attribute.
		/// </summary>
		/// <param name="component">Component that owns the changed property.</param>
		/// <param name="property">Property that owns the value to be validated.</param>
		/// <param name="newValue">Value to validate.</param>
		/// <returns>Result of the validation. </returns>
		public override ValueValidationInfo QueryPropertyValueValid(SmartComponent component, DynamicProperty property, object newValue)
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
				int diNumber = -1;
				int.TryParse(Right(property.Name, property.Name.Length - 11), out diNumber);
				if ((diNumber >= 0) && (diNumber < bDI_AddressIsValid.Length))
				{
					bDI_AddressIsValid[diNumber] = false;
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

					bDI_AddressIsValid[diNumber] = true;
				}
				else
					return new ValueValidationInfo(ValueValidationResult.InvalidProject);
			}
			if (property.Name.StartsWith("DO_Address_"))
			{
				int doNumber = -1;
				int.TryParse(Right(property.Name, property.Name.Length - 11), out doNumber);
				if ((doNumber >= 0) && (doNumber < bDO_AddressIsValid.Length))
				{
					bDO_AddressIsValid[doNumber] = false;
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

					bDO_AddressIsValid[doNumber] = true;
				}
				else
					return new ValueValidationInfo(ValueValidationResult.InvalidProject);
			}

			bCanConnect = bPLC_AddrIsValid;
			for (int i = 0; i < bDI_AddressIsValid.Length; ++i)
				bCanConnect &= bDI_AddressIsValid[i] || ((int)component.Properties["DI_Number"].Value == 0);
			for (int i = 0; i < bDO_AddressIsValid.Length; ++i)
				bCanConnect &= bDO_AddressIsValid[i] || ((int)component.Properties["DO_Number"].Value == 0);
			component.IOSignals["Connect"].UIVisible = bCanConnect;
			return ValueValidationInfo.Valid;
		}

		/// <summary>
		/// Mark sure client is initialized.
		/// </summary>
		/// <param name="component"></param>
		private void CheckClientAndValues(SmartComponent component)
		{
			if (client == null)
			{
				client = new S7Client();
				Disconnect(component);
			}

			int diCount = (int)component.Properties["DI_Number"].Value;
			if (diCount != bDI_AddressIsValid.Length)// When just loaded array is not initialized
				UpdateDICount(component, diCount);
			int doCount = (int)component.Properties["DO_Number"].Value;
			if (doCount != bDO_AddressIsValid.Length)// When just loaded array is not initialized
				UpdateDOCount(component, doCount);

			for (int i = 0; i < component.Properties.Count; ++i)
			{
				component.Properties[i].ValidateValue(component.Properties[i].Value);
			}
		}

		/// <summary>
		/// Connect component to PLC
		/// </summary>
		/// <param name="component"></param>
		private void Connect(SmartComponent component)
		{
			int result;
			string ip = (string)component.Properties["PLC_Addr"].Value;
			int rack = (int)component.Properties["PLC_Rack"].Value;
			int slot = (int)component.Properties["PLC_Slot"].Value;
			result = client.ConnectTo(ip, rack, slot);
			ShowResult(component, result);
			UpdateConnected(component, result == 0);
		}

		/// <summary>
		/// Disconnect component to PLC
		/// </summary>
		/// <param name="component"></param>
		private void Disconnect(SmartComponent component)
		{
			client.Disconnect();
			component.IOSignals["Connect"].Value = 0;
			UpdateConnected(component, false);
		}

		/// <summary>
		/// Update each property depends connection status
		/// </summary>
		/// <param name="component"></param>
		/// <param name="bConnected"></param>
		private void UpdateConnected(SmartComponent component, Boolean bConnected)
		{
			component.Properties["Status"].Value = bConnected ? "Connected" : "Disconnected";

			component.Properties["PLC_Addr"].ReadOnly = bConnected;
			component.Properties["PLC_Rack"].ReadOnly = bConnected;
			component.Properties["PLC_Slot"].ReadOnly = bConnected;

			component.Properties["DI_Number"].ReadOnly = bConnected;
			for (int i = 0; i < bDI_AddressIsValid.Length; ++i)
				component.Properties["DI_Address_" + i.ToString()].ReadOnly = bConnected;

			component.Properties["DO_Number"].ReadOnly = bConnected;
			for (int i = 0; i < bDO_AddressIsValid.Length; ++i)
				component.Properties["DO_Address_" + i.ToString()].ReadOnly = bConnected;

			component.IOSignals["Read"].UIVisible = (bDI_AddressIsValid.Length > 0) && bConnected;
		}

		/// <summary>
		/// Read all GI from PLC.
		/// </summary>
		/// <param name="component"></param>
		private void Read(SmartComponent component)
		{
			if (client.Connected)
			{
				S7MultiVar reader = new S7MultiVar(client);

				bool allDI_OK = true;
				int diCount = (int)component.Properties["DI_Number"].Value;
				List<byte[]> list = new List<byte[]>();
				for (int i = 0; i < diCount; ++i)
				{
					S7Client.S7DataItem item = new S7Client.S7DataItem();
					allDI_OK &= GetS7DataItem((string)component.Properties["DI_Address_" + i.ToString()].Value, ref item);
					if (allDI_OK)
					{
						byte[] b = new byte[1];
						list.Add(b);
						if (!reader.Add(item.Area, item.WordLen, item.DBNumber, item.Start, item.Amount, ref b))
							ShowResult(component, 0x00200000);// S7Consts.errCliInvalidParams
					}
				}

				if (allDI_OK && (diCount > 0))
				{
					int result = 0x01700000;// S7Consts.errCliInvalidBlockType
					result = reader.Read();
					ShowResult(component, result);
					if (result == 0)
					{
						for (int i = 0; i < diCount; ++i)
						{
							ShowResult(component, reader.Results[i]);
							string giName = "DI_" + i.ToString();
							if (component.IOSignals.Contains(giName))
							{
								byte[] b = list[i];
								component.IOSignals[giName].Value = ((int)b[0] != 0 ? 1 : 0);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Update DI list depends DI_Number
		/// </summary>
		/// <param name="component">Component that owns signals. </param>
		/// <param name="oldCount">Old DI count</param>
		private void UpdateDICount(SmartComponent component, int oldCount)
		{
			int newDICount = (int)component.Properties["DI_Number"].Value;
			if (newDICount > oldCount)
			{
				Array.Resize(ref bDI_AddressIsValid, newDICount);
				for (int i = oldCount; i < newDICount; i++)
				{
					string diName = "DI_" + i.ToString();
					if (!component.IOSignals.Contains(diName))
					{
						IOSignal ios = new IOSignal(diName, IOSignalType.DigitalOutput);
						ios.ReadOnly = true;
						ios.UIVisible = false;
						component.IOSignals.Add(ios);
					}
					string diAddress = "DI_Address_" + i.ToString();
					if (!component.Properties.Contains(diAddress))
					{
						DynamicProperty idp = new DynamicProperty(diAddress, "System.String");
						idp.Value = "M0.0";
						idp.ReadOnly = false;
						idp.UIVisible = true;
						idp.Attributes["AutoApply"] = "true";
						idp.Attributes["CustomValidation"] = "true";
						component.Properties.Add(idp);
						bDI_AddressIsValid[i] = false;
					}
				}
			}
			else
			{
				for (int i = oldCount - 1; i >= newDICount; i--)
				{
					string diName = "DI_" + i.ToString();
					if (component.IOSignals.Contains(diName))
						component.IOSignals.Remove(diName);
					string diAddress = "DI_Address_" + i.ToString();
					if (component.Properties.Contains(diAddress))
						component.Properties.Remove(diAddress);
				}
				Array.Resize(ref bDI_AddressIsValid, newDICount);
			}
		}

		/// <summary>
		/// Update DO list depends DO_Number
		/// </summary>
		/// <param name="component">Component that owns signals. </param>
		/// <param name="oldCount">Old DO count</param>
		private void UpdateDOCount(SmartComponent component, int oldCount)
		{
			int newDOCount = (int)component.Properties["DO_Number"].Value;
			if (newDOCount > oldCount)
			{
				Array.Resize(ref bDO_AddressIsValid, newDOCount);
				for (int i = oldCount; i < newDOCount; i++)
				{
					string doName = "DO_" + i.ToString();
					if (!component.IOSignals.Contains(doName))
					{
						IOSignal ios = new IOSignal(doName, IOSignalType.DigitalInput);
						ios.ReadOnly = true;
						ios.UIVisible = false;
						component.IOSignals.Add(ios);
					}
					string doAddress = "DO_Address_" + i.ToString();
					if (!component.Properties.Contains(doAddress))
					{
						DynamicProperty idp = new DynamicProperty(doAddress, "System.String");
						idp.Value = "M0.0";
						idp.ReadOnly = false;
						idp.UIVisible = true;
						idp.Attributes["AutoApply"] = "true";
						idp.Attributes["CustomValidation"] = "true";
						component.Properties.Add(idp);
						bDO_AddressIsValid[i] = false;
					}
				}
			}
			else
			{
				for (int i = oldCount - 1; i >= newDOCount; i--)
				{
					string doName = "DO_" + i.ToString();
					if (component.IOSignals.Contains(doName))
						component.IOSignals.Remove(doName);
					string doAddress = "DO_Address_" + i.ToString();
					if (component.Properties.Contains(doAddress))
						component.Properties.Remove(doAddress);
				}
				Array.Resize(ref bDO_AddressIsValid, newDOCount);
			}
		}

		/// <summary>
		/// This function returns a textual explaination of the error code
		/// </summary>
		/// <param name="component"></param>
		/// <param name="result"></param>
		private void ShowResult(SmartComponent component, int result)
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
