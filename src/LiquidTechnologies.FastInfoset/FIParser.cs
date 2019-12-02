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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace LiquidTechnologies.FastInfoset
{
	/// <summary>
	/// Summary description for FastInfosetReader.
	/// </summary>
	internal sealed class FIParser
	{
		private const int NS_GROW_SIZE = 10;

		#region Inner Classes
		internal class StreamBuffer
		{
			internal StreamBuffer(Stream input)
			{
				_buffer = new byte[_blockSize];
				_input = input;
			}
			
			internal void Close()
			{
				_input.Close();
				_input = null;
				_buffer = null;
			}

			internal void MoveBack(int len)
			{
				// WARNING: this method moves back current buffer offset NOT stream
				if (len > _bufferOffset)
					throw new LtFastInfosetException(string.Format("Internal Error in StreamBuffer::MoveBack. Length = {0}, Offset = {1}", len, _bufferOffset));

				_bufferOffset -= len;
			}

			internal byte ReadByte()
			{
				if (_bufferOffset == _bufferSize)
					ReadStream();

				return _buffer[_bufferOffset++];
			}

			internal byte[] ReadBytes(int len)
			{
				if (len < 1)
					throw new LtFastInfosetException("Internal Error in StreamBuffer::ReadBytes. Length = " + len.ToString());

				byte[] outBuffer = new byte[len];

				int bytesLeftInBuffer = (_bufferSize - _bufferOffset);
				if (bytesLeftInBuffer == 0)
				{
					ReadStream();
					bytesLeftInBuffer = _bufferSize;
				}

				int offset = 0;
				int bytesToCopy = len;

				while (true)
				{
					if (bytesLeftInBuffer >= bytesToCopy)
					{
						// enough bytes in buffer, so just copy to output buffer
						Buffer.BlockCopy(_buffer, _bufferOffset, outBuffer, offset, bytesToCopy);
						_bufferOffset += bytesToCopy;
						break;
					}

					// not enough bytes in buffer, so copy what's left and then get more bytes from stream
					Buffer.BlockCopy(_buffer, _bufferOffset, outBuffer, offset, bytesLeftInBuffer);
					offset += bytesLeftInBuffer;
					bytesToCopy -= bytesLeftInBuffer;

					ReadStream();
					bytesLeftInBuffer = _bufferSize;
				}

				return outBuffer;
			}

			private void ReadStream()
			{
				int offset = 0;
				int count = _blockSize;

				do
				{
					int bytesRead = _input.Read(_buffer, offset, count);
					if (bytesRead == 0)
						break;

					offset += bytesRead;
					count -= bytesRead;
				}
				while (count > 0);

				if (offset == 0)
					throw new LtFastInfosetException("Unexpected End of File.");

				// may not have enough bytes, so set buffer size to actual number read
				_bufferSize = offset;
				_bufferOffset = 0;
			}

			private Stream _input;
			private int _blockSize = 4096; // 4K
			private byte[] _buffer;
			private int _bufferSize = 0;
			private int _bufferOffset = 0;
		}

		internal class FINode
		{
			internal struct QNameValue
			{
				internal void Init(string prefix, string ns, string localName)
				{
					qname.Init(prefix, ns, localName);
				}

				internal QualifiedName qname;
				internal string value;
			}

			private const int GROW_SIZE = 10;

			internal FINode()
			{
				_nodeType = XmlNodeType.None;
				_depth = 0;
				_attributeTop = -1;
				_attributes = new QNameValue[GROW_SIZE];
			}

			internal void Init()
			{
				_nodeType = XmlNodeType.None;
				_depth = 0;
				_nodeValue = new QNameValue();
				_attributeTop = -1;
			}

			internal void Init(XmlNodeType nodeType, int depth)
			{
				_nodeType = nodeType;
				_depth = depth;
				_nodeValue = new QNameValue();
				_attributeTop = -1;
			}
			
			internal void Init(XmlNodeType nodeType, int depth, QualifiedName qname)
			{
				_nodeType = nodeType;
				_depth = depth;
				_nodeValue.qname = qname;
				_nodeValue.value = null;
				_attributeTop = -1;
			}

			internal void Init(XmlNodeType nodeType, int depth, string name, string value)
			{
				_nodeType = nodeType;
				_depth = depth;
				_nodeValue.Init(null, null, name);
				_nodeValue.value = value;
				_attributeTop = -1;
			}

			internal void AddAttribute(QNameValue attrNode)
			{
				if (_attributeTop == (_attributes.Length - 1))
				{
					QNameValue[] destinationArray = new QNameValue[_attributes.Length + GROW_SIZE];
					if (_attributeTop > 0)
						Array.Copy(_attributes, destinationArray, _attributeTop + 1);

					_attributes = destinationArray;
				}

				_attributeTop++;
				_attributes[_attributeTop] = attrNode;
			}

			internal void SetAttributes(QNameValue[] nodeList, int count)
			{
				if (_attributes.Length < count)
					_attributes = new QNameValue[count];

				Array.Copy(nodeList, _attributes, count);
				_attributeTop = count - 1;
			}

			internal QNameValue[] Attributes
			{
				get { return _attributes; }
			}

			internal int AttributeCount
			{
				get { return _attributeTop + 1; }
			}

			internal QualifiedName QName
			{
				get { return _nodeValue.qname; }
				set { _nodeValue.qname = value; }
			}

			internal string Value
			{
				get { return _nodeValue.value; }
			}

			internal XmlNodeType NodeType
			{
				get { return _nodeType; }
			}

			internal int Depth
			{
				get { return _depth; }
			}

			private XmlNodeType _nodeType = XmlNodeType.None;
			private QNameValue _nodeValue;
			private int _depth;
			private QNameValue[] _attributes = null;
			private int _attributeTop;
		};
		#endregion

		#region Enums
		private enum EnumStringOrIndex { isoiString, isoiIndex };
		#endregion

		#region Constructors
		internal FIParser(Stream input, FIVocabularyManager vocabularyManager, XmlNameTable nameTable)
		{
			if (nameTable == null)
				_nameTable = new NameTable();
			else
				_nameTable = nameTable;
			
			_nameTable.Add(FIConsts.FI_DEFAULT_PREFIX);
			_nameTable.Add(FIConsts.FI_XML_NAMESPACE_NAME);

			_vocabularyManager = vocabularyManager;
			_buffer = new StreamBuffer(input);

			_lastCharBuffer = new char[1024]; // 1K
			_lastCharBufferUsed = 0;

			_namespaceNodes = new FINode.QNameValue[NS_GROW_SIZE];
			_namespaceNodesTop = -1;
		}
		#endregion

		#region Exposed Interface
		internal FINode Read()
		{
			if (_currentNode == null)
			{
				// first time in so see if we have Declaration
				ReadHeader();
				ReadOptionalData();

				// if no initial vocabulary was set then create it
				if (_vocabulary == null)
					_vocabulary = new FIReaderVocabulary();

				if (_currentNode == null)
				{
					// no XmlDeclaration so read fist child
					_currentNode = new FINode();
					ReadChild();
				}
			}
			else
				ReadChild();
			
			return ((_currentNode.NodeType == XmlNodeType.None) ? null : _currentNode);
		}

		internal void Close()
		{
			_buffer.Close();

			_vocabulary = null;
		}

		internal XmlNameTable NameTable
		{
			get { return _nameTable; }
		}
		#endregion

		#region Move Methods
		// All methods use these methods to move through Fast infoset
		private byte Move()
		{
			_lastByte = _buffer.ReadByte();

			return _lastByte;
		}
		
		private byte[] Move(int length)
		{
			byte[] read = _buffer.ReadBytes(length);
			_lastByte = read[0];

			return read;
		}
		#endregion

		#region Read Methods
		// C.1
		private void ReadHeader()
		{
			// see if file begins with FI Header
			if (!CheckHeader())
			{
				// No FI Header so check for Declaration
				int maxDeclLen = 0;
				foreach (byte[] b in FIConsts.FI_DECLARATION_LIST)
				{
					if (b.Length > maxDeclLen)
						maxDeclLen = b.Length;
				}

				// read in enough bytes to cover longest declaration
				byte[] buffer = Move(maxDeclLen);

				// C.12.3
				bool found = false;
				foreach (byte[] decl in FIConsts.FI_DECLARATION_LIST)
				{
					// check for FastInfoset xml declaration
					if (Utils.CompareByteArrays(buffer, decl, decl.Length))
					{
						// found match so check for header
						_buffer.MoveBack(maxDeclLen - decl.Length);
						if (CheckHeader())
						{
							string strVal = Encoding.UTF8.GetString(decl, 0, decl.Length);
							_currentNode = new FINode();
							_currentNode.Init(XmlNodeType.XmlDeclaration, 0, FIConsts.FI_DECLARATION_NAME, strVal.Substring(FIConsts.FI_DECLARATION_START_TAG_CHAR_LEN, strVal.Length - (FIConsts.FI_DECLARATION_START_TAG_CHAR_LEN + FIConsts.FI_DECLARATION_END_TAG_CHAR_LEN)));
							found = true;
						}
						break;
					}
				}

				// if not found header or declaration and header then invalid document
				if (!found)
                    throw new LtFastInfosetException("Invalid Fast Infoset Document");
			}
		}

		private bool CheckHeader()
		{
			// check first 16 bits for FastInfoset magic number (1110000000000000)
			// and check next 16 bits for FastInfoset version number (0000000000000001)
			byte[] header = Move(FIConsts.FI_HEADER_SIZE);
			if ((header[0] == 0xE0)
				&& (header[1] == 0)
				&& (header[2] == 0)
				&& (header[3] == 0x01))
			{
				return true;
			}

			// not header so rest internal buffer
			_buffer.MoveBack(FIConsts.FI_HEADER_SIZE);

			return false;
		}

		private bool ReadOptionalData()
		{
			byte flags = Move();

			if (flags != 0)
			{
				// ignore first bit (padding)

				if ((flags & FIConsts.DOCUMENT_ADDITIONAL_DATA) != 0)
					AdditionalData();

				if ((flags & FIConsts.DOCUMENT_INITIAL_VOCABULARY) != 0)
					InitialVocabulary();

				// if no initial vocabulary was set then create it
				if (_vocabulary == null)
					_vocabulary = new FIReaderVocabulary();
				
				if ((flags & FIConsts.DOCUMENT_NOTATIONS) != 0)
					Notations();
				
				if ((flags & FIConsts.DOCUMENT_UNPARSED_ENTITIES) != 0)
					UnparsedEntities();
				
				if ((flags & FIConsts.DOCUMENT_CHARACTER_ENCODING_SCHEME) != 0)
					CharacterEncodingScheme();
				
				if ((flags & FIConsts.DOCUMENT_STANDALONE) != 0)
					Standalone();
				
				if ((flags & FIConsts.DOCUMENT_VERSION) != 0)
					Version();
			}

			return true;
		}

		// C.2.11
		//	children                   SEQUENCE (SIZE(0..MAX)) OF 
		//		CHOICE {
		//		element                   Element,
		//		processing-instruction    ProcessingInstruction,
		//		comment                   Comment,
		//		document-type-declaration DocumentTypeDeclaration }
		private void ReadChild()
		{
			_currentNode.Init();
			if (_endElement)
				_endElement = false;
			else
			{
				// read next child
				byte val = Move();
				if (val != FIConsts.FI_TERMINATOR)
				{
					if (val == FIConsts.FI_DOUBLE_TERMINATOR)
					{
						// need to jump back twice
						_endElement = true;
					}
					else
					{
						if (_depth == 0)
						{
							// read top level item
							if (val < 0x80)
								// identifier '0xxxxxxx'
								ElementBit2();
							else if (val == 0xE1)
								// identifier '11100001'
								ProcessingInstruction();
							else if (val == 0xE2)
								// identifier '11100010'
								Comment();
							else if ((val >> 2) == 0x31)
								// identifier '110001xx'
								DocumentTypeDeclarationBit7();
							else if ((val == FIConsts.FI_TERMINATOR) || (val == FIConsts.FI_DOUBLE_TERMINATOR))
								// End of File
								_currentNode = null;
							else
								throw new LtFastInfosetException("Failed to parse FastInfoset. Unknown Child Item.");
						}
						else
							ReadElementChild();
					}
				}
			}

			if (_currentNode != null)
			{
				// if no node then we must be ending current Element
				if ((_currentNode.NodeType == XmlNodeType.None) && (_depth > 0))
					_currentNode.Init(XmlNodeType.EndElement, _depth--);
			}
		}
		#endregion

		#region Optional Data Methods

		// C.2.4	
		//	additional-data         SEQUENCE (SIZE(1..one-meg)) OF
		//		additional-datum SEQUENCE {
		//			id                   URI,
		//			data                 NonEmptyOctetString } OPTIONAL,
		private void AdditionalData()
		{
			// get number of additional-datum...and ignore them
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				// ignore id
				//			string val = NonEmptyOctetString(NonEmptyOctetStringBit2Length());
                Move();
				Move(NonEmptyOctetStringBit2Length());
				// ignore data
				//			string val = NonEmptyOctetString(NonEmptyOctetStringBit2Length());
                Move();
                Move(NonEmptyOctetStringBit2Length());
			}
		}

		// C.2.5
		private void InitialVocabulary()
		{
			// uses 2 bytes to store flags (first 3 bits padding)
			byte flags = Move();
			byte flags2 = Move();

			// first byte
			if ((flags & FIConsts.INITIAL_VOCABULARY_EXTERNAL_VOCABULARY) != 0)
				ExternalVocabulary();

			// if no external vocabulary was set then create our own initial vocabulry
			if (_vocabulary == null)
				_vocabulary = new FIReaderVocabulary();

			if ((flags & FIConsts.INITIAL_VOCABULARY_RESTRICTED_ALPHABETS) != 0)
				RestrictedAlphabets();

			if ((flags & FIConsts.INITIAL_VOCABULARY_ENCODING_ALGORITHMS) != 0)
				EncodingAlogrithms();
			
			if ((flags & FIConsts.INITIAL_VOCABULARY_PREFIXES) != 0)
				Prefixes();
			
			if ((flags & FIConsts.INITIAL_VOCABULARY_NAMESPACE_NAMES) != 0)
				NamespaceNames();

			// second byte
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_LOCAL_NAMES) != 0)
				LocalNames();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_OTHER_NCNAMES) != 0)
				OtherNCNames();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_OTHER_URIS) != 0)
				OtherURIs();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_ATTRIBUTE_VALUES) != 0)
				AttributeValues();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_CONTENT_CHARACTER_CHUNKS) != 0)
				ContentCharacterChunks();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_OTHER_STRINGS) != 0)
				OtherStrings();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_ELEMENT_NAME_SURROGATES) != 0)
				ElementNameSurrogates();
			
			if ((flags2 & FIConsts.INITIAL_VOCABULARY_ATTRIBUTE_NAME_SURROGATES) != 0)
				AttributeNameSurrogates();
		}

		#region Initial Vocabulary Methods

		// C.2.5.2
		// external-vocabulary        URI OPTIONAL,
		private void ExternalVocabulary()
		{
            Move();
			// ignore first bit padding
			NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
			string val = LastString;

			FIReaderVocabulary externalVocabulary = null;
			if (_vocabularyManager != null)
				externalVocabulary = _vocabularyManager.ReaderVocabulary(val);

			if (externalVocabulary == null)
				throw new LtFastInfosetException("Unable to find External Vocabulary for URI " + val);

			// copy external vocabulary
			_vocabulary = new FIReaderVocabulary(externalVocabulary);
		}

		// C.2.5.3
		private void RestrictedAlphabets()
		{
			// get number of restricted-alphabets
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
				NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				_vocabulary.AddRestrictedAlphabet(LastString);
			}
		}

		// C.2.5.3
		private void EncodingAlogrithms()
		{
			// get number of encoding-algorithms
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				string val = LastString;
				if (_vocabulary.EncodingAlgorithm(val) == null)
					throw new LtFastInfosetException("Failed to find Encoding Algorithm for Uri " + val);
			}
		}

		// C.2.5.3
		private void Prefixes()
		{
			// get number of prefixes
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				_vocabulary.AddPrefixName(LastNameString);
			}
		}

		// C.2.5.3
		private void NamespaceNames()
		{
			// get number of namespace-names
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				_vocabulary.AddNamespaceName(LastNameString);
			}
		}

		// C.2.5.3
		private void LocalNames()
		{
			// get number of local-names
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				_vocabulary.AddLocalName(LastNameString);
			}
		}

		// C.2.5.3
		private void OtherNCNames()
		{
			// get number of other-ncnames
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				_vocabulary.AddOtherNCName(LastNameString);
			}
		}

		// C.2.5.3
		private void OtherURIs()
		{
			// get number of other-uris...and ignore them
			for (int item = 0; item < LengthOfSequence(); item++)
			{
                Move();
                NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
//				_vocabulary.AddOtherURI(LastNameString);
			}
		}

		// C.2.5.4
		private void AttributeValues()
		{
			// get attribute-values
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				EncodedCharacterStringBit3();
				_vocabulary.AddAttributeValue(LastString);
			}
		}

		// C.2.5.4
		private void ContentCharacterChunks()
		{
			// get content-character-chunks
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				EncodedCharacterStringBit3();
				_vocabulary.AddContentCharacterChunk(LastString);
			}
		}

		// C.2.5.4
		private void OtherStrings()
		{
			// get other-strings
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				EncodedCharacterStringBit3();
				_vocabulary.AddOtherString(LastString);
			}
		}

		// C.2.5.5
		private void ElementNameSurrogates()
		{
			// get number of element-name-surrogates...and ignore them
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				NonIdentifyingStringOrIndexBit1();
//				_vocabulary.AddElementNameSurrogate(val);
			}
		}

		// C.2.5.5
		private void AttributeNameSurrogates()
		{
			// get number of attribute-name-surrogates...and ignore them
			for (int item = 0; item < LengthOfSequence(); item++)
			{
				NonIdentifyingStringOrIndexBit1();
//				_vocabulary.AddAttributeNameSurrogate(val);
			}
		}
		#endregion

		// C.2.6
		private void Notations()
		{
			// check for identifier '110000xx'
			while (((Move()) >> 2) == 0x30)
			{
				NotationBit7();
			}
		}

		// C.2.7
		private void UnparsedEntities()
		{
			// check for identifier '1101000x'
			while (((Move()) >> 1) == 0x68)
			{
				UnparsedEntityBit8();
			}
		}

		// C.2.8
		private void CharacterEncodingScheme()
		{
			Move();
			Move(NonEmptyOctetStringBit2Length());
		}

		// C.2.9
		private bool Standalone()
		{
			return ((Move()) == 0x01);
		}

		// C.2.10
		private void Version()
		{
			NonIdentifyingStringOrIndexBit1();
		}
		#endregion

		#region Children Methods
		/////////////////////////////////////////////////////////////////////////////////
		// Children Methods

		// C.3
		//	Element ::= SEQUENCE {
		//		namespace-attributes	 SEQUENCE (SIZE(1..MAX)) OF 
		//			NamespaceAttribute OPTIONAL,
		//		qualified-name      	 QualifiedNameOrIndex
		//							-- ELEMENT NAME category --,
		//		attributes          	 SEQUENCE (SIZE(1..MAX)) OF 
		//			Attribute OPTIONAL,
		//		children            	 SEQUENCE (SIZE(0..MAX)) OF 
		//			CHOICE {
		//				element                      Element,
		//				processing-instruction       ProcessingInstruction,
		//				unexpanded-entity-reference  UnexpandedEntityReference,
		//				character-chunk              CharacterChunk,
		//				comment                      Comment }}
		private void ElementBit2()
		{
			// check for identifier 'x1xxxxxx'
			bool bHasAttributes = ((_lastByte & 0x40) != 0);

			// reset ns node stack
			_namespaceNodesTop = -1;

			// check for namespace-attributes 'xx1110xx'
			if (((_lastByte & 0x3F) >> 2) == 0x0E)
			{
				// check for identifier '110011xx'
				while ((Move() >> 2) == 0x33)
				{
					// Read NamespaceAttribute
					if (_namespaceNodesTop == (_namespaceNodes.Length - 1))
					{
						FINode.QNameValue[] destinationArray = new FINode.QNameValue[_namespaceNodes.Length + NS_GROW_SIZE];
						if (_namespaceNodesTop > 0)
							Array.Copy(_namespaceNodes, destinationArray, _namespaceNodesTop + 1);

						_namespaceNodes = destinationArray;
					}
					
					_namespaceNodesTop++;
					_namespaceNodes[_namespaceNodesTop] = NamespaceAttributeBit7();
				}

				// move over terminator '11110000'
				// as QualifiedNameOrIndexBit3 does not Move next
				Move();
			}

			// read qualified-name
			if (QualifiedNameOrIndexBit3() == EnumStringOrIndex.isoiIndex)
				_qname = _vocabulary.ElementName(_lastValue - 1);
			else
				_vocabulary.AddElement(_qname);

			// create element node
			_currentNode.Init(XmlNodeType.Element, _depth, _qname);

			if (_namespaceNodesTop != -1)
				_currentNode.SetAttributes(_namespaceNodes, _namespaceNodesTop + 1);

			_endElement = false;

			if (bHasAttributes)
			{
				// check for identifier '0xxxxxxx'
				while (Move() < 0x80)
				{
					AtrributeBit2();
				}

				// if empty element then we need to close both
				// the attribute and its element
				_endElement = (_lastByte == FIConsts.FI_DOUBLE_TERMINATOR);
			}

			_depth++;
		}

		private void ReadElementChild()
		{
			if (_lastByte < 0x80)
			{
				// child element identifier '0xxxxxxx'
				// - causes recursive call
				ElementBit2();
			}
			else if (_lastByte < 0xC0)
			{
				// character-chunk identifier '1xxxxxxx'
				CharacterChunkBit3();
			}
			else if (_lastByte == 0xE1)
			{
				// processing-instruction identifier '11100001'
				ProcessingInstruction();

				// TODO: return PI Node
				ReadElementChild();
			}
			else if (_lastByte == 0xE2)
			{
				// comment identifier '11100010'
				Comment();
			}
			else if ((_lastByte >> 2) == 0x32)
			{
				// unexpanded-entity-reference identifier '110010xx'
				UnexpandedEntityReferenceBit7();

				// TODO: return Entity Node
				ReadElementChild();
			}
			else
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid Encoding of Element type.");

		}

		// C.4
		//	Attribute ::= SEQUENCE {
		//		qualified-name      QualifiedNameOrIndex
		//							-- ATTRIBUTE NAME category --,
		//		normalized-value    NonIdentifyingStringOrIndex
		//							-- ATTRIBUTE VALUE category -- }
		private void AtrributeBit2()
		{
			// qualified-name
			if (QualifiedNameOrIndexBit2() == EnumStringOrIndex.isoiIndex)
				_qname = _vocabulary.AttributeName(_lastValue - 1);
			else
				_vocabulary.AddAttribute(_qname);

			// new attribute as QNameValue
			FINode.QNameValue qnameValue = new FINode.QNameValue();
			qnameValue.qname = _qname;

			// normalized-value
			if (NonIdentifyingStringOrIndexBit1() == EnumStringOrIndex.isoiIndex)
			{
				if (_lastValue > 0)
					qnameValue.value = Convert.ToString(_vocabulary.AttributeValue(_lastValue - 1));
			}
			else
			{
				qnameValue.value = LastString;

				if (_addToTable)
					_vocabulary.AddAttributeValue(qnameValue.value);
			}

			_currentNode.AddAttribute(qnameValue);
		}


		// C.5
		//	ProcessingInstruction ::= SEQUENCE {
		//		target       IdentifyingStringOrIndex
		//						-- OTHER NCNAME category --,
		//		content      NonIdentifyingStringOrIndex
		//						-- OTHER STRING category -- }
		private void ProcessingInstruction()
		{
			string target;
			string content;
			
			// read target
			if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
				target = _vocabulary.OtherNCName(_lastValue - 1);
			else
				target = LastNameString;

			// read content
			if (NonIdentifyingStringOrIndexBit1() == EnumStringOrIndex.isoiIndex)
				content = _vocabulary.OtherString(_lastValue - 1);
			else
				content = LastString;

			if (target == "xml")
				_currentNode.Init(XmlNodeType.XmlDeclaration, _depth, target, content);
			else
				// NOTE: XmlReader expects value to contain entire PI content including target
				_currentNode.Init(XmlNodeType.ProcessingInstruction, _depth, target, target + " " + content);
		}

		// C.6
		//	UnexpandedEntityReference ::= SEQUENCE {
		//		name                 IdentifyingStringOrIndex
		//							-- OTHER NCNAME category --,
		//		system-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category --,
		//		public-identifier    IdentifyingStringOrIndex OPTIONAL 
		//							-- OTHER URI category -- }
		private void UnexpandedEntityReferenceBit7()
		{
			bool bHasSystemIdentifier = ((_lastByte & 0x02) != 0);
			bool bHasPublicIdentifier = ((_lastByte & 0x01) != 0);

			// read name
			IdentifyingStringOrIndex();
			// read optional system-identifier
			if (bHasSystemIdentifier)
				IdentifyingStringOrIndex();
			// read optional public-identifier
			if (bHasPublicIdentifier)
				IdentifyingStringOrIndex();
		}

		// C.7
		//	CharacterChunk ::= SEQUENCE {
		//		character-codes             NonIdentifyingStringOrIndex
		//								-- CONTENT CHARACTER CHUNK category -- }
		private void CharacterChunkBit3()
		{
			// type may be set to CDATA in NonEmptyOctetEncodingAlgorithmString
			_lastType = XmlNodeType.Text;

			// read character-codes
			if (NonIdentifyingStringOrIndexBit3() == EnumStringOrIndex.isoiIndex)
			{
				_currentNode.Init(_lastType, _depth + 1, string.Empty, Convert.ToString(_vocabulary.CharacterChunk(_lastValue - 1)));
			}
			else
			{
				string chunkVal = LastString;

				if (_addToTable)
					_vocabulary.AddContentCharacterChunk(chunkVal);

				_currentNode.Init(_lastType, _depth + 1, string.Empty, chunkVal);
			}
		}

		// C.8
		//	Comment ::= SEQUENCE {
		//		content     NonIdentifyingStringOrIndex -- OTHER STRING category --}
		private void Comment()
		{
			if (NonIdentifyingStringOrIndexBit1() == EnumStringOrIndex.isoiIndex)
			{
				_currentNode.Init(XmlNodeType.Comment, _depth + 1, string.Empty, Convert.ToString(_vocabulary.OtherString(_lastValue - 1)));
			}
			else
			{
				string commentVal = LastString;

				if (_addToTable)
					_vocabulary.AddOtherString(commentVal);

				_currentNode.Init(XmlNodeType.Comment, _depth + 1, string.Empty, commentVal);
			}
		}

		// C.9
		//	DocumentTypeDeclaration ::= SEQUENCE {
		//		system-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category --,
		//		public-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category --,
		//		children             SEQUENCE (SIZE(0..MAX)) OF 
		//			ProcessingInstruction }
		private void DocumentTypeDeclarationBit7()
		{
			bool bHasSystemIdentifier = ((_lastByte & 0x02) != 0);
			bool bHasPublicIdentifier = ((_lastByte & 0x01) != 0);

			// read optional system-identifier
			if (bHasSystemIdentifier)
				IdentifyingStringOrIndex();
			// read optional public-identifier
			if (bHasPublicIdentifier)
				IdentifyingStringOrIndex();
		}
		#endregion

		#region Helper Parser Methods
		// C.10
		//	UnparsedEntity ::= SEQUENCE {
		//		name                 IdentifyingStringOrIndex
		//							-- OTHER NCNAME category --,
		//		system-identifier    IdentifyingStringOrIndex
		//							-- OTHER URI category --,
		//		public-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category --,
		//		notation-name        IdentifyingStringOrIndex
		//							-- OTHER NCNAME category -- }
		private void UnparsedEntityBit8()
		{
			bool bHasPublicIdentifier = ((_lastByte & 0x01) != 0);

			// read name
			IdentifyingStringOrIndex();
			// read system-identifier
			IdentifyingStringOrIndex();
			// read optional public-identifier
			if (bHasPublicIdentifier)
				IdentifyingStringOrIndex();
			// read notation-name
			IdentifyingStringOrIndex();
		}

		// C.11
		//	Notation ::= SEQUENCE {
		//		name                 IdentifyingStringOrIndex
		//							-- OTHER NCNAME category --,
		//		system-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category --,
		//		public-identifier    IdentifyingStringOrIndex OPTIONAL
		//							-- OTHER URI category -- }
		private void NotationBit7()
		{
			bool bHasSystemIdentifier = ((_lastByte & 0x02) != 0);
			bool bHasPublicIdentifier = ((_lastByte & 0x01) != 0);

			// read name
			IdentifyingStringOrIndex();
			// read optional system-identifier
			if (bHasSystemIdentifier)
				IdentifyingStringOrIndex();
			// read optional public-identifier
			if (bHasPublicIdentifier)
				IdentifyingStringOrIndex();

		}

		// C.12
		//	NamespaceAttribute ::= SEQUENCE {
		//		prefix             IdentifyingStringOrIndex OPTIONAL
		//							-- PREFIX category --,
		//		namespace-name     IdentifyingStringOrIndex OPTIONAL
		//							-- NAMESPACE NAME category -- }
		private FINode.QNameValue NamespaceAttributeBit7()
		{
			// NOTE: draft spec is incorrect as it says this starts on bit 8 (rather than bit 7)

			bool bHasPrefix = ((_lastByte & 0x02) != 0);
			bool bHasNamespaceName = ((_lastByte & 0x01) != 0);

			string prefix = string.Empty;
			string uri = string.Empty;

			// read optional prefix
			if (bHasPrefix)
			{
				if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
				{
					prefix = _vocabulary.PrefixName(_lastValue - 1);
				}
				else
				{
					prefix = LastNameString;
					_vocabulary.AddPrefixName(prefix);
				}
			}

			// read optional namespace-name
			if (bHasNamespaceName)
			{
				if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
				{
					uri = _vocabulary.NamespaceName(_lastValue - 1);
				}
				else
				{
					uri = LastNameString;
					_vocabulary.AddNamespaceName(uri);
				}
			}

			FINode.QNameValue qnameValue = new FINode.QNameValue();
			if (prefix == string.Empty)
			{
				// xmlns
				qnameValue.Init(prefix, FIConsts.FI_XML_NAMESPACE, FIConsts.FI_XML_NAMESPACE_NAME);
				qnameValue.value = uri;
			}
			else
			{
				// xmlns:abc, i.e. prefix goes in localName
				qnameValue.Init(FIConsts.FI_XML_NAMESPACE_NAME, FIConsts.FI_XML_NAMESPACE, prefix);
				qnameValue.value = uri;
			}

			return qnameValue;
		}

		// C.13
		//	IdentifyingStringOrIndex ::= CHOICE {
		//		literal-character-string   NonEmptyOctetString,
		//		string-index               INTEGER (1..one-meg) }
		private EnumStringOrIndex IdentifyingStringOrIndex()
		{
			// check identifier '10000000'
			if (Move() < 0x80)
			{
				// literal-character-string
				NonEmptyOctetUTF8String(NonEmptyOctetStringBit2Length());
				return EnumStringOrIndex.isoiString;
			}
			else
			{
				// string-index
				_lastValue = Integer1To2pw20Bit2();
				return EnumStringOrIndex.isoiIndex;
			}
		}


		// C.14
		//	NonIdentifyingStringOrIndex ::= CHOICE {
		//		literal-character-string    SEQUENCE {
		//			add-to-table                BOOLEAN,
		//			character-string            EncodedCharacterString },
		//		string-index                INTEGER (0..one-meg) }
		private EnumStringOrIndex NonIdentifyingStringOrIndexBit1()
		{
			// check identifier '10000000'
			if (Move() < 0x80)
			{
				// literal-character-string
				_addToTable = ((_lastByte & 0x40) != 0);
				EncodedCharacterStringBit3();
				return EnumStringOrIndex.isoiString;
			}
			else
			{
				// string-index
				_lastValue = Integer0To2pw20Bit2();

				return EnumStringOrIndex.isoiIndex;
			}
		}

		// C.15
		//	NonIdentifyingStringOrIndex ::= CHOICE {
		//		literal-character-string    SEQUENCE {
		//			add-to-table                BOOLEAN,
		//			character-string            EncodedCharacterString },
		//		string-index                INTEGER (0..one-meg) }
		private EnumStringOrIndex NonIdentifyingStringOrIndexBit3()
		{
			// check identifier 'xx0xxxxx'
			if ((_lastByte & 0x20) == 0)
			{
				// literal-character-string
				_addToTable = ((_lastByte & 0x10) != 0);
				EncodedCharacterStringBit5();
				return EnumStringOrIndex.isoiString;
			}
			else
			{
				// string-index
				_lastValue = Integer1To2pw20Bit4();
				return EnumStringOrIndex.isoiIndex;
			}
		}

		// C.17
		//	QualifiedNameOrIndex ::= CHOICE {
		//		literal-qualified-name  SEQUENCE {
		//			prefix                IdentifyingStringOrIndex OPTIONAL
		//								-- PREFIX category --,
		//			namespace-name        IdentifyingStringOrIndex OPTIONAL
		//								-- NAMESPACE NAME category --,
		//			local-name            IdentifyingStringOrIndex
		//								-- LOCAL NAME category -- },
		//		name-surrogate-index    INTEGER (1..one-meg) }
		private EnumStringOrIndex QualifiedNameOrIndexBit2()
		{
			// check identifier 'x1111xxx'
			if ((_lastByte & 0x78) == 0x78)
			{
				ReadQName();
				return EnumStringOrIndex.isoiString;
			}
			else
			{
				// name-surrogate-index
				_lastValue = Integer1To2pw20Bit2();
				return EnumStringOrIndex.isoiIndex;
			}
		}

		// C.18
		//	QualifiedNameOrIndex ::= CHOICE {
		//		literal-qualified-name  SEQUENCE {
		//			prefix                IdentifyingStringOrIndex OPTIONAL
		//								-- PREFIX category --,
		//			namespace-name        IdentifyingStringOrIndex OPTIONAL
		//								-- NAMESPACE NAME category --,
		//			local-name            IdentifyingStringOrIndex
		//								-- LOCAL NAME category -- },
		//		name-surrogate-index    INTEGER (1..one-meg) }
		private EnumStringOrIndex QualifiedNameOrIndexBit3()
		{
			// check identifier 'xx1111xx'
			if ((_lastByte & 0x3C) == 0x3C)
			{
				ReadQName();
				return EnumStringOrIndex.isoiString;
			}
			else
			{
				// name-surrogate-index
				_lastValue = Integer1To2pw20Bit3();
				return EnumStringOrIndex.isoiIndex;
			}
		}

		// Helper for C.17 and C.18
		private void ReadQName()
		{
			// literal-qualified-name
			bool bHasPrefix = ((_lastByte & 0x02) != 0);
			bool bHasNamespaceName = ((_lastByte & 0x01) != 0);

			if (bHasPrefix && !bHasNamespaceName)
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid QName type Prefix without Namespace.");

			int prefixIndex = 0;
			int namespaceIndex = 0;
			int localNameIndex = 0;

			// read optional prefix
			if (bHasPrefix)
			{
				if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
				{
					_qname.prefix = _vocabulary.PrefixName(_lastValue - 1);
					prefixIndex = _lastValue;
				}
				else
				{
					_qname.prefix = LastNameString;
					prefixIndex = _vocabulary.AddPrefixName(_qname.prefix);
				}
			}
			else
			{
				_qname.prefix = string.Empty;
			}

			// read optional namespace-name
			if (bHasNamespaceName)
			{
				if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
				{
					_qname.ns = _vocabulary.NamespaceName(_lastValue - 1);
					namespaceIndex = _lastValue;
				}
				else
				{
					_qname.ns = LastNameString;
					namespaceIndex = _vocabulary.AddNamespaceName(_qname.ns);
				}
			}
			else
			{
				_qname.ns = string.Empty;
			}

			// read local-name
			if (IdentifyingStringOrIndex() == EnumStringOrIndex.isoiIndex)
			{
				_qname.localName = _vocabulary.LocalName(_lastValue - 1);
				localNameIndex = _lastValue;
			}
			else
			{
				_qname.localName = LastNameString;
				localNameIndex = _vocabulary.AddLocalName(_qname.localName);
			}
		}

		// C.19
		//	EncodedCharacterString ::= SEQUENCE {
		//		encoding-format       CHOICE {
		//			utf-8                  NULL, 
		//			utf-16                 NULL,
		//			restricted-alphabet    INTEGER(1..256),
		//			encoding-algorithm     INTEGER(1..256) },
		//		octets                NonEmptyOctetString }
		private void EncodedCharacterStringBit3()
		{
			// mask last 6 bits
			int dwVal = (_lastByte & 0x3F);
			int dwIntVal = 0;

			switch (dwVal >> 4)
			{
				case FIConsts.ENCODED_CHARACTER_STRING_UTF8:
					{	// UTF-8
						NonEmptyOctetUTF8String(NonEmptyOctetStringBit5Length());
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_UTF16BE:
					{	// UTF-16BE
						NonEmptyOctetUTF16BEString(NonEmptyOctetStringBit5Length());
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_RESTRICTED_ALPHABET:
					{	// restricted-alphabet
						dwIntVal = ((dwVal & 0x0F) << 4);
						dwVal = Move();
						dwIntVal |= ((dwVal & 0xF0) >> 4);
						NonEmptyOctetRestrictedAlphabetString(NonEmptyOctetStringBit5Length(), dwIntVal + 1);
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_ENCODING_ALGORITHM:
					{	// encoding-algorithm
						dwIntVal = ((dwVal & 0x0F) << 4);
						dwVal = Move();
						dwIntVal |= ((dwVal & 0xF0) >> 4);
						NonEmptyOctetEncodingAlgorithmString(NonEmptyOctetStringBit5Length(), dwIntVal + 1);
						break;
					}
				default:
					{
						throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid EncodedCharacterString type  on the third bit of an octet.");
					}
			}
		}

		// C.20
		//	EncodedCharacterString ::= SEQUENCE {
		//		encoding-format       CHOICE {
		//			utf-8                  NULL, 
		//			utf-16                 NULL,
		//			restricted-alphabet    INTEGER(1..256),
		//			encoding-algorithm     INTEGER(1..256) },
		//		octets                NonEmptyOctetString }
		private void EncodedCharacterStringBit5()
		{
			// mask last 4 bits
			int dwVal = (_lastByte & 0x0F);
			int dwIntVal = 0;

			switch (dwVal >> 2)
			{
				case FIConsts.ENCODED_CHARACTER_STRING_UTF8:
					{	// UTF-8
						NonEmptyOctetUTF8String(NonEmptyOctetStringBit7Length());
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_UTF16BE:
					{	// UTF-16BE
						NonEmptyOctetUTF16BEString(NonEmptyOctetStringBit7Length());
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_RESTRICTED_ALPHABET:
					{	// restricted-alphabet
						dwIntVal = ((dwVal & 0x03) << 6);
						dwVal = Move();
						dwIntVal |= ((dwVal & 0xFC) >> 2);
						NonEmptyOctetRestrictedAlphabetString(NonEmptyOctetStringBit7Length(), dwIntVal + 1);
						break;
					}
				case FIConsts.ENCODED_CHARACTER_STRING_ENCODING_ALGORITHM:
					{	// encoding-algorithm
						dwIntVal = ((dwVal & 0x03) << 6);
						dwVal = Move();
						dwIntVal |= ((dwVal & 0xFC) >> 2);
						NonEmptyOctetEncodingAlgorithmString(NonEmptyOctetStringBit7Length(), dwIntVal + 1);
						break;
					}
				default:
					{
						throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid EncodedCharacterString type  on the fifth bit of an octet.");
					}
			}
		}

		// C.21
		private int LengthOfSequence()
		{
			int dwVal = Move();

			// see if value fits in 7 bits (max 127)
			if (dwVal < 0x80)
			{
				// range is 1 to 128, so add 1 to value
				dwVal += 1;
			}
			else
			{
				// value is stored as 24 bits, ignore first 4 bits (1000) which are just padding
				dwVal = ((dwVal & 0x0F) << 16);
				dwVal |= (Move() << 8);
				dwVal |= Move();

				// range is 129 to 2^20, so add 129 to value
				dwVal += 129;
			}

			return dwVal;
		}

		// Read values for lengths read in C.22, C.23, C.24
		private void NonEmptyOctetUTF8String(int len)
		{
			// OCTET STRING is UTF8
			if (_lastCharBuffer.Length < len)
				_lastCharBuffer = new char[len];

			_lastCharBufferUsed = Encoding.UTF8.GetChars(Move(len), 0, len, _lastCharBuffer, 0);
		}

		// Read values using lengths read in C.22, C.23, C.24
		private void NonEmptyOctetUTF16BEString(int len)
		{
			// OCTET STRING is UTF16
			if (_lastCharBuffer.Length < len / 2)
				_lastCharBuffer = new char[len / 2];

			_lastCharBufferUsed = Encoding.BigEndianUnicode.GetChars(Move(len), 0, len, _lastCharBuffer, 0);
		}

		// Read values using lengths read in C.22, C.23, C.24
		private void NonEmptyOctetRestrictedAlphabetString(int len, int index)
		{
			SetLastCharBuffer(_vocabulary.RestrictedAlphabet(index).Decode(Move(len)).ToCharArray());
		}

		// Read values using lengths read in C.22, C.23, C.24
		private void NonEmptyOctetEncodingAlgorithmString(int len, int index)
		{
            // if the encoding is CDATA we need to change the _lastType to CDATA
            FIEncoding fiEncoding = _vocabulary.EncodingAlgorithm(index);
            if ((fiEncoding is InternalEncodingAlgorithm)
                && (fiEncoding.TableIndex == (int)InternalEncodingAlgorithm.EncodingType.CDataEncoding))
                _lastType = XmlNodeType.CDATA;
            
            SetLastCharBuffer(fiEncoding.Decode(Move(len)).ToCharArray());
		}

		// C.22
		// NonEmptyOctetString ::= OCTET STRING (SIZE(1..four-gig))
		private int NonEmptyOctetStringBit2Length()
		{
			// mask last 7 bits
			int dwVal = (_lastByte & 0x7F);
			int dwLen = 0;

			if (dwVal < 0x40)
			{
				// value fits in 6 bits (max 63)
				// range is 1 to 64, so add 1 to value
				dwLen = dwVal + 1;
			}
			else if (dwVal == 0x40)
			{
				// value fits in next 8 bits (max 255)
				dwLen = Move();
				// range is 65 to 320, so add 65 to value
				dwLen += 65;
			}
			else if (dwVal == 0x60)
			{
				// value fits in next 32 bits (max 4gig)
				dwLen = Move() << 24;
				dwLen |= Move() << 16;
				dwLen |= Move() << 8;
				dwLen |= Move();

				// range is 321 to 2^32, so add 321 to value
				dwLen += 321;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid NonEmptyOctetString type  on the second bit of an octet.");
			}

			return dwLen;
		}

		// C.23
		// NonEmptyOctetString ::= OCTET STRING (SIZE(1..four-gig))
		private int NonEmptyOctetStringBit5Length()
		{
			// mask last 4 bits
			int dwVal = (_lastByte & 0x0F);
			int dwLen = 0;

			if (dwVal < 0x08)
			{
				// value fits in 3 bits (max 7)
				// range is 1 to 7, so add 1 to value
				dwLen = dwVal + 1;
			}
			else if (dwVal == 0x08)
			{
				// value fits in next 8 bits (max 255)
				dwLen = Move();
				// range is 9 to 264, so add 9 to value
				dwLen += 9;
			}
			else if (dwVal == 0x0C)
			{
				// value fits in next 32 bits (max 4gig)
				dwLen = Move() << 24;
				dwLen |= Move() << 16;
				dwLen |= Move() << 8;
				dwLen |= Move();

				// range is 265 to 2^32, so add 265 to value
				dwLen += 265;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid NonEmptyOctetString type  on the fifth bit of an octet.");
			}

			return dwLen;
		}

		// C.24
		// NonEmptyOctetString ::= OCTET STRING (SIZE(1..four-gig))
		private int NonEmptyOctetStringBit7Length()
		{
			// mask last 2 bits
			int dwVal = (_lastByte & 0x03);
			int dwLen = 0;

			if (dwVal < 0x02)
			{
				// value fits in 1 bit (max 1)
				// range is 1 to 2, so add 1 to value
				dwLen = dwVal + 1;
			}
			else if (dwVal == 0x02)
			{
				// value fits in next 8 bits (max 255)
				dwLen = Move();
				// range is 3 to 258, so add 3 to value
				dwLen += 3;
			}
			else if (dwVal == 0x03)
			{
				// value fits in next 32 bits (max 4gig)
				dwLen = Move() << 24;
				dwLen |= Move() << 16;
				dwLen |= Move() << 8;
				dwLen |= Move();

				// range is 259 to 2^32, so add 259 to value
				dwLen += 259;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid NonEmptyOctetString type  on the seventh bit of an octet.");
			}

			return dwLen;
		}

		// C.25
		private int Integer1To2pw20Bit2()
		{
			// mask last 7 bits
			int dwVal = _lastByte & 0x7F;
			int dwIntVal = 0;

			if (dwVal < 0x40)
			{
				// value fits in 6 bits (max 63)
				// range is 1 to 64, so add 1 to value
				dwIntVal = dwVal + 1;
			}
			else if ((dwVal & 0x60) == 0x40)
			{
				// value fits in next 13 bits (max 8256)
				dwIntVal = ((dwVal & 0x1F) << 8);
				dwIntVal |= Move();

				// range is 65 to 8256, so add 65 to value
				dwIntVal += 65;
			}
			else if ((dwVal & 0x60) == 0x60)
			{
				// value fits in next 20 bits (max 2^20)
				// ignore 1 bit of padding added
				dwIntVal = ((dwVal & 0x0F) << 16);
				dwIntVal |= Move() << 8;
				dwIntVal |= Move();

				// range is 8257 to 2^32, so add 8257 to value
				dwIntVal += 8257;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid Encoding of integers 1 To 2^20 type  on the second bit of an octet.");
			}

			return dwIntVal;
		}

		// C.26
		private int Integer0To2pw20Bit2()
		{
			// mask last 7 bits
			// check for 0 value of x1111111
			int dwVal = _lastByte & 0x7F;
			int dwIntVal = 0;
			if (dwVal != 0x7F)
			{
				try
				{
					dwIntVal = Integer1To2pw20Bit2();
				}
				catch (LtFastInfosetException)
				{
					throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid Encoding of integers 0 To 2^20 type  on the second bit of an octet.");
				}
			}

			return dwIntVal;
		}

		// C.27
		private int Integer1To2pw20Bit3()
		{
			// mask last 6 bits
			int dwVal = (_lastByte & 0x3F);
			int dwIntVal = 0;

			if (dwVal < 0x20)
			{
				// value fits in 5 bits (max 31)
				// range is 1 to 32, so add 1 to value
				dwIntVal = dwVal + 1;
			}
			else if ((dwVal & 0x38) == 0x20)
			{
				// value fits in next 11 bits (max 2080)
				dwIntVal = ((dwVal & 0x07) << 8);
				dwIntVal |= Move();

				// range is 33 to 2080, so add 33 to value
				dwIntVal += 33;
			}
			else if ((dwVal & 0x38) == 0x28)
			{
				// value fits in next 19 bits (max 526368)
				dwIntVal = ((dwVal & 0x07) << 16);
				dwIntVal |= Move() << 8;
				dwIntVal |= Move();

				// range is 2081 to 526368, so add 2081 to value
				dwIntVal += 2081;
			}
			else if ((dwVal & 0x38) == 0x30)
			{
				// value fits in next 20 bits (max 2^20)
				// ignore '0000000' padding
				dwIntVal = ((Move()) << 16);
				dwIntVal |= Move() << 8;
				dwIntVal |= Move();

				// range is 526369 to 2^20, so add 526369 to value
				dwIntVal += 526369;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid Encoding of integers 1 To 2^20 type  on the third bit of an octet.");
			}

			return dwIntVal;
		}

		// C.28
		private int Integer1To2pw20Bit4()
		{
			// mask last 4 bits
			int dwVal = (_lastByte & 0x1F);
			int dwIntVal = 0;

			if (dwVal < 0x10)
			{
				// value fits in 4 bits (max 15)
				// range is 1 to 16, so add 1 to value
				dwIntVal = dwVal + 1;
			}
			else if ((dwVal & 0x1C) == 0x10)
			{
				// value fits in next 10 bits (max 1040)
				dwIntVal = ((dwVal & 0x03) << 8);
				dwIntVal |= Move();

				// range is 17 to 1040, so add 17 to value
				dwIntVal += 17;
			}
			else if ((dwVal & 0x1C) == 0x14)
			{
				// value fits in next 18 bits (max 263184)
				dwIntVal = ((dwVal & 0x03) << 16);
				dwIntVal |= Move() << 8;
				dwIntVal |= Move();

				// range is 1041 to 263184, so add 1041 to value
				dwIntVal += 1041;
			}
			else if ((dwVal & 0x1C) == 0x18)
			{
				// value fits in next 20 bits (max 2^20)
				// ignore '000000' padding
				dwIntVal = ((Move()) << 16);
				dwIntVal |= Move() << 8;
				dwIntVal |= Move();

				// range is 263185 to 2^20, so add 263185 to value
				dwIntVal += 263185;
			}
			else
			{
				throw new LtFastInfosetException("Failed to parse FastInfoset. Invalid Encoding of integers 1 To 2^20 type  on the fourth bit of an octet.");
			}

			return dwIntVal;
		}

		private string LastString
		{
			get { return new string(_lastCharBuffer, 0, _lastCharBufferUsed); }
		}

		private string LastNameString
		{
			get { return _nameTable.Add(_lastCharBuffer, 0, _lastCharBufferUsed); }
		}

		private void SetLastCharBuffer(char[] val)
		{
			_lastCharBufferUsed = val.Length;
			if (_lastCharBufferUsed > 0)
			{
				if (_lastCharBuffer.Length < _lastCharBufferUsed)
					_lastCharBuffer = new char[_lastCharBufferUsed];

				Array.Copy(val, _lastCharBuffer, _lastCharBufferUsed);
			}
		}
		#endregion

		#region Data Members
		private XmlNameTable _nameTable = null;
		private FIVocabularyManager _vocabularyManager = null;
		private FIReaderVocabulary _vocabulary = null;

		private FINode _currentNode = null;
		private FINode.QNameValue[] _namespaceNodes = null;
		private int _namespaceNodesTop;

		private int _depth = 0;
		private bool _endElement = false;

		private StreamBuffer _buffer = null;
		private byte _lastByte = 0;

		private QualifiedName _qname;

		private char[] _lastCharBuffer = null;
		private int _lastCharBufferUsed = 0;

		private int _lastValue = 0;
		private bool _addToTable = false;
		private XmlNodeType _lastType = XmlNodeType.None;
		#endregion
	}
}
