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
using System.Collections.Generic;
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
	internal class FIEncodingAlgorithmManager
	{
		#region Consts
		private const byte ENCODING_ALGORITHM_COUNT = 10;
		private const byte EXTENDED_ENCODING_ALGORITHM_START = 32;
		private const int EXTENDED_ENCODING_ALGORITHM_MAX = 256;
		#endregion

		#region Constructors
		internal FIEncodingAlgorithmManager()
		{
			_internalEncodingAlgorithm = null;
			_uriToEncodingMap = null;
		}
		#endregion

		internal void Add(FIEncodingAlgorithm encoding)
		{
			string uri = encoding.URI.ToString();

			if (_uriToEncodingMap == null)
				_uriToEncodingMap = new Dictionary<string, FIEncodingAlgorithm>();
			else
				if (_uriToEncodingMap.ContainsKey(uri))
					throw new LtFastInfosetException("An encoding algorithm already exists for URI " + uri);

			_uriToEncodingMap.Add(uri, encoding);
		}

		internal FIEncodingAlgorithm Encoding(string uri)
		{
			if (_uriToEncodingMap != null)
			{
				FIEncodingAlgorithm encoding = null;
				if (_uriToEncodingMap.TryGetValue(uri, out encoding))
					return encoding;
			}

			return null;
		}

		internal FIEncoding Encoding(int fiTableIndex)
		{
			FIEncoding encoding = null;

			if (fiTableIndex > 0)
			{
				if (fiTableIndex < EXTENDED_ENCODING_ALGORITHM_START)
				{
					if (fiTableIndex <= ENCODING_ALGORITHM_COUNT)
					{
						if (_internalEncodingAlgorithm == null)
							_internalEncodingAlgorithm = new InternalEncodingAlgorithm();

						_internalEncodingAlgorithm.Encoding = (InternalEncodingAlgorithm.EncodingType)fiTableIndex;
						encoding = _internalEncodingAlgorithm;
					}
				}
				else if ((_uriToEncodingMap != null) && (fiTableIndex < EXTENDED_ENCODING_ALGORITHM_MAX))
				{
					// index - 1 to move from FI table index to list index
					int realIndex = fiTableIndex - 1;
					if (realIndex < _uriToEncodingMap.Count)
					{
						Dictionary<string, FIEncodingAlgorithm>.Enumerator e = _uriToEncodingMap.GetEnumerator();
						while (e.MoveNext())
						{
							if (e.Current.Value.TableIndex == fiTableIndex)
							{
								encoding = e.Current.Value;
								break;
							}
						}
					}
				}
			}

			return encoding;
		}

		private InternalEncodingAlgorithm _internalEncodingAlgorithm;
		private Dictionary<string, FIEncodingAlgorithm> _uriToEncodingMap;
	}
}
