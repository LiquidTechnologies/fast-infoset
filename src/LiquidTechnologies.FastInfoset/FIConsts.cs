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
	/// <summary>
	/// Summary description for FastInfosetConsts.
	/// </summary>
	internal class FIConsts
	{
		internal static byte[] BIT_MASKS = { 0x0, 0x1, 0x3, 0x7, 0xF, 0x1F, 0x3F, 0x7F };
		internal const int TWO_POWER_TWENTY = 0x100000;

		// FastInfoset Header Block Constants
		internal const short FI_HEADER_SIZE = 4;
		internal static byte[][] FI_DECLARATION_LIST =
		{
			Encoding.UTF8.GetBytes("<?xml encoding='finf'?>"),
			Encoding.UTF8.GetBytes("<?xml encoding='finf' standalone='yes'?>"),
			Encoding.UTF8.GetBytes("<?xml encoding='finf' standalone='no'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='finf'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='finf' standalone='yes'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='finf' standalone='no'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.1' encoding='finf'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.1' encoding='finf' standalone='yes'?>"),
			Encoding.UTF8.GetBytes("<?xml version='1.1' encoding='finf' standalone='no'?>")
		};

		internal static int FI_DECLARATION_START_TAG_CHAR_LEN = 6; // '<?xml '
		internal static int FI_DECLARATION_END_TAG_CHAR_LEN = 2; // '?>'

		internal const string FI_DEFAULT_PREFIX = "xml";
		internal const string FI_DEFAULT_NAMESPACE = "http://www.w3.org/XML/1998/namespace";

		internal const string FI_XML_NAMESPACE_NAME = "xmlns";
		internal const string FI_XML_NAMESPACE = "http://www.w3.org/2000/xmlns/";

		internal const string FI_DECLARATION_NAME = "xml";

		internal const byte FI_TERMINATOR = 0xF0;
		internal const byte FI_DOUBLE_TERMINATOR = 0xFF;

		// FastInfoset Document 7 Optional Components Constants
		// NOTE: The most significant bit is never set as this is alignment padding
		internal const byte DOCUMENT_ADDITIONAL_DATA = 0x40;
		internal const byte DOCUMENT_INITIAL_VOCABULARY = 0x20;
		internal const byte DOCUMENT_NOTATIONS = 0x10;
		internal const byte DOCUMENT_UNPARSED_ENTITIES = 0x08;
		internal const byte DOCUMENT_CHARACTER_ENCODING_SCHEME = 0x04;
		internal const byte DOCUMENT_STANDALONE = 0x02;
		internal const byte DOCUMENT_VERSION = 0x01;

		// FastInfoset Document Initial Vocabulary Constants
		// NOTE: The most significant 3 bits are never set as this is alignment padding

		internal const byte INITIAL_VOCABULARY_EXTERNAL_VOCABULARY = 0x10;
		internal const byte INITIAL_VOCABULARY_RESTRICTED_ALPHABETS = 0x08;
		internal const byte INITIAL_VOCABULARY_ENCODING_ALGORITHMS = 0x04;
		internal const byte INITIAL_VOCABULARY_PREFIXES = 0x02;
		internal const byte INITIAL_VOCABULARY_NAMESPACE_NAMES = 0x01;

		internal const byte INITIAL_VOCABULARY_LOCAL_NAMES = 0x80;
		internal const byte INITIAL_VOCABULARY_OTHER_NCNAMES = 0x40;
		internal const byte INITIAL_VOCABULARY_OTHER_URIS = 0x20;
		internal const byte INITIAL_VOCABULARY_ATTRIBUTE_VALUES = 0x10;
		internal const byte INITIAL_VOCABULARY_CONTENT_CHARACTER_CHUNKS = 0x08;
		internal const byte INITIAL_VOCABULARY_OTHER_STRINGS = 0x04;
		internal const byte INITIAL_VOCABULARY_ELEMENT_NAME_SURROGATES = 0x02;
		internal const byte INITIAL_VOCABULARY_ATTRIBUTE_NAME_SURROGATES = 0x01;

		// Encoded Character String options
		internal const byte ENCODED_CHARACTER_STRING_UTF8 = 0;
		internal const byte ENCODED_CHARACTER_STRING_UTF16BE = 1;
		internal const byte ENCODED_CHARACTER_STRING_RESTRICTED_ALPHABET = 2;
		internal const byte ENCODED_CHARACTER_STRING_ENCODING_ALGORITHM = 3;

		internal const int ENCODING_TABLE_MIN = 1;
		internal const int ENCODING_TABLE_MAX = 256;
	}
}
