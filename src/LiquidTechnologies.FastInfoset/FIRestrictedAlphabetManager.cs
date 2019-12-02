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
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
	internal class FIRestrictedAlphabetManager
	{
		#region Consts
		private const int GROW_ARRAY_SIZE = 2;

		// Built In Restricted Alphabets
		private const string RESTRICTED_ALPHABET_VALUES_NUMERIC = "0123456789-+.e ";
		private const string RESTRICTED_ALPHABET_VALUES_DATE_TIME = "0123456789-:TZ ";

		private const byte RESTRICTED_ALPHABET_NUMERIC = 1;
		private const byte RESTRICTED_ALPHABET_DATE_TIME = 2;
		private const byte BUILT_IN_RESTRICTED_ALPHABET_COUNT = 2;
		private const byte EXTENDED_RESTRICTED_ALPHABET_START = 16;
		private const int EXTENDED_RESTRICTED_ALPHABET_MAX = 256;
		#endregion

		#region Constructors
		internal FIRestrictedAlphabetManager()
		{
			// initialize array
			_restrictedAlphabets = new FIRestrictedAlphabet[GROW_ARRAY_SIZE];
			_restrictedAlphabetTop = 0;

			// Add built in restricted alphabets
			FIRestrictedAlphabet alphabet = new FIRestrictedAlphabet(RESTRICTED_ALPHABET_VALUES_NUMERIC);
			alphabet.TableIndex = RESTRICTED_ALPHABET_NUMERIC;
			_restrictedAlphabets[_restrictedAlphabetTop] = alphabet;

			alphabet = new FIRestrictedAlphabet(RESTRICTED_ALPHABET_VALUES_DATE_TIME);
			alphabet.TableIndex = RESTRICTED_ALPHABET_DATE_TIME;
			_restrictedAlphabets[++_restrictedAlphabetTop] = alphabet;
		}
		#endregion

		#region Internal Interface
		internal int Add(FIRestrictedAlphabet alphabet)
		{
			// set table index
			alphabet.TableIndex = _restrictedAlphabets.Length + (EXTENDED_RESTRICTED_ALPHABET_START - BUILT_IN_RESTRICTED_ALPHABET_COUNT);

			if (_restrictedAlphabetTop == (_restrictedAlphabets.Length - 1))
			{
				// grow array
				FIRestrictedAlphabet[] destinationArray = new FIRestrictedAlphabet[_restrictedAlphabets.Length + GROW_ARRAY_SIZE];
				if (_restrictedAlphabetTop > 0)
					Array.Copy(_restrictedAlphabets, destinationArray, (int)(_restrictedAlphabetTop + 1));

				_restrictedAlphabets = destinationArray;
			}

			// add new restricted alphabet
			_restrictedAlphabetTop++;
			_restrictedAlphabets[_restrictedAlphabetTop] = alphabet;

			return alphabet.TableIndex;
		}

		internal FIRestrictedAlphabet Alphabet(int fiTableIndex)
		{
			FIRestrictedAlphabet alphabet = null;

			if (fiTableIndex > 0)
			{
				if (fiTableIndex < EXTENDED_RESTRICTED_ALPHABET_START)
				{
					if (fiTableIndex <= BUILT_IN_RESTRICTED_ALPHABET_COUNT)
					{
						// index - 1 to move from FI table index to list index
						alphabet = _restrictedAlphabets[fiTableIndex - 1];
					}
				}
				else if (fiTableIndex < EXTENDED_RESTRICTED_ALPHABET_MAX)
				{
					// to move from FI table index to list index
					int realIndex = fiTableIndex - (EXTENDED_RESTRICTED_ALPHABET_START - BUILT_IN_RESTRICTED_ALPHABET_COUNT);
					if (realIndex < _restrictedAlphabets.Length)
						alphabet = _restrictedAlphabets[realIndex];
				}
			}

			return alphabet;
		}
		#endregion

		#region Member Variables
		private FIRestrictedAlphabet[] _restrictedAlphabets;
		private int _restrictedAlphabetTop;
		#endregion
	}
}
