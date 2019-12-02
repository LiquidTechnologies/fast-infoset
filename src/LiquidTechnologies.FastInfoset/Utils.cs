/*
 *  Liquid Fast Infoset - XML Compression Library
 *  Copyright © 2001-2011 Liquid Technologies Limited. All rights reserved.
 *  
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Affero General Public License as
 *  published by the Free Software Foundation, either version 3 of the
 *  License, or (at your option) any later version.
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Affero General Public License for more details.
 *  
 *  You should have received a copy of the GNU Affero General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  For product and commercial licensing details please contact us:
 *  http://www.liquid-technologies.com
 */

using System;

namespace LiquidTechnologies.FastInfoset
{
	/// <summary>
	/// Summary description for Util.
	/// </summary>
	internal class Utils
	{
		internal static bool CompareByteArrays(byte[] array1, byte[] array2, int length)
		{
			// assumes both arrays are != null and are >= length
			for (int i = 0; i < length; i++)
			{
				if (array1[i] != array2[i])
				{
					return false;
				}
			}

			return true;
		}
	
		internal static bool CompareByteArrays(byte[] array1, byte[] array2)
		{
			if (array1 != null && array2 != null)
			{
				if (array1.Length == array2.Length)
				{
					return CompareByteArrays(array1, array2, array1.Length);
				}
			}
			else if (array1 == null && array2 == null)
			{
				return true;
			}

			return false;
		}

	}
}
