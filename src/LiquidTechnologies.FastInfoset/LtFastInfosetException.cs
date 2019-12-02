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
	/// Returns detailed information about the last exception.
	/// </summary>
	public class LtFastInfosetException : Exception 
	{
		/// <summary>
		/// Initializes a new instance of the XmlException class.
		/// </summary>
		public LtFastInfosetException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the XmlException class with a specified error message. 
		/// </summary>
		/// <param name="message">The error description.</param>
		public LtFastInfosetException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the XmlException class.
		/// </summary>
		/// <param name="message">The description of the error condition.</param>
		/// <param name="innerException">The <see cref="Exception"/> that threw the XmlException, if any. This value can be a null reference (Nothing in Visual Basic).</param>
        public LtFastInfosetException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
